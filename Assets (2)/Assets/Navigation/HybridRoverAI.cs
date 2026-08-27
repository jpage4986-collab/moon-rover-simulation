using UnityEngine;
using UnityEngine.AI;
using UnityStandardAssets.Vehicles.Car;
using System.Collections.Generic;
using MoonRover.Driver;

namespace MoonRover.Navigation
{
    [RequireComponent(typeof(CarController))]
    public class HybridRoverAI : MonoBehaviour, IDriver
    {
        public enum NavMode { SatelliteGlobal, SensorLocal }
        [Header("智驾控制中心")]
        public Transform target;
        public NavMode currentMode = NavMode.SatelliteGlobal;

        [Header("卫星全局导航参数 (Mode 1)")]
        public float waypointTolerance = 2.5f;
        public float pathResolution = 1.0f;
        [Header("雷达局部避障参数 (Mode 2)")]
        public float lookAheadDistance = 10f;
        public int feelerCount = 15;
        public float maxSteerAngle = 25f;
        public float slopePenaltyWeight = 4.0f;

        public float PlannedSteerAngle { get; private set; }

        private CarController m_Car;
        [SerializeField] private Terrain targetTerrain;

        private NavMeshPath rawNavPath;
        private List<Vector3> detailedPath = new List<Vector3>();

        private int currentWaypointIndex = 0;
        [SerializeField] private Material pathMaterial;
        private LineRenderer satellitePathRenderer;

        private float stuckTimer = 0f;
        private float decisionTimer = 0f;

        void Awake()
        {
            m_Car = GetComponent<CarController>();
            if (targetTerrain == null)
            {
                targetTerrain = Terrain.activeTerrain;
                if (targetTerrain == null)
                {
                    Debug.LogError("[HybridRoverAI] No Terrain found in scene!");
                    enabled = false;
                }
            }
        }

        public float GetSteeringInput()
        {
            return PlannedSteerAngle;
        }

        public void EnableDriver(bool enable)
        {
            this.enabled = enable;
        }

        void Start()
        {
            rawNavPath = new NavMeshPath();
            GameObject lineObj = new GameObject("Satellite_Global_Route");
            lineObj.transform.SetParent(this.transform);
            satellitePathRenderer = lineObj.AddComponent<LineRenderer>();
            satellitePathRenderer.useWorldSpace = true;
            satellitePathRenderer.startWidth = 0.6f;
            satellitePathRenderer.endWidth = 0.6f;
            if (pathMaterial == null)
            {
                pathMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            satellitePathRenderer.material = pathMaterial;
            satellitePathRenderer.startColor = new Color(1f, 0.8f, 0f, 0.7f);
            satellitePathRenderer.endColor = new Color(1f, 0.8f, 0f, 0.7f);

            SnapTargetToTerrain();

            RequestSatelliteRoute();
        }

        void SnapTargetToTerrain()
        {
            if (target == null || targetTerrain == null) return;
            float targetHeight = targetTerrain.SampleHeight(target.position) + targetTerrain.transform.position.y + 0.3f;
            target.position = new Vector3(target.position.x, targetHeight, target.position.z);
        }

        Vector3 GetDestinationPoint()
        {
            if (detailedPath != null && detailedPath.Count > 0)
                return detailedPath[detailedPath.Count - 1];
            if (target != null)
                return target.position;
            return transform.position;
        }

        public void RequestSatelliteRoute()
        {
            if (target == null || targetTerrain == null) return;

            Vector3 startPos = transform.position;
            Vector3 endPos = target.position;

            NavMeshHit startHit;
            bool startFound = NavMesh.SamplePosition(startPos, out startHit, 5.0f, NavMesh.AllAreas);

            if (!startFound)
            {
                currentMode = NavMode.SensorLocal;
                return;
            }

            Vector3 navStart = startHit.position;
            Vector3 navEnd = endPos;

            NavMesh.CalculatePath(navStart, navEnd, NavMesh.AllAreas, rawNavPath);

            if (rawNavPath.status == NavMeshPathStatus.PathComplete)
            {
                currentMode = NavMode.SatelliteGlobal;
                detailedPath.Clear();

                for (int i = 0; i < rawNavPath.corners.Length - 1; i++)
                {
                    Vector3 p1 = rawNavPath.corners[i];
                    Vector3 p2 = rawNavPath.corners[i + 1];

                    float segmentLength = Vector3.Distance(p1, p2);
                    int numPoints = Mathf.Max(1, Mathf.CeilToInt(segmentLength / pathResolution));

                    for (int j = 0; j < numPoints; j++)
                    {
                        float t = (float)j / numPoints;
                        Vector3 lerpedPoint = Vector3.Lerp(p1, p2, t);

                        lerpedPoint.y = targetTerrain.SampleHeight(lerpedPoint) + targetTerrain.transform.position.y + 0.3f;

                        detailedPath.Add(lerpedPoint);
                    }
                }

                Vector3 lastCorner = rawNavPath.corners[rawNavPath.corners.Length - 1];
                lastCorner.y = targetTerrain.SampleHeight(lastCorner) + targetTerrain.transform.position.y + 0.3f;
                detailedPath.Add(lastCorner);

                satellitePathRenderer.positionCount = detailedPath.Count;
                satellitePathRenderer.SetPositions(detailedPath.ToArray());

                currentWaypointIndex = 1;
                Debug.Log($"卫星路线已生成！共生成 {detailedPath.Count} 个高精度地形贴合点。");
            }
            else
            {
                currentMode = NavMode.SensorLocal;
                satellitePathRenderer.positionCount = 0;
            }
        }

        void FixedUpdate()
        {
            if (target == null || targetTerrain == null) return;

            Vector3 destination = GetDestinationPoint();
            float distToTarget = Vector3.Distance(transform.position, destination);
            if (distToTarget < 3f) { m_Car.Move(0f, 0f, 1f, 1f); return; }

            if (m_Car.CurrentSpeed < 1f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > 2.5f && currentMode == NavMode.SatelliteGlobal)
                {
                    currentMode = NavMode.SensorLocal;
                    satellitePathRenderer.startColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                    stuckTimer = 0f;
                    return;
                }
            }
            else stuckTimer = 0f;

            float finalAccel = 0f;
            if (currentMode == NavMode.SatelliteGlobal)
            {
                ExecuteGlobalSatellitePilot(out float steer, out finalAccel);
                PlannedSteerAngle = steer;
            }
            else
            {
                decisionTimer += Time.fixedDeltaTime;
                if (decisionTimer >= 0.5f)
                {
                    decisionTimer = 0f;
                    ExecuteLocalSensorPilot(destination, distToTarget, out float steer, out float accel);
                    PlannedSteerAngle = steer;
                    finalAccel = accel;
                }
                else finalAccel = 1f;
            }

            if (distToTarget < 8f) finalAccel *= (distToTarget / 8f);
            m_Car.Move(PlannedSteerAngle, finalAccel, 0f, 0f);
        }

        void ExecuteGlobalSatellitePilot(out float steer, out float accel)
        {
            steer = 0f; accel = 1f;
            if (detailedPath == null || detailedPath.Count == 0 || currentWaypointIndex >= detailedPath.Count) return;

            Vector3 currentTargetWP = detailedPath[currentWaypointIndex];
            Vector3 toWP = currentTargetWP - transform.position;

            if (toWP.magnitude < waypointTolerance)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= detailedPath.Count) return;
                currentTargetWP = detailedPath[currentWaypointIndex];
                toWP = currentTargetWP - transform.position;
            }

            Vector3 localTarget = transform.InverseTransformPoint(currentTargetWP);

            steer = Mathf.Clamp(localTarget.x / 3f, -1f, 1f);
            accel = 1f - (Mathf.Abs(steer) * 0.4f);
        }

        void ExecuteLocalSensorPilot(Vector3 targetPos, float distToTarget, out float bestSteer, out float bestAccel)
        {
            bestSteer = 0f; bestAccel = 1f;
            float bestScore = -Mathf.Infinity;
            float maxSteepnessOnBestPath = 0f;

            TerrainData tData = targetTerrain.terrainData;
            Vector3 tPos = targetTerrain.transform.position;

            Vector3 forward = transform.forward;
            Vector3 pos = transform.position;
            Vector3 toTarget = targetPos - pos;

            for (int i = 0; i < feelerCount; i++)
            {
                float steerInput = -1f + (2f * i / (feelerCount - 1));
                float angle = steerInput * maxSteerAngle;
                float turnRadius = (steerInput == 0) ? 999f : (2.5f / Mathf.Tan(angle * Mathf.Deg2Rad));

                float maxSteepnessAlongArc = 0f;
                Vector3 finalPoint = pos;

                for (float dist = 3f; dist <= lookAheadDistance; dist += 3.5f)
                {
                    float angleRadians = dist / turnRadius;
                    float localX = turnRadius * (1 - Mathf.Cos(angleRadians));
                    float localZ = turnRadius * Mathf.Sin(angleRadians);
                    Vector3 samplePoint = pos + forward * localZ + transform.right * localX;

                    float normX = (samplePoint.x - tPos.x) / tData.size.x;
                    float normZ = (samplePoint.z - tPos.z) / tData.size.z;
                    float currentSteep = tData.GetSteepness(normX, normZ);

                    if (currentSteep > maxSteepnessAlongArc) maxSteepnessAlongArc = currentSteep;
                    if (dist >= lookAheadDistance) finalPoint = samplePoint;
                }

                Vector3 feelerDir = (finalPoint - pos).normalized;
                float alignmentScore = Vector3.Dot(feelerDir, toTarget.normalized);
                float steepPenalty = (maxSteepnessAlongArc / 45f) * slopePenaltyWeight;
                if (maxSteepnessAlongArc > 35f) steepPenalty += 1000f;

                float finalScore = alignmentScore - steepPenalty;

                if (finalScore > bestScore)
                {
                    bestScore = finalScore;
                    bestSteer = steerInput;
                    maxSteepnessOnBestPath = maxSteepnessAlongArc;
                }
            }

            if (maxSteepnessOnBestPath > 10f || transform.forward.y > 0.1f) bestAccel = 1f;
            else bestAccel = 1f - (Mathf.Abs(bestSteer) * 0.35f);
        }
    }
}

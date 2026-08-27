using UnityEngine;
using UnityStandardAssets.Vehicles.Car;

namespace MoonRover.Navigation
{
    [RequireComponent(typeof(CarController))]
    public class AutoDriver : MonoBehaviour, MoonRover.Driver.IDriver
    {
        [Header("导航目标")]
        public Transform target;

        [Header("拉力越野级雷达")]
        public float lookAheadDistance = 10f;
        public int feelerCount = 15;
        public float maxSteerAngle = 25f;

        [Header("AI 性格与决策频率")]
        public float targetWeight = 1.0f;
        public float slopePenaltyWeight = 4.0f;
        public float decisionInterval = 0.5f;

        public float PlannedSteerAngle { get; private set; }
        private float plannedAccel = 0f;

        private CarController m_Car;
        [SerializeField] private Terrain targetTerrain;
        private TerrainData tData;
        private Vector3 tPos;

        private float decisionTimer = 0f;
        private bool isReversing = false;
        private float stuckTimer = 0f;
        private float reverseTimer = 0f;

        void Awake()
        {
            m_Car = GetComponent<CarController>();
            if (targetTerrain == null)
            {
                targetTerrain = Terrain.activeTerrain;
            }
            if (targetTerrain != null)
            {
                tData = targetTerrain.terrainData;
                tPos = targetTerrain.transform.position;
            }
            else
            {
                Debug.LogError("[AutoDriver] No Terrain found in scene!");
                enabled = false;
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

        void FixedUpdate()
        {
            if (target == null || tData == null) return;

            float currentSpeed = m_Car.CurrentSpeed;
            float distToTarget = Vector3.Distance(transform.position, target.position);

            if (distToTarget < 3f)
            {
                m_Car.Move(0f, 0f, 1f, 1f);
                return;
            }

            if (isReversing)
            {
                reverseTimer += Time.fixedDeltaTime;
                m_Car.Move(0f, 0f, 1f, 0f);
                if (reverseTimer > 3f)
                {
                    isReversing = false;
                    stuckTimer = 0f;
                }
                return;
            }

            if (currentSpeed < 1f && plannedAccel > 0.5f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer > 2.5f)
                {
                    isReversing = true; reverseTimer = 0f; return;
                }
            }
            else stuckTimer = 0f;

            decisionTimer += Time.fixedDeltaTime;
            if (decisionTimer >= decisionInterval)
            {
                decisionTimer = 0f;
                CalculateBestOffRoadPath(distToTarget);
            }

            m_Car.Move(PlannedSteerAngle, plannedAccel, 0f, 0f);
        }

        void CalculateBestOffRoadPath(float distToTarget)
        {
            float bestSteerInput = 0f;
            float bestScore = -Mathf.Infinity;
            float maxSteepnessOnBestPath = 0f;

            Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
            Vector3 right = new Vector3(transform.right.x, 0, transform.right.z).normalized;
            Vector3 pos = transform.position;
            Vector3 toTarget = target.position - pos;
            toTarget.y = 0;

            for (int i = 0; i < feelerCount; i++)
            {
                float steerInput = -1f + (2f * i / (feelerCount - 1));
                float angle = steerInput * maxSteerAngle;
                float absTurnRadius = (steerInput == 0) ? 999f : Mathf.Abs(2.5f / Mathf.Tan(angle * Mathf.Deg2Rad));
                int steerSign = steerInput > 0 ? 1 : -1;

                float maxSteepnessAlongArc = 0f;
                Vector3 finalPoint = pos;

                for (float dist = 3f; dist <= lookAheadDistance; dist += 3.5f)
                {
                    float angleRadians = dist / absTurnRadius;
                    float localX = absTurnRadius * (1 - Mathf.Cos(angleRadians)) * steerSign;
                    float localZ = absTurnRadius * Mathf.Sin(angleRadians);
                    Vector3 samplePoint = pos + forward * localZ + right * localX;

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

                float hysteresisBonus = (steerInput * PlannedSteerAngle > 0) ? 0.2f : 0f;
                float finalScore = (alignmentScore * targetWeight) - steepPenalty + hysteresisBonus;

                if (finalScore > bestScore)
                {
                    bestScore = finalScore;
                    bestSteerInput = steerInput;
                    maxSteepnessOnBestPath = maxSteepnessAlongArc;
                }
            }

            PlannedSteerAngle = bestSteerInput;

            if (maxSteepnessOnBestPath > 10f || transform.forward.y > 0.1f) plannedAccel = 1f;
            else plannedAccel = 1f - (Mathf.Abs(bestSteerInput) * 0.35f);

            if (distToTarget < 8f) plannedAccel *= (distToTarget / 8f);
        }
    }
}

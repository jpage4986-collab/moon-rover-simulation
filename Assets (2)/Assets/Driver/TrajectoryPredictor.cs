using UnityEngine;
using UnityStandardAssets.Vehicles.Car;

namespace MoonRover.Driver
{
    [RequireComponent(typeof(CarController))]
    public class TrajectoryPredictor : MonoBehaviour
    {
        [Header("轨迹预测参数")]
        public float predictionDistance = 10f;
        public int numPoints = 30;

        [Header("车辆物理尺寸")]
        public float wheelBase = 2.5f;
        public float trackWidth = 1.6f;
        public float maxSteerAngle = 25f;

        [Header("HUD 防抖系统")]
        public float smoothSpeed = 8f;

        [SerializeField] private Material trajectoryMaterial;
        [SerializeField] private Terrain targetTerrain;
        private LineRenderer leftLine, rightLine;

        private float displaySteerInput = 0f;

        private IDriver[] drivers;

        void Awake()
        {
            drivers = GetComponents<IDriver>();
        }

        void Start()
        {
            if (targetTerrain == null)
            {
                targetTerrain = Terrain.activeTerrain;
                if (targetTerrain == null)
                {
                    Debug.LogError("[TrajectoryPredictor] No Terrain found in scene!");
                    enabled = false;
                    return;
                }
            }
            leftLine = CreateGlowingLine("Left_Trajectory");
            rightLine = CreateGlowingLine("Right_Trajectory");
        }

        LineRenderer CreateGlowingLine(string lineName)
        {
            GameObject lineObj = new GameObject(lineName);
            lineObj.transform.SetParent(this.transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.startWidth = 0.15f;
            lr.endWidth = 0.05f;
            if (trajectoryMaterial == null)
            {
                trajectoryMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            lr.material = trajectoryMaterial;
            lr.positionCount = numPoints;
            lr.useWorldSpace = true;
            return lr;
        }

        void Update()
        {
            if (targetTerrain == null || leftLine == null || rightLine == null) return;
            if (drivers == null || drivers.Length == 0) return;

            float targetSteerInput = 0f;

            foreach (var driver in drivers)
            {
                if (((MonoBehaviour)driver).enabled)
                {
                    targetSteerInput = driver.GetSteeringInput();
                    break;
                }
            }

            displaySteerInput = Mathf.Lerp(displaySteerInput, targetSteerInput, Time.deltaTime * smoothSpeed);

            float steerAngleInDegrees = displaySteerInput * maxSteerAngle;

            DrawTrajectory(leftLine, -trackWidth / 2f, steerAngleInDegrees);
            DrawTrajectory(rightLine, trackWidth / 2f, steerAngleInDegrees);
        }

        void DrawTrajectory(LineRenderer lr, float xOffset, float steerAngle)
        {
            Vector3 flatForward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
            Vector3 flatRight = new Vector3(transform.right.x, 0, transform.right.z).normalized;
            Vector3 startPos = transform.position + flatRight * xOffset + flatForward * 1f;
            bool isDangerous = false;

            for (int i = 0; i < numPoints; i++)
            {
                float dist = (predictionDistance / (numPoints - 1)) * i;
                Vector3 point = Vector3.zero;

                if (Mathf.Abs(steerAngle) < 1f)
                {
                    point = startPos + flatForward * dist;
                }
                else
                {
                    float turnRadius = wheelBase / Mathf.Tan(steerAngle * Mathf.Deg2Rad);
                    float angleRadians = dist / turnRadius;
                    float localX = turnRadius * (1 - Mathf.Cos(angleRadians));
                    float localZ = turnRadius * Mathf.Sin(angleRadians);
                    point = startPos + flatForward * localZ + flatRight * localX;
                }

                point.y = targetTerrain.SampleHeight(point) + targetTerrain.transform.position.y + 0.15f;

                float normX = (point.x - targetTerrain.transform.position.x) / targetTerrain.terrainData.size.x;
                float normZ = (point.z - targetTerrain.transform.position.z) / targetTerrain.terrainData.size.z;
                if (targetTerrain.terrainData.GetSteepness(normX, normZ) > 25f)
                {
                    isDangerous = true;
                }

                lr.SetPosition(i, point);
            }

            if (isDangerous)
            {
                lr.startColor = new Color(1f, 0f, 0f, 0.8f);
                lr.endColor = new Color(1f, 0f, 0f, 0.0f);
            }
            else
            {
                lr.startColor = new Color(0f, 1f, 1f, 0.8f);
                lr.endColor = new Color(0f, 1f, 1f, 0.0f);
            }
        }
    }
}

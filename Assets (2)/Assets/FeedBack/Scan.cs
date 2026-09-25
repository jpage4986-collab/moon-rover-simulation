using System.Collections.Generic;
using UnityEngine;

namespace MoonRover.FeedBack
{
    /// <summary>
    /// 地形坡度可视化。
    /// 方块锚定在世界地面网格上，不再随车移动；车辆只决定当前显示范围。
    /// </summary>
    public class Scan : MonoBehaviour
    {
        [Header("前方地形颠簸程度 (输出给硬件)")]
        public float Fv;

        [Header("地形采样参数")]
        public float radarHeightOffset = 2.5f;
        public float minRadius = 4f;
        public float maxRadius = 35f;
        public float fovAngle = 360f;
        public int rings = 12;
        public int rays = 45;

        [Header("地面固定坡度方块")]
        [Tooltip("方块网格的水平间隔(米)")]
        public float cellSize = 4f;
        [Tooltip("以车为中心的可视半径(米)，只渲染该半径内的方块")]
        public float viewRadius = 25f;
        [Tooltip("以初始位置为中心铺方块的正方形半宽(米)，方块铺好后完全固定不动")]
        public float gridCoverRadius = 150f;
        [Tooltip("方块离地表的高度(米)")]
        public float cubeHeightAboveGround = 0.15f;
        [Tooltip("方块尺寸")]
        public Vector3 cubeScale = new Vector3(0.25f, 0.05f, 0.25f);

        [Header("坡度颜色滞后带")]
        [Tooltip("坡度在阈值附近时，需越过阈值再加上此余量才切换颜色，单位：度")]
        [Range(0f, 5f)]
        public float colorHysteresis = 2f;

        // 供 HUD 顶视雷达使用的当前可视地面点。
        private readonly List<Vector3> physical_points = new List<Vector3>(2048);
        public List<Vector3> PhysicalPoints => physical_points;

        private const float YellowThreshold = 8f;
        private const float RedThreshold = 22f;
        private const int MaxInstancesPerDraw = 1023;

        private Mesh pointCloudMesh;
        private Material greenMat;
        private Material yellowMat;
        private Material redMat;
        private Matrix4x4[] drawBatchMatrices;

        private Matrix4x4[] greenMatrices;
        private Matrix4x4[] yellowMatrices;
        private Matrix4x4[] redMatrices;
        private int greenCount;
        private int yellowCount;
        private int redCount;

        // 每个方块的世界锚点和颜色状态：0=绿，1=黄，2=红。
        private Vector3[] tileAnchors;
        private int[] tileLevels;

        [SerializeField] private Terrain targetTerrain;
        private TerrainData terrainData;
        private Vector3 normal0 = Vector3.up;
        private int frameIndex;

        private void Start()
        {
            if (targetTerrain == null)
                targetTerrain = Terrain.activeTerrain;

            if (targetTerrain == null)
            {
                Debug.LogError("[Scan] No Terrain found in scene!");
                enabled = false;
                return;
            }

            terrainData = targetTerrain.terrainData;

            float safeCellSize = Mathf.Max(0.1f, cellSize);
            int cellsPerHalf = Mathf.CeilToInt(Mathf.Max(0f, gridCoverRadius) / safeCellSize);
            int total = (2 * cellsPerHalf + 1) * (2 * cellsPerHalf + 1);

            greenMatrices = new Matrix4x4[total];
            yellowMatrices = new Matrix4x4[total];
            redMatrices = new Matrix4x4[total];
            drawBatchMatrices = new Matrix4x4[MaxInstancesPerDraw];
            tileAnchors = new Vector3[total];
            tileLevels = new int[total];

            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pointCloudMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Material defaultMaterial = tempCube.GetComponent<MeshRenderer>().sharedMaterial;

            greenMat = CreateInstancedMaterial(defaultMaterial, Color.green);
            yellowMat = CreateInstancedMaterial(defaultMaterial, Color.yellow);
            redMat = CreateInstancedMaterial(defaultMaterial, Color.red);

            Destroy(tempCube);

            Vector3 origin = transform.position;
            int index = 0;
            for (int cx = -cellsPerHalf; cx <= cellsPerHalf; cx++)
            {
                for (int cz = -cellsPerHalf; cz <= cellsPerHalf; cz++)
                {
                    Vector3 point = origin + new Vector3(cx * safeCellSize, 0f, cz * safeCellSize);
                    point.y = GroundHeight(point);
                    tileAnchors[index] = point;
                    tileLevels[index] = DetermineInitialColorLevel(SteepnessAt(point));
                    index++;
                }
            }
        }

        private static Material CreateInstancedMaterial(Material baseMaterial, Color color)
        {
            Material material = new Material(baseMaterial)
            {
                color = color,
                enableInstancing = true
            };
            return material;
        }

        private void Update()
        {
            if (tileAnchors == null || terrainData == null)
                return;

            frameIndex++;
            if (frameIndex % 4 == 0)
                CalculatePointCloudData();

            Vector3 carPosition = transform.position;
            float viewSquared = Mathf.Max(0.1f, viewRadius) * Mathf.Max(0.1f, viewRadius);

            greenCount = 0;
            yellowCount = 0;
            redCount = 0;

            for (int i = 0; i < tileAnchors.Length; i++)
            {
                Vector3 point = tileAnchors[i];
                float dx = point.x - carPosition.x;
                float dz = point.z - carPosition.z;
                if (dx * dx + dz * dz > viewSquared)
                    continue;

                tileLevels[i] = DetermineColorLevel(SteepnessAt(point), tileLevels[i]);
                Matrix4x4 matrix = Matrix4x4.TRS(
                    point + new Vector3(0f, cubeHeightAboveGround, 0f),
                    Quaternion.identity,
                    cubeScale);

                switch (tileLevels[i])
                {
                    case 0:
                        greenMatrices[greenCount++] = matrix;
                        break;
                    case 1:
                        yellowMatrices[yellowCount++] = matrix;
                        break;
                    default:
                        redMatrices[redCount++] = matrix;
                        break;
                }
            }

            DrawMeshInstances(greenMat, greenMatrices, greenCount);
            DrawMeshInstances(yellowMat, yellowMatrices, yellowCount);
            DrawMeshInstances(redMat, redMatrices, redCount);

        }

        /// <summary>
        /// 保留原有前方采样，供 HUD 雷达和底盘颠簸反馈使用。
        /// 地面固定方块只负责坡度可视化，不改变采样方向和 Fv 定义。
        /// </summary>
        private void CalculatePointCloudData()
        {
            if (targetTerrain == null)
                return;

            Vector3 forward = transform.forward;
            if (Input.GetAxis("Vertical") < 0f)
                forward = -transform.forward;

            Vector3 basePosition = transform.position + transform.up * radarHeightOffset;
            physical_points.Clear();

            int safeRings = Mathf.Max(2, rings);
            int safeRays = Mathf.Max(2, rays);
            for (int i = 0; i < safeRings; i++)
            {
                float radius = Mathf.Lerp(minRadius, maxRadius, (float)i / (safeRings - 1));
                for (int j = 0; j < safeRays; j++)
                {
                    float angle = Mathf.Lerp(-fovAngle / 2f, fovAngle / 2f, (float)j / (safeRays - 1));
                    Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                    Vector3 point = basePosition + direction * radius;
                    point.y = GroundHeight(point);

                    if (radius <= 15f && Mathf.Abs(angle) <= 20f)
                        physical_points.Add(point);
                }
            }

            Fv = physical_points.Count > 3 ? Vector3.Angle(Vector3.up, fit3(physical_points.ToArray())) : 0f;
        }

        private void DrawMeshInstances(Material material, Matrix4x4[] matrices, int count)
        {
            if (pointCloudMesh == null || material == null || matrices == null || count <= 0)
                return;

            for (int offset = 0; offset < count; offset += MaxInstancesPerDraw)
            {
                int batchCount = Mathf.Min(MaxInstancesPerDraw, count - offset);
                System.Array.Copy(matrices, offset, drawBatchMatrices, 0, batchCount);
                Graphics.DrawMeshInstanced(pointCloudMesh, 0, material, drawBatchMatrices, batchCount);
            }
        }

        private float GroundHeight(Vector3 point)
        {
            return targetTerrain.SampleHeight(point) + targetTerrain.transform.position.y;
        }

        private float SteepnessAt(Vector3 point)
        {
            float normalizedX = (point.x - targetTerrain.transform.position.x) / terrainData.size.x;
            float normalizedZ = (point.z - targetTerrain.transform.position.z) / terrainData.size.z;
            return terrainData.GetSteepness(normalizedX, normalizedZ);
        }

        private int DetermineColorLevel(float steepness, int currentLevel)
        {
            float hysteresis = colorHysteresis;
            switch (currentLevel)
            {
                case 0:
                    return steepness >= YellowThreshold + hysteresis ? 1 : 0;
                case 1:
                    if (steepness < YellowThreshold - hysteresis) return 0;
                    if (steepness >= RedThreshold + hysteresis) return 2;
                    return 1;
                case 2:
                    return steepness < RedThreshold - hysteresis ? 1 : 2;
                default:
                    return DetermineInitialColorLevel(steepness);
            }
        }

        private static int DetermineInitialColorLevel(float steepness)
        {
            if (steepness < YellowThreshold) return 0;
            if (steepness < RedThreshold) return 1;
            return 2;
        }

        private Vector3 fit3(Vector3[] array)
        {
            int count = array.Length;
            float x2 = 0f, x1 = 0f, z2 = 0f, z1 = 0f;
            float xz = 0f, xy = 0f, zy = 0f, y1 = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = array[i];
                x2 += point.x * point.x;
                x1 += point.x;
                z2 += point.z * point.z;
                z1 += point.z;
                xz += point.x * point.z;
                xy += point.x * point.y;
                zy += point.z * point.y;
                y1 += point.y;
            }

            Vector3 normal = equations3(x2, xz, x1, xz, z2, z1, x1, z1, count, xy, zy, y1);
            float rss = 0f, tss = 0f, average = y1 / count;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = array[i];
                rss += Mathf.Pow(point.y - normal.x * point.x - normal.y * point.z - normal.z, 2f);
                tss += Mathf.Pow(point.y - average, 2f);
            }

            if (tss == 0f || 1f - rss / tss < 0.25f) return normal0;
            Vector3 finalNormal = new Vector3(normal.x, -1f, normal.y);
            finalNormal = finalNormal.y < 0f ? -finalNormal.normalized : finalNormal.normalized;
            normal0 = finalNormal;
            return finalNormal;
        }

        private Vector3 equations3(
            double a1, double a2, double a3,
            double b1, double b2, double b3,
            double c1, double c2, double c3,
            double d1, double d2, double d3)
        {
            double denominator = ((a1 * c2 - a2 * c1) * (b2 * c3 - b3 * c2)
                - (b1 * c2 - b2 * c1) * (a2 * c3 - a3 * c2));
            if (denominator == 0d)
                return normal0;

            double x = ((c2 * d1 - c1 * d2) * (b2 * c3 - b3 * c2)
                - (c3 * d2 - c2 * d3) * (b1 * c2 - b2 * c1)) / denominator;
            double y = ((c3 * d2 - c2 * d3) * (a1 * c2 - a2 * c1)
                - (c2 * d1 - c1 * d2) * (a2 * c3 - a3 * c2)) / denominator;
            double z = (d1 - a1 * x - b1 * y) / c1;
            return new Vector3((float)x, (float)y, (float)z);
        }
    }
}

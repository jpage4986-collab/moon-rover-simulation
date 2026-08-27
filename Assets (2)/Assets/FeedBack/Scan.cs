using UnityEngine;
using System.Collections.Generic;

namespace MoonRover.FeedBack
{
    public class Scan : MonoBehaviour
    {
        [Header("前方地形颠簸程度 (输出给硬件)")]
        public float Fv;

        [Header("全景雷达参数")]
        public float radarHeightOffset = 2.5f;
        public float minRadius = 4f;
        public float maxRadius = 35f;
        public float fovAngle = 360f;

        public int rings = 12;
        public int rays = 45;

        private Mesh pointCloudMesh;
        private Material greenMat, yellowMat, redMat;

        private Matrix4x4[] greenMatrices;
        private Matrix4x4[] yellowMatrices;
        private Matrix4x4[] redMatrices;

        private int greenCount = 0;
        private int yellowCount = 0;
        private int redCount = 0;

        [SerializeField] private Terrain targetTerrain;
        private TerrainData terrainData;
        private int frame_index = -1;
        private Vector3 normal0 = new Vector3(0, 1, 0);
        private List<Vector3> physical_points = new List<Vector3>();

        void Start()
        {
            if (targetTerrain == null)
            {
                targetTerrain = Terrain.activeTerrain;
            }
            if (targetTerrain == null)
            {
                Debug.LogError("[Scan] No Terrain found in scene!");
                enabled = false;
                return;
            }
            terrainData = targetTerrain.terrainData;

            int totalPoints = rings * rays;

            greenMatrices = new Matrix4x4[totalPoints];
            yellowMatrices = new Matrix4x4[totalPoints];
            redMatrices = new Matrix4x4[totalPoints];

            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pointCloudMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Material defaultMaterial = tempCube.GetComponent<MeshRenderer>().sharedMaterial;

            greenMat = CreateInstancedMaterial(defaultMaterial, Color.green);
            yellowMat = CreateInstancedMaterial(defaultMaterial, Color.yellow);
            redMat = CreateInstancedMaterial(defaultMaterial, Color.red);

            Destroy(tempCube);
        }

        Material CreateInstancedMaterial(Material baseMat, Color color)
        {
            Material mat = new Material(baseMat);
            mat.color = color;
            mat.enableInstancing = true;
            return mat;
        }

        void Update()
        {
            frame_index++;
            if (frame_index % 4 == 0)
            {
                CalculatePointCloudData();
            }

            if (greenCount > 0) Graphics.DrawMeshInstanced(pointCloudMesh, 0, greenMat, greenMatrices, greenCount);
            if (yellowCount > 0) Graphics.DrawMeshInstanced(pointCloudMesh, 0, yellowMat, yellowMatrices, yellowCount);
            if (redCount > 0) Graphics.DrawMeshInstanced(pointCloudMesh, 0, redMat, redMatrices, redCount);
        }

        void CalculatePointCloudData()
        {
            if (targetTerrain == null) return;

            Vector3 forward = transform.forward;
            if (Input.GetAxis("Vertical") < 0) forward = -transform.forward;

            Vector3 basePos = transform.position + transform.up * radarHeightOffset;
            physical_points.Clear();

            greenCount = 0;
            yellowCount = 0;
            redCount = 0;

            Vector3 scale = new Vector3(0.25f, 0.05f, 0.25f);

            for (int i = 0; i < rings; i++)
            {
                float currentRadius = Mathf.Lerp(minRadius, maxRadius, (float)i / (rings - 1));
                for (int j = 0; j < rays; j++)
                {
                    float currentAngle = Mathf.Lerp(-fovAngle / 2f, fovAngle / 2f, (float)j / (rays - 1));
                    Vector3 dir = Quaternion.AngleAxis(currentAngle, Vector3.up) * forward;
                    Vector3 point = basePos + dir * currentRadius;
                    point.y = targetTerrain.SampleHeight(point) + targetTerrain.transform.position.y;

                    Vector3 position = point + new Vector3(0, 0.15f, 0);
                    Matrix4x4 mat = Matrix4x4.TRS(position, Quaternion.identity, scale);

                    float normX = (point.x - targetTerrain.transform.position.x) / terrainData.size.x;
                    float normZ = (point.z - targetTerrain.transform.position.z) / terrainData.size.z;
                    float steepness = terrainData.GetSteepness(normX, normZ);

                    if (steepness < 8f) greenMatrices[greenCount++] = mat;
                    else if (steepness < 22f) yellowMatrices[yellowCount++] = mat;
                    else redMatrices[redCount++] = mat;

                    if (currentRadius <= 15f && Mathf.Abs(currentAngle) <= 20f)
                    {
                        physical_points.Add(point);
                    }
                }
            }

            if (physical_points.Count > 3)
            {
                Fv = Vector3.Angle(new Vector3(0, 1, 0), fit3(physical_points.ToArray()));
            }
            else Fv = 0;
        }

        Vector3 fit3(Vector3[] array)
        {
            int m = array.Length;
            float x2 = 0, x1 = 0, y2 = 0, y1 = 0, xy = 0, xz = 0, yz = 0, z1 = 0;
            for (int i = 0; i < m; i++)
            {
                x2 += Mathf.Pow(array[i].x, 2); x1 += array[i].x;
                y2 += Mathf.Pow(array[i].z, 2); y1 += array[i].z;
                z1 += array[i].y;
                xy += array[i].x * array[i].z; xz += array[i].x * array[i].y; yz += array[i].y * array[i].z;
            }
            Vector3 normal = equations3(x2, xy, x1, xy, y2, y1, x1, y1, m, xz, yz, z1);
            float RSS = 0, TSS = 0, z_average = z1 / m;
            for (int i = 0; i < m; i++)
            {
                RSS += Mathf.Pow(array[i].y - normal.x * array[i].x - normal.y * array[i].z - normal.z, 2);
                TSS += Mathf.Pow(array[i].y - z_average, 2);
            }
            if (TSS == 0 || (1 - RSS / TSS) < 0.25) return normal0;
            Vector3 finalNormal = new Vector3(normal.x, -1, normal.y);
            finalNormal = finalNormal.y < 0 ? -finalNormal.normalized : finalNormal.normalized;
            normal0 = finalNormal;
            return finalNormal;
        }
        Vector3 equations3(double a1, double a2, double a3, double b1, double b2, double b3, double c1, double c2, double c3, double d1, double d2, double d3)
        {
            double denominator = ((a1 * c2 - a2 * c1) * (b2 * c3 - b3 * c2) - (b1 * c2 - b2 * c1) * (a2 * c3 - a3 * c2));
            if (denominator == 0) return new Vector3(0, 1, 0);
            double x = ((c2 * d1 - c1 * d2) * (b2 * c3 - b3 * c2) - (c3 * d2 - c2 * d3) * (b1 * c2 - b2 * c1)) / denominator;
            double y = ((c3 * d2 - c2 * d3) * (a1 * c2 - a2 * c1) - (c2 * d1 - c1 * d2) * (a2 * c3 - a3 * c2)) / denominator;
            double z = (d1 - a1 * x - b1 * y) / c1;
            return new Vector3((float)x, (float)y, (float)z);
        }
    }
}

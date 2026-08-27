using UnityEngine;

namespace MoonRover.FeedBack
{
    public static class ScanMath
    {
        public static Vector3 Fit3(Vector3[] array)
        {
            int m = array.Length;
            if (m < 3) return Vector3.up;

            float x2 = 0, x1 = 0, y2 = 0, y1 = 0, xy = 0, xz = 0, yz = 0, z1 = 0;
            for (int i = 0; i < m; i++)
            {
                x2 += Mathf.Pow(array[i].x, 2); x1 += array[i].x;
                y2 += Mathf.Pow(array[i].z, 2); y1 += array[i].z;
                z1 += array[i].y;
                xy += array[i].x * array[i].z; xz += array[i].x * array[i].y; yz += array[i].y * array[i].z;
            }
            Vector3 normal = Equations3(x2, xy, x1, xy, y2, y1, x1, y1, m, xz, yz, z1);
            float RSS = 0, TSS = 0, z_average = z1 / m;
            for (int i = 0; i < m; i++)
            {
                RSS += Mathf.Pow(array[i].y - normal.x * array[i].x - normal.y * array[i].z - normal.z, 2);
                TSS += Mathf.Pow(array[i].y - z_average, 2);
            }

            Vector3 normal0 = new Vector3(0, 1, 0);
            if (TSS == 0 || (1 - RSS / TSS) < 0.25f) return normal0;

            Vector3 finalNormal = new Vector3(normal.x, -1, normal.y);
            finalNormal = finalNormal.y < 0 ? -finalNormal.normalized : finalNormal.normalized;
            return finalNormal;
        }

        public static Vector3 Equations3(double a1, double a2, double a3, double b1, double b2, double b3, double c1, double c2, double c3, double d1, double d2, double d3)
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

using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Vehicles.Car;

namespace MoonRover.Vision
{
    /// <summary>
    /// 迷你姿态指示器 — 替代旧版人工地平线
    /// 绘制 pitch ladder + roll 指针到 RawImage
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class MiniAttitudeIndicator : MonoBehaviour
    {
        [Header("UI 引用")]
        public RawImage attitudeImage;
        public Text pitchText;
        public Text rollText;

        [Header("绘制参数")]
        public int texWidth = 256;
        public int texHeight = 256;
        public float pitchRange = 60f;       // ±60°
        public float ladderSpacing = 10f;    // 每格 10°
        public float updateInterval = 0.05f;

        private Texture2D attitudeTex;
        private CarController car;
        private float timer = 0f;

        // 颜色
        private Color skyColor = new Color(0.15f, 0.15f, 0.15f, 1f);      // 深灰 "天空"
        private Color groundColor = new Color(0.08f, 0.08f, 0.08f, 1f);    // 更深的 "地面"  
        private Color lineColor = new Color(1f, 1f, 1f, 0.6f);
        private Color warnColor = new Color(1f, 0.3f, 0.1f, 1f);
        private Color centerColor = new Color(0.3f, 1f, 0.3f, 1f);

        void Start()
        {
            car = GetComponent<CarController>();
            attitudeTex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            attitudeTex.filterMode = FilterMode.Bilinear;
            attitudeTex.wrapMode = TextureWrapMode.Clamp;

            if (attitudeImage != null)
                attitudeImage.texture = attitudeTex;
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < updateInterval) return;
            timer = 0f;

            if (car == null) return;

            Vector3 euler = transform.eulerAngles;
            float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            float roll = euler.z > 180f ? euler.z - 360f : euler.z;

            // 更新文本
            if (pitchText != null)
                pitchText.text = "俯仰: " + (-pitch).ToString("F1") + "°";
            if (rollText != null)
                rollText.text = "侧倾: " + (-roll).ToString("F1") + "°";

            RenderAttitude(pitch, roll);

            if (attitudeImage != null)
                attitudeImage.texture = attitudeTex;
        }

        void RenderAttitude(float pitch, float roll)
        {
            int w = texWidth, h = texHeight;
            int cx = w / 2, cy = h / 2;
            float pxPerDeg = (h * 0.8f) / pitchRange;

            Color[] pixels = new Color[w * h];

            // 旋转矩阵逆旋转 (模拟姿态)
            float cosR = Mathf.Cos(-roll * Mathf.Deg2Rad);
            float sinR = Mathf.Sin(-roll * Mathf.Deg2Rad);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 相对中心的坐标
                    float dx = x - cx;
                    float dy = y - cy;

                    // 逆旋转变换 (roll)
                    float rx = dx * cosR - dy * sinR;
                    float ry = dx * sinR + dy * cosR;

                    // 加上 pitch 偏移
                    float horizonY = ry + pitch * pxPerDeg;

                    // 天空/地面
                    pixels[y * w + x] = horizonY < 0 ? skyColor : groundColor;
                }
            }

            // ── 绘制地平线 (白色) ──
            DrawLineAA(pixels, w, h, 0, cy + Mathf.RoundToInt(pitch * pxPerDeg),
                       w - 1, cy + Mathf.RoundToInt(pitch * pxPerDeg), lineColor);

            // ── 绘制 pitch ladder ──
            float startDeg = Mathf.Floor((pitch - pitchRange / 2f) / ladderSpacing) * ladderSpacing;
            float endDeg = Mathf.Ceil((pitch + pitchRange / 2f) / ladderSpacing) * ladderSpacing;

            for (float deg = startDeg; deg <= endDeg; deg += ladderSpacing)
            {
                if (Mathf.Abs(deg) < 0.01f) continue;  // 0° 是地平线，已画

                int lineY = cy + Mathf.RoundToInt((pitch + deg) * pxPerDeg);
                if (lineY < 0 || lineY >= h) continue;

                int lineLen = (Mathf.Abs(deg) % 20f < 0.01f) ? w / 3 : w / 6;
                int x1 = cx - lineLen / 2;
                int x2 = cx + lineLen / 2;

                DrawLineAA(pixels, w, h, x1, lineY, x2, lineY, lineColor);

                // 标签
                string label = deg.ToString("F0") + "°";
                DrawLabel(pixels, w, h, x2 + 4, lineY, label, lineColor);
            }

            // ── 绘制中心十字 (绿色) ──
            int crossSize = 12;
            DrawLineAA(pixels, w, h, cx - crossSize, cy, cx + crossSize, cy, centerColor);
            DrawLineAA(pixels, w, h, cx, cy - crossSize, cx, cy + crossSize, centerColor);

            // ── 绘制 roll 指针 (顶部) ──
            int topY = 8;
            int ptrLen = 16;
            float rollRad = roll * Mathf.Deg2Rad;
            int px1 = cx + Mathf.RoundToInt(-Mathf.Sin(rollRad) * ptrLen);
            int py1 = topY + Mathf.RoundToInt(Mathf.Cos(rollRad) * ptrLen);
            DrawLineAA(pixels, w, h, cx, topY + ptrLen, px1, py1, warnColor);

            // roll 刻度弧
            for (float a = -30f; a <= 30f; a += 10f)
            {
                float aRad = a * Mathf.Deg2Rad;
                int tickLen = Mathf.Abs(a) < 0.1f ? 8 : 4;
                int tx = cx + Mathf.RoundToInt(-Mathf.Sin(aRad) * (ptrLen + 4));
                int ty = topY + Mathf.RoundToInt(Mathf.Cos(aRad) * (ptrLen + 4));
                int tx2 = cx + Mathf.RoundToInt(-Mathf.Sin(aRad) * (ptrLen + 4 + tickLen));
                int ty2 = topY + Mathf.RoundToInt(Mathf.Cos(aRad) * (ptrLen + 4 + tickLen));
                DrawLineAA(pixels, w, h, tx, ty, tx2, ty2, lineColor);
            }

            attitudeTex.SetPixels(pixels);
            attitudeTex.Apply();
        }

        void DrawLineAA(Color[] pixels, int w, int h, int x0, int y0, int x1, int y1, Color color)
        {
            // Bresenham 简易画线
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;

            while (true)
            {
                if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h)
                    pixels[y0 * w + x0] = color;

                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        void DrawLabel(Color[] pixels, int w, int h, int x, int y, string label, Color color)
        {
            // 简化标签: 只画几个像素的示意文本 (实用中由 Unity Text 组件完成)
            // 这里留空 — 实际仰赖 Unity UI Text
        }
    }
}

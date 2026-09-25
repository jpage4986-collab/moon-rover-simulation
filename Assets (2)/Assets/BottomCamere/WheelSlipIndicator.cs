using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Vehicles.Car;

namespace MoonRover.Vision
{
    /// <summary>
    /// 车轮滑转率指示器 — 读取 WheelCollider 前滑/侧滑值
    /// 每轮以填充条 + 文本显示
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class WheelSlipIndicator : MonoBehaviour
    {
        [Header("UI 引用 (4轮, 顺序: FL, FR, RL, RR)")]
        public Image[] slipBars = new Image[4];       // 填充条 0~1
        public Text[] slipTexts = new Text[4];         // 数值文本
        public Text labelText;                         // "滑转率" 标题

        [Header("显示参数")]
        public float updateInterval = 0.1f;
        public float warningThreshold = 0.45f;         // 黄色警告（原始滑移值）
        public float criticalThreshold = 0.75f;        // 红色临界（原始滑移值）
        public float displayDeadZone = 0.02f;          // 传感器噪声不显示为进度
        public float displayFullSlip = 0.95f;          // 到此值时条形达到满长
        public float displayResponse = 10f;            // 条形平滑响应速度

        private CarController car;
        private float timer = 0f;
        private readonly float[] displayedFill = new float[4];
        private readonly float[] maxBarWidths = new float[4];
        private readonly Color[] displayedColors = new Color[4];

        private static readonly Color SafeColor = new Color(0.28f, 1f, 0.42f, 1f);
        private static readonly Color WarningColor = new Color(1f, 0.72f, 0.18f, 1f);
        private static readonly Color CriticalColor = new Color(1f, 0.24f, 0.28f, 1f);

        // 标签
        private static readonly string[] WHEEL_NAMES = { "FL", "FR", "RL", "RR" };

        void Awake()
        {
            car = GetComponent<CarController>();
            for (int i = 0; i < maxBarWidths.Length; i++)
            {
                if (slipBars != null && i < slipBars.Length && slipBars[i] != null)
                {
                    maxBarWidths[i] = slipBars[i].rectTransform.sizeDelta.x;
                    displayedColors[i] = SafeColor;
                }
            }
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < updateInterval) return;
            timer = 0f;

            if (car == null || car.WheelColliders == null) return;
            var wcs = car.WheelColliders;

            for (int i = 0; i < 4 && i < wcs.Length; i++)
            {
                if (wcs[i] == null) continue;

                WheelHit hit;
                float slip = 0f;
                if (wcs[i].GetGroundHit(out hit))
                {
                    // 综合滑转: 前滑 + 侧滑的矢量和
                    slip = Mathf.Sqrt(hit.forwardSlip * hit.forwardSlip
                                    + hit.sidewaysSlip * hit.sidewaysSlip);
                }

                // 先把原始 WheelHit 滑移映射到显示区间，再更新长度。
                // 旧版本直接除以 0.3，导致平台静止时的小噪声也会被判成红色，
                // 同时让条形很快冲到满长。现在 0.02 以下视为零，0.95 为满长。
                float targetFill = MapSlipToFill(slip);

                // 更新填充条：直接改变 RectTransform 宽度，避免无 Sprite 的
                // Filled Image 在不同 Unity 版本中不显示长度变化。
                if (slipBars != null && i < slipBars.Length && slipBars[i] != null)
                {
                    displayedFill[i] = Mathf.MoveTowards(displayedFill[i], targetFill,
                        Mathf.Max(0.1f, displayResponse) * Time.deltaTime);
                    var barRect = slipBars[i].rectTransform;
                    if (maxBarWidths[i] <= 0f) maxBarWidths[i] = barRect.sizeDelta.x;
                    barRect.sizeDelta = new Vector2(maxBarWidths[i] * displayedFill[i], barRect.sizeDelta.y);
                    slipBars[i].type = Image.Type.Simple;
                    slipBars[i].color = displayedColors[i] = Color.Lerp(displayedColors[i],
                        GetSlipColor(targetFill), 1f - Mathf.Exp(-Mathf.Max(0.1f, displayResponse) * Time.deltaTime));
                }

                // 更新文本
                if (slipTexts != null && i < slipTexts.Length && slipTexts[i] != null)
                {
                    slipTexts[i].text = string.Format("{0} {1:F2}", WHEEL_NAMES[i], slip);
                }
            }
        }

        private float MapSlipToFill(float slip)
        {
            float deadZone = Mathf.Max(0f, displayDeadZone);
            float fullScale = Mathf.Max(deadZone + 0.01f, displayFullSlip);
            return slip <= deadZone ? 0f : Mathf.InverseLerp(deadZone, fullScale, slip);
        }

        private Color GetSlipColor(float fill)
        {
            float warningFill = MapSlipToFill(warningThreshold);
            float criticalFill = MapSlipToFill(criticalThreshold);
            if (fill >= criticalFill)
                return Color.Lerp(WarningColor, CriticalColor,
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(criticalFill, 1f, fill)));
            if (fill >= warningFill)
                return Color.Lerp(SafeColor, WarningColor,
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(warningFill, criticalFill, fill)));
            return SafeColor;
        }
    }
}

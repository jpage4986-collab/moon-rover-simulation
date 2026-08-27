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
        public float warningThreshold = 0.15f;         // 黄色警告
        public float criticalThreshold = 0.3f;         // 红色临界

        private CarController car;
        private float timer = 0f;

        // 标签
        private static readonly string[] WHEEL_NAMES = { "FL", "FR", "RL", "RR" };

        void Awake()
        {
            car = GetComponent<CarController>();
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

                // 更新填充条
                if (slipBars != null && i < slipBars.Length && slipBars[i] != null)
                {
                    slipBars[i].fillAmount = Mathf.Clamp01(slip / criticalThreshold * 1.2f);
                    slipBars[i].color = slip < warningThreshold ? new Color(0.3f, 1f, 0.3f)
                        : (slip < criticalThreshold ? Color.yellow : Color.red);
                }

                // 更新文本
                if (slipTexts != null && i < slipTexts.Length && slipTexts[i] != null)
                {
                    slipTexts[i].text = string.Format("{0} {1:F2}", WHEEL_NAMES[i], slip);
                }
            }
        }
    }
}

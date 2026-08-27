using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Vehicles.Car;
using MoonRover.Navigation;
using MoonRover.Driver;
using MoonRover.Platform;

namespace MoonRover.UI
{
    /// <summary>
    /// HUD 综合仪表盘 — 集中管理电池/GPS/罗盘/通信/航点/事件日志
    /// 所有数据为模拟值 (月球车无真实传感器)
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class HUDDashboard : MonoBehaviour
    {
        [Header("— 电池电源 —")]
        public Text batteryText;
        [Tooltip("虚拟电池总容量 (Wh)")]
        public float batteryCapacity = 2000f;
        [Tooltip("全油门放电功率 (W)")]
        public float maxDischargePower = 800f;
        [Tooltip("基础待机功耗 (W)")]
        public float idlePower = 50f;

        [Header("— GPS/卫星状态 —")]
        public Text gpsText;

        [Header("— 罗盘/航向 —")]
        public Text compassText;
        public RectTransform compassBar;       // 航向条指示器 (可选)

        [Header("— 通信链路 —")]
        public Text commsText;

        [Header("— 航点导航 —")]
        public Text waypointText;

        [Header("— 事件日志 —")]
        public Text eventLogText;
        public int maxLogLines = 8;

        // ── 内部状态 ──
        private CarController car;
        private HybridRoverAI aiDriver;
        private DriveModeManager modeMgr;
        private ManualDriverWithAEB manualDriver;
        private MotionPlatformController platformCtrl;

        private float remainingCharge;          // Wh
        private float logTimer = 0f;
        private float logInterval = 0.5f;
        private Queue<string> eventLog = new Queue<string>();
        private string lastModeName = "";
        private bool lastAEBState = false;
        private bool lastPlatformState = false;

        // GPS 模拟
        private float gpsUpdateTimer = 0f;
        private float gpsUpdateInterval = 0.5f;
        private int gpsSatellites = 0;

        // 罗盘
        private float compassUpdateTimer = 0f;
        private float compassUpdateInterval = 0.1f;

        // 电池
        private float batteryUpdateTimer = 0f;
        private float batteryUpdateInterval = 0.3f;

        void Awake()
        {
            car = GetComponent<CarController>();
            aiDriver = GetComponent<HybridRoverAI>();
            modeMgr = GetComponent<DriveModeManager>();
            manualDriver = GetComponent<ManualDriverWithAEB>();
            platformCtrl = GetComponent<MotionPlatformController>();

            remainingCharge = batteryCapacity;

            // 初始日志
            PushEvent("系统启动 · 虚拟电池 " + batteryCapacity + " Wh");
        }

        void Update()
        {
            UpdateBattery();
            UpdateGPS();
            UpdateCompass();
            UpdateComms();
            UpdateWaypoints();
            UpdateEventLog();

            // 检测事件
            DetectEvents();
        }

        // ══════════════════════════════════════════════════
        //  电池 (虚拟)
        // ══════════════════════════════════════════════════

        void UpdateBattery()
        {
            batteryUpdateTimer += Time.deltaTime;

            // 放电: 油门越大耗电越快 + 基础待机
            float throttle = car != null ? car.AccelInput : 0f;
            float dischargeW = idlePower + throttle * maxDischargePower;
            float drainWh = dischargeW * Time.deltaTime / 3600f;
            remainingCharge = Mathf.Max(0, remainingCharge - drainWh);

            if (batteryUpdateTimer < batteryUpdateInterval) return;
            batteryUpdateTimer = 0f;

            if (batteryText == null) return;

            float pct = batteryCapacity > 0 ? remainingCharge / batteryCapacity : 0f;
            float volts = 28.8f;          // 标称 28.8V (航天级锂电)
            float currentA = dischargeW / volts;
            float rangeEstimate = pct * 15f; // 满电约 15km

            batteryText.text = string.Format("⚡ {0:F1}%  {1:F1}V  {2:F1}A",
                pct * 100f, volts, currentA);
            batteryText.color = pct > 0.3f ? new Color(0.3f, 1f, 0.3f)
                : (pct > 0.15f ? Color.yellow : Color.red);
        }

        // ══════════════════════════════════════════════════
        //  GPS / 卫星状态 (模拟)
        // ══════════════════════════════════════════════════

        void UpdateGPS()
        {
            gpsUpdateTimer += Time.deltaTime;
            if (gpsUpdateTimer < gpsUpdateInterval) return;
            gpsUpdateTimer = 0f;

            if (gpsText == null) return;

            // 模拟: 卫星数随位置变化
            Vector3 pos = transform.position;
            float noise = Mathf.PerlinNoise(pos.x * 0.01f, pos.z * 0.01f);
            gpsSatellites = Mathf.RoundToInt(8 + noise * 6);  // 8~14 颗

            // 模拟经纬度 (以世界原点为参考)
            float lat = 25.0f + pos.z * 0.00001f;
            float lon = 121.0f + pos.x * 0.00001f;

            gpsText.text = string.Format("GPS {0}颗   {1:F5}°N  {2:F5}°E",
                gpsSatellites, lat, lon);
        }

        // ══════════════════════════════════════════════════
        //  罗盘 / 航向
        // ══════════════════════════════════════════════════

        void UpdateCompass()
        {
            compassUpdateTimer += Time.deltaTime;
            if (compassUpdateTimer < compassUpdateInterval) return;
            compassUpdateTimer = 0f;

            float heading = transform.eulerAngles.y;
            string dir = HeadingToCompass(heading);

            if (compassText != null)
                compassText.text = string.Format("{0}  {1:F0}°", dir, heading);

            if (compassBar != null)
            {
                // 航向条指针: 0°=N=0, 360°=N=1
                compassBar.anchorMin = new Vector2(heading / 360f, 0);
                compassBar.anchorMax = new Vector2(heading / 360f, 1);
            }
        }

        static string HeadingToCompass(float h)
        {
            string[] dirs = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                              "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
            int i = Mathf.RoundToInt(h / 22.5f) % 16;
            return dirs[i];
        }

        // ══════════════════════════════════════════════════
        //  通信链路 (模拟)
        // ══════════════════════════════════════════════════

        float commsSignalQuality = 1f;
        float commsTimer = 0f;

        void UpdateComms()
        {
            commsTimer += Time.deltaTime;
            if (commsTimer < 0.5f) return;
            commsTimer = 0f;

            if (commsText == null) return;

            // 信号质量: 离原点越远越差 + 地形遮挡
            Vector3 pos = transform.position;
            float distKm = pos.magnitude / 1000f;
            float terrainBlock = Mathf.PerlinNoise(pos.x * 0.005f, pos.z * 0.005f) * 0.3f;
            commsSignalQuality = Mathf.Clamp01(1f - distKm * 0.02f - terrainBlock);

            int bars = Mathf.RoundToInt(commsSignalQuality * 5f);
            string barStr = new string('█', bars) + new string('░', 5 - bars);
            int latencyMs = Mathf.RoundToInt(20 + (1f - commsSignalQuality) * 180f);
            float dataRate = commsSignalQuality * 10f;

            commsText.text = string.Format("📡 [{0}]  {1}ms  {2:F1} Mbps",
                barStr, latencyMs, dataRate);
            commsText.color = commsSignalQuality > 0.6f ? COL.Green
                : (commsSignalQuality > 0.3f ? COL.Yellow : COL.Red);
        }

        // ══════════════════════════════════════════════════
        //  航点导航
        // ══════════════════════════════════════════════════

        void UpdateWaypoints()
        {
            if (waypointText == null) return;

            if (aiDriver != null && aiDriver.isActiveAndEnabled)
            {
                // 读取 AI 驾驶的当前目标
                System.Type navType = aiDriver.GetType();
                var targetField = navType.GetField("target",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                Transform target = targetField?.GetValue(aiDriver) as Transform;

                if (target != null)
                {
                    float dist = Vector3.Distance(transform.position, target.position);
                    Vector3 dir = (target.position - transform.position).normalized;
                    float angle = Vector3.Angle(transform.forward, dir);
                    string side = Vector3.Cross(transform.forward, dir).y > 0 ? "←" : "→";

                    waypointText.text = string.Format("航点 {0:F0}m  {1} {2:F0}°",
                        dist, side, angle);
                }
                else
                {
                    waypointText.text = "航点: —";
                }
            }
            else
            {
                waypointText.text = "航点: 手动模式";
                waypointText.color = COL.Gray;
            }
        }

        // ══════════════════════════════════════════════════
        //  事件日志
        // ══════════════════════════════════════════════════

        void DetectEvents()
        {
            if (modeMgr != null)
            {
                string curMode = modeMgr.currentMode.ToString();
                if (curMode != lastModeName)
                {
                    lastModeName = curMode;
                    PushEvent("驾驶模式 → " + curMode);
                }
            }

            if (manualDriver != null)
            {
                bool aebOn = manualDriver.isAEBActive;
                if (aebOn != lastAEBState)
                {
                    lastAEBState = aebOn;
                    if (aebOn) PushEvent("⚠ AEB 紧急制动触发");
                    else PushEvent("✓ AEB 解除");
                }
            }

            if (platformCtrl != null)
            {
                // 通过 MotionPlatformController 的反射或公共字段
                // 直接用 statusText 的颜色判断
                bool platOnline = false;
                var statusField = typeof(MotionPlatformController).GetField("statusText",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var st = statusField?.GetValue(platformCtrl) as Text;
                if (st != null) platOnline = st.color == Color.green;

                if (platOnline != lastPlatformState)
                {
                    lastPlatformState = platOnline;
                    PushEvent(platOnline ? "✓ 动感平台已连接" : "动感平台已断开");
                }
            }
        }

        void UpdateEventLog()
        {
            logTimer += Time.deltaTime;
            if (logTimer < logInterval) return;
            logTimer = 0f;

            if (eventLogText == null) return;

            var lines = eventLog.ToArray();
            System.Array.Reverse(lines);
            eventLogText.text = string.Join("\n", lines);
        }

        void PushEvent(string msg)
        {
            string entry = System.DateTime.Now.ToString("HH:mm:ss") + " " + msg;
            eventLog.Enqueue(entry);
            if (eventLog.Count > maxLogLines) eventLog.Dequeue();
        }

        // ══════════════════════════════════════════════════
        //  静态颜色工具
        // ══════════════════════════════════════════════════

        static class COL
        {
            public static readonly Color Green  = new Color(0.3f, 1f, 0.3f);
            public static readonly Color Yellow = new Color(1f, 0.75f, 0.1f);
            public static readonly Color Red    = new Color(1f, 0.2f, 0.15f);
            public static readonly Color Gray   = new Color(0.6f, 0.6f, 0.6f);
        }
    }
}

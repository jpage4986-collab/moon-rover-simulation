using System.Text;
using UnityEngine;

namespace MoonRover.Driver
{
    /// <summary>
    /// Shows the legacy Unity joystick name, steering/throttle axes and pressed buttons.
    /// Diagnostic only: it never sends commands to the motion platform.
    /// </summary>
    public sealed class GamepadInputMonitor : MonoBehaviour
    {
        public bool showOverlay = true;
        public bool logInputToConsole = true;
        public float refreshInterval = 0.25f;

        private string[] joystickNames = new string[0];
        private string lastReport = "";
        private float nextRefresh;
        private GUIStyle boxStyle;
        private LogitechDriver wheelDriver;

        private void Start()
        {
            wheelDriver = FindObjectOfType<LogitechDriver>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);

            joystickNames = Input.GetJoystickNames();
            string report = BuildReport();
            if (logInputToConsole && report != lastReport)
            {
                Debug.Log("[手柄检测] " + report.Replace("\n", " | "));
                lastReport = report;
            }
        }

        private string BuildReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("设备=");

            bool hasDevice = false;
            for (int i = 0; i < joystickNames.Length; i++)
            {
                if (string.IsNullOrEmpty(joystickNames[i])) continue;
                if (hasDevice) sb.Append(", ");
                sb.Append(joystickNames[i]);
                hasDevice = true;
            }
            if (!hasDevice) sb.Append("未识别");

            if (wheelDriver != null)
            {
                sb.Append("\n左盘  转向原始=");
                sb.Append(wheelDriver.LeftSteeringRaw.ToString("F2"));
                sb.Append("  油门原始=");
                sb.Append(wheelDriver.LeftThrottleRaw.ToString("F2"));
                sb.Append("  刹车原始=");
                sb.Append(wheelDriver.LeftBrakeRaw.ToString("F2"));
                sb.Append("  Bottom3=");
                sb.Append(IsDeviceButtonPressed(2, 2) ? "按下" : "-");

                sb.Append("\n右盘  转向原始=");
                sb.Append(wheelDriver.RightSteeringRaw.ToString("F2"));
                sb.Append("  油门原始=");
                sb.Append(wheelDriver.RightThrottleRaw.ToString("F2"));
                sb.Append("  刹车原始=");
                sb.Append(wheelDriver.RightBrakeRaw.ToString("F2"));
                sb.Append("  Bottom3=");
                sb.Append(IsDeviceButtonPressed(3, 2) ? "按下" : "-");

                sb.Append("\n当前主控=");
                sb.Append(wheelDriver.GetActiveWheelLabel());
                sb.Append("  回中锁定=");
                sb.Append(wheelDriver.IsWaitingForCenter ? "是" : "否");
                sb.Append("  踏板校准=");
                sb.Append(wheelDriver.PedalsCalibrated ? "完成" : "进行中");
                sb.Append("  最终转向=");
                sb.Append(wheelDriver.GetSteeringInput().ToString("F2"));
                sb.Append("  最终油门=");
                sb.Append(wheelDriver.GetAccelInput().ToString("F2"));
                sb.Append("  最终刹车=");
                sb.Append(wheelDriver.GetBrakeInput().ToString("F2"));
            }
            else
            {
                sb.Append("\n未找到 LogitechDriver");
            }

            sb.Append("\n左盘全轴: ");
            sb.Append(BuildAxisScan(2));
            sb.Append("\n右盘全轴: ");
            sb.Append(BuildAxisScan(3));

            sb.Append("\n按下按钮=");
            bool hasButton = false;
            for (int i = 0; i < 20; i++)
            {
                if (!Input.GetKey((KeyCode)((int)KeyCode.JoystickButton0 + i))) continue;
                if (hasButton) sb.Append(", ");
                sb.Append(i + 1);
                hasButton = true;
            }
            if (!hasButton) sb.Append("无");

            sb.Append("\n底座设备4按钮=");
            bool hasBaseButton = false;
            for (int i = 0; i < 20; i++)
            {
                if (!IsDeviceButtonPressed(4, i)) continue;
                if (hasBaseButton) sb.Append(", ");
                // Display numbering is one-based; the code is Joystick4Button(i).
                sb.Append(i + 1);
                hasBaseButton = true;
            }
            if (!hasBaseButton) sb.Append("无");

            return sb.ToString();
        }

        private static string BuildAxisScan(int joystickNumber)
        {
            StringBuilder sb = new StringBuilder();
            for (int axis = 0; axis < 10; axis++)
            {
                float value = Input.GetAxisRaw(
                    "MoonRoverScanJ" + joystickNumber + "A" + axis);
                if (axis > 0) sb.Append("  ");
                sb.Append("A");
                sb.Append(axis);
                sb.Append("=");
                sb.Append(value.ToString("F2"));
            }
            return sb.ToString();
        }

        private static bool IsDeviceButtonPressed(int joystickNumber, int buttonIndex)
        {
            int keyCode = (int)KeyCode.Joystick1Button0 +
                (joystickNumber - 1) * 20 + buttonIndex;
            return Input.GetKey((KeyCode)keyCode);
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.alignment = TextAnchor.UpperLeft;
                boxStyle.fontSize = 16;
                boxStyle.normal.textColor = Color.white;
            }

            GUI.Box(new Rect(16, 16, 1100, 290),
                "双方向盘实时检测（仅显示，不控制平台）\n" + BuildReport() +
                "\n任一方向盘 Bottom 3：该方向盘接管主控；底座第1按钮：方向盘/手柄切换",
                boxStyle);
        }
    }
}

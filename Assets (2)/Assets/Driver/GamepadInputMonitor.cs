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

            sb.Append("\nHorizontal=");
            sb.Append(Input.GetAxis("Horizontal").ToString("F2"));
            sb.Append("  Vertical=");
            sb.Append(Input.GetAxis("Vertical").ToString("F2"));

            sb.Append("\n按下按钮=");
            bool hasButton = false;
            for (int i = 0; i < 20; i++)
            {
                if (!Input.GetKey((KeyCode)((int)KeyCode.JoystickButton0 + i))) continue;
                if (hasButton) sb.Append(", ");
                sb.Append(i);
                hasButton = true;
            }
            if (!hasButton) sb.Append("无");

            return sb.ToString();
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

            GUI.Box(new Rect(16, 16, 520, 130),
                "通用手柄\n" + BuildReport() +
                "\n左摇杆：转向/前进  A(按钮0)：刹车  B(按钮1)：手刹",
                boxStyle);
        }
    }
}

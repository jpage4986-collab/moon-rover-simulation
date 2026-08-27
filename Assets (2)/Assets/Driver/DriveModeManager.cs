using UnityEngine;
using UnityEngine.UI;
using MoonRover.Driver;
using MoonRover.Navigation;

namespace MoonRover.Driver
{
    public class DriveModeManager : MonoBehaviour
    {
        public enum DriveMode { Manual, LocalAI, HybridAI }

        [Header("控制权状态")]
        public DriveMode currentMode = DriveMode.Manual;

        [Header("UI 引用")]
        public Text modeText;
        public Button btnManual;
        public Button btnLocalAI;
        public Button btnHybridAI;
        public Image bgManual;
        public Image bgLocalAI;
        public Image bgHybridAI;

        private MonoBehaviour manualDriver;
        private MonoBehaviour localAI;
        private MonoBehaviour hybridAI;

        private Color activeGreen = new Color(0.2f, 0.8f, 0.2f);
        private Color activeCyan = new Color(0.2f, 0.8f, 0.8f);
        private Color activeYellow = new Color(0.8f, 0.8f, 0.2f);
        private Color inactiveGray = new Color(0.35f, 0.35f, 0.35f);

        void Awake()
        {
            manualDriver = GetComponent<ManualDriverWithAEB>() as MonoBehaviour;
            localAI = GetComponent<AutoDriver>() as MonoBehaviour;
            hybridAI = GetComponent<HybridRoverAI>() as MonoBehaviour;
        }

        void Start()
        {
            SwitchMode(currentMode);

            if (btnManual != null) btnManual.onClick.AddListener(() => SwitchMode(DriveMode.Manual));
            if (btnLocalAI != null) btnLocalAI.onClick.AddListener(() => SwitchMode(DriveMode.LocalAI));
            if (btnHybridAI != null) btnHybridAI.onClick.AddListener(() => SwitchMode(DriveMode.HybridAI));
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchMode(DriveMode.Manual);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchMode(DriveMode.LocalAI);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchMode(DriveMode.HybridAI);
        }

        void SwitchMode(DriveMode newMode)
        {
            currentMode = newMode;
            (manualDriver as IDriver)?.EnableDriver(currentMode == DriveMode.Manual);
            (localAI as IDriver)?.EnableDriver(currentMode == DriveMode.LocalAI);
            (hybridAI as IDriver)?.EnableDriver(currentMode == DriveMode.HybridAI);

            UpdateUI();
        }

        void UpdateUI()
        {
            if (modeText != null)
            {
                switch (currentMode)
                {
                    case DriveMode.Manual: modeText.text = "当前模式: 手动+AEB"; break;
                    case DriveMode.LocalAI: modeText.text = "当前模式: 局部雷达 AI"; break;
                    case DriveMode.HybridAI: modeText.text = "当前模式: 卫星混合 AI"; break;
                }
            }

            if (bgManual != null) bgManual.color = currentMode == DriveMode.Manual ? activeGreen : inactiveGray;
            if (bgLocalAI != null) bgLocalAI.color = currentMode == DriveMode.LocalAI ? activeCyan : inactiveGray;
            if (bgHybridAI != null) bgHybridAI.color = currentMode == DriveMode.HybridAI ? activeYellow : inactiveGray;
        }
    }
}

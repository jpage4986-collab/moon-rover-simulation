using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Vehicles.Car;

namespace MoonRover.Driver
{
    [RequireComponent(typeof(CarController))]
    public class ManualDriverWithAEB : MonoBehaviour, IDriver
    {
        [Header("AEB 紧急刹车雷达")]
        public bool enableAEB = false;
        public float aebDetectDistance = 4.0f;
        public float dangerSteepness = 30f;

        [Header("G29 方向盘设置")]
        [Tooltip("使用G29方向盘代替键盘控制")]
        public bool useLogitechSteeringWheel = true;
        public LogitechDriver logitechDriver;

        [Header("输入滤波")]
        [Tooltip("油门/刹车死区，低于此值归零")]
        [Range(0f, 0.4f)]
        public float inputDeadzone = 0.15f;
        [Tooltip("手柄转向死区。方向盘不使用这个较大的手柄死区。")]
        [Range(0f, 0.2f)]
        public float steeringInputDeadzone = 0.05f;
        [Tooltip("油门响应曲线指数：大于 1 时轻推更柔和，满油门仍接近 100%。")]
        [Range(1f, 3f)]
        public float throttleResponseExponent = 1.6f;
        [Tooltip("油门释放平滑速度（越大越快跟上）")]
        public float releaseSmoothing = 5f;

        [Header("通用手柄")]
        [Tooltip("未连接 G29 时，使用 Unity 的通用手柄轴控制月球车。")]
        public bool useGenericGamepad = true;
        [Tooltip("通用手柄刹车按钮，实际读取 Unity 按钮 2，面板显示为按钮 3。")]
        public int gamepadBrakeButton = 2;
        [Tooltip("通用手柄手刹按钮，实际读取 Unity 按钮 3，面板显示为按钮 4。")]
        public int gamepadHandbrakeButton = 3;
        [Tooltip("通用手柄当前为 Unity 设备 1；与设备 4 的底座按钮盒分开读取。")]
        public int genericGamepadJoystickNumber = 1;
        [Tooltip("Reverse engagement speed threshold in meters per second")]
        public float reverseEngageSpeed = 0.15f;

        [Header("手柄静止保护")]
        [Tooltip("启动或切换到手柄模式后，自动记录当前摇杆中心，修正 USB 手柄的中心偏差。")]
        public bool autoCalibrateGamepadCenter = true;
        [Tooltip("手柄中心校准时长（秒）。校准期间请不要推动摇杆。")]
        public float gamepadCenterCalibrationSeconds = 0.6f;
        [Tooltip("没有任何输入时保持刹车，避免底盘在坡面上自行向后滑。")]
        public bool holdBrakeWhenNeutral = true;
        [Tooltip("切换到方向盘模式时，方向盘回到中位后才重新接收转向输入。")]
        [Range(0.03f, 0.2f)]
        public float steeringWheelCenterDeadzone = 0.1f;

        public enum DriveInputMode
        {
            SteeringWheel = 0,
            Joystick = 1
        }

        [Header("驾驶模式切换")]
        [Tooltip("底座控制器检测顺序第 1 个按钮：在方向盘驾驶和手柄驾驶之间切换。")]
        public DriveInputMode driveMode = DriveInputMode.SteeringWheel;
        [Tooltip("检测顺序第 1 个按钮对应 Unity 按钮 0。")]
        public int modeToggleButton = 0;
        [Tooltip("底座控制器的 Unity 摇杆编号。当前设备顺序中 Generic USB Joystick 是第 4 个。")]
        public int baseControllerJoystickNumber = 4;
        [Tooltip("是否启用底座控制器按钮切换驾驶模式。")]
        public bool toggleModeFromBaseController = true;
        [Tooltip("底座灯光按钮：与检测程序显示的 11 号按钮对应 Unity 索引 10。")]
        public int headlightToggleButton = 10;

        [Header("UI 引用 (由 UISetupWizard 绑定)")]
        public Text aebAlertText;
        public Image aebOverlayImage;
        public Text g29StatusText;

        [Header("UI 警报")]
        public bool isAEBActive = false;

        private CarController m_Car;
        [SerializeField] private Terrain targetTerrain;
        private float currentSteerInput = 0f;
        private string aebReason = "";
        private float m_SmoothedAccel = 0f;
        private float m_SmoothedBrake = 0f;
        private bool modeToggleButtonWasPressed = false;
        private bool headlightToggleButtonWasPressed = false;
        private float gamepadCenterHorizontal = 0f;
        private float gamepadCenterVertical = 0f;
        private float gamepadCalibrationTime = 0f;
        private Vector2 gamepadCalibrationSum = Vector2.zero;
        private int gamepadCalibrationSamples = 0;
        private bool gamepadCenterCalibrated = false;
        private bool steeringWheelNeedsCentering = false;

        void Awake()
        {
            m_Car = GetComponent<CarController>();
            if (targetTerrain == null)
            {
                targetTerrain = Terrain.activeTerrain;
                if (targetTerrain == null)
                {
                    Debug.LogError("[ManualDriverWithAEB] No Terrain found in scene!");
                    enabled = false;
                }
            }
            if (useLogitechSteeringWheel && logitechDriver == null)
            {
                logitechDriver = GetComponent<LogitechDriver>();
            }

        }

        void Start()
        {
            ResetGamepadCenterCalibration();
            UpdateG29Status();
        }

        public float GetSteeringInput()
        {
            return currentSteerInput;
        }

        public void EnableDriver(bool enable)
        {
            this.enabled = enable;
            if (!enable)
            {
                isAEBActive = false;
                UpdateAEBAlert();
            }
        }

        void Update()
        {
            // 方向盘状态刷新不依赖底座按钮控制器是否存在。
            if (driveMode == DriveInputMode.SteeringWheel &&
                useLogitechSteeringWheel && logitechDriver != null)
            {
                logitechDriver.UpdateState();
            }

            bool hasBaseController = useGenericGamepad && HasGamepad();
            if (!hasBaseController)
            {
                modeToggleButtonWasPressed = false;
                headlightToggleButtonWasPressed = false;
                return;
            }

            if (driveMode == DriveInputMode.Joystick)
                UpdateGamepadCenterCalibration(Time.deltaTime);

            if (!toggleModeFromBaseController)
            {
                modeToggleButtonWasPressed = false;
            }

            bool pressed = IsJoystickButtonPressed(modeToggleButton, baseControllerJoystickNumber);
            if (toggleModeFromBaseController && pressed && !modeToggleButtonWasPressed)
            {
                driveMode = driveMode == DriveInputMode.SteeringWheel
                    ? DriveInputMode.Joystick
                    : DriveInputMode.SteeringWheel;

                // 切换瞬间清掉滤波残留，避免上一种模式的油门/刹车继续保持。
                m_SmoothedAccel = 0f;
                m_SmoothedBrake = 0f;
                ResetGamepadCenterCalibration();
                steeringWheelNeedsCentering = driveMode == DriveInputMode.SteeringWheel;

                Debug.Log("[ManualDriverWithAEB] 驾驶模式切换为: " + GetDriveModeLabel());
                UpdateG29Status();
            }

            // 边沿触发：按住按钮只切换一次，松开后才能再次切换。
            modeToggleButtonWasPressed = pressed;

            // 灯光切换直接复用驾驶模式按钮的底座设备和 Input.GetKey 读取方式。
            bool headlightPressed = IsJoystickButtonPressed(
                headlightToggleButton, baseControllerJoystickNumber);
            if (headlightPressed && !headlightToggleButtonWasPressed && m_Car != null)
            {
                m_Car.ToggleHeadlights();
                Debug.Log("[ManualDriverWithAEB] 收到底座灯光按钮11，Unity索引=" +
                    headlightToggleButton);
            }
            headlightToggleButtonWasPressed = headlightPressed;
        }

        void FixedUpdate()
        {
            float h, v, footbrake = 0f, handbrake = 0f;

            if (driveMode == DriveInputMode.SteeringWheel)
            {
                // 方向盘模式：方向盘、油门、刹车均来自 LogitechDriver。
                h = useLogitechSteeringWheel && logitechDriver != null
                    ? ApplyDeadzone(logitechDriver.GetSteeringInput(), steeringInputDeadzone)
                    : 0f;
                v = useLogitechSteeringWheel && logitechDriver != null
                    ? logitechDriver.GetAccelInput()
                    : 0f;
                footbrake = useLogitechSteeringWheel && logitechDriver != null
                    ? logitechDriver.GetBrakeInput()
                    : 0f;

                if (steeringWheelNeedsCentering)
                {
                    // 方向盘回中之前，车辆保持直行并刹车，不接受残留转角/踏板输入。
                    bool wheelIsCentered = !useLogitechSteeringWheel || logitechDriver == null ||
                        !logitechDriver.IsConnected() ||
                        Mathf.Abs(logitechDriver.GetSteeringInput()) <= steeringWheelCenterDeadzone;
                    h = 0f;
                    v = 0f;
                    footbrake = 1f;
                    if (wheelIsCentered)
                    {
                        steeringWheelNeedsCentering = false;
                        Debug.Log("[ManualDriverWithAEB] 方向盘已回中，恢复方向盘控制");
                    }
                }
            }
            else
            {
                // 手柄模式：使用项目中已经验证能读到 PXN 数值的 Unity 通用轴。
                // 不再动态访问未登记的“第 2 号轴”，避免 InputException 中断 FixedUpdate。
                float configuredHorizontal = Input.GetAxisRaw("Horizontal");
                float configuredVertical = Input.GetAxisRaw("Vertical");
                if (autoCalibrateGamepadCenter && !gamepadCenterCalibrated)
                {
                    // 校准期间不把中心偏差当成倒车输入。
                    configuredHorizontal = 0f;
                    configuredVertical = 0f;
                }
                else if (autoCalibrateGamepadCenter && gamepadCenterCalibrated)
                {
                    configuredHorizontal = NormalizeCenteredAxis(configuredHorizontal, gamepadCenterHorizontal);
                    configuredVertical = NormalizeCenteredAxis(configuredVertical, gamepadCenterVertical);
                }
                h = ApplyDeadzone(configuredHorizontal);
                v = ApplyDeadzone(configuredVertical);
                if (useGenericGamepad && HasGamepad() &&
                    IsJoystickButtonPressed(gamepadBrakeButton, genericGamepadJoystickNumber))
                    footbrake = 1f;
                if (useGenericGamepad && HasGamepad() &&
                    IsJoystickButtonPressed(gamepadHandbrakeButton, genericGamepadJoystickNumber))
                    handbrake = 1f;
            }

            // 非线性油门：轻推时压低输出，推满时保持满输出。
            v = ApplyThrottleResponse(v);

            // 平滑油门释放：释放时慢慢归零，避免急停
            // 但油门和刹车互斥：有油门时立刻清刹车，有刹车时立刻清油门
            float rawAccel = v;
            float rawBrake = footbrake;

            float signedSpeed = Vector3.Dot(m_Car.CarRigidbody.velocity, transform.forward);
            bool brakingBeforeDirectionChange =
                (rawAccel < -0.01f && signedSpeed > reverseEngageSpeed) ||
                (rawAccel > 0.01f && signedSpeed < -reverseEngageSpeed);
            if (brakingBeforeDirectionChange)
            {
                rawAccel = 0f;
                rawBrake = Mathf.Max(rawBrake, 1f);
            }

            if (Mathf.Abs(rawAccel) > 0.01f)
            {
                // 有油门 → 油门快速响应，刹车立刻清零
                m_SmoothedAccel = Mathf.Lerp(m_SmoothedAccel, rawAccel, Time.fixedDeltaTime * 12f);
                m_SmoothedBrake = 0f;
            }
            else if (rawBrake > 0.01f)
            {
                // 有刹车 → 刹车快速响应，油门立刻清零
                m_SmoothedBrake = Mathf.Lerp(m_SmoothedBrake, rawBrake, Time.fixedDeltaTime * 12f);
                m_SmoothedAccel = 0f;
            }
            else
            {
                // 都松开 → 两者都慢慢归零
                m_SmoothedAccel = Mathf.Lerp(m_SmoothedAccel, 0f, Time.fixedDeltaTime * releaseSmoothing);
                m_SmoothedBrake = Mathf.Lerp(m_SmoothedBrake, 0f, Time.fixedDeltaTime * releaseSmoothing);
            }
            v = m_SmoothedAccel;
            footbrake = m_SmoothedBrake;

            if (steeringWheelNeedsCentering)
            {
                v = 0f;
                footbrake = 1f;
            }

            // 无输入时锁住底盘，避免月面地形的坡度或物理误差让车辆自行倒退。
            // 一旦检测到明确的前进/后退输入，立即交回正常驾驶逻辑。
            if (holdBrakeWhenNeutral &&
                Mathf.Abs(v) <= 0.01f && footbrake <= 0.01f && handbrake <= 0.01f)
            {
                footbrake = 1f;
            }

            currentSteerInput = h;

            isAEBActive = false;
            aebReason = "";

            if (enableAEB && v > 0.1f)
            {
                if (targetTerrain != null)
                {
                    Vector3 sensorPos = transform.position + transform.forward * aebDetectDistance;
                    float normX = (sensorPos.x - targetTerrain.transform.position.x) / targetTerrain.terrainData.size.x;
                    float normZ = (sensorPos.z - targetTerrain.transform.position.z) / targetTerrain.terrainData.size.x;

                    if (targetTerrain.terrainData.GetSteepness(normX, normZ) > dangerSteepness)
                    {
                        isAEBActive = true;
                        aebReason = "前方悬崖/陡坡拦截";
                    }
                }

                if (!isAEBActive)
                {
                    Vector3 rayStart = transform.position + transform.up * 0.5f + transform.forward * 1.5f;

                    if (Physics.Raycast(rayStart, transform.forward, out RaycastHit hit, aebDetectDistance))
                    {
                        if (hit.collider.gameObject.layer != LayerMask.NameToLayer("Terrain") && hit.transform.root != this.transform.root)
                        {
                            isAEBActive = true;
                            aebReason = "前方障碍物拦截";
                            Debug.DrawRay(rayStart, transform.forward * hit.distance, Color.red, 0.1f);
                        }
                    }
                }
            }

            if (isAEBActive)
            {
                // 分级制动：低速用刹车，高速不用手刹避免锁死
                float currentSpeed = m_Car.CarRigidbody.velocity.magnitude * 2.23693629f; // MPH
                float brakeAmount = Mathf.Clamp01(currentSpeed / 30f); // 速度越快刹车越重
                m_Car.Move(currentSteerInput, 0f, -brakeAmount, 0f);
            }
            else
            {
                m_Car.Move(currentSteerInput, v, footbrake, handbrake);
            }

            UpdateAEBAlert();
            UpdateG29Status();
        }

        void UpdateG29Status()
        {
            if (g29StatusText == null) return;

            g29StatusText.text = "模式：" + GetDriveModeLabel();
            if (driveMode == DriveInputMode.SteeringWheel && steeringWheelNeedsCentering)
                g29StatusText.text += "（回中中）";
            else if (driveMode == DriveInputMode.SteeringWheel && logitechDriver != null)
                g29StatusText.text += " · " + logitechDriver.GetActiveWheelLabel() +
                    (logitechDriver.IsWaitingForCenter ? "（回中中）" : "主控");
            if (driveMode == DriveInputMode.SteeringWheel && logitechDriver != null)
                g29StatusText.text += " · " + logitechDriver.GetGearLabel();

            if (driveMode == DriveInputMode.SteeringWheel)
            {
                g29StatusText.color = Color.cyan;
            }
            else
            {
                g29StatusText.color = Color.green;
            }
        }

        private string GetDriveModeLabel()
        {
            return driveMode == DriveInputMode.SteeringWheel ? "方向盘" : "手柄";
        }

        /// <summary>
        /// 输入死区：低于阈值归零，高于阈值重新映射到 0~1
        /// </summary>
        private float ApplyDeadzone(float value)
        {
            return ApplyDeadzone(value, inputDeadzone);
        }

        private float ApplyDeadzone(float value, float deadzone)
        {
            deadzone = Mathf.Clamp01(deadzone);
            if (Mathf.Abs(value) < deadzone) return 0f;
            // 将 [deadzone, 1] 重新映射到 [0, 1]
            return Mathf.Sign(value) * (Mathf.Abs(value) - deadzone) / (1f - deadzone);
        }

        private float ApplyThrottleResponse(float value)
        {
            float exponent = Mathf.Max(1f, throttleResponseExponent);
            return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), exponent);
        }

        private float NormalizeCenteredAxis(float raw, float center)
        {
            float delta = raw - center;
            if (delta >= 0f)
                return Mathf.Clamp01(delta / Mathf.Max(0.001f, 1f - center));
            return -Mathf.Clamp01(-delta / Mathf.Max(0.001f, 1f + center));
        }

        private void ResetGamepadCenterCalibration()
        {
            gamepadCenterHorizontal = 0f;
            gamepadCenterVertical = 0f;
            gamepadCalibrationTime = 0f;
            gamepadCalibrationSum = Vector2.zero;
            gamepadCalibrationSamples = 0;
            gamepadCenterCalibrated = !autoCalibrateGamepadCenter;
        }

        private void UpdateGamepadCenterCalibration(float deltaTime)
        {
            if (!autoCalibrateGamepadCenter || gamepadCenterCalibrated)
                return;

            gamepadCalibrationSum += new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));
            gamepadCalibrationSamples++;
            gamepadCalibrationTime += deltaTime;

            if (gamepadCalibrationTime < Mathf.Max(0.1f, gamepadCenterCalibrationSeconds))
                return;

            if (gamepadCalibrationSamples > 0)
            {
                Vector2 center = gamepadCalibrationSum / gamepadCalibrationSamples;
                // 有些手柄静止时会报告 -1 或 1，不能截断为 ±0.95，
                // 否则静止值会再次被归一化成满量程倒车/前进。
                gamepadCenterHorizontal = Mathf.Clamp(center.x, -0.999f, 0.999f);
                gamepadCenterVertical = Mathf.Clamp(center.y, -0.999f, 0.999f);
            }
            gamepadCenterCalibrated = true;
            Debug.Log(string.Format("[ManualDriverWithAEB] 手柄中心校准完成: H={0:F3}, V={1:F3}",
                gamepadCenterHorizontal, gamepadCenterVertical));
        }

        private bool HasGamepad()
        {
            string[] names = Input.GetJoystickNames();
            if (names == null) return false;

            for (int i = 0; i < names.Length; i++)
            {
                if (!string.IsNullOrEmpty(names[i])) return true;
            }

            return false;
        }

        private bool IsJoystickButtonPressed(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex > 19) return false;
            return Input.GetKey((KeyCode)((int)KeyCode.JoystickButton0 + buttonIndex));
        }

        private bool IsJoystickButtonPressed(int buttonIndex, int joystickNumber)
        {
            if (buttonIndex < 0 || buttonIndex > 19) return false;

            // Unity 的 Joystick1Button0...Joystick8Button19 按每个摇杆 20 个按钮连续排列。
            if (joystickNumber < 1 || joystickNumber > 8)
                return IsJoystickButtonPressed(buttonIndex);

            int keyCodeValue = (int)KeyCode.Joystick1Button0
                + (joystickNumber - 1) * 20
                + buttonIndex;
            return Input.GetKey((KeyCode)keyCodeValue);
        }

        void UpdateAEBAlert()
        {
            if (aebAlertText != null)
            {
                if (isAEBActive)
                {
                    aebAlertText.text = "⚠ AEB 紧急制动 ⚠\n(" + aebReason + ")";
                    // 闪烁效果: 每 0.2s 切换可见性
                    aebAlertText.gameObject.SetActive(Mathf.FloorToInt(Time.time * 5f) % 2 == 0);
                }
                else
                {
                    aebAlertText.gameObject.SetActive(false);
                }
            }

            if (aebOverlayImage != null)
            {
                if (isAEBActive && Mathf.FloorToInt(Time.time * 5f) % 2 == 0)
                {
                    aebOverlayImage.color = new Color(0.5f, 0, 0, 0.3f);
                }
                else
                {
                    aebOverlayImage.color = new Color(0, 0, 0, 0f);
                }
            }
        }

        void OnDisable()
        {
            isAEBActive = false;
            UpdateAEBAlert();
        }
    }
}

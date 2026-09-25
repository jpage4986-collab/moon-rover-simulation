using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;

namespace MoonRover.Driver
{
    [RequireComponent(typeof(CarController))]
    public class LogitechDriver : MonoBehaviour, IDriver
    {
        public enum WheelSide { Left, Right }
        public enum DriveGear { Reverse = -1, Forward = 1 }

        private bool isLogiInit = false;

        [Header("双方向盘")]
        [Tooltip("Unity 输入列表中的左方向盘编号。当前设备顺序为 PXN、左 G29、右 G29、底座按钮盒。")]
        public int leftUnityJoystickNumber = 2;
        [Tooltip("Unity 输入列表中的右方向盘编号。")]
        public int rightUnityJoystickNumber = 3;
        [Tooltip("Logitech SDK 中左方向盘索引。根据启动力反馈实测，左盘对应 0。")]
        public int leftLogitechSdkIndex = 2;
        [Tooltip("Logitech SDK 中右方向盘索引。")]
        public int rightLogitechSdkIndex = 3;
        [Tooltip("左方向盘 HID 实例标识，用于从混合控制器列表中自动找到 G29。")]
        public string leftWheelHidToken = "31e78f2e";
        [Tooltip("右方向盘 HID 实例标识，用于从混合控制器列表中自动找到 G29。")]
        public string rightWheelHidToken = "2e08a0a1";
        [Tooltip("G29 油门所在的 Unity 轴（从 0 开始）。全轴实测为 A2。")]
        [Range(0, 9)] public int throttleAxisIndex = 2;
        [Tooltip("G29 刹车所在的 Unity 轴（从 0 开始）。G29 标准映射为 A3。")]
        [Range(0, 9)] public int brakeAxisIndex = 3;
        [Tooltip("实体按键显示编号。Bottom 3 对应编号 3。")]
        public int masterSelectButtonNumber = 3;
        [Tooltip("左拨片检测编号 6：切换后退档。")]
        public int reversePaddleButtonNumber = 6;
        [Tooltip("右拨片检测编号 5：切换前进档。")]
        public int forwardPaddleButtonNumber = 5;
        public WheelSide defaultMaster = WheelSide.Left;
        public DriveGear defaultGear = DriveGear.Forward;
        [Tooltip("从动方向盘使用力反馈弹簧跟随主控方向盘。")]
        public bool enableSlaveFollow = true;
        [Range(0f, 100f)] public float slaveFollowSaturation = 100f;
        [Range(0f, 100f)] public float slaveFollowCoefficient = 100f;
        public bool invertSlaveFollow = true;
        [Tooltip("主动跟随比例增益：主从方向盘角度差越大，电机拉力越大。")]
        [Range(1f, 150f)] public float slaveFollowForceGain = 100f;
        [Tooltip("从动方向盘最大电机拉力百分比，限制突发转动。")]
        [Range(5f, 80f)] public float slaveFollowMaxForce = 45f;
        [Tooltip("克服 G29 齿轮静摩擦所需的最小驱动力百分比。")]
        [Range(0f, 40f)] public float slaveFollowMinForce = 20f;
        [Tooltip("从盘转动速度阻尼，减少追随目标时来回振荡。")]
        [Range(0f, 30f)] public float slaveFollowDamping = 8f;
        [Tooltip("主从角度差小于该值时停止施力，避免中位抖动。")]
        [Range(0.005f, 0.15f)] public float slaveFollowDeadzone = 0.01f;
        [Tooltip("两只 G29 统一工作范围。G29 最大为 900 度。")]
        [Range(180, 900)] public int wheelOperatingRange = 900;

        public WheelSide ActiveMaster { get; private set; }
        public DriveGear CurrentGear { get; private set; }
        public bool IsWaitingForCenter { get; private set; }
        public bool PedalsCalibrated { get { return pedalsCalibrated; } }
        public float LeftSteeringRaw { get { return leftSteer; } }
        public float RightSteeringRaw { get { return rightSteer; } }
        public float LeftThrottleRaw { get { return leftThrottle; } }
        public float RightThrottleRaw { get { return rightThrottle; } }
        public float LeftBrakeRaw { get { return leftBrake; } }
        public float RightBrakeRaw { get { return rightBrake; } }

        [Header("控制设置")]
        public float steerSensitivity = 1.0f;
        [Range(0f, 0.2f)]
        public float steerDeadzone = 0.05f;

        [Header("力反馈设置")]
        [Range(0f, 100f)]
        public float springSaturationMin = 30f;
        [Range(0f, 100f)]
        public float springSaturationMax = 80f;
        [Range(0f, 100f)]
        public float springCoefficientMin = 30f;
        [Range(0f, 100f)]
        public float springCoefficientMax = 80f;
        public float maxSpeedForFF = 20f;

        [Header("踏板曲线设置")]
        [Range(1f, 3f)]
        public float accelCurvePower = 1.5f;
        [Range(1f, 3f)]
        public float brakeCurvePower = 1.5f;

        [Header("地形震动反馈设置")]
        public bool enableTerrainVibration = true;
        [Range(0f, 100f)]
        public float maxVibrationStrength = 70f;
        public float vibrationSmoothSpeed = 5f;
        [Header("坡度震动阈值")]
        public float slopeThreshold = 15f;
        [Range(0f, 90f)]
        public float maxSlopeForVibration = 45f;
        [Header("颠簸震动阈值")]
        public float fvThreshold = 5f;
        [Range(0f, 90f)]
        public float maxFvForVibration = 45f;
        public float terrainCheckDistance = 8f;

        [Header("数据源")]
        public MoonRover.FeedBack.Scan terrainScanner;

        private float leftSteer;
        private float rightSteer;
        private float leftThrottle;
        private float rightThrottle;
        private float leftBrake;
        private float rightBrake;
        private float leftThrottleRest;
        private float rightThrottleRest;
        private float leftBrakeRest;
        private float rightBrakeRest;
        private bool pedalsCalibrated;
        private bool leftSelectWasPressed;
        private bool rightSelectWasPressed;
        private bool reversePaddleWasPressed;
        private bool forwardPaddleWasPressed;
        private bool inputAxisErrorLogged;
        private bool followCommandErrorLogged;
        private float previousSlaveSteering;
        private bool leftSdkStateAvailable;
        private bool rightSdkStateAvailable;
        private bool sdkStateStatusLogged;
        private CarController carController;
        private Terrain targetTerrain;
        private float currentVibrationStrength = 0f;
        private float lastCheckedSlope = 0f;

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        void Start()
        {
            IntPtr unityWindow = GetActiveWindow();
            if (unityWindow == IntPtr.Zero)
                unityWindow = GetForegroundWindow();

            isLogiInit = unityWindow != IntPtr.Zero &&
                LogitechGSDK.LogiSteeringInitializeWithWindow(false, unityWindow);
            if (!isLogiInit)
                isLogiInit = LogitechGSDK.LogiSteeringInitialize(false);

            if (isLogiInit)
            {
                LogitechGSDK.LogiControllerPropertiesData properties =
                    new LogitechGSDK.LogiControllerPropertiesData
                    {
                        forceEnable = true,
                        overallGain = 100,
                        springGain = 100,
                        damperGain = 100,
                        defaultSpringEnabled = false,
                        defaultSpringGain = 0,
                        combinePedals = false,
                        wheelRange = 900,
                        gameSettingsEnabled = true,
                        allowGameSettings = true
                    };
                LogitechGSDK.LogiSetPreferredControllerProperties(properties);
                ResolveSdkWheelIndices();
            }
            carController = GetComponent<CarController>();
            targetTerrain = Terrain.activeTerrain;
            ActiveMaster = defaultMaster;
            CurrentGear = defaultGear;
            IsWaitingForCenter = true;
            Debug.Log("<color=cyan>罗技方向盘初始化状态: </color>" + isLogiInit +
                "，默认主控=" + GetActiveWheelLabel());
        }

        private void LogSdkDevice(int index)
        {
            StringBuilder name = new StringBuilder(256);
            StringBuilder path = new StringBuilder(512);
            bool gotName = LogitechGSDK.LogiGetFriendlyProductName(index, name, name.Capacity);
            bool gotPath = LogitechGSDK.LogiGetDevicePath(index, path, path.Capacity);
            Debug.Log(string.Format(
                "[LogitechDriver] SDK[{0}] connected={1}, forceFeedback={2}, name={3}, path={4}",
                index,
                LogitechGSDK.LogiIsConnected(index),
                LogitechGSDK.LogiHasForceFeedback(index),
                gotName ? name.ToString() : "<无>",
                gotPath ? path.ToString() : "<无>"));
        }

        private void ResolveSdkWheelIndices()
        {
            int firstG29 = -1;
            int secondG29 = -1;

            for (int index = 0; index < LogitechGSDK.LOGI_MAX_CONTROLLERS; index++)
            {
                StringBuilder name = new StringBuilder(256);
                StringBuilder path = new StringBuilder(512);
                bool gotName = LogitechGSDK.LogiGetFriendlyProductName(index, name, name.Capacity);
                bool gotPath = LogitechGSDK.LogiGetDevicePath(index, path, path.Capacity);
                string nameText = gotName ? name.ToString() : string.Empty;
                string pathText = gotPath ? path.ToString() : string.Empty;
                string identity = (nameText + " " + pathText).ToLowerInvariant();

                LogSdkDevice(index);

                bool isG29 = identity.Contains("g29") ||
                    (identity.Contains("vid_046d") && identity.Contains("pid_c24f"));
                if (!isG29) continue;

                if (!string.IsNullOrEmpty(leftWheelHidToken) &&
                    identity.Contains(leftWheelHidToken.ToLowerInvariant()))
                {
                    leftLogitechSdkIndex = index;
                }
                else if (!string.IsNullOrEmpty(rightWheelHidToken) &&
                    identity.Contains(rightWheelHidToken.ToLowerInvariant()))
                {
                    rightLogitechSdkIndex = index;
                }
                else if (firstG29 < 0)
                {
                    firstG29 = index;
                }
                else if (secondG29 < 0)
                {
                    secondG29 = index;
                }
            }

            if (firstG29 >= 0 && leftLogitechSdkIndex < 0)
                leftLogitechSdkIndex = firstG29;
            if (secondG29 >= 0 && rightLogitechSdkIndex < 0)
                rightLogitechSdkIndex = secondG29;

            Debug.Log("[LogitechDriver] G29 力反馈映射：左盘 SDK=" + leftLogitechSdkIndex +
                "，右盘 SDK=" + rightLogitechSdkIndex);
            ConfigureWheelOperatingRange(leftLogitechSdkIndex, "左盘");
            ConfigureWheelOperatingRange(rightLogitechSdkIndex, "右盘");
        }

        private void ConfigureWheelOperatingRange(int sdkIndex, string label)
        {
            bool setOk = LogitechGSDK.LogiSetOperatingRange(sdkIndex, wheelOperatingRange);
            int actualRange = 0;
            bool getOk = LogitechGSDK.LogiGetOperatingRange(sdkIndex, ref actualRange);
            Debug.Log("[LogitechDriver] " + label + "工作范围设置=" + setOk +
                "，读取=" + getOk + "，实际=" + actualRange + "°");
        }

        public void EnableDriver(bool enable)
        {
            this.enabled = enable;
            if (!enable && isLogiInit)
            {
                StopWheelEffects(leftLogitechSdkIndex);
                StopWheelEffects(rightLogitechSdkIndex);
                currentVibrationStrength = 0f;
            }
        }

        public bool IsConnected()
        {
            return IsUnityWheelConnected(GetActiveUnityJoystickNumber());
        }

        public void UpdateState()
        {
            // Unity 输入只作为 SDK 不可用时的后备。方向盘、踏板和主控按钮优先使用
            // 按 HID 路径确认的 SDK 设备，避免两个同名 G29 在 Unity 中交换设备槽位。
            UpdateUnityWheelStates();
            bool sdkUpdated = isLogiInit && LogitechGSDK.LogiUpdate();
            if (sdkUpdated)
                UpdateSdkSteeringStates();
            UpdatePedalCalibration();
            UpdateMasterSelection();
            UpdateGearSelection();

            if (IsWaitingForCenter && Mathf.Abs(GetActiveSteeringRaw()) <= steerDeadzone)
                IsWaitingForCenter = false;

            if (sdkUpdated)
            {
                UpdateSpringForce();
                if (enableTerrainVibration)
                    UpdateTerrainVibration();
            }
        }

        private void UpdateSdkSteeringStates()
        {
            leftSdkStateAvailable = TryReadSdkState(
                leftLogitechSdkIndex, out float sdkLeftSteer,
                out float sdkLeftThrottle, out float sdkLeftBrake);
            rightSdkStateAvailable = TryReadSdkState(
                rightLogitechSdkIndex, out float sdkRightSteer,
                out float sdkRightThrottle, out float sdkRightBrake);
            if (leftSdkStateAvailable)
            {
                leftSteer = sdkLeftSteer;
                leftThrottle = sdkLeftThrottle;
                leftBrake = sdkLeftBrake;
            }
            if (rightSdkStateAvailable)
            {
                rightSteer = sdkRightSteer;
                rightThrottle = sdkRightThrottle;
                rightBrake = sdkRightBrake;
            }

            if (!sdkStateStatusLogged)
            {
                sdkStateStatusLogged = true;
                Debug.Log("[LogitechDriver] G29 SDK方向及踏板数据：左=" + leftSdkStateAvailable +
                    "，右=" + rightSdkStateAvailable);
            }
        }

        private bool TryReadSdkState(int sdkIndex, out float steering,
            out float throttle, out float brake)
        {
            steering = 0f;
            throttle = 0f;
            brake = 0f;
            IntPtr pointer = LogitechGSDK.LogiGetStateENGINES(sdkIndex);
            if (pointer == IntPtr.Zero) return false;
            LogitechGSDK.DIJOYSTATE2ENGINES state =
                (LogitechGSDK.DIJOYSTATE2ENGINES)Marshal.PtrToStructure(
                    pointer, typeof(LogitechGSDK.DIJOYSTATE2ENGINES));
            steering = Mathf.Clamp(state.lX / 32767f, -1f, 1f);
            // Logitech Steering Wheel SDK 的 G29 映射与 Unity 扫描轴编号不同：
            // Y 为油门，Rz 为刹车。松开约为 +32767，踩下趋向 -32768。
            // 左右设备仍由 HID 路径对应的 sdkIndex 隔离。
            throttle = Mathf.Clamp(state.lY / 32767f, -1f, 1f);
            brake = Mathf.Clamp(state.lRz / 32767f, -1f, 1f);
            return true;
        }

        private void UpdateTerrainVibration()
        {
            float targetVibration = 0f;

            if (targetTerrain != null)
            {
                float slopeVibration = CalculateSlopeVibration();
                float fvVibration = CalculateFvVibration();
                targetVibration = Mathf.Max(slopeVibration, fvVibration);
            }

            currentVibrationStrength = Mathf.Lerp(currentVibrationStrength, targetVibration, Time.deltaTime * vibrationSmoothSpeed);

            if (currentVibrationStrength > 1f)
            {
                int magnitude = Mathf.RoundToInt(currentVibrationStrength);
                int period = 50;
                LogitechGSDK.LogiPlaySurfaceEffect(GetActiveSdkIndex(), 2, magnitude, period);
            }
            else
            {
                LogitechGSDK.LogiStopSurfaceEffect(GetActiveSdkIndex());
            }
        }

        private float CalculateSlopeVibration()
        {
            Vector3 checkPos = transform.position + transform.forward * terrainCheckDistance;
            
            float normX = (checkPos.x - targetTerrain.transform.position.x) / targetTerrain.terrainData.size.x;
            float normZ = (checkPos.z - targetTerrain.transform.position.z) / targetTerrain.terrainData.size.z;
            
            normX = Mathf.Clamp01(normX);
            normZ = Mathf.Clamp01(normZ);
            
            float currentSlope = targetTerrain.terrainData.GetSteepness(normX, normZ);
            float slopeDelta = Mathf.Abs(currentSlope - lastCheckedSlope);
            lastCheckedSlope = currentSlope;

            if (slopeDelta > slopeThreshold)
            {
                return Mathf.Clamp(slopeDelta / maxSlopeForVibration, 0f, 1f) * maxVibrationStrength;
            }
            
            if (currentSlope > slopeThreshold)
            {
                return Mathf.Clamp((currentSlope - slopeThreshold) / (maxSlopeForVibration - slopeThreshold), 0f, 1f) * maxVibrationStrength;
            }

            return 0f;
        }

        private float CalculateFvVibration()
        {
            if (terrainScanner == null)
            {
                terrainScanner = FindObjectOfType<MoonRover.FeedBack.Scan>();
            }
            
            if (terrainScanner == null) return 0f;

            float fv = terrainScanner.Fv;
            
            if (fv > fvThreshold)
            {
                return Mathf.Clamp((fv - fvThreshold) / (maxFvForVibration - fvThreshold), 0f, 1f) * maxVibrationStrength;
            }

            return 0f;
        }

        private void UpdateSpringForce()
        {
            float currentSpeed = carController != null ? carController.CurrentSpeed : 0f;
            float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeedForFF);

            int saturation = Mathf.RoundToInt(Mathf.Lerp(springSaturationMin, springSaturationMax, speedRatio));
            int coefficient = Mathf.RoundToInt(Mathf.Lerp(springCoefficientMin, springCoefficientMax, speedRatio));

            int activeIndex = GetActiveSdkIndex();
            int slaveIndex = ActiveMaster == WheelSide.Left
                ? rightLogitechSdkIndex
                : leftLogitechSdkIndex;

            LogitechGSDK.LogiPlaySpringForce(activeIndex, 0, saturation, coefficient);

            if (enableSlaveFollow)
            {
                // Logitech SDK 没有位置伺服接口，因此用实时角度误差闭环产生扭矩。
                LogitechGSDK.LogiStopSpringForce(slaveIndex);
                float slaveSteering = ActiveMaster == WheelSide.Left ? rightSteer : leftSteer;
                float targetSteering = GetActiveSteeringRaw();
                float error = targetSteering - slaveSteering;

                float dt = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                float slaveVelocity = (slaveSteering - previousSlaveSteering) / dt;
                previousSlaveSteering = slaveSteering;

                if (Mathf.Abs(error) <= slaveFollowDeadzone)
                {
                    LogitechGSDK.LogiStopConstantForce(slaveIndex);
                }
                else
                {
                    float force = error * slaveFollowForceGain - slaveVelocity * slaveFollowDamping;
                    // G29 的 ConstantForce 命令方向与 Unity/SDK 转角正方向相反。
                    // 必须把完整控制量（比例项和阻尼项）一起反向，避免正反馈打死。
                    if (invertSlaveFollow) force = -force;
                    if (Mathf.Abs(force) < slaveFollowMinForce)
                        force = Mathf.Sign(force) * slaveFollowMinForce;
                    int magnitude = Mathf.RoundToInt(Mathf.Clamp(
                        force, -slaveFollowMaxForce, slaveFollowMaxForce));
                    bool followAccepted = LogitechGSDK.LogiPlayConstantForce(slaveIndex, magnitude);
                    if (!followAccepted && !followCommandErrorLogged)
                    {
                        followCommandErrorLogged = true;
                        Debug.LogWarning("[LogitechDriver] 从动方向盘拒绝闭环扭矩，SDK索引=" + slaveIndex);
                    }
                }
            }
            else
            {
                LogitechGSDK.LogiStopSpringForce(slaveIndex);
                LogitechGSDK.LogiStopConstantForce(slaveIndex);
            }
        }

        public float GetAccelInput()
        {
            if (!IsConnected() || !pedalsCalibrated || IsWaitingForCenter) return 0f;
            float raw = ActiveMaster == WheelSide.Left ? leftThrottle : rightThrottle;
            float rest = ActiveMaster == WheelSide.Left ? leftThrottleRest : rightThrottleRest;
            float throttle = ApplyCurve(NormalizePedal(raw, rest), accelCurvePower);
            return throttle * (int)CurrentGear;
        }

        public float GetBrakeInput()
        {
            if (!IsConnected()) return 1f;
            if (!pedalsCalibrated || IsWaitingForCenter) return 1f;
            float raw = ActiveMaster == WheelSide.Left ? leftBrake : rightBrake;
            float rest = ActiveMaster == WheelSide.Left ? leftBrakeRest : rightBrakeRest;
            return ApplyCurve(NormalizePedal(raw, rest), brakeCurvePower);
        }

        private float ApplyCurve(float input, float power)
        {
            if (power <= 1f) return input;
            return Mathf.Pow(input, power);
        }

        public bool IsButtonPressed(int buttonIndex)
        {
            if (!IsConnected()) return false;
            return IsUnityJoystickButtonPressed(buttonIndex, GetActiveUnityJoystickNumber());
        }

        public float GetSteeringInput()
        {
            if (!IsConnected()) return 0f;
            if (IsWaitingForCenter) return 0f;
            float steerInput = GetActiveSteeringRaw();
            if (Mathf.Abs(steerInput) < steerDeadzone)
                return 0f;
            return steerInput * steerSensitivity;
        }

        public string GetActiveWheelLabel()
        {
            return ActiveMaster == WheelSide.Left ? "左方向盘" : "右方向盘";
        }

        public string GetGearLabel()
        {
            return CurrentGear == DriveGear.Reverse ? "R 后退档" : "D 前进档";
        }

        private void UpdateUnityWheelStates()
        {
            // 使用扫描轴的 0.001 小死区；旧 Horizontal 轴有 0.19 死区，
            // 会吞掉约 90 度以内的转向并破坏主从角度比例。
            leftSteer = ReadScanAxis(leftUnityJoystickNumber, 0);
            rightSteer = ReadScanAxis(rightUnityJoystickNumber, 0);
            leftThrottle = ReadScanAxis(leftUnityJoystickNumber, throttleAxisIndex);
            rightThrottle = ReadScanAxis(rightUnityJoystickNumber, throttleAxisIndex);
            leftBrake = ReadScanAxis(leftUnityJoystickNumber, brakeAxisIndex);
            rightBrake = ReadScanAxis(rightUnityJoystickNumber, brakeAxisIndex);
        }

        private float ReadScanAxis(int joystickNumber, int axisIndex)
        {
            try
            {
                return Input.GetAxisRaw("MoonRoverScanJ" + joystickNumber + "A" + axisIndex);
            }
            catch (System.ArgumentException exception)
            {
                if (!inputAxisErrorLogged)
                {
                    inputAxisErrorLogged = true;
                    Debug.LogError("[LogitechDriver] 全轴输入未配置，" + exception.Message);
                }
                return 0f;
            }
        }

        private float ReadAxis(string baseName, int joystickNumber)
        {
            string axisName = baseName + (joystickNumber > 1 ? joystickNumber.ToString() : "");
            try
            {
                return Input.GetAxisRaw(axisName);
            }
            catch (System.ArgumentException exception)
            {
                if (!inputAxisErrorLogged)
                {
                    inputAxisErrorLogged = true;
                    Debug.LogError("[LogitechDriver] 输入轴未配置: " + axisName + "，" + exception.Message);
                }
                return 0f;
            }
        }

        private void UpdatePedalCalibration()
        {
            if (pedalsCalibrated) return;

            // 恢复项目最初的踏板逻辑：第一次有效读取直接记录松开位置。
            // 这里只是把原来单设备的一套 offset 扩展成左右设备各一套。
            if (!leftSdkStateAvailable || !rightSdkStateAvailable)
                return;

            leftThrottleRest = leftThrottle;
            rightThrottleRest = rightThrottle;
            leftBrakeRest = leftBrake;
            rightBrakeRest = rightBrake;
            pedalsCalibrated = true;
            Debug.Log(string.Format(
                "[LogitechDriver] 双方向盘校准完成，左油门={0:F2} 左刹车={1:F2} 右油门={2:F2} 右刹车={3:F2}",
                leftThrottleRest, leftBrakeRest, rightThrottleRest, rightBrakeRest));
        }

        private float NormalizePedal(float raw, float rest)
        {
            // 等价于最初的 Clamp(-(state - offset) / 32768, 0, 1)。
            // 只接受“数值下降”这一踩踏方向，反向漂移不会被误判成油门。
            float pressed = rest - raw;
            if (pressed < 0.05f) return 0f;
            return Mathf.Clamp01(pressed);
        }

        private void UpdateMasterSelection()
        {
            int buttonIndex = Mathf.Max(0, masterSelectButtonNumber - 1);
            bool slot2Pressed = IsUnityJoystickButtonPressed(buttonIndex, 2);
            bool slot3Pressed = IsUnityJoystickButtonPressed(buttonIndex, 3);
            bool leftPressed = leftSdkStateAvailable
                ? LogitechGSDK.LogiButtonIsPressed(leftLogitechSdkIndex, buttonIndex)
                : IsUnityJoystickButtonPressed(buttonIndex, leftUnityJoystickNumber);
            bool rightPressed = rightSdkStateAvailable
                ? LogitechGSDK.LogiButtonIsPressed(rightLogitechSdkIndex, buttonIndex)
                : IsUnityJoystickButtonPressed(buttonIndex, rightUnityJoystickNumber);

            if (leftPressed && !leftSelectWasPressed)
            {
                LearnUnitySlots(WheelSide.Left, slot2Pressed, slot3Pressed);
                SelectMaster(WheelSide.Left);
            }
            if (rightPressed && !rightSelectWasPressed)
            {
                LearnUnitySlots(WheelSide.Right, slot2Pressed, slot3Pressed);
                SelectMaster(WheelSide.Right);
            }

            leftSelectWasPressed = leftPressed;
            rightSelectWasPressed = rightPressed;
        }

        private void LearnUnitySlots(WheelSide physicalSide, bool slot2Pressed, bool slot3Pressed)
        {
            int pressedSlot = slot2Pressed ? 2 : (slot3Pressed ? 3 : 0);
            if (pressedSlot == 0) return;

            if (physicalSide == WheelSide.Left)
            {
                leftUnityJoystickNumber = pressedSlot;
                rightUnityJoystickNumber = pressedSlot == 2 ? 3 : 2;
            }
            else
            {
                rightUnityJoystickNumber = pressedSlot;
                leftUnityJoystickNumber = pressedSlot == 2 ? 3 : 2;
            }
            Debug.Log("[LogitechDriver] Unity左右槽位自动校正：左=" + leftUnityJoystickNumber +
                "，右=" + rightUnityJoystickNumber);
        }

        private void UpdateGearSelection()
        {
            int reverseIndex = Mathf.Max(0, reversePaddleButtonNumber - 1);
            int forwardIndex = Mathf.Max(0, forwardPaddleButtonNumber - 1);
            int activeSdkIndex = GetActiveSdkIndex();
            bool sdkAvailable = ActiveMaster == WheelSide.Left
                ? leftSdkStateAvailable
                : rightSdkStateAvailable;

            bool reversePressed = sdkAvailable
                ? LogitechGSDK.LogiButtonIsPressed(activeSdkIndex, reverseIndex)
                : IsUnityJoystickButtonPressed(reverseIndex, GetActiveUnityJoystickNumber());
            bool forwardPressed = sdkAvailable
                ? LogitechGSDK.LogiButtonIsPressed(activeSdkIndex, forwardIndex)
                : IsUnityJoystickButtonPressed(forwardIndex, GetActiveUnityJoystickNumber());

            if (reversePressed && !reversePaddleWasPressed)
            {
                CurrentGear = DriveGear.Reverse;
                Debug.Log("[LogitechDriver] " + GetActiveWheelLabel() + " 左拨片：切换 R 后退档");
            }
            if (forwardPressed && !forwardPaddleWasPressed)
            {
                CurrentGear = DriveGear.Forward;
                Debug.Log("[LogitechDriver] " + GetActiveWheelLabel() + " 右拨片：切换 D 前进档");
            }

            reversePaddleWasPressed = reversePressed;
            forwardPaddleWasPressed = forwardPressed;
        }

        private void SelectMaster(WheelSide side)
        {
            ActiveMaster = side;
            IsWaitingForCenter = true;
            followCommandErrorLogged = false;
            previousSlaveSteering = side == WheelSide.Left ? rightSteer : leftSteer;
            // 接管瞬间不把仍被按住的拨片误判为一次换挡。
            int activeSdk = GetActiveSdkIndex();
            reversePaddleWasPressed = LogitechGSDK.LogiButtonIsPressed(
                activeSdk, Mathf.Max(0, reversePaddleButtonNumber - 1));
            forwardPaddleWasPressed = LogitechGSDK.LogiButtonIsPressed(
                activeSdk, Mathf.Max(0, forwardPaddleButtonNumber - 1));
            Debug.Log("[LogitechDriver] 主控切换为: " + GetActiveWheelLabel() + "，等待回中");
        }

        private float GetActiveSteeringRaw()
        {
            return ActiveMaster == WheelSide.Left ? leftSteer : rightSteer;
        }

        private int GetActiveSdkIndex()
        {
            return ActiveMaster == WheelSide.Left ? leftLogitechSdkIndex : rightLogitechSdkIndex;
        }

        private int GetActiveUnityJoystickNumber()
        {
            return ActiveMaster == WheelSide.Left ? leftUnityJoystickNumber : rightUnityJoystickNumber;
        }

        private bool IsUnityWheelConnected(int joystickNumber)
        {
            string[] names = Input.GetJoystickNames();
            int index = joystickNumber - 1;
            return names != null && index >= 0 && index < names.Length &&
                !string.IsNullOrEmpty(names[index]);
        }

        private bool IsUnityJoystickButtonPressed(int buttonIndex, int joystickNumber)
        {
            if (buttonIndex < 0 || buttonIndex > 19 || joystickNumber < 1 || joystickNumber > 8)
                return false;
            int keyCode = (int)KeyCode.Joystick1Button0 + (joystickNumber - 1) * 20 + buttonIndex;
            return Input.GetKey((KeyCode)keyCode);
        }

        private void StopWheelEffects(int sdkIndex)
        {
            LogitechGSDK.LogiStopSpringForce(sdkIndex);
            LogitechGSDK.LogiStopConstantForce(sdkIndex);
            LogitechGSDK.LogiStopSurfaceEffect(sdkIndex);
        }

        void OnDisable()
        {
            if (isLogiInit)
            {
                StopWheelEffects(leftLogitechSdkIndex);
                StopWheelEffects(rightLogitechSdkIndex);
            }
        }

        void OnApplicationQuit()
        {
            if (isLogiInit)
            {
                StopWheelEffects(leftLogitechSdkIndex);
                StopWheelEffects(rightLogitechSdkIndex);
                LogitechGSDK.LogiSteeringShutdown();
            }
        }
    }
}

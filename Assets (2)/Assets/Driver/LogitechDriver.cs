using UnityEngine;
using UnityStandardAssets.Vehicles.Car;

namespace MoonRover.Driver
{
    [RequireComponent(typeof(CarController))]
    public class LogitechDriver : MonoBehaviour, IDriver
    {
        private bool isLogiInit = false;

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

        private LogitechGSDK.DIJOYSTATE2ENGINES state;
        private float accelOffset = 0f;
        private float brakeOffset = 0f;
        private bool calibrated = false;
        private CarController carController;
        private Terrain targetTerrain;
        private float currentVibrationStrength = 0f;
        private float lastCheckedSlope = 0f;

        void Start()
        {
            isLogiInit = LogitechGSDK.LogiSteeringInitialize(false);
            carController = GetComponent<CarController>();
            targetTerrain = Terrain.activeTerrain;
            Debug.Log("<color=cyan>罗技方向盘初始化状态: </color>" + isLogiInit);
        }

        public void EnableDriver(bool enable)
        {
            this.enabled = enable;
            if (!enable && isLogiInit)
            {
                LogitechGSDK.LogiStopSpringForce(0);
                LogitechGSDK.LogiStopSurfaceEffect(0);
                currentVibrationStrength = 0f;
            }
        }

        public bool IsConnected()
        {
            return isLogiInit && LogitechGSDK.LogiIsConnected(0);
        }

        public void UpdateState()
        {
            if (!isLogiInit || !LogitechGSDK.LogiUpdate())
                return;
            if (!IsConnected())
                return;
            state = LogitechGSDK.LogiGetStateCSharp(0);
            
            if (!calibrated)
            {
                accelOffset = state.lY;
                brakeOffset = state.lRz;
                calibrated = true;
            }

            UpdateSpringForce();
            if (enableTerrainVibration)
            {
                UpdateTerrainVibration();
            }
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
                LogitechGSDK.LogiPlaySurfaceEffect(0, 2, magnitude, period);
            }
            else
            {
                LogitechGSDK.LogiStopSurfaceEffect(0);
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

            LogitechGSDK.LogiPlaySpringForce(0, 0, saturation, coefficient);
        }

        public float GetAccelInput()
        {
            if (!IsConnected()) return 0f;
            float accelRaw = state.lY - accelOffset;
            float linearInput = Mathf.Clamp(-accelRaw / 32768f, 0f, 1f);
            return ApplyCurve(linearInput, accelCurvePower);
        }

        public float GetBrakeInput()
        {
            if (!IsConnected()) return 0f;
            float brakeRaw = state.lRz - brakeOffset;
            float linearInput = Mathf.Clamp(-brakeRaw / 32768f, 0f, 1f);
            return ApplyCurve(linearInput, brakeCurvePower);
        }

        private float ApplyCurve(float input, float power)
        {
            if (power <= 1f) return input;
            return Mathf.Pow(input, power);
        }

        public bool IsButtonPressed(int buttonIndex)
        {
            if (!IsConnected()) return false;
            return buttonIndex >= 0 && buttonIndex < 128 && state.rgbButtons[buttonIndex] == 128;
        }

        public float GetSteeringInput()
        {
            if (!IsConnected()) return 0f;
            float steerInput = state.lX / 32768f;
            if (Mathf.Abs(steerInput) < steerDeadzone)
                return 0f;
            return steerInput * steerSensitivity;
        }

        void OnDisable()
        {
            if (isLogiInit)
            {
                LogitechGSDK.LogiStopSpringForce(0);
                LogitechGSDK.LogiStopSurfaceEffect(0);
            }
        }

        void OnApplicationQuit()
        {
            if (isLogiInit)
            {
                LogitechGSDK.LogiStopSpringForce(0);
                LogitechGSDK.LogiStopSurfaceEffect(0);
                LogitechGSDK.LogiSteeringShutdown();
            }
        }
    }
}

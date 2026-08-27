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
        public float inputDeadzone = 0.05f;
        [Tooltip("油门释放平滑速度（越大越快跟上）")]
        public float releaseSmoothing = 5f;

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

        void FixedUpdate()
        {
            float h, v, footbrake = 0f, handbrake = 0f;

            if (useLogitechSteeringWheel && logitechDriver != null && logitechDriver.IsConnected())
            {
                logitechDriver.UpdateState();
                h = logitechDriver.GetSteeringInput();
                v = ApplyDeadzone(logitechDriver.GetAccelInput());
                footbrake = ApplyDeadzone(logitechDriver.GetBrakeInput());
                handbrake = logitechDriver.IsButtonPressed(0) ? 1f : 0f;
            }
            else
            {
                h = Input.GetAxis("Horizontal");
                v = Input.GetAxis("Vertical");
                // S / ↓ = 脚刹（高速刹车，低速自动切倒车）
                footbrake = (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f;
                handbrake = Input.GetKey(KeyCode.Space) ? 1f : 0f;
            }

            // 平滑油门释放：释放时慢慢归零，避免急停
            // 但油门和刹车互斥：有油门时立刻清刹车，有刹车时立刻清油门
            float rawAccel = v;
            float rawBrake = footbrake;

            if (rawAccel > 0.01f)
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
            if (useLogitechSteeringWheel && logitechDriver != null)
            {
                bool connected = logitechDriver.IsConnected();
                g29StatusText.text = connected ? "G29: 已连接" : "G29: 未连接 (键盘)";
                g29StatusText.color = connected ? Color.green : Color.yellow;
            }
            else
            {
                g29StatusText.text = "G29: 未启用 (键盘)";
                g29StatusText.color = Color.yellow;
            }
        }

        /// <summary>
        /// 输入死区：低于阈值归零，高于阈值重新映射到 0~1
        /// </summary>
        private float ApplyDeadzone(float value)
        {
            if (Mathf.Abs(value) < inputDeadzone) return 0f;
            // 将 [deadzone, 1] 重新映射到 [0, 1]
            return Mathf.Sign(value) * (Mathf.Abs(value) - inputDeadzone) / (1f - inputDeadzone);
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

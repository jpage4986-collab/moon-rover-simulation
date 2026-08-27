using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Text;
using System.Globalization;
using UnityStandardAssets.Vehicles.Car;

namespace MoonRover.Platform
{
    /// <summary>
    /// 6自由度底座TCP通信控制器
    /// 通过TCP连接MPSdkMiddleware.exe (127.0.0.1:9999)
    /// 发送格式: Runing#Rx#Ry#Rz#X#Y#Z#effcet1#effcet2#time#end
    /// </summary>
    public class MotionPlatformController : MonoBehaviour
    {
        [Header("TCP连接设置")]
        [SerializeField] private string serverIP = "127.0.0.1";
        [SerializeField] private int serverPort = 9999;

        [Header("数据源引用")]
        [SerializeField] private CarController carController;
        [SerializeField] private Transform roverTransform;

        [Header("发送参数")]
        [SerializeField] private float sendInterval = 0.02f; // 20ms = 50Hz
        [SerializeField] private int motionTime = 20; // 运动时间(ms)
        [SerializeField] private float reconnectInterval = 2f;

        [Header("实体平台安全限位")]
        [SerializeField] private float maxRollAngle = 5f;
        [SerializeField] private float minPitchAngle = -3f;
        [SerializeField] private float maxPitchAngle = 3f;
        [SerializeField] private bool sendYaw = false;

        [Header("延迟预测补偿")]
        [SerializeField] private bool enablePrediction = true;
        [Tooltip("向前预测时间。建议从 0.05~0.08 秒开始，最大不超过 0.15 秒。")]
        [Range(0f, 0.15f)]
        [SerializeField] private float predictionHorizon = 0.08f;
        [Tooltip("角速度滤波响应，越高越灵敏，但也更容易放大震动。")]
        [Range(1f, 40f)]
        [SerializeField] private float predictionFilterSharpness = 18f;
        [SerializeField] private float maxPredictedPitchLead = 1f;
        [SerializeField] private float maxPredictedRollLead = 1.5f;

        [Header("Terrain preview compensation")]
        [SerializeField] private bool enableTerrainPreview = true;
        [SerializeField] private Terrain targetTerrain;
        [Tooltip("How far ahead the wheel contact points are sampled. This should roughly match the measured platform delay.")]
        [Range(0.02f, 0.25f)]
        [SerializeField] private float terrainPreviewHorizon = 0.12f;
        [Tooltip("Wheel geometry of this rover, measured from the four WheelCollider transforms.")]
        [SerializeField] private float wheelbase = 2.9f;
        [SerializeField] private float trackWidth = 2f;
        [Range(0f, 1f)]
        [SerializeField] private float terrainPreviewBlend = 0.7f;
        [Range(1f, 40f)]
        [SerializeField] private float terrainPreviewSharpness = 16f;
        [SerializeField] private float minimumPreviewSpeed = 0.15f;
        [SerializeField] private float maxTerrainPitchLead = 1.5f;
        [SerializeField] private float maxTerrainRollLead = 1.5f;

        [Header("特效控制 (二进制位开关)")]
        [SerializeField] private byte effect1 = 0; // 特效口1-8
        [SerializeField] private byte effect2 = 0; // 特效口9-12

        [Header("UI 引用 (由 UISetupWizard 绑定)")]
        public Text statusText;
        public Text pitchText;
        public Text rollText;
        public Text motionFPSText;

        [Header("调试选项")]
        [SerializeField] private bool enablePlatform = true;
        [SerializeField] private bool printDebugLog = false;

        private TcpClient tcpClient;
        private NetworkStream stream;
        private float sendTimer = 0f;
        private volatile bool isConnected = false;
        private volatile bool isConnecting = false;
        private float nextReconnectTime = 0f;
        private float statsSendCount = 0f;
        private float statsTimer = 0f;
        private float actualHz = 0f;
        private Vector3 filteredLocalAngularVelocity;
        private float filteredTerrainPitchLead;
        private float filteredTerrainRollLead;

        // 轴映射系数 (可根据实际底座调整)
        private const float pitchScale = 1f;
        private const float rollScale = 1f;

        void Start()
        {
            if (!enablePlatform)
            {
                Debug.Log("[底座控制] 已禁用底座控制");
                UpdateUI();
                return;
            }

            // 自动获取引用
            if (carController == null)
                carController = GetComponent<CarController>();

            if (roverTransform == null)
                roverTransform = transform;

            if (targetTerrain == null)
                targetTerrain = Terrain.activeTerrain;

            ConnectToMiddleware();
            UpdateUI();
        }

        void Update()
        {
            if (!enablePlatform)
            {
                UpdateUI();
                return;
            }

            if (!isConnected)
            {
                if (!isConnecting && Time.unscaledTime >= nextReconnectTime)
                {
                    nextReconnectTime = Time.unscaledTime + reconnectInterval;
                    ConnectToMiddleware();
                }
                UpdateUI();
                return;
            }

            // 每秒统计实际发送频率
            statsTimer += Time.deltaTime;
            if (statsTimer >= 1f)
            {
                actualHz = statsSendCount / statsTimer;
                statsSendCount = 0f;
                statsTimer = 0f;
            }

            UpdateUI();
        }

        // 与 50Hz 物理步同步采样，避免渲染帧波动造成发送抖动和额外滞后。
        void FixedUpdate()
        {
            if (!enablePlatform || !isConnected)
            {
                sendTimer = 0f;
                filteredLocalAngularVelocity = Vector3.zero;
                return;
            }

            sendTimer += Time.fixedDeltaTime;
            if (sendTimer + 0.0001f < sendInterval) return;

            sendTimer = Mathf.Max(0f, sendTimer - sendInterval);
            SendMotionData();
            statsSendCount++;
        }

        void UpdateUI()
        {
            if (statusText != null)
            {
                if (!enablePlatform)
                    statusText.text = "动感平台: 已禁用";
                else if (isConnected)
                    statusText.text = "动感平台: 已连接 ✓";
                else
                    statusText.text = "动感平台: 未连接";
                statusText.color = isConnected ? Color.green : Color.yellow;
            }

            if (roverTransform != null)
            {
                Vector3 euler = roverTransform.eulerAngles;
                float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
                float roll = euler.z > 180f ? euler.z - 360f : euler.z;

                if (pitchText != null) pitchText.text = "平台俯仰: " + pitch.ToString("F1") + "°";
                if (rollText != null) rollText.text = "平台侧倾: " + roll.ToString("F1") + "°";
            }

            if (motionFPSText != null)
            {
                if (isConnected)
                    motionFPSText.text = "底座 50Hz: " + actualHz.ToString("F0") + " Hz";
                else
                    motionFPSText.text = "底座 50Hz: —";
            }
        }

        /// <summary>
        /// 连接TCP中间件
        /// </summary>
        void ConnectToMiddleware()
        {
            if (isConnected || isConnecting) return;

            try
            {
                isConnecting = true;
                if (tcpClient != null) tcpClient.Close();
                tcpClient = new TcpClient();
                tcpClient.BeginConnect(serverIP, serverPort, OnConnectCallback, tcpClient);

                if (printDebugLog)
                    Debug.Log($"[底座控制] 正在连接 {serverIP}:{serverPort}...");
            }
            catch (System.Exception e)
            {
                isConnecting = false;
                Debug.LogError($"[底座控制] 连接失败: {e.Message}");
            }
        }

        void OnConnectCallback(System.IAsyncResult result)
        {
            try
            {
                TcpClient connectedClient = (TcpClient)result.AsyncState;
                connectedClient.EndConnect(result);
                connectedClient.NoDelay = true; // 禁用 Nagle，避免 50Hz 小包被合并等待
                tcpClient = connectedClient;
                stream = connectedClient.GetStream();
                isConnected = true;
                isConnecting = false;

                if (printDebugLog)
                    Debug.Log("[底座控制] ✓ 连接成功!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[底座控制] 连接失败: {e.Message}");
                isConnected = false;
                isConnecting = false;
            }
        }

        /// <summary>
        /// 发送运动数据到底座
        /// </summary>
        void SendMotionData()
        {
            if (stream == null || !stream.CanWrite) return;

            // 获取姿态数据
            Vector3 euler = roverTransform.eulerAngles;

            // 转换为 -180~180 范围
            float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            float roll = euler.z > 180f ? euler.z - 360f : euler.z;
            float yaw = euler.y > 180f ? euler.y - 360f : euler.y;

            if (enablePrediction && carController != null && carController.CarRigidbody != null)
            {
                // Rigidbody.angularVelocity 是世界空间弧度/秒；转换到车体局部坐标后进行短时外推。
                Vector3 localAngularVelocity = roverTransform.InverseTransformDirection(
                    carController.CarRigidbody.angularVelocity) * Mathf.Rad2Deg;
                float filterFactor = 1f - Mathf.Exp(-predictionFilterSharpness * Time.fixedDeltaTime);
                filteredLocalAngularVelocity = Vector3.Lerp(
                    filteredLocalAngularVelocity, localAngularVelocity, filterFactor);

                float pitchLead = Mathf.Clamp(
                    filteredLocalAngularVelocity.x * predictionHorizon,
                    -maxPredictedPitchLead, maxPredictedPitchLead);
                float rollLead = Mathf.Clamp(
                    filteredLocalAngularVelocity.z * predictionHorizon,
                    -maxPredictedRollLead, maxPredictedRollLead);

                pitch += pitchLead;
                roll += rollLead;
            }

            // The angular-velocity predictor above only reacts after the body has begun to rotate.
            // Terrain preview samples the four future wheel contact points, allowing the platform
            // to begin a small, bounded cue before the rover body reaches the slope.
            float terrainPitchLead;
            float terrainRollLead;
            if (TryGetTerrainPreview(pitch, roll, out terrainPitchLead, out terrainRollLead))
            {
                float terrainFilterFactor = 1f - Mathf.Exp(-terrainPreviewSharpness * Time.fixedDeltaTime);
                filteredTerrainPitchLead = Mathf.Lerp(filteredTerrainPitchLead, terrainPitchLead, terrainFilterFactor);
                filteredTerrainRollLead = Mathf.Lerp(filteredTerrainRollLead, terrainRollLead, terrainFilterFactor);
            }
            else
            {
                float releaseFactor = 1f - Mathf.Exp(-terrainPreviewSharpness * Time.fixedDeltaTime);
                filteredTerrainPitchLead = Mathf.Lerp(filteredTerrainPitchLead, 0f, releaseFactor);
                filteredTerrainRollLead = Mathf.Lerp(filteredTerrainRollLead, 0f, releaseFactor);
            }

            pitch += filteredTerrainPitchLead * terrainPreviewBlend;
            roll += filteredTerrainRollLead * terrainPreviewBlend;

            // 预测之后再次限位，预测量也绝不允许突破实体安全边界。
            roll = Mathf.Clamp(roll, -maxRollAngle, maxRollAngle);
            pitch = Mathf.Clamp(pitch, minPitchAngle, maxPitchAngle);

            // 映射到SDK轴 (Unity X→Pitch, Unity Z→Roll)
            float rx = roll * rollScale;      // Roll
            float ry = -pitch * pitchScale;   // Pitch (负号根据实际调整)
            float rz = sendYaw ? yaw : 0f;    // 当前安装环境禁用 Yaw

            // 平移量 (通常月球车不需要，设为0)
            float x = 0f, y = 0f, z = 0f;

            // 构建指令
            string command = string.Format(CultureInfo.InvariantCulture,
                "Runing#{0:F2}#{1:F2}#{2:F2}#{3:F2}#{4:F2}#{5:F2}#{6}#{7}#{8}#end\n",
                rx, ry, rz, x, y, z, effect1, effect2, motionTime);

            try
            {
                byte[] data = Encoding.ASCII.GetBytes(command);
                stream.Write(data, 0, data.Length);

                if (printDebugLog)
                    Debug.Log($"[底座控制] 发送: {command.Trim()}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[底座控制] 发送失败: {e.Message}");
                isConnected = false;
                try { if (stream != null) stream.Close(); } catch { }
                try { if (tcpClient != null) tcpClient.Close(); } catch { }
                stream = null;
            }
        }

        private bool TryGetTerrainPreview(float currentPitch, float currentRoll,
            out float pitchLead, out float rollLead)
        {
            pitchLead = 0f;
            rollLead = 0f;

            if (!enableTerrainPreview || targetTerrain == null || roverTransform == null ||
                carController == null || carController.CarRigidbody == null ||
                wheelbase <= 0.01f || trackWidth <= 0.01f)
                return false;

            Rigidbody body = carController.CarRigidbody;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.velocity, Vector3.up);
            if (planarVelocity.magnitude < minimumPreviewSpeed)
                return false;

            Vector3 forward = Vector3.ProjectOnPlane(roverTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(roverTransform.right, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.5f || right.sqrMagnitude < 0.5f)
                return false;

            // Include the measured delay in the future centre and a short yaw extrapolation.
            Vector3 futureCentre = body.position + planarVelocity * terrainPreviewHorizon;
            float yawLead = Vector3.Dot(body.angularVelocity, Vector3.up) * Mathf.Rad2Deg * terrainPreviewHorizon;
            Quaternion yawRotation = Quaternion.AngleAxis(yawLead, Vector3.up);
            forward = yawRotation * forward;
            right = yawRotation * right;

            float halfWheelbase = wheelbase * 0.5f;
            float halfTrack = trackWidth * 0.5f;
            Vector3 frontLeft = futureCentre + forward * halfWheelbase - right * halfTrack;
            Vector3 frontRight = futureCentre + forward * halfWheelbase + right * halfTrack;
            Vector3 rearLeft = futureCentre - forward * halfWheelbase - right * halfTrack;
            Vector3 rearRight = futureCentre - forward * halfWheelbase + right * halfTrack;

            if (!IsInsideTerrain(frontLeft) || !IsInsideTerrain(frontRight) ||
                !IsInsideTerrain(rearLeft) || !IsInsideTerrain(rearRight))
                return false;

            float fl = SampleTerrainWorldHeight(frontLeft);
            float fr = SampleTerrainWorldHeight(frontRight);
            float rl = SampleTerrainWorldHeight(rearLeft);
            float rr = SampleTerrainWorldHeight(rearRight);

            float frontHeight = (fl + fr) * 0.5f;
            float rearHeight = (rl + rr) * 0.5f;
            float leftHeight = (fl + rl) * 0.5f;
            float rightHeight = (fr + rr) * 0.5f;

            // Unity positive X rotation points the nose down, hence the minus sign for pitch.
            float terrainPitch = -Mathf.Atan2(frontHeight - rearHeight, wheelbase) * Mathf.Rad2Deg;
            float terrainRoll = Mathf.Atan2(rightHeight - leftHeight, trackWidth) * Mathf.Rad2Deg;

            pitchLead = Mathf.Clamp(Mathf.DeltaAngle(currentPitch, terrainPitch),
                -maxTerrainPitchLead, maxTerrainPitchLead);
            rollLead = Mathf.Clamp(Mathf.DeltaAngle(currentRoll, terrainRoll),
                -maxTerrainRollLead, maxTerrainRollLead);
            return true;
        }

        private bool IsInsideTerrain(Vector3 worldPosition)
        {
            Vector3 terrainPosition = targetTerrain.transform.position;
            Vector3 terrainSize = targetTerrain.terrainData.size;
            return worldPosition.x >= terrainPosition.x && worldPosition.x <= terrainPosition.x + terrainSize.x &&
                   worldPosition.z >= terrainPosition.z && worldPosition.z <= terrainPosition.z + terrainSize.z;
        }

        private float SampleTerrainWorldHeight(Vector3 worldPosition)
        {
            return targetTerrain.SampleHeight(worldPosition) + targetTerrain.transform.position.y;
        }

        /// <summary>
        /// 发送回零指令
        /// </summary>
        public void SendZeroCommand()
        {
            if (!isConnected || stream == null) return;

            string command = "Zero#end\n";
            byte[] data = Encoding.ASCII.GetBytes(command);
            stream.Write(data, 0, data.Length);

            if (printDebugLog)
                Debug.Log("[底座控制] 发送回零指令");
        }

        /// <summary>
        /// 发送复位指令 (设置当前位置为原点)
        /// </summary>
        public void SendResetCommand()
        {
            if (!isConnected || stream == null) return;

            string command = "Reset#end\n";
            byte[] data = Encoding.ASCII.GetBytes(command);
            stream.Write(data, 0, data.Length);

            if (printDebugLog)
                Debug.Log("[底座控制] 发送复位指令");
        }

        void OnApplicationQuit()
        {
            // 退出前发送回零指令
            if (isConnected)
            {
                SendZeroCommand();

                if (stream != null) stream.Close();
                if (tcpClient != null) tcpClient.Close();

                isConnected = false;

                if (printDebugLog)
                    Debug.Log("[底座控制] 已断开连接并回零");
            }
        }
    }
}

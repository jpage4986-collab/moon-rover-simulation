using System;
using UnityEngine;

namespace UnityStandardAssets.Vehicles.Car
{
    internal enum CarDriveType { FrontWheelDrive, RearWheelDrive, FourWheelDrive }
    internal enum SpeedType { MPH, KPH }

    public class CarController : MonoBehaviour
    {
        [Header("驱动与车轮设置")]
        [SerializeField] private CarDriveType m_CarDriveType = CarDriveType.FourWheelDrive;
        [SerializeField] private WheelCollider[] m_WheelColliders = new WheelCollider[4]; 
        [SerializeField] private GameObject[] m_WheelMeshes = new GameObject[4];


        [Header("核心物理参数")][SerializeField] private Vector3 m_CentreOfMassOffset;
        [SerializeField] private float m_MaximumSteerAngle = 25f;
        [SerializeField] private float m_FullTorqueOverAllWheels = 2500f;
        [SerializeField] private float m_ReverseTorque = 500f;
        [SerializeField] private float m_BrakeTorque = 20000f;
        [SerializeField] private float m_MaxHandbrakeTorque = 100000f;

        [Header("魔法辅助 (月球车设为0)")]
        [Range(0, 1)][SerializeField] private float m_SteerHelper = 0f;
        [Range(0, 1)][SerializeField] private float m_TractionControl = 0.3f;
        [SerializeField] private float m_Downforce = 0f;

        [Header("速度与档位设置")]
        [SerializeField] private SpeedType m_SpeedType;
        [SerializeField] private float m_Topspeed = 40f;
        // 【修复致命Bug】：去掉了 static，现在可以在 Inspector 里正常保存了！
        [SerializeField] private int NoOfGears = 5;
        [SerializeField] private float m_RevRangeBoundary = 1f; [SerializeField] private float m_SlipLimit = 0.3f;

        [Header("Startup ground placement")]
        [SerializeField] private bool m_AutoPlaceOnGround = true;
        [SerializeField] private Terrain m_StartupTerrain;
        [Range(0f, 1f)]
        [SerializeField] private float m_StartupSuspensionExtension = 0.5f;
        [SerializeField] private float m_StartupGroundClearance = 0.02f;

        // 私有变量区
        private Quaternion[] m_WheelMeshLocalRotations;
        private Vector3 m_Prevpos, m_Pos;
        private float m_SteerAngle;
        private int m_GearNum;
        private float m_GearFactor;
        private float m_OldRotation;
        private float m_CurrentTorque;
        private Rigidbody m_Rigidbody;
        private const float k_ReversingThreshold = 0.01f;

        // 月球低重力补偿：存储原始摩擦刚度
        private float[] m_OriginalFwdStiffness;
        private float[] m_OriginalSideStiffness;

        // --- 供外部硬件和仪表盘读取的数据接口 ---
        public Rigidbody CarRigidbody { get { return m_Rigidbody; } }
        public WheelCollider[] WheelColliders { get { return m_WheelColliders; } }

        public bool Skidding { get; private set; }
        public float BrakeInput { get; private set; }
        public float CurrentSteerAngle { get { return m_SteerAngle; } }
        public float CurrentSpeed { get { return m_Rigidbody.velocity.magnitude * 2.23693629f; } }
        public float MaxSpeed { get { return m_Topspeed; } }
        public float Revs { get; private set; }
        public float AccelInput { get; private set; }

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            PlaceRoverOnGround();

            m_WheelMeshLocalRotations = new Quaternion[4];
            for (int i = 0; i < 4; i++)
            {
                m_WheelMeshLocalRotations[i] = m_WheelMeshes[i].transform.localRotation;
            }
            if (m_WheelColliders[0] != null && m_WheelColliders[0].attachedRigidbody != null)
                m_WheelColliders[0].attachedRigidbody.centerOfMass = m_CentreOfMassOffset;
            // 手刹扭矩：合理值而非 float.MaxValue，避免锁死
            m_MaxHandbrakeTorque = 50000f;
            // ── 月球低重力补偿 ──
            // 存储原始摩擦值，只在有驱动力时临时拉高
            m_OriginalFwdStiffness = new float[4];
            m_OriginalSideStiffness = new float[4];
            float gc = 6f;
            for (int i = 0; i < 4; i++)
            {
                if (m_WheelColliders[i] != null)
                {
                    m_OriginalFwdStiffness[i] = m_WheelColliders[i].forwardFriction.stiffness;
                    m_OriginalSideStiffness[i] = m_WheelColliders[i].sidewaysFriction.stiffness;

                    WheelFrictionCurve fwd = m_WheelColliders[i].forwardFriction;
                    fwd.stiffness = m_OriginalFwdStiffness[i] * gc;
                    m_WheelColliders[i].forwardFriction = fwd;

                    WheelFrictionCurve side = m_WheelColliders[i].sidewaysFriction;
                    side.stiffness = m_OriginalSideStiffness[i] * gc;
                    m_WheelColliders[i].sidewaysFriction = side;
                }
            }

            m_CurrentTorque = m_FullTorqueOverAllWheels - (m_TractionControl * m_FullTorqueOverAllWheels);
        }

        private void PlaceRoverOnGround()
        {
            if (!m_AutoPlaceOnGround || m_Rigidbody == null || m_WheelColliders == null ||
                m_WheelColliders.Length == 0)
                return;

            if (m_StartupTerrain == null)
                m_StartupTerrain = Terrain.activeTerrain;
            if (m_StartupTerrain == null)
            {
                Debug.LogWarning("[Rover startup] Auto ground placement skipped: no Terrain found.");
                return;
            }

            Vector3 terrainPosition = m_StartupTerrain.transform.position;
            Vector3 terrainSize = m_StartupTerrain.terrainData.size;
            float requiredVerticalOffset = float.NegativeInfinity;
            int validWheelCount = 0;

            for (int i = 0; i < m_WheelColliders.Length; i++)
            {
                WheelCollider wheel = m_WheelColliders[i];
                if (wheel == null) continue;

                Vector3 wheelCentre = wheel.transform.TransformPoint(wheel.center);
                if (wheelCentre.x < terrainPosition.x || wheelCentre.x > terrainPosition.x + terrainSize.x ||
                    wheelCentre.z < terrainPosition.z || wheelCentre.z > terrainPosition.z + terrainSize.z)
                    continue;

                float groundHeight = m_StartupTerrain.SampleHeight(wheelCentre) + terrainPosition.y;
                float suspensionExtension = wheel.suspensionDistance *
                                            Mathf.Clamp01(m_StartupSuspensionExtension);
                float desiredWheelCentreY = groundHeight + wheel.radius + suspensionExtension;
                requiredVerticalOffset = Mathf.Max(requiredVerticalOffset,
                    desiredWheelCentreY - wheelCentre.y);
                validWheelCount++;
            }

            if (validWheelCount == 0)
            {
                Debug.LogWarning("[Rover startup] Auto ground placement skipped: wheels are outside the Terrain.");
                return;
            }

            Vector3 position = m_Rigidbody.position;
            position.y += requiredVerticalOffset + Mathf.Max(0f, m_StartupGroundClearance);
            m_Rigidbody.position = position;
            m_Rigidbody.velocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            transform.position = position;
            Physics.SyncTransforms();
        }

        // ... (下方 Update 和 Move 等方法保持之前的最新版不变) ...

        
        private void Update()
        {
            for (int i = 0; i < 4; i++)
            {
                if (m_WheelColliders[i] != null && m_WheelMeshes[i] != null)
                {
                    Quaternion quat;
                    Vector3 position;
                    m_WheelColliders[i].GetWorldPose(out position, out quat);
                    m_WheelMeshes[i].transform.position = position;
                    m_WheelMeshes[i].transform.rotation = quat;
                }
            }
        }

        public void Move(float steering, float accel, float footbrake, float handbrake)
        {
            

            steering = Mathf.Clamp(steering, -1, 1);
            AccelInput = accel = Mathf.Clamp(accel, 0, 1);
            BrakeInput = footbrake = Mathf.Clamp(footbrake, -1, 1);
            handbrake = Mathf.Clamp(handbrake, 0, 1);

            m_SteerAngle = steering * m_MaximumSteerAngle;
            m_WheelColliders[0].steerAngle = m_SteerAngle;
            m_WheelColliders[1].steerAngle = m_SteerAngle;

            SteerHelper();
            ApplyDrive(accel, footbrake);
            CapSpeed(accel, footbrake);

            if (handbrake > 0f)
            {
                var hbTorque = handbrake * m_MaxHandbrakeTorque;
                m_WheelColliders[2].brakeTorque = hbTorque;
                m_WheelColliders[3].brakeTorque = hbTorque;
            }

            CalculateRevs();
            GearChanging();

            AddDownForce();
            TractionControl();
        }

        private void GearChanging()
        {
            float f = Mathf.Abs(CurrentSpeed / MaxSpeed);
            float upgearlimit = (1 / (float)NoOfGears) * (m_GearNum + 1);
            float downgearlimit = (1 / (float)NoOfGears) * m_GearNum;

            if (m_GearNum > 0 && f < downgearlimit) m_GearNum--;
            if (f > upgearlimit && (m_GearNum < (NoOfGears - 1))) m_GearNum++;
        }

        private static float CurveFactor(float factor) { return 1 - (1 - factor) * (1 - factor); }
        private static float ULerp(float from, float to, float value) { return (1.0f - value) * from + value * to; }

        private void CalculateGearFactor()
        {
            float f = (1 / (float)NoOfGears);
            var targetGearFactor = Mathf.InverseLerp(f * m_GearNum, f * (m_GearNum + 1), Mathf.Abs(CurrentSpeed / MaxSpeed));
            m_GearFactor = Mathf.Lerp(m_GearFactor, targetGearFactor, Time.deltaTime * 5f);
        }

        private void CalculateRevs()
        {
            CalculateGearFactor();
            var gearNumFactor = m_GearNum / (float)NoOfGears;
            var revsRangeMin = ULerp(0f, m_RevRangeBoundary, CurveFactor(gearNumFactor));
            var revsRangeMax = ULerp(m_RevRangeBoundary, 1f, gearNumFactor);
            Revs = ULerp(revsRangeMin, revsRangeMax, m_GearFactor);
        }

        private void CapSpeed(float accel, float footbrake)
        {
            // 只保留最高速保护，不再根据踏板开度硬改速度
            float speed = m_Rigidbody.velocity.magnitude;
            float topMs = m_Topspeed / 2.23693629f; // MPH → m/s

            if (speed > topMs)
            {
                m_Rigidbody.velocity = topMs * m_Rigidbody.velocity.normalized;
            }
        }

        private void ApplyDrive(float accel, float footbrake)
        {
            float thrustTorque = 0f;
            switch (m_CarDriveType)
            {
                case CarDriveType.FourWheelDrive:
                    thrustTorque = accel * (m_CurrentTorque / 4f);
                    for (int i = 0; i < 4; i++) m_WheelColliders[i].motorTorque = thrustTorque;
                    break;
                case CarDriveType.FrontWheelDrive:
                    thrustTorque = accel * (m_CurrentTorque / 2f);
                    m_WheelColliders[0].motorTorque = m_WheelColliders[1].motorTorque = thrustTorque;
                    break;
                case CarDriveType.RearWheelDrive:
                    thrustTorque = accel * (m_CurrentTorque / 2f);
                    m_WheelColliders[2].motorTorque = m_WheelColliders[3].motorTorque = thrustTorque;
                    break;
            }

            // ── 动态摩擦补偿：有油门时平滑插值到 ×6，松油门时平滑恢复 ──
            float gc = 6f;
            float lerpSpeed = 8f; // 插值速度
            for (int i = 0; i < 4; i++)
            {
                if (m_WheelColliders[i] != null && m_OriginalFwdStiffness != null)
                {
                    float targetFwd = (accel > 0f) ? m_OriginalFwdStiffness[i] * gc : m_OriginalFwdStiffness[i];
                    float targetSide = (accel > 0f) ? m_OriginalSideStiffness[i] * gc : m_OriginalSideStiffness[i];

                    WheelFrictionCurve fwd = m_WheelColliders[i].forwardFriction;
                    fwd.stiffness = Mathf.Lerp(fwd.stiffness, targetFwd, Time.deltaTime * lerpSpeed);
                    m_WheelColliders[i].forwardFriction = fwd;

                    WheelFrictionCurve side = m_WheelColliders[i].sidewaysFriction;
                    side.stiffness = Mathf.Lerp(side.stiffness, targetSide, Time.deltaTime * lerpSpeed);
                    m_WheelColliders[i].sidewaysFriction = side;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if (footbrake > 0f)
                {
                    // 刹车：总是优先，通过 brakeTorque 减速
                    m_WheelColliders[i].brakeTorque = m_BrakeTorque * footbrake;
                    m_WheelColliders[i].motorTorque = 0f;
                }
                else if (accel > 0f)
                {
                    // 加速
                    m_WheelColliders[i].brakeTorque = 0f;
                    m_WheelColliders[i].motorTorque = thrustTorque;
                }
                else
                {
                    // 滑行
                    m_WheelColliders[i].brakeTorque = 0f;
                    m_WheelColliders[i].motorTorque = 0f;
                }
            }
        }

        private void SteerHelper()
        {
            for (int i = 0; i < 4; i++)
            {
                WheelHit wheelhit;
                m_WheelColliders[i].GetGroundHit(out wheelhit);
                if (wheelhit.normal == Vector3.zero) return;
            }

            if (Mathf.Abs(m_OldRotation - transform.eulerAngles.y) < 10f)
            {
                var turnadjust = (transform.eulerAngles.y - m_OldRotation) * m_SteerHelper;
                Quaternion velRotation = Quaternion.AngleAxis(turnadjust, Vector3.up);
                m_Rigidbody.velocity = velRotation * m_Rigidbody.velocity;
            }
            m_OldRotation = transform.eulerAngles.y;
        }

        private void AddDownForce()
        {
            m_WheelColliders[0].attachedRigidbody.AddForce(-transform.up * m_Downforce *
                                                         m_WheelColliders[0].attachedRigidbody.velocity.magnitude);
        }



        private void TractionControl()
        {
            // 每帧根据四轮最大滑移计算目标扭矩，不再跨帧累积
            float maxSlip = 0f;
            WheelHit wheelHit;

            for (int i = 0; i < 4; i++)
            {
                if (m_WheelColliders[i].GetGroundHit(out wheelHit))
                {
                    maxSlip = Mathf.Max(maxSlip, Mathf.Abs(wheelHit.forwardSlip));
                }
            }

            // 滑移超过限制时按比例降低扭矩
            if (maxSlip > m_SlipLimit)
            {
                float reduction = Mathf.InverseLerp(m_SlipLimit, m_SlipLimit * 2f, maxSlip);
                m_CurrentTorque = m_FullTorqueOverAllWheels * (1f - reduction * m_TractionControl);
            }
            else
            {
                m_CurrentTorque = m_FullTorqueOverAllWheels * (1f - m_TractionControl);
            }
        }
    }
}

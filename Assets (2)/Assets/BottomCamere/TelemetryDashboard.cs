using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Vehicles.Car;

namespace MoonRover.Vision
{
    [RequireComponent(typeof(CarController))]
    public class TelemetryDashboard : MonoBehaviour
    {
        [Header("UI 引用")]
        public Text pitchText;
        public Text rollText;
        public RectTransform horizonLine;
        public Image suspFLImg;
        public Image suspFRImg;
        public Image suspRLImg;
        public Image suspRRImg;

        [Header("悬挂条最大高度 (由 UISetupWizard 自动设置)")]
        public float suspFLMax = 100f;
        public float suspFRMax = 100f;
        public float suspRLMax = 100f;
        public float suspRRMax = 100f;

        private CarController car;

        private float[] suspensions = new float[4];
        private float pitchAngle = 0f;
        private float rollAngle = 0f;

        private float uiUpdateTimer = 0f;
        private float uiUpdateInterval = 0.05f;

        void Awake()
        {
            car = GetComponent<CarController>();
        }

        void Update()
        {
            if (car == null || car.WheelColliders == null || car.WheelColliders.Length < 4) return;

            Vector3 euler = transform.eulerAngles;
            pitchAngle = euler.x > 180f ? euler.x - 360f : euler.x;
            rollAngle = euler.z > 180f ? euler.z - 360f : euler.z;

            for (int i = 0; i < 4; i++)
            {
                WheelCollider wc = car.WheelColliders[i];
                WheelHit hit;
                if (wc.GetGroundHit(out hit))
                {
                    Vector3 vectorToGround = hit.point - wc.transform.position;
                    float distanceToGround = Vector3.Dot(vectorToGround, -wc.transform.up);
                    float currentExtension = distanceToGround - wc.radius;
                    float compression = 1.0f - (currentExtension / wc.suspensionDistance);
                    suspensions[i] = Mathf.Clamp01(compression);
                }
                else
                {
                    suspensions[i] = 0f;
                }
            }

            uiUpdateTimer += Time.deltaTime;
            if (uiUpdateTimer >= uiUpdateInterval)
            {
                uiUpdateTimer = 0f;
                UpdateUI();
            }
        }

        void UpdateUI()
        {
            if (pitchText != null)
            {
                pitchText.text = "俯仰: " + (-pitchAngle).ToString("F1") + "°";
                pitchText.color = Mathf.Abs(pitchAngle) > 30f ? Color.red : Color.cyan;
            }

            if (rollText != null)
            {
                rollText.text = "侧倾: " + (-rollAngle).ToString("F1") + "°";
                rollText.color = Mathf.Abs(rollAngle) > 30f ? Color.red : Color.cyan;
            }

            if (horizonLine != null)
            {
                horizonLine.localRotation = Quaternion.Euler(0, 0, -rollAngle);
                Vector3 pos = horizonLine.anchoredPosition;
                pos.y = pitchAngle * 3f;
                horizonLine.anchoredPosition = pos;
            }

            UpdateSuspensionBar(suspFLImg, suspensions[0], suspFLMax);
            UpdateSuspensionBar(suspFRImg, suspensions[1], suspFRMax);
            UpdateSuspensionBar(suspRLImg, suspensions[2], suspRLMax);
            UpdateSuspensionBar(suspRRImg, suspensions[3], suspRRMax);
        }

        void UpdateSuspensionBar(Image img, float compression, float maxHeight)
        {
            if (img == null) return;

            img.fillAmount = compression;
            img.color = compression < 0.5f ? Color.green : (compression < 0.8f ? Color.yellow : Color.red);
        }
    }
}

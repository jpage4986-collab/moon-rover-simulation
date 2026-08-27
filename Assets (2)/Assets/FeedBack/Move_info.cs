using UnityEngine;
using UnityEngine.UI;

namespace MoonRover.FeedBack
{
    public class Move_info : MonoBehaviour
    {
        [Header("核心引用 (请将车和雷达拖入)")]
        public Rigidbody carRigidbody;
        public Scan terrainRadar;

        [Header("UI 引用")]
        public Text speedText;
        public Text distanceText;
        public Text fvText;
        public Text steerText;

        private float speed = 0;
        private float distance = 0;
        private float displayFv = 0f;
        private float uiUpdateTimer = 0f;
        private float uiUpdateInterval = 0.15f;

        void Update()
        {
            if (carRigidbody != null)
            {
                speed = carRigidbody.velocity.magnitude;
                distance += speed * Time.deltaTime;
            }

            uiUpdateTimer += Time.deltaTime;
            if (uiUpdateTimer >= uiUpdateInterval)
            {
                uiUpdateTimer = 0f;
                if (terrainRadar != null)
                {
                    displayFv = terrainRadar.Fv;
                }

                UpdateUI();
            }
        }

        void UpdateUI()
        {
            if (speedText != null)
                speedText.text = "车速: " + (speed * 3.6f).ToString("F1") + " km/h";

            if (distanceText != null)
                distanceText.text = "里程: " + distance.ToString("F1") + " m";

            if (fvText != null)
            {
                fvText.text = "颠簸(Fv): " + displayFv.ToString("F2");
                fvText.color = displayFv > 10f ? Color.red : (displayFv > 5f ? Color.yellow : Color.green);
            }

            if (steerText != null && carRigidbody != null)
            {
                // 显示当前转向角度（从 CarController 或 Transform 读取）
                float steerAngle = carRigidbody.gameObject.transform.eulerAngles.y;
                steerText.text = "方向: " + steerAngle.ToString("F1") + "°";
            }
        }
    }
}

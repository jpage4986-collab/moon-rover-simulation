using UnityEngine;
using UnityEngine.UI;

namespace MoonRover.FeedBack
{
    /// <summary>
    /// 雷达扫描动画 — 纯视觉效果，无逻辑
    /// 旋转扫线 + 中心脉冲 + 扩展波纹
    /// </summary>
    public class MapAnimations : MonoBehaviour
    {
        [Header("脉冲点")]
        public RectTransform pulseDot;
        public float pulseSpeed = 2f;
        public float pulseMin = 0.6f;
        public float pulseMax = 1.2f;

        [Header("扩展波纹")]
        public Image ringImage;
        public float ringExpandSpeed = 80f;
        public float ringFadeSpeed = 1.5f;
        public float ringMaxSize = 200f;
        public float ringSpawnInterval = 2.5f;

        private float pulsePhase;
        private float ringTimer;
        private float ringAlpha;
        private float spawnTimer;

        void Start()
        {
            SpawnRing();
        }

        void Update()
        {
            // 脉冲点缩放
            if (pulseDot != null)
            {
                pulsePhase += pulseSpeed * Time.deltaTime;
                float s = Mathf.Lerp(pulseMin, pulseMax, Mathf.Sin(pulsePhase) * 0.5f + 0.5f);
                pulseDot.localScale = new Vector3(s, s, 1f);
            }

            // 扩展波纹
            if (ringImage != null)
            {
                if (ringImage.gameObject.activeSelf)
                {
                    ringTimer += Time.deltaTime;
                    float progress = ringTimer * ringExpandSpeed / ringMaxSize;
                    float size = Mathf.Min(progress * ringMaxSize, ringMaxSize);
                    ringImage.rectTransform.sizeDelta = new Vector2(size, size);

                    ringAlpha = Mathf.Max(0, 1f - progress * ringFadeSpeed);
                    var c = ringImage.color;
                    ringImage.color = new Color(c.r, c.g, c.b, ringAlpha);

                    if (ringAlpha <= 0)
                        ringImage.gameObject.SetActive(false);
                }

                // 定期生成波纹
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= ringSpawnInterval)
                {
                    spawnTimer = 0f;
                    SpawnRing();
                }
            }
        }

        public void SpawnRing()
        {
            if (ringImage == null) return;
            ringImage.gameObject.SetActive(true);
            ringImage.rectTransform.sizeDelta = Vector2.zero;
            var c = ringImage.color;
            ringImage.color = new Color(c.r, c.g, c.b, 0.6f);
            ringTimer = 0f;
            ringAlpha = 0.6f;
        }
    }
}

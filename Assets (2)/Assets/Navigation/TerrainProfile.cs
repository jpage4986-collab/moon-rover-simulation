using UnityEngine;
using UnityEngine.UI;

namespace MoonRover.Navigation
{
    /// <summary>
    /// 地形剖面仪 — 在车前采样一段地形高度，绘制到 RawImage
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TerrainProfile : MonoBehaviour
    {
        [Header("UI 引用")]
        public RawImage profileImage;
        public Text labelText;

        [Header("采样参数")]
        public float lookAhead = 20f;
        public int sampleCount = 60;
        public float sampleSpacing = 0.5f;

        [Header("显示参数")]
        public Color groundColor = new Color(0.3f, 0.6f, 0.3f, 0.8f);
        public Color roverLineColor = new Color(1f, 0.3f, 0.3f, 1f);
        public int textureWidth = 256;
        public int textureHeight = 128;
        public float updateInterval = 0.15f;

        private Texture2D profileTex;
        private Terrain targetTerrain;
        private float timer = 0f;
        private float[] heightProfile;

        void Start()
        {
            targetTerrain = Terrain.activeTerrain;
            if (targetTerrain == null)
            {
                Debug.LogError("[TerrainProfile] No Terrain found!");
                enabled = false;
                return;
            }

            profileTex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            profileTex.filterMode = FilterMode.Bilinear;
            profileTex.wrapMode = TextureWrapMode.Clamp;
            heightProfile = new float[sampleCount];

            if (profileImage != null)
                profileImage.texture = profileTex;
        }

        void Update()
        {
            if (targetTerrain == null || profileTex == null) return;

            timer += Time.deltaTime;
            if (timer < updateInterval) return;
            timer = 0f;

            SampleProfile();
            RenderProfile();
        }

        void SampleProfile()
        {
            Vector3 origin = transform.position;
            Vector3 fwd = transform.forward;
            Vector3 tPos = targetTerrain.transform.position;
            TerrainData tData = targetTerrain.terrainData;

            for (int i = 0; i < sampleCount; i++)
            {
                float dist = i * sampleSpacing;
                Vector3 samplePos = origin + fwd * dist;
                float height = targetTerrain.SampleHeight(samplePos) + tPos.y;
                // 相对于车底高度
                heightProfile[i] = height - (origin.y - 0.3f);
            }
        }

        void RenderProfile()
        {
            Color bg = new Color(0, 0, 0, 0);
            Color[] pixels = new Color[textureWidth * textureHeight];

            // 清空
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = bg;

            if (heightProfile.Length < 2) return;

            // 归一化高度范围
            float minH = float.MaxValue, maxH = float.MinValue;
            for (int i = 0; i < heightProfile.Length; i++)
            {
                if (heightProfile[i] < minH) minH = heightProfile[i];
                if (heightProfile[i] > maxH) maxH = heightProfile[i];
            }
            float range = Mathf.Max(maxH - minH, 1f);

            int centerY = textureHeight / 2;
            float scaleY = (textureHeight - 8) / range;

            // 绘制地形轮廓 (填充)
            for (int x = 0; x < textureWidth; x++)
            {
                float t = (float)x / textureWidth;
                int idx = Mathf.Clamp(Mathf.RoundToInt(t * (sampleCount - 1)), 0, sampleCount - 1);
                float hNorm = (heightProfile[idx] - minH) / range;
                int y = Mathf.Clamp(Mathf.RoundToInt(centerY + (hNorm - 0.5f) * scaleY), 0, textureHeight - 1);

                // 从 y 到 底部填充地面颜色
                for (int py = y; py < textureHeight; py++)
                    pixels[py * textureWidth + x] = groundColor;

                // 地面线
                if (y > 0 && y < textureHeight)
                    pixels[y * textureWidth + x] = Color.Lerp(groundColor, Color.white, 0.5f);
            }

            // 绘制车位置标记 (红线, 左侧 5%)
            int roverX = Mathf.RoundToInt(textureWidth * 0.05f);
            for (int py = 0; py < textureHeight; py++)
            {
                int idx = py * textureWidth + roverX;
                if (idx >= 0 && idx < pixels.Length)
                    pixels[idx] = roverLineColor;
            }

            profileTex.SetPixels(pixels);
            profileTex.Apply();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoonRover.FeedBack
{
    public class ExplorationMap : MonoBehaviour
    {
        struct RevealTask
        {
            public int x, y;
            public Color finalColor;
            public float timer;
            public bool settled;
        }

        [Header("核心引用")]
        public Transform rover;
        public Scan scanner;
        [SerializeField] private Terrain targetTerrain;

        [Header("UGUI 引用")]
        public RawImage mapImage;
        public RectTransform arrowTransform;

        [Header("高清测绘设置")]
        public int mapResolution = 2048;
        public float contourInterval = 2.0f;
        public float majorContour = 10.0f;

        [Header("抖动效果")]
        public float jitterDuration = 0.4f;
        public int pixelsPerFrame = 300;

        private Texture2D mapTexture;
        private bool[,] exploredGrid;
        private bool[,] queuedGrid;
        private Queue<RevealTask> revealQueue;
        private TerrainData tData;
        private Vector3 tPos;

        private float updateTimer = 0f;
        private float updateInterval = 0.1f;

        void Start()
        {
            if (targetTerrain == null)
            {
                targetTerrain = Terrain.activeTerrain;
            }
            if (targetTerrain == null)
            {
                Debug.LogError("[ExplorationMap] No Terrain found in scene!");
                enabled = false;
                return;
            }

            tData = targetTerrain.terrainData;
            tPos = targetTerrain.transform.position;

            mapTexture = new Texture2D(mapResolution, mapResolution);
            mapTexture.filterMode = FilterMode.Bilinear;
            mapTexture.wrapMode = TextureWrapMode.Clamp;

            exploredGrid = new bool[mapResolution, mapResolution];
            queuedGrid = new bool[mapResolution, mapResolution];
            revealQueue = new Queue<RevealTask>();

            Color unexploredColor = new Color(0.3f, 0.3f, 0.35f, 1f);
            for (int x = 0; x < mapResolution; x++)
            {
                for (int y = 0; y < mapResolution; y++)
                {
                    mapTexture.SetPixel(x, y, unexploredColor);
                    exploredGrid[x, y] = false;
                }
            }
            mapTexture.Apply();

            if (mapImage != null)
            {
                mapImage.texture = mapTexture;
                mapImage.color = Color.white;
                Debug.Log($"[ExplorationMap] ({gameObject.name}) 地图已初始化。地形: {targetTerrain.name}");
            }
            else
            {
                Debug.LogWarning($"[ExplorationMap] ({gameObject.name}) mapImage 未绑定。可能是残留旧对象，已自动禁用。");
                enabled = false;
                return;
            }
        }

        void Update()
        {
            if (rover == null || scanner == null || targetTerrain == null) return;
            if (exploredGrid == null || queuedGrid == null)
            {
                Debug.LogWarning("[ExplorationMap] Grids not initialized yet, reinitializing...");
                if (targetTerrain != null)
                {
                    tData = targetTerrain.terrainData;
                    tPos = targetTerrain.transform.position;
                    exploredGrid = new bool[mapResolution, mapResolution];
                    queuedGrid = new bool[mapResolution, mapResolution];
                    revealQueue = new Queue<RevealTask>();
                }
                else return;
            }

            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                UpdateExplorationMap();
            }

            ProcessRevealQueue();
            UpdateArrow();
        }

        void UpdateExplorationMap()
        {
            if (rover == null || scanner == null || targetTerrain == null || tData == null) return;

            Vector3 rPos = rover.position;
            Vector2 roverPos2D = new Vector2(rPos.x, rPos.z);
            Vector2 roverFwd2D = new Vector2(rover.forward.x, rover.forward.z).normalized;

            float maxRad = scanner.maxRadius;
            float fov = scanner.fovAngle;

            // 将车的世界坐标映射到地图纹理像素
            int centerPx = Mathf.RoundToInt(((rPos.x - tPos.x) / tData.size.x) * mapResolution);
            int centerPy = Mathf.RoundToInt(((rPos.z - tPos.z) / tData.size.z) * mapResolution);

            // 扫描半径对应的像素半径
            int pixelRadius = Mathf.CeilToInt((maxRad / tData.size.x) * mapResolution);
            pixelRadius = Mathf.Max(1, pixelRadius);

            for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
            {
                for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
                {
                    int mapX = centerPx + dx;
                    int mapY = centerPy + dy;

                    if (mapX < 0 || mapX >= mapResolution || mapY < 0 || mapY >= mapResolution) continue;
                    if (exploredGrid[mapX, mapY] || queuedGrid[mapX, mapY]) continue;

                    // 像素对应的世界坐标
                    float wX = tPos.x + ((float)mapX / mapResolution) * tData.size.x;
                    float wZ = tPos.z + ((float)mapY / mapResolution) * tData.size.z;
                    Vector2 pixelWorld2D = new Vector2(wX, wZ);

                    // 距离检测
                    if (Vector2.Distance(roverPos2D, pixelWorld2D) > maxRad) continue;

                    // FOV 检测
                    Vector2 dirToPixel = (pixelWorld2D - roverPos2D).normalized;
                    if (Vector2.Angle(roverFwd2D, dirToPixel) > fov / 2f) continue;

                    // 采样高度和坡度以计算最终颜色
                    float height = targetTerrain.SampleHeight(new Vector3(wX, 1000f, wZ));
                    float normX = (wX - tPos.x) / tData.size.x;
                    float normZ = (wZ - tPos.z) / tData.size.z;
                    float steepness = tData.GetSteepness(normX, normZ);

                    int level = Mathf.FloorToInt(height / contourInterval);
                    float baseBright = Mathf.Clamp01(0.08f + level * 0.03f);
                    Color finalColor = new Color(baseBright, baseBright + 0.06f, baseBright + 0.12f, 1f);

                    float minorVal = height % contourInterval;
                    float minorDist = minorVal > (contourInterval / 2f) ? contourInterval - minorVal : minorVal;
                    float majorVal = height % majorContour;
                    float majorDist = majorVal > (majorContour / 2f) ? majorContour - majorVal : majorVal;
                    float lineTolerance = 0.08f + (steepness / 90f) * 1.5f;

                    if (majorDist <= lineTolerance * 1.5f) finalColor = Color.cyan;
                    else if (minorDist <= lineTolerance) finalColor = Color.green;
                    if (steepness > 22f) finalColor = Color.Lerp(finalColor, Color.red, 0.65f);

                    queuedGrid[mapX, mapY] = true;
                    revealQueue.Enqueue(new RevealTask { x = mapX, y = mapY, finalColor = finalColor, timer = 0f, settled = false });
                }
            }
        }

        void ProcessRevealQueue()
        {
            if (revealQueue == null || revealQueue.Count == 0) return;

            bool textureChanged = false;
            int processed = 0;

            while (revealQueue.Count > 0 && processed < pixelsPerFrame)
            {
                RevealTask task = revealQueue.Dequeue();

                if (!task.settled)
                {
                    task.timer += Time.deltaTime;

                    if (task.timer >= jitterDuration)
                    {
                        // 抖动结束，在正确位置绘制最终颜色
                        mapTexture.SetPixel(task.x, task.y, task.finalColor);
                        exploredGrid[task.x, task.y] = true;
                        textureChanged = true;
                    }
                    else
                    {
                        // 抖动阶段：随机偏移 ±1~2px + 亮度噪点
                        int offsetX = Random.Range(0, 3) - 1; // -1, 0, 1
                        int offsetY = Random.Range(0, 3) - 1;
                        int jx = Mathf.Clamp(task.x + offsetX, 0, mapResolution - 1);
                        int jy = Mathf.Clamp(task.y + offsetY, 0, mapResolution - 1);

                        Color jitterColor = task.finalColor;
                        float noise = Random.Range(-0.12f, 0.12f);
                        jitterColor.r = Mathf.Clamp01(jitterColor.r + noise);
                        jitterColor.g = Mathf.Clamp01(jitterColor.g + noise);
                        jitterColor.b = Mathf.Clamp01(jitterColor.b + noise);

                        mapTexture.SetPixel(jx, jy, jitterColor);
                        textureChanged = true;

                        // 未完成，重新入队
                        revealQueue.Enqueue(task);
                    }
                }
                processed++;
            }

            if (textureChanged) mapTexture.Apply();
        }

        void UpdateArrow()
        {
            if (arrowTransform == null || mapImage == null) return;

            RectTransform mapRect = mapImage.rectTransform;
            Vector2 mapSize = mapRect.rect.size;

            float rxNorm = (rover.position.x - tPos.x) / tData.size.x;
            float rzNorm = (rover.position.z - tPos.z) / tData.size.z;

            float localX = (rxNorm - 0.5f) * mapSize.x;
            float localY = (rzNorm - 0.5f) * mapSize.y;

            arrowTransform.anchoredPosition = new Vector2(localX, localY);
            arrowTransform.localRotation = Quaternion.Euler(0, 0, -rover.eulerAngles.y);
        }
    }
}

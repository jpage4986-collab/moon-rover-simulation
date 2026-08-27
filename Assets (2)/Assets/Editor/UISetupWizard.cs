using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using MoonRover.Driver;
using MoonRover.FeedBack;
using MoonRover.Vision;
using MoonRover.Platform;
using MoonRover.UI;
using MoonRover.Navigation;
using UnityStandardAssets.Vehicles.Car;

/// <summary>
/// MoonRover UI 生成器 — SpaceX 极简 HUD 风格
/// 设计原则: 纯黑背景 + 白/灰极简边框 + 幽灵描边按钮 + 数据优先
/// </summary>
public class UISetupWizard : EditorWindow
{
    // ══════════════════════════════════════════════════
    //  布局常量 (7680×2160 超宽屏 32:9)
    //  左列: Mode(200) → Status(340) → Telemetry(800)  [640px 统一宽]
    //  右列: Map(580) → Cam(580) → SysStatus(360)      [580px 统一宽]
    // ══════════════════════════════════════════════════
    const int REF_WIDTH = 7680;
    const int REF_HEIGHT = 2160;
    const int MARGIN = 60;
    const int PANEL_GAP = 30;

    // ── 左列 (640px 宽, 左上锚定) ──
    const int COL1_W = 640;
    static readonly Vector2 MODE_PANEL_POS       = new Vector2(MARGIN, -MARGIN);
    static readonly Vector2 MODE_PANEL_SIZE       = new Vector2(COL1_W, 200);
    const int MODE_BOTTOM = MARGIN + 200;
    static readonly Vector2 STATUS_PANEL_POS      = new Vector2(MARGIN, -(MODE_BOTTOM + PANEL_GAP));
    static readonly Vector2 STATUS_PANEL_SIZE     = new Vector2(COL1_W, 340);
    const int STATUS_BOTTOM = MODE_BOTTOM + PANEL_GAP + 340;
    static readonly Vector2 TELEMETRY_PANEL_POS   = new Vector2(MARGIN, -(STATUS_BOTTOM + PANEL_GAP));
    static readonly Vector2 TELEMETRY_PANEL_SIZE  = new Vector2(COL1_W, 800);

    // ── 右列 (580px 宽, 右上锚定) ──
    const int COL2_W = 580;
    static readonly Vector2 MAP_PANEL_POS         = new Vector2(-MARGIN, -MARGIN);
    static readonly Vector2 MAP_PANEL_SIZE        = new Vector2(COL2_W, 580);
    const int MAP_BOTTOM = MARGIN + 580;
    static readonly Vector2 CAM_PANEL_POS         = new Vector2(-MARGIN, -(MAP_BOTTOM + PANEL_GAP));
    static readonly Vector2 CAM_PANEL_SIZE        = new Vector2(COL2_W, 580);
    const int CAM_BOTTOM = MAP_BOTTOM + PANEL_GAP + 580;
    static readonly Vector2 SYSSTATUS_PANEL_POS   = new Vector2(-MARGIN, -(CAM_BOTTOM + PANEL_GAP));
    static readonly Vector2 SYSSTATUS_PANEL_SIZE  = new Vector2(COL2_W, 360);
    const int SYS_BOTTOM = CAM_BOTTOM + PANEL_GAP + 360;
    static readonly Vector2 BATT_PANEL_POS        = new Vector2(-MARGIN, -(SYS_BOTTOM + PANEL_GAP));
    static readonly Vector2 BATT_PANEL_SIZE       = new Vector2(COL2_W, 180);
    const int BATT_BOTTOM = SYS_BOTTOM + PANEL_GAP + 180;
    static readonly Vector2 EVENTLOG_PANEL_POS    = new Vector2(-MARGIN, -(BATT_BOTTOM + PANEL_GAP));
    static readonly Vector2 EVENTLOG_PANEL_SIZE  = new Vector2(COL2_W, 220);

    // ── 罗盘条 (顶边居中) ──
    const int COMPASS_BAR_W = 800;
    const int COMPASS_BAR_H = 60;

    // ══════════════════════════════════════════════════
    //  配色方案 — SpaceX 极简风格 (纯黑 + 白/灰)
    // ══════════════════════════════════════════════════
    static readonly Color COL_BG          = new Color(0f, 0f, 0f, 1f);       // Pure black (SpaceX)
    static readonly Color COL_BG_GRADIENT = new Color(0f, 0f, 0f, 1f);       // Solid black — no gradient
    static readonly Color COL_ACCENT      = new Color(1f, 1f, 1f, 1f);       // White (was cyan)
    static readonly Color COL_ACCENT_DIM  = new Color(0.65f, 0.65f, 0.65f, 0.7f); // Light gray
    static readonly Color COL_ACCENT_GLOW = new Color(1f, 1f, 1f, 0.3f);     // White glow (was cyan)
    static readonly Color COL_TITLE       = new Color(1f, 1f, 1f, 1f);       // White titles
    static readonly Color COL_VALUE       = new Color(0.3f, 1f, 0.3f, 1f);   // Keep green (functional HUD)
    static readonly Color COL_WARN        = new Color(1f, 0.75f, 0.1f, 1f);  // Keep yellow
    static readonly Color COL_ALERT       = new Color(1f, 0.2f, 0.15f, 1f);  // Keep red
    static readonly Color COL_TEXT        = new Color(1f, 1f, 1f, 1f);       // Pure white
    static readonly Color COL_TEXT_DIM    = new Color(0.6f, 0.6f, 0.6f, 1f); // Medium gray
    static readonly Color COL_BORDER      = new Color(1f, 1f, 1f, 0.5f);     // White border (was cyan)

    static Font _uiFont;
    static Texture2D _gridTex;

    // ── 圆角缓存 ──
    const int ROUND_RADIUS = 36;
    const float BORDER_AA_WIDTH = 1.5f;
    static Dictionary<Vector2Int, Texture2D> _roundedBorderCache = new Dictionary<Vector2Int, Texture2D>();
    static Dictionary<Vector2Int, Texture2D> _roundedFillCache = new Dictionary<Vector2Int, Texture2D>();

    // ══════════════════════════════════════════════════
    //  资源加载
    // ══════════════════════════════════════════════════

    static Font GetCJKFont()
    {
        if (_uiFont != null) return _uiFont;
        _uiFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 28);
        if (_uiFont == null) _uiFont = Font.CreateDynamicFontFromOSFont("SimHei", 28);
        if (_uiFont == null) _uiFont = Font.CreateDynamicFontFromOSFont("SimSun", 28);
        if (_uiFont == null) Debug.LogError("[UI] 未找到系统 CJK 字体");
        return _uiFont;
    }

    static void EnsureTextures()
    {
        if (_gridTex != null) return;
        _gridTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Kenney/Generated/GridPattern.png");
        if (_gridTex == null) { SciFiTextureGen.GenerateAll(); _gridTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Kenney/Generated/GridPattern.png"); }
    }

    // ══════════════════════════════════════════════════
    //  窗口
    // ══════════════════════════════════════════════════

    [MenuItem("Tools/MoonRover/一键生成UI (7680x2160)")]
    public static void ShowWindow() => GetWindow<UISetupWizard>("MoonRover UI");

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("MoonRover UI — SpaceX 极简 HUD", EditorStyles.boldLabel);
        GUILayout.Space(8);
        if (GUILayout.Button("一键生成全部 UI", GUILayout.Height(40))) GenerateAllUI();
        GUILayout.Space(3);
        if (GUILayout.Button("清除旧 UI", GUILayout.Height(24))) ClearOldUI();
        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "设计方向: SpaceX 极简 HUD — 纯黑 + 白/灰 + 圆角\n" +
            "· 纯黑底色 (#000000) + 白色圆角边框 (SDF 抗锯齿)\n" +
            "· 极淡网格 (仅大面板, 4% 透明度)\n" +
            "· Apple 风格圆角 (36px radius)\n" +
            "· 幽灵描边按钮 (Ghost Pill)\n" +
            "· 功能保留: 绿色数据 / 黄色警告 / 红色警报\n" +
            "· 保留中文字体 (微软雅黑)",
            MessageType.Info);
    }

    // ══════════════════════════════════════════════════
    //  清除
    // ══════════════════════════════════════════════════

    void ClearOldUI()
    {
        var canvas = GameObject.Find("MoonUI_Canvas");
        if (canvas != null) DestroyImmediate(canvas);
        string[] names = { "DriveModeManager", "Move_info", "ExplorationMap",
            "ChassisCamera", "TelemetryDashboard", "Scan_Radar",
            "MapPanel", "CamPanel", "ModePanel", "StatusPanel",
            "TelemetryPanel", "SystemStatusPanel",
            "MapCrossH", "MapCrossV", "MapFrame", "PulseDot", "PulseRing",
            "RingExpand", "SweepLine", "CenterRing", "MapAnimations",
            "OuterGlow", "InnerBorder" };
        foreach (string n in names)
            foreach (GameObject o in FindObjectsOfType<GameObject>().Where(x => x.name == n))
                DestroyImmediate(o);
        var cam = Camera.main;
        if (cam != null)
            foreach (string c in new[] { "ExplorationMap", "ChassisCamera", "TelemetryDashboard", "Move_info" })
            { var x = cam.GetComponent(c); if (x != null) DestroyImmediate(x); }

        // 额外保险：移除所有 MapAnimations 组件（可能挂载在任意对象上）
        foreach (var ma in FindObjectsOfType<MapAnimations>()) DestroyImmediate(ma.gameObject);

        Debug.Log("已清除旧 UI");
    }

    // ══════════════════════════════════════════════════
    //  主入口
    // ══════════════════════════════════════════════════

    void GenerateAllUI()
    {
        ClearOldUI();
        EnsureTextures();
        var car = FindCar();
        if (car == null) { EditorUtility.DisplayDialog("错误", "找不到 CarController 车辆", "确定"); return; }
        var canvas = CreateCanvas();
        GenerateModePanel(canvas, car);
        GenerateStatusPanel(canvas, car);
        GenerateTelemetryPanel(canvas, car);
        GenerateSystemStatusPanel(canvas, car);
        GenerateMapPanel(canvas, car);
        GenerateCameraPanel(canvas, car);
        GenerateCompassBar(canvas, car);
        GenerateBatteryPanel(canvas, car);
        GenerateEventLogPanel(canvas, car);
        GenerateAlertOverlay(canvas, car);

        // ── 新组件挂载到车辆 ──
        car.GetOrAddComponent<HUDDashboard>();
        car.GetOrAddComponent<MiniAttitudeIndicator>();
        car.GetOrAddComponent<WheelSlipIndicator>();
        car.GetOrAddComponent<TerrainProfile>();

        Selection.activeGameObject = canvas;
        Debug.Log("UI 生成完毕 — SpaceX 极简 HUD + 9 项新功能");
    }

    // ══════════════════════════════════════════════════
    //  Canvas
    // ══════════════════════════════════════════════════

    GameObject CreateCanvas()
    {
        var o = new GameObject("MoonUI_Canvas");
        var c = o.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        o.AddComponent<GraphicRaycaster>();
        var s = o.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(REF_WIDTH, REF_HEIGHT);
        s.matchWidthOrHeight = 0f;
        return o;
    }

    // ══════════════════════════════════════════════════
    //  面板生成 — 工业科幻 HUD 风格
    // ══════════════════════════════════════════════════

    GameObject CreateSciFiPanel(GameObject parent, string name, Vector2 pos, Vector2 size, bool topLeft)
    {
        var p = new GameObject(name, typeof(RectTransform));
        p.transform.SetParent(parent.transform, false);

        // ── SpaceX: 圆角背景 (填充纹理 + Mask, 剪裁子元素) ──
        var fillTex = GenerateRoundedFillTexture(
            Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), ROUND_RADIUS);
        var fillSprite = Sprite.Create(fillTex,
            new Rect(0, 0, fillTex.width, fillTex.height), new Vector2(0, 0));
        var bg = p.AddComponent<Image>();
        bg.sprite = fillSprite;
        bg.color = Color.black;
        var mask = p.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // ── SpaceX: 圆角边框 (RawImage 覆盖, SDF 抗锯齿) ──
        var borderTex = GenerateRoundedBorderTexture(
            Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y),
            ROUND_RADIUS, COL_BORDER, 1f);
        var borderGo = new GameObject("RoundedBorder", typeof(RectTransform));
        borderGo.transform.SetParent(p.transform, false);
        var borderImg = borderGo.AddComponent<RawImage>();
        borderImg.texture = borderTex;
        borderImg.color = Color.white;
        var borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = Vector2.zero;
        borderRt.offsetMax = Vector2.zero;

        // ── SpaceX: 极淡网格 (只有在大面板显示) ──
        if (_gridTex != null && size.x > 400 && size.y > 200)
        {
            var grid = new GameObject("GridOverlay", typeof(RectTransform));
            grid.transform.SetParent(p.transform, false);
            var gridImg = grid.AddComponent<RawImage>();
            gridImg.texture = _gridTex;
            gridImg.color = new Color(1f, 1f, 1f, 0.04f);
            var gridRt = grid.GetComponent<RectTransform>();
            gridRt.anchorMin = Vector2.zero; gridRt.anchorMax = Vector2.one;
            gridRt.offsetMin = Vector2.zero; gridRt.offsetMax = Vector2.zero;
        }

        var rt = p.GetComponent<RectTransform>();
        if (topLeft) { rt.anchorMin = Vector2.up; rt.anchorMax = Vector2.up; rt.pivot = new Vector2(0, 1); }
        else { rt.anchorMin = Vector2.one; rt.anchorMax = Vector2.one; rt.pivot = Vector2.one; }
        rt.anchoredPosition = pos; rt.sizeDelta = size;

        // ── SpaceX: 状态指示灯 (保留功能颜色) ──
        AddStatusLights(p, "StatusLights", new Vector2(size.x - 40, -10));

        AddTitleBar(p, "TitleBar", size);
        return p;
    }

    void AddLine(GameObject parent, string name, Vector2 pos, Vector2 size, Color color, bool topEdge)
    {
        var o = new GameObject(name, typeof(RectTransform));
        o.transform.SetParent(parent.transform, false);
        var img = o.AddComponent<Image>();
        img.color = color;
        var r = o.GetComponent<RectTransform>();
        r.anchorMin = topEdge ? new Vector2(0, 1) : new Vector2(0, 0);
        r.anchorMax = topEdge ? new Vector2(1, 1) : new Vector2(1, 0);
        r.pivot = topEdge ? new Vector2(0, 1) : new Vector2(0, 0);
        r.anchoredPosition = pos; r.sizeDelta = size;
    }

    void AddSeparator(GameObject parent, Vector2 pos, float width)
    {
        var o = new GameObject("Separator_" + parent.transform.childCount, typeof(RectTransform));
        o.transform.SetParent(parent.transform, false);
        var img = o.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.15f);
        var r = o.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = pos; r.sizeDelta = new Vector2(width, 1);
    }

    void AddSeparatorRight(GameObject parent, Vector2 pos, float width)
    {
        var o = new GameObject("SeparatorR_" + parent.transform.childCount, typeof(RectTransform));
        o.transform.SetParent(parent.transform, false);
        var img = o.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.15f);
        var r = o.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1);
        r.anchoredPosition = pos; r.sizeDelta = new Vector2(-width, 1);
    }

    void AddCornerBracket(GameObject parent, string name, float x, float y, float len, float thickness)
    {
        bool left = x == 0, top = y == 0;

        var h = new GameObject(name + "_H", typeof(RectTransform));
        h.transform.SetParent(parent.transform, false);
        var hI = h.AddComponent<Image>(); hI.color = COL_ACCENT_DIM;
        var hR = h.GetComponent<RectTransform>();
        hR.anchorMin = hR.anchorMax = new Vector2(0, 1); hR.pivot = new Vector2(0, 1);
        hR.anchoredPosition = new Vector2(left ? 0 : -len, top ? 0 : -len);
        hR.sizeDelta = new Vector2(left ? len : -len, thickness);

        var v = new GameObject(name + "_V", typeof(RectTransform));
        v.transform.SetParent(parent.transform, false);
        var vI = v.AddComponent<Image>(); vI.color = COL_ACCENT_DIM;
        var vR = v.GetComponent<RectTransform>();
        vR.anchorMin = vR.anchorMax = new Vector2(0, 1); vR.pivot = new Vector2(0, 1);
        vR.anchoredPosition = new Vector2(left ? 0 : -len, top ? 0 : -len);
        vR.sizeDelta = new Vector2(thickness, top ? -len : len);
    }

    void AddThickBorder(GameObject parent, string name, Vector2 size, Color color, float thickness = 1f)
    {
        float t = thickness;

        var top = new GameObject(name + "_T", typeof(RectTransform));
        top.transform.SetParent(parent.transform, false);
        top.AddComponent<Image>().color = color;
        var tr = top.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1);
        tr.pivot = new Vector2(0, 1); tr.anchoredPosition = Vector2.zero;
        tr.sizeDelta = new Vector2(0, -t);

        var bot = new GameObject(name + "_B", typeof(RectTransform));
        bot.transform.SetParent(parent.transform, false);
        bot.AddComponent<Image>().color = color;
        var br = bot.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0);
        br.pivot = new Vector2(0, 0); br.anchoredPosition = Vector2.zero;
        br.sizeDelta = new Vector2(0, t);

        var left = new GameObject(name + "_L", typeof(RectTransform));
        left.transform.SetParent(parent.transform, false);
        left.AddComponent<Image>().color = color;
        var lr = left.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(0, 1);
        lr.pivot = new Vector2(0, 0); lr.anchoredPosition = Vector2.zero;
        lr.sizeDelta = new Vector2(t, 0);

        var right = new GameObject(name + "_R", typeof(RectTransform));
        right.transform.SetParent(parent.transform, false);
        right.AddComponent<Image>().color = color;
        var rr = right.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(1, 0); rr.anchorMax = new Vector2(1, 1);
        rr.pivot = new Vector2(1, 0); rr.anchoredPosition = Vector2.zero;
        rr.sizeDelta = new Vector2(-t, 0);
    }

    // ── SpaceX: 测量刻度标记 ──
    void AddMeasurementMarks(GameObject parent, string name, float x, float startY, float totalHeight, bool leftSide)
    {
        int numMarks = 5;
        float spacing = totalHeight / numMarks;

        for (int i = 1; i < numMarks; i++)
        {
            float y = startY + i * spacing;
            float markLen = (i % 2 == 0) ? 16f : 8f;
            float markThick = 1f;

            var mark = new GameObject(name + "_" + i, typeof(RectTransform));
            mark.transform.SetParent(parent.transform, false);
            var markImg = mark.AddComponent<Image>();
            markImg.color = new Color(1f, 1f, 1f, 0.2f);
            var markR = mark.GetComponent<RectTransform>();
            markR.anchorMin = markR.anchorMax = new Vector2(0, 1);
            markR.pivot = new Vector2(0, 1);
            markR.anchoredPosition = new Vector2(leftSide ? 0 : -markLen, y);
            markR.sizeDelta = new Vector2(leftSide ? markLen : -markLen, markThick);
        }
    }

    // ── React Bits 风格: 状态指示灯 ──
    void AddStatusLights(GameObject parent, string name, Vector2 pos)
    {
        float dotSize = 12f;
        float spacing = 28f;
        Color[] colors = { COL_VALUE, COL_WARN, COL_ACCENT };

        for (int i = 0; i < colors.Length; i++)
        {
            var dot = new GameObject(name + "_" + i, typeof(RectTransform));
            dot.transform.SetParent(parent.transform, false);
            var dotImg = dot.AddComponent<Image>();
            dotImg.color = colors[i];

            // 发光层
            var glow = new GameObject(name + "_Glow" + i, typeof(RectTransform));
            glow.transform.SetParent(dot.transform, false);
            var glowImg = glow.AddComponent<Image>();
            glowImg.color = new Color(colors[i].r, colors[i].g, colors[i].b, 0.3f);
            var glowR = glow.GetComponent<RectTransform>();
            glowR.anchorMin = glowR.anchorMax = new Vector2(0.5f, 0.5f);
            glowR.pivot = new Vector2(0.5f, 0.5f);
            glowR.sizeDelta = new Vector2(dotSize + 16, dotSize + 16);

            var dotR = dot.GetComponent<RectTransform>();
            dotR.anchorMin = dotR.anchorMax = new Vector2(1, 1);
            dotR.pivot = new Vector2(1, 1);
            dotR.anchoredPosition = new Vector2(-i * spacing, pos.y);
            dotR.sizeDelta = new Vector2(dotSize, dotSize);
        }
    }

    void AddTitleBar(GameObject parent, string name, Vector2 size)
    {
        // ── SpaceX: 标题底部的白线 ──
        float barH = 1f;
        var bar = new GameObject(name, typeof(RectTransform));
        bar.transform.SetParent(parent.transform, false);
        var barImg = bar.AddComponent<Image>();
        barImg.color = new Color(1f, 1f, 1f, 0.2f);
        var barR = bar.GetComponent<RectTransform>();
        barR.anchorMin = new Vector2(0, 1); barR.anchorMax = new Vector2(1, 1);
        barR.pivot = new Vector2(0, 1); barR.anchoredPosition = new Vector2(0, -1);
        barR.sizeDelta = new Vector2(0, -barH);

        // ── SpaceX: 小白点 ──
        float dotSize = 5f;
        float[] dotPositions = { 12f, 24f, 36f };
        Color[] dotColors = { COL_ACCENT, COL_ACCENT_DIM, new Color(0.4f, 0.4f, 0.4f, 0.4f) };
        for (int i = 0; i < dotPositions.Length; i++)
        {
            var dot = new GameObject(name + "_Dot" + i, typeof(RectTransform));
            dot.transform.SetParent(parent.transform, false);
            var dotImg = dot.AddComponent<Image>();
            dotImg.color = dotColors[i];
            var dotR = dot.GetComponent<RectTransform>();
            dotR.anchorMin = dotR.anchorMax = new Vector2(0, 1);
            dotR.pivot = new Vector2(0, 1);
            dotR.anchoredPosition = new Vector2(dotPositions[i], -4f);
            dotR.sizeDelta = new Vector2(dotSize, dotSize);
        }
    }

    // ══════════════════════════════════════════════════
    //  SpaceX 圆角边框纹理生成 (SDF 抗锯齿)
    // ══════════════════════════════════════════════════

    static Texture2D GenerateRoundedBorderTexture(int w, int h, int radius, Color color, float borderWidth)
    {
        var key = new Vector2Int(w, h);
        if (_roundedBorderCache.TryGetValue(key, out var cached))
            return cached;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float cx = w * 0.5f;
        float cy = h * 0.5f;
        float hw = cx;  // half width
        float hh = cy;  // half height
        float innerR = Mathf.Max(0, radius - borderWidth);

        var pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                // ── SDF to outer rounded rect ──
                float odx = Mathf.Abs(px - cx) - hw + radius;
                float ody = Mathf.Abs(py - cy) - hh + radius;
                float outerDist = Mathf.Min(Mathf.Max(odx, ody), 0f)
                    + Mathf.Sqrt(Mathf.Max(odx, 0f) * Mathf.Max(odx, 0f) + Mathf.Max(ody, 0f) * Mathf.Max(ody, 0f))
                    - radius;

                // ── SDF to inner rounded rect ──
                float iw = hw - borderWidth;
                float ih = hh - borderWidth;
                float idx = Mathf.Abs(px - cx) - iw + innerR;
                float idy = Mathf.Abs(py - cy) - ih + innerR;
                float innerDist = Mathf.Min(Mathf.Max(idx, idy), 0f)
                    + Mathf.Sqrt(Mathf.Max(idx, 0f) * Mathf.Max(idx, 0f) + Mathf.Max(idy, 0f) * Mathf.Max(idy, 0f))
                    - innerR;

                // ── Alpha: 1 in border, 0 outside outer or inside inner ──
                // Transition centered at SDF=0 with BORDER_AA_WIDTH pixel ramp
                float alphaOuter = Mathf.Clamp01(0.5f - outerDist / BORDER_AA_WIDTH);
                float alphaInner = Mathf.Clamp01(0.5f + innerDist / BORDER_AA_WIDTH);
                float alpha = alphaOuter * alphaInner;

                pixels[y * w + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        _roundedBorderCache[key] = tex;
        return tex;
    }

    static Texture2D GenerateRoundedFillTexture(int w, int h, int radius)
    {
        var key = new Vector2Int(w, h);
        if (_roundedFillCache.TryGetValue(key, out var cached))
            return cached;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float cx = w * 0.5f;
        float cy = h * 0.5f;
        float hw = cx;
        float hh = cy;

        var pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                // SDF to outer rounded rect (same as border but filled — no inner cutout)
                float dx = Mathf.Abs(px - cx) - hw + radius;
                float dy = Mathf.Abs(py - cy) - hh + radius;
                float dist = Mathf.Min(Mathf.Max(dx, dy), 0f)
                    + Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f))
                    - radius;

                float alpha = Mathf.Clamp01(0.5f - dist / BORDER_AA_WIDTH);
                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        _roundedFillCache[key] = tex;
        return tex;
    }

    // ─ 子面板 (用于姿态/悬挂并排) ──
    GameObject CreateSubPanel(GameObject parent, string name, Vector2 pos, Vector2 size)
    {
        var p = new GameObject(name, typeof(RectTransform));
        p.transform.SetParent(parent.transform, false);

        // ── SpaceX: 纯黑背景 ──
        var bg = p.AddComponent<Image>();
        bg.color = Color.black;

        // ── SpaceX: 细边框 ──
        var border = new GameObject(name + "_Border", typeof(RectTransform));
        border.transform.SetParent(p.transform, false);
        var bImg = border.AddComponent<Image>();
        bImg.color = new Color(1f, 1f, 1f, 0.25f);
        var bR = border.GetComponent<RectTransform>();
        bR.anchorMin = Vector2.zero; bR.anchorMax = Vector2.one;
        bR.offsetMin = Vector2.zero; bR.offsetMax = Vector2.zero;

        var rt = p.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return p;
    }

    // ── 渐变填充条 (车轮悬挂) ──
    Image AddGradientBar(GameObject parent, string name, Vector2 pos, Vector2 size, Color topColor, Color bottomColor)
    {
        var bg = new GameObject(name + "_BG", typeof(RectTransform));
        bg.transform.SetParent(parent.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        var bgR = bg.GetComponent<RectTransform>();
        bgR.anchorMin = bgR.anchorMax = new Vector2(0, 1); bgR.pivot = new Vector2(0, 1);
        bgR.anchoredPosition = pos; bgR.sizeDelta = size;

        var o = new GameObject(name, typeof(RectTransform));
        o.transform.SetParent(parent.transform, false);
        var gradTex = new Texture2D(1, (int)size.y, TextureFormat.RGBA32, false);
        for (int y = 0; y < (int)size.y; y++)
        {
            float t = (float)y / size.y;
            gradTex.SetPixel(0, y, Color.Lerp(bottomColor, topColor, t));
        }
        gradTex.Apply();
        gradTex.filterMode = FilterMode.Bilinear;
        gradTex.wrapMode = TextureWrapMode.Clamp;

        var sprite = Sprite.Create(gradTex, new Rect(0, 0, gradTex.width, gradTex.height), Vector2.zero);
        var img = o.AddComponent<Image>();
        img.sprite = sprite;
        img.color = new Color(1, 1, 1, 0.85f);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Vertical;
        img.fillOrigin = (int)Image.OriginVertical.Bottom;
        img.fillAmount = 0.7f;

        var r = o.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = pos; r.sizeDelta = size;

        var glow = new GameObject(name + "_Glow", typeof(RectTransform));
        glow.transform.SetParent(parent.transform, false);
        var glowImg = glow.AddComponent<Image>();
        glowImg.color = new Color(1f, 1f, 1f, 0.15f);
        var glowR = glow.GetComponent<RectTransform>();
        glowR.anchorMin = glowR.anchorMax = new Vector2(0, 1); glowR.pivot = new Vector2(0, 1);
        glowR.anchoredPosition = pos;
        glowR.sizeDelta = new Vector2(size.x + 2, size.y + 2);

        return img;
    }

    // ══════════════════════════════════════════════════
    //  TMP 文本
    // ══════════════════════════════════════════════════

    Text AddText(GameObject parent, string name, Vector2 pos, Vector2 size,
        string text, float fontSize, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var o = new GameObject(name, typeof(RectTransform));
        o.transform.SetParent(parent.transform, false);
        var t = o.AddComponent<Text>();
        t.text = text; t.fontSize = Mathf.RoundToInt(fontSize * 0.8f); t.color = color;
        t.alignment = align;
        t.font = GetCJKFont();
        var r = o.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return t;
    }

    Text AddTextRight(GameObject parent, string name, Vector2 pos, Vector2 size,
        string text, float fontSize, Color color, TextAnchor align = TextAnchor.MiddleRight)
    {
        var o = new GameObject(name, typeof(RectTransform));
        o.transform.SetParent(parent.transform, false);
        var t = o.AddComponent<Text>();
        t.text = text; t.fontSize = Mathf.RoundToInt(fontSize * 0.8f); t.color = color;
        t.alignment = align;
        t.font = GetCJKFont();
        var r = o.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(1, 1);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return t;
    }

    Button AddKenneyButton(GameObject parent, string name, Vector2 pos, Vector2 size, string text, Sprite sprite = null)
    {
        var o = new GameObject(name, typeof(RectTransform));
        o.transform.SetParent(parent.transform, false);

        // ═══ SpaceX: Ghost pill button (white outline, transparent fill) ═══
        // Border: 4 thin lines around the button
        float t = 1f;
        var borderColor = new Color(1f, 1f, 1f, 0.5f);

        var top = new GameObject(name + "_T", typeof(RectTransform));
        top.transform.SetParent(o.transform, false);
        top.AddComponent<Image>().color = borderColor;
        var tr2 = top.GetComponent<RectTransform>();
        tr2.anchorMin = new Vector2(0, 1); tr2.anchorMax = new Vector2(1, 1);
        tr2.pivot = new Vector2(0, 1); tr2.anchoredPosition = Vector2.zero;
        tr2.sizeDelta = new Vector2(0, -t);

        var bot = new GameObject(name + "_B", typeof(RectTransform));
        bot.transform.SetParent(o.transform, false);
        bot.AddComponent<Image>().color = borderColor;
        var br2 = bot.GetComponent<RectTransform>();
        br2.anchorMin = new Vector2(0, 0); br2.anchorMax = new Vector2(1, 0);
        br2.pivot = new Vector2(0, 0); br2.anchoredPosition = Vector2.zero;
        br2.sizeDelta = new Vector2(0, t);

        var left = new GameObject(name + "_L", typeof(RectTransform));
        left.transform.SetParent(o.transform, false);
        left.AddComponent<Image>().color = borderColor;
        var lr2 = left.GetComponent<RectTransform>();
        lr2.anchorMin = new Vector2(0, 0); lr2.anchorMax = new Vector2(0, 1);
        lr2.pivot = new Vector2(0, 0); lr2.anchoredPosition = Vector2.zero;
        lr2.sizeDelta = new Vector2(t, 0);

        var right = new GameObject(name + "_R", typeof(RectTransform));
        right.transform.SetParent(o.transform, false);
        right.AddComponent<Image>().color = borderColor;
        var rr2 = right.GetComponent<RectTransform>();
        rr2.anchorMin = new Vector2(1, 0); rr2.anchorMax = new Vector2(1, 1);
        rr2.pivot = new Vector2(1, 0); rr2.anchoredPosition = Vector2.zero;
        rr2.sizeDelta = new Vector2(-t, 0);

        // Invisible click target
        var btnImg = o.AddComponent<Image>();
        btnImg.color = new Color(1f, 1f, 1f, 0.01f);

        var btn = o.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        // White text
        var t2 = AddText(o, "Text", Vector2.zero, size, text, 38, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
        var tt = t2.GetComponent<RectTransform>();
        tt.anchorMin = Vector2.zero; tt.anchorMax = Vector2.one;
        tt.offsetMin = tt.offsetMax = Vector2.zero;

        var r = o.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return btn;
    }

    Image AddFilledBar(GameObject parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        var bg = new GameObject(name + "_BG", typeof(RectTransform));
        bg.transform.SetParent(parent.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        var bgR = bg.GetComponent<RectTransform>();
        bgR.anchorMin = bgR.anchorMax = new Vector2(0, 1); bgR.pivot = new Vector2(0, 1);
        bgR.anchoredPosition = pos; bgR.sizeDelta = size;

        var o = new GameObject(name, typeof(RectTransform));
        o.transform.SetParent(parent.transform, false);
        var img = o.AddComponent<Image>();
        img.color = color; img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Vertical;
        img.fillOrigin = (int)Image.OriginVertical.Bottom; img.fillAmount = 0.7f;
        var r = o.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return img;
    }

    RawImage AddRawImage(GameObject parent, string name, Vector2 pos, Vector2 size)
    {
        var o = new GameObject(name, typeof(RectTransform));
        o.transform.SetParent(parent.transform, false);
        var ri = o.AddComponent<RawImage>();
        var r = o.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return ri;
    }

    // ══════════════════════════════════════════════════
    //  查找
    // ══════════════════════════════════════════════════

    GameObject FindCar()
    {
        var c = FindObjectOfType<CarController>();
        return c != null ? c.gameObject : null;
    }

    // ══════════════════════════════════════════════════
    //  面板生成
    // ══════════════════════════════════════════════════

    void GenerateModePanel(GameObject canvas, GameObject car)
    {
        var p = CreateSciFiPanel(canvas, "ModePanel", MODE_PANEL_POS, MODE_PANEL_SIZE, true);
        AddText(p, "Label", new Vector2(37, -44), new Vector2(150, 68), "⚙ 驾驶模式", 48, COL_TITLE);
        var btnM = AddKenneyButton(p, "BtnManual", new Vector2(200, -44), new Vector2(126, 110), "手动+AEB");
        var btnA = AddKenneyButton(p, "BtnLocalAI", new Vector2(336, -44), new Vector2(126, 110), "局部AI");
        var btnH = AddKenneyButton(p, "BtnHybridAI", new Vector2(472, -44), new Vector2(126, 110), "混合AI");
        var mgr = car.GetOrAddComponent<DriveModeManager>();
        var modeT = AddText(p, "ModeText", new Vector2(15, -158), new Vector2(610, 38),
            "当前模式: 手动+AEB", 30, COL_TEXT_DIM, TextAnchor.MiddleCenter);
        mgr.modeText = modeT; mgr.btnManual = btnM; mgr.btnLocalAI = btnA; mgr.btnHybridAI = btnH;
        mgr.bgManual = btnM.GetComponent<Image>(); mgr.bgLocalAI = btnA.GetComponent<Image>(); mgr.bgHybridAI = btnH.GetComponent<Image>();
    }

    void GenerateStatusPanel(GameObject canvas, GameObject car)
    {
        var p = CreateSciFiPanel(canvas, "StatusPanel", STATUS_PANEL_POS, STATUS_PANEL_SIZE, true);
        AddText(p, "Title", new Vector2(30, -36), new Vector2(580, 56), "■ 月球车状态", 40, COL_TITLE);
        AddSeparator(p, new Vector2(30, -76), 580);
        var sp = AddText(p, "Speed", new Vector2(20, -124), new Vector2(295, 72), " 车速: 0.0 km/h", 56, COL_VALUE);
        var dp = AddText(p, "Distance", new Vector2(325, -124), new Vector2(295, 72), " 里程: 0.0 m", 56, COL_VALUE);
        var fp = AddText(p, "Fv", new Vector2(20, -204), new Vector2(295, 56), "〰 颠簸(Fv): 0.00", 44, COL_VALUE);
        var st = AddText(p, "Steer", new Vector2(325, -204), new Vector2(295, 56), "↺ 转向: 0°", 44, COL_VALUE);
        var io = new GameObject("Move_info"); io.transform.SetParent(canvas.transform, false);
        var info = io.AddComponent<Move_info>();
        info.speedText = sp; info.distanceText = dp; info.fvText = fp; info.steerText = st;
        info.carRigidbody = car.GetComponent<Rigidbody>();
        info.terrainRadar = FindObjectOfType<Scan>();
    }

    void GenerateTelemetryPanel(GameObject canvas, GameObject car)
    {
        var p = CreateSciFiPanel(canvas, "TelemetryPanel", TELEMETRY_PANEL_POS, TELEMETRY_PANEL_SIZE, true);
        AddText(p, "Title", new Vector2(30, -36), new Vector2(580, 56), "■ 姿态 & 悬挂 & 滑转", 40, COL_TITLE);
        AddSeparator(p, new Vector2(30, -76), 580);

        // ── 迷你姿态指示器 (替换旧人工地平线) ──
        var attPanel = CreateSubPanel(p, "AttitudePanel", new Vector2(21, -210), new Vector2(295, 400));
        AddText(attPanel, "AttTitle", new Vector2(11, -24), new Vector2(274, 36), "姿态指示器", 24, COL_TEXT_DIM, TextAnchor.MiddleCenter);

        var attImg = AddRawImage(attPanel, "AttitudeImage", new Vector2(0, -24), new Vector2(256, 256));

        // 俯仰/侧倾文本 (由 MiniAttitudeIndicator 更新)
        var pt = AddText(attPanel, "Pitch", new Vector2(11, -300), new Vector2(127, 36), "俯仰: 0.0°", 24, COL_VALUE, TextAnchor.MiddleLeft);
        var rt2 = AddText(attPanel, "Roll", new Vector2(148, -300), new Vector2(127, 36), "侧倾: 0.0°", 24, COL_VALUE, TextAnchor.MiddleLeft);

        // ── 悬挂行程 (保留不变) ──
        var suspPanel = CreateSubPanel(p, "SuspensionPanel", new Vector2(338, -210), new Vector2(295, 240));
        AddText(suspPanel, "SuspTitle", new Vector2(11, -24), new Vector2(274, 36), "悬挂行程", 24, COL_TEXT_DIM, TextAnchor.MiddleCenter);
        AddText(suspPanel, "CarFront", new Vector2(148, -60), new Vector2(127, 24), "▲ 车头方向", 18, COL_ACCENT_DIM, TextAnchor.MiddleCenter);

        int cx = 148, cy = -100;
        int gapX = 58, gapY = 90;
        int barW = 32, barH = 60;

        var fl = AddGradientBar(suspPanel, "SuspFL", new Vector2(cx - gapX, cy), new Vector2(barW, barH), new Color(0.2f, 1f, 0.4f), new Color(0f, 0.6f, 0.2f));
        AddText(suspPanel, "LFL", new Vector2(cx - gapX, cy - barH - 22), new Vector2(barW, 22), "FL", 18, COL_TEXT, TextAnchor.MiddleCenter);
        var fr = AddGradientBar(suspPanel, "SuspFR", new Vector2(cx + gapX, cy), new Vector2(barW, barH), new Color(0.2f, 1f, 0.4f), new Color(0f, 0.6f, 0.2f));
        AddText(suspPanel, "LFR", new Vector2(cx + gapX, cy - barH - 22), new Vector2(barW, 22), "FR", 18, COL_TEXT, TextAnchor.MiddleCenter);
        var rl = AddGradientBar(suspPanel, "SuspRL", new Vector2(cx - gapX, cy - gapY), new Vector2(barW, barH), new Color(0.2f, 1f, 0.4f), new Color(0f, 0.6f, 0.2f));
        AddText(suspPanel, "LRL", new Vector2(cx - gapX, cy - gapY - barH - 22), new Vector2(barW, 22), "RL", 18, COL_TEXT, TextAnchor.MiddleCenter);
        var rr = AddGradientBar(suspPanel, "SuspRR", new Vector2(cx + gapX, cy - gapY), new Vector2(barW, barH), new Color(0.2f, 1f, 0.4f), new Color(0f, 0.6f, 0.2f));
        AddText(suspPanel, "LRR", new Vector2(cx + gapX, cy - gapY - barH - 22), new Vector2(barW, 22), "RR", 18, COL_TEXT, TextAnchor.MiddleCenter);
        AddText(suspPanel, "Legend", new Vector2(211, -100), new Vector2(74, 130), "满\n↑\n空", 16, COL_TEXT_DIM);

        // ── 车轮滑转率 (右下方) ──
        var slipPanel = CreateSubPanel(p, "SlipPanel", new Vector2(338, -470), new Vector2(295, 140));
        AddText(slipPanel, "SlipTitle", new Vector2(11, -22), new Vector2(274, 28), "滑转率", 22, COL_TEXT_DIM, TextAnchor.MiddleCenter);

        // 4 轮滑动条 (窄填充条)
        int slipBarY = -60;
        int slipGap = 62;
        int slipBarW = 22, slipBarH = 50;
        var slipBars = new Image[4];
        var slipTexts = new Text[4];
        int[] slipOffsets = { -slipGap, slipGap, 3*slipGap, 5*slipGap };

        for (int i = 0; i < 4; i++)
        {
            // 背景
            var sbg = new GameObject("SlipBG" + i, typeof(RectTransform));
            sbg.transform.SetParent(slipPanel.transform, false);
            var sbgI = sbg.AddComponent<Image>();
            sbgI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            var sbgR = sbg.GetComponent<RectTransform>();
            sbgR.anchorMin = sbgR.anchorMax = new Vector2(0.5f, 0.5f);
            sbgR.pivot = new Vector2(0.5f, 0);
            sbgR.anchoredPosition = new Vector2(slipOffsets[i], -60);
            sbgR.sizeDelta = new Vector2(slipBarW, slipBarH);

            // 填充条
            var bar = new GameObject("SlipBar" + i, typeof(RectTransform));
            bar.transform.SetParent(slipPanel.transform, false);
            var barI = bar.AddComponent<Image>();
            barI.color = new Color(0.3f, 1f, 0.3f);
            barI.type = Image.Type.Filled;
            barI.fillMethod = Image.FillMethod.Vertical;
            barI.fillOrigin = (int)Image.OriginVertical.Bottom;
            barI.fillAmount = 0f;
            var barR = bar.GetComponent<RectTransform>();
            barR.anchorMin = barR.anchorMax = new Vector2(0.5f, 0.5f);
            barR.pivot = new Vector2(0.5f, 0);
            barR.anchoredPosition = new Vector2(slipOffsets[i], -60);
            barR.sizeDelta = new Vector2(slipBarW - 2, slipBarH - 2);
            slipBars[i] = barI;

            // 标签
            string[] slipNames = { "FL", "FR", "RL", "RR" };
            var label = AddText(slipPanel, "SlipLabel" + i,
                new Vector2(slipOffsets[i] - 20, -116), new Vector2(40, 18),
                slipNames[i], 16, COL_TEXT_DIM, TextAnchor.MiddleCenter);
            slipTexts[i] = label;
        }

        // ── 绑定组件 ──
        var dash = car.GetOrAddComponent<TelemetryDashboard>();
        dash.suspFLImg = fl; dash.suspFRImg = fr;
        dash.suspRLImg = rl; dash.suspRRImg = rr;
        dash.suspFLMax = dash.suspFRMax = dash.suspRLMax = dash.suspRRMax = 100f;

        var att = car.GetComponent<MiniAttitudeIndicator>();
        if (att != null) { att.attitudeImage = attImg; att.pitchText = pt; att.rollText = rt2; }

        var slip = car.GetComponent<WheelSlipIndicator>();
        if (slip != null) { slip.slipBars = slipBars; slip.slipTexts = slipTexts; }

        // ── 地形剖面 (面板底部) ──
        var terSub = CreateSubPanel(p, "TerrainProfileSub", new Vector2(21, -640), new Vector2(595, 140));
        AddText(terSub, "TerTitle", new Vector2(7, -12), new Vector2(140, 24), "地形剖面", 20, COL_TEXT_DIM, TextAnchor.MiddleLeft);
        var terImg = AddRawImage(terSub, "TerrainImage", new Vector2(0, -16), new Vector2(496, 96));
        var terLabel = AddText(terSub, "TerrainLabel", new Vector2(7, -118), new Vector2(580, 18),
            "最大坡度: —  前方 —m", 16, COL_TEXT_DIM, TextAnchor.MiddleLeft);
        var tp = car.GetComponent<TerrainProfile>();
        if (tp != null) { tp.profileImage = terImg; tp.labelText = terLabel; }
    }

    void GenerateSystemStatusPanel(GameObject canvas, GameObject car)
    {
        var p = CreateSciFiPanel(canvas, "SystemStatusPanel", SYSSTATUS_PANEL_POS, SYSSTATUS_PANEL_SIZE, false);
        AddTextRight(p, "Title", new Vector2(-37, -36), new Vector2(500, 48), "■ 外设状态", 36, COL_TITLE);
        AddSeparatorRight(p, new Vector2(-37, -72), 500);
        var g29 = AddTextRight(p, "G29", new Vector2(-21, -120), new Vector2(535, 48), "● G29: 未连接 (键盘)", 36, COL_WARN);
        var plat = AddTextRight(p, "Platform", new Vector2(-21, -176), new Vector2(535, 48), "● 动感平台: 未连接", 36, COL_WARN);
        var pp = AddTextRight(p, "PPitch", new Vector2(-21, -232), new Vector2(260, 44), "平台俯仰: 0.0°", 30, COL_TEXT_DIM);
        var pr = AddTextRight(p, "PRoll", new Vector2(-290, -232), new Vector2(260, 44), "平台侧倾: 0.0°", 30, COL_TEXT_DIM);
        var fps = AddTextRight(p, "MotionFPS", new Vector2(-21, -284), new Vector2(535, 44), "底座 50Hz: —", 30, COL_TEXT_DIM);

        // ── 通信链路 ──
        AddSeparatorRight(p, new Vector2(-37, -330), 500);
        var comms = AddTextRight(p, "CommsText", new Vector2(-21, -352), new Vector2(535, 40),
            "📡 [█████]  20ms  10.0 Mbps", 30, COL_VALUE);

        // ── 绑定 ──
        var manual = FindObjectOfType<ManualDriverWithAEB>();
        if (manual != null) manual.g29StatusText = g29;
        var mp = FindObjectOfType<MotionPlatformController>();
        if (mp != null) { mp.statusText = plat; mp.pitchText = pp; mp.rollText = pr; mp.motionFPSText = fps; }

        var hud = car.GetComponent<HUDDashboard>();
        if (hud != null) hud.commsText = comms;
    }

    void GenerateMapPanel(GameObject canvas, GameObject car)
    {
        var p = CreateSciFiPanel(canvas, "MapPanel", MAP_PANEL_POS, MAP_PANEL_SIZE, false);
        AddTextRight(p, "Title", new Vector2(-37, -36), new Vector2(500, 56), "■ 探索地图 / 导航", 36, COL_TITLE);
        AddSeparatorRight(p, new Vector2(-37, -72), 500);

        // ── 地图 (缩小高度以容纳下方导航信息) ──
        var mi = AddRawImage(p, "MapImage", new Vector2(0, -48), new Vector2(540, 400));
        var mir = mi.GetComponent<RectTransform>();
        mir.anchorMin = new Vector2(0.5f, 0.5f); mir.anchorMax = new Vector2(0.5f, 0.5f);
        mir.pivot = new Vector2(0.5f, 0.5f);

        var arrow = new GameObject("MapArrow", typeof(RectTransform));
        arrow.transform.SetParent(mi.transform, false);
        var ai = arrow.AddComponent<Image>(); ai.color = Color.red;
        var ar = arrow.GetComponent<RectTransform>();
        ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f); ar.pivot = new Vector2(0.5f, 0.5f);
        ar.sizeDelta = new Vector2(10, 10);

        // ── GPS / 航点信息栏 (地图下方) ──
        var infoPanel = CreateSubPanel(p, "NavInfoPanel", new Vector2(-272, -472), new Vector2(540, 96));
        var gpsT = AddText(infoPanel, "GPSText", new Vector2(7, -12), new Vector2(270, 32),
            "GPS 12颗  25.00000°N  121.00000°E", 24, COL_VALUE, TextAnchor.MiddleLeft);
        var wpT = AddText(infoPanel, "WaypointText", new Vector2(277, -12), new Vector2(260, 32),
            "航点:  —", 24, COL_VALUE, TextAnchor.MiddleLeft);
        var gpsDetail = AddText(infoPanel, "GPSDetail", new Vector2(7, -55), new Vector2(270, 28),
            "定位精度 ±0.5m  高程 0m", 18, COL_TEXT_DIM, TextAnchor.MiddleLeft);
        var wpDetail = AddText(infoPanel, "WaypointDetail", new Vector2(277, -55), new Vector2(260, 28),
            "目标方向: —", 18, COL_TEXT_DIM, TextAnchor.MiddleLeft);

        // ── 绑定 ──
        var mo = new GameObject("ExplorationMap"); mo.transform.SetParent(canvas.transform, false);
        var map = mo.AddComponent<ExplorationMap>();
        map.mapImage = mi; map.arrowTransform = ar; map.rover = car.transform;
        map.scanner = FindObjectOfType<Scan>() ?? new GameObject("Scan_Radar").AddComponent<Scan>();

        var hud = car.GetComponent<HUDDashboard>();
        if (hud != null) { hud.gpsText = gpsT; hud.waypointText = wpT; }
    }

    void GenerateCameraPanel(GameObject canvas, GameObject car)
    {
        var p = CreateSciFiPanel(canvas, "CamPanel", CAM_PANEL_POS, CAM_PANEL_SIZE, false);
        AddTextRight(p, "Title", new Vector2(-37, -36), new Vector2(500, 56), "■ 底盘避障 (HazCam)", 36, COL_TITLE);
        AddSeparatorRight(p, new Vector2(-37, -72), 500);

        // ── SpaceX: 摄像头装饰框 (填满面板) ──
        var camFrame = new GameObject("CamFrame", typeof(RectTransform));
        camFrame.transform.SetParent(p.transform, false);
        var frameImg = camFrame.AddComponent<Image>();
        frameImg.color = new Color(1f, 1f, 1f, 0.05f);
        var frameR = camFrame.GetComponent<RectTransform>();
        frameR.anchorMin = new Vector2(0.5f, 0.5f); frameR.anchorMax = new Vector2(0.5f, 0.5f);
        frameR.pivot = new Vector2(0.5f, 0.5f);
        frameR.anchoredPosition = new Vector2(0, -22);
        frameR.sizeDelta = new Vector2(548, 432);

        var ci = AddRawImage(p, "CamImage", new Vector2(0, -42), new Vector2(538, 418));
        var cir = ci.GetComponent<RectTransform>();
        cir.anchorMin = new Vector2(0.5f, 0.5f); cir.anchorMax = new Vector2(0.5f, 0.5f);
        cir.pivot = new Vector2(0.5f, 0.5f);
        cir.anchoredPosition = new Vector2(0, -22);

        // ── SpaceX: 摄像头十字线 + 角标 ──
        var camCrossH = new GameObject("CamCrossH", typeof(RectTransform));
        camCrossH.transform.SetParent(p.transform, false);
        var camChI = camCrossH.AddComponent<Image>(); camChI.color = new Color(1f, 1f, 1f, 0.15f);
        var camChR = camCrossH.GetComponent<RectTransform>();
        camChR.anchorMin = new Vector2(0.5f, 0.5f); camChR.anchorMax = new Vector2(0.5f, 0.5f);
        camChR.pivot = new Vector2(0.5f, 0.5f);
        camChR.anchoredPosition = new Vector2(0, -22);
        camChR.sizeDelta = new Vector2(538, 1);

        var camCrossV = new GameObject("CamCrossV", typeof(RectTransform));
        camCrossV.transform.SetParent(p.transform, false);
        var camCvI = camCrossV.AddComponent<Image>(); camCvI.color = new Color(1f, 1f, 1f, 0.15f);
        var camCvR = camCrossV.GetComponent<RectTransform>();
        camCvR.anchorMin = new Vector2(0.5f, 0.5f); camCvR.anchorMax = new Vector2(0.5f, 0.5f);
        camCvR.pivot = new Vector2(0.5f, 0.5f);
        camCvR.anchoredPosition = new Vector2(0, -22);
        camCvR.sizeDelta = new Vector2(2, 418);

        // 四角标记
        float camHalf = 538f / 2f;
        float camHeightHalf = 418f / 2f;
        AddCameraCornerMark(p, "CamTL", -(camHalf - 30), camHeightHalf - 30, true, true);
        AddCameraCornerMark(p, "CamTR", camHalf - 30, camHeightHalf - 30, false, true);
        AddCameraCornerMark(p, "CamBL", -(camHalf - 30), -(camHeightHalf - 30), true, false);
        AddCameraCornerMark(p, "CamBR", camHalf - 30, -(camHeightHalf - 30), false, false);

        var camBorder = new GameObject("CamBorder", typeof(RectTransform));
        camBorder.transform.SetParent(p.transform, false);
        var cbI = camBorder.AddComponent<Image>();
        cbI.color = new Color(1f, 1f, 1f, 0.15f);
        var cbR = camBorder.GetComponent<RectTransform>();
        cbR.anchorMin = new Vector2(0.5f, 0.5f); cbR.anchorMax = new Vector2(0.5f, 0.5f);
        cbR.pivot = new Vector2(0.5f, 0.5f);
        cbR.anchoredPosition = new Vector2(0, -22);
        cbR.sizeDelta = new Vector2(544, 426);

        car.GetOrAddComponent<ChassisCamera>().camImage = ci;
    }

    // ── SpaceX: 摄像头角标 ──
    void AddCameraCornerMark(GameObject parent, string name, float x, float y, bool left, bool top)
    {
        float len = 24, t = 2;
        var h = new GameObject(name + "_H", typeof(RectTransform));
        h.transform.SetParent(parent.transform, false);
        var hI = h.AddComponent<Image>(); hI.color = new Color(1f, 1f, 1f, 0.25f);
        var hR = h.GetComponent<RectTransform>();
        hR.anchorMin = hR.anchorMax = new Vector2(0.5f, 0.5f);
        hR.pivot = new Vector2(left ? 0 : 1, top ? 1 : 0);
        hR.anchoredPosition = new Vector2(x + (left ? 0 : -len), y);
        hR.sizeDelta = new Vector2(len, t);

        var v = new GameObject(name + "_V", typeof(RectTransform));
        v.transform.SetParent(parent.transform, false);
        var vI = v.AddComponent<Image>(); vI.color = new Color(1f, 1f, 1f, 0.25f);
        var vR = v.GetComponent<RectTransform>();
        vR.anchorMin = vR.anchorMax = new Vector2(0.5f, 0.5f);
        vR.pivot = new Vector2(left ? 0 : 1, top ? 1 : 0);
        vR.anchoredPosition = new Vector2(x, y + (top ? 0 : -len));
        vR.sizeDelta = new Vector2(t, len);
    }

    void GenerateCompassBar(GameObject canvas, GameObject car)
    {
        var bar = new GameObject("CompassBar", typeof(RectTransform));
        bar.transform.SetParent(canvas.transform, false);
        var bg = bar.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.05f, 0.8f);

        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -MARGIN);
        rt.sizeDelta = new Vector2(COMPASS_BAR_W, COMPASS_BAR_H);

        // 边框 (圆角)
        var borderTex = GenerateRoundedBorderTexture(COMPASS_BAR_W, COMPASS_BAR_H, ROUND_RADIUS, COL_BORDER, 1f);
        var borderGo = new GameObject("RoundedBorder", typeof(RectTransform));
        borderGo.transform.SetParent(bar.transform, false);
        var borderImg = borderGo.AddComponent<RawImage>();
        borderImg.texture = borderTex;
        borderImg.color = Color.white;
        var borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero; borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = Vector2.zero; borderRt.offsetMax = Vector2.zero;

        // 航向标签
        var headingLabel = AddText(bar, "HeadingLabel", new Vector2(20, -16), new Vector2(120, 40),
            "罗盘", 22, COL_TEXT_DIM, TextAnchor.MiddleLeft);

        // 航向值 (由 HUDDashboard 更新)
        var headingVal = AddText(bar, "HeadingValue", new Vector2(140, -16), new Vector2(180, 40),
            "N 0°", 30, COL_ACCENT, TextAnchor.MiddleLeft);

        // N/W/S/E 固定刻度
        float barW = COMPASS_BAR_W;
        float[] degPos = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        string[] degLabel = { "W", "N", "E", "S", "W" };
        for (int i = 0; i < degPos.Length; i++)
        {
            float x = MARGIN + (barW - 2 * MARGIN) * degPos[i];
            var dot = new GameObject("Cardinal_" + degLabel[i], typeof(RectTransform));
            dot.transform.SetParent(bar.transform, false);
            var dotImg = dot.AddComponent<Image>();
            dotImg.color = new Color(1f, 1f, 1f, 0.4f);
            var dotR = dot.GetComponent<RectTransform>();
            dotR.anchorMin = dotR.anchorMax = new Vector2(0, 0.5f);
            dotR.pivot = new Vector2(0.5f, 0.5f);
            dotR.anchoredPosition = new Vector2(x, 4);
            dotR.sizeDelta = new Vector2(2, 16);

            AddText(bar, degLabel[i] + "_Label", new Vector2(x - 20, -36), new Vector2(40, 20),
                degLabel[i], 18, new Color(1f, 1f, 1f, 0.25f), TextAnchor.MiddleCenter);
        }

        // 当前航向指示器 (白色三角)
        var pointer = new GameObject("HeadingPointer", typeof(RectTransform));
        pointer.transform.SetParent(bar.transform, false);
        var ptrImg = pointer.AddComponent<Image>();
        ptrImg.color = new Color(1f, 0.3f, 0.3f, 1f);
        var ptrR = pointer.GetComponent<RectTransform>();
        ptrR.anchorMin = ptrR.anchorMax = new Vector2(0.5f, 0.5f);
        ptrR.pivot = new Vector2(0.5f, 0.5f);
        ptrR.sizeDelta = new Vector2(4, 20);
        ptrR.anchoredPosition = new Vector2(0, 8);

        // 绑定到 HUDDashboard
        var hud = car.GetComponent<HUDDashboard>();
        if (hud != null)
        {
            hud.compassText = headingVal;
        }
    }

    void GenerateBatteryPanel(GameObject canvas, GameObject car)
    {
        var p = CreateSciFiPanel(canvas, "BatteryPanel", BATT_PANEL_POS, BATT_PANEL_SIZE, false);
        AddTextRight(p, "Title", new Vector2(-30, -28), new Vector2(520, 36), "■ 电源系统", 30, COL_TITLE);
        AddSeparatorRight(p, new Vector2(-30, -54), 520);

        var battText = AddTextRight(p, "BatteryValue", new Vector2(-30, -96), new Vector2(520, 40),
            "⚡ 100.0%  28.8V  0.0A", 30, COL_VALUE, TextAnchor.MiddleRight);
        var rangeText = AddTextRight(p, "RangeValue", new Vector2(-30, -136), new Vector2(520, 32),
            "续航估计: -- km", 24, COL_TEXT_DIM, TextAnchor.MiddleRight);

        var hud = car.GetComponent<HUDDashboard>();
        if (hud != null) hud.batteryText = battText;
    }

    void GenerateEventLogPanel(GameObject canvas, GameObject car)
    {
        var p = CreateSciFiPanel(canvas, "EventLogPanel", EVENTLOG_PANEL_POS, EVENTLOG_PANEL_SIZE, false);
        AddTextRight(p, "Title", new Vector2(-30, -28), new Vector2(520, 36), "■ 事件日志", 30, COL_TITLE);
        AddSeparatorRight(p, new Vector2(-30, -54), 520);

        var logText = AddTextRight(p, "LogContent", new Vector2(-30, -170), new Vector2(540, 120),
            "系统启动", 20, COL_TEXT_DIM, TextAnchor.MiddleRight);
        logText.alignment = TextAnchor.LowerRight;

        var hud = car.GetComponent<HUDDashboard>();
        if (hud != null) hud.eventLogText = logText;
    }

    void GenerateAlertOverlay(GameObject canvas, GameObject car)
    {
        var a = new GameObject("AEBAlertOverlay", typeof(RectTransform));
        a.transform.SetParent(canvas.transform, false);
        var ov = a.AddComponent<Image>();
        ov.color = new Color(0, 0, 0, 0); ov.raycastTarget = false;
        var ar = a.GetComponent<RectTransform>();
        ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one;
        ar.offsetMin = ar.offsetMax = Vector2.zero;

        // ── React Bits 风格: 多层警告边框 ──
        var outerWarn = new GameObject("OuterWarnBorder", typeof(RectTransform));
        outerWarn.transform.SetParent(a.transform, false);
        var owI = outerWarn.AddComponent<Image>();
        owI.color = new Color(1f, 0.1f, 0f, 0); owI.raycastTarget = false;
        var owR = outerWarn.GetComponent<RectTransform>();
        owR.anchorMin = Vector2.zero; owR.anchorMax = Vector2.one;
        owR.offsetMin = new Vector2(16, 16); owR.offsetMax = new Vector2(-16, -16);

        var at = AddText(a, "AlertText", Vector2.zero, new Vector2(3000, 600),
            "", 216, COL_ALERT, TextAnchor.MiddleCenter);
        var atr = at.GetComponent<RectTransform>();
        atr.anchorMin = atr.anchorMax = new Vector2(0.5f, 0.5f); atr.pivot = new Vector2(0.5f, 0.5f);
        atr.anchoredPosition = new Vector2(-528, 0);

        var br = new GameObject("WarnBorder", typeof(RectTransform));
        br.transform.SetParent(a.transform, false);
        var bi = br.AddComponent<Image>();
        bi.color = new Color(1f, 0.15f, 0f, 0); bi.raycastTarget = false;
        var brr = br.GetComponent<RectTransform>();
        brr.anchorMin = Vector2.zero; brr.anchorMax = Vector2.one;
        brr.offsetMin = new Vector2(30, 30); brr.offsetMax = new Vector2(-30, -30);
        AddCornerBracketAlert(br, "WTL", true, true);
        AddCornerBracketAlert(br, "WTR", false, true);
        AddCornerBracketAlert(br, "WBL", true, false);
        AddCornerBracketAlert(br, "WBR", false, false);

        var innerBorder = new GameObject("WarnInnerBorder", typeof(RectTransform));
        innerBorder.transform.SetParent(a.transform, false);
        var ibI = innerBorder.AddComponent<Image>();
        ibI.color = new Color(1f, 0.2f, 0f, 0); ibI.raycastTarget = false;
        var ibR = innerBorder.GetComponent<RectTransform>();
        ibR.anchorMin = Vector2.zero; ibR.anchorMax = Vector2.one;
        ibR.offsetMin = new Vector2(60, 60); ibR.offsetMax = new Vector2(-60, -60);

        // ── React Bits 风格: 警告面板四角能量点 ──
        AddWarningEnergyDots(a, "WarnEnergyDots");

        var manual = car.GetOrAddComponent<ManualDriverWithAEB>();
        manual.aebAlertText = at; manual.aebOverlayImage = ov;
    }

    // ── React Bits 风格: 警告能量点 ──
    void AddWarningEnergyDots(GameObject parent, string name)
    {
        float dotSize = 24f;
        Vector2[] positions = {
            new Vector2(40, -40),
            new Vector2(-40, -40),
            new Vector2(40, 40),
            new Vector2(-40, 40)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var dot = new GameObject(name + "_" + i, typeof(RectTransform));
            dot.transform.SetParent(parent.transform, false);
            var dotImg = dot.AddComponent<Image>();
            dotImg.color = COL_ALERT;

            // 发光层
            var glow = new GameObject(name + "_Glow" + i, typeof(RectTransform));
            glow.transform.SetParent(dot.transform, false);
            var glowImg = glow.AddComponent<Image>();
            glowImg.color = new Color(1f, 0.3f, 0f, 0.4f);
            var glowR = glow.GetComponent<RectTransform>();
            glowR.anchorMin = glowR.anchorMax = new Vector2(0.5f, 0.5f);
            glowR.pivot = new Vector2(0.5f, 0.5f);
            glowR.sizeDelta = new Vector2(dotSize + 32, dotSize + 32);

            var dotR = dot.GetComponent<RectTransform>();
            dotR.anchorMin = dotR.anchorMax = new Vector2(i < 2 ? 0 : 1, i % 2 == 0 ? 1 : 0);
            dotR.pivot = new Vector2(i < 2 ? 0 : 1, i % 2 == 0 ? 1 : 0);
            dotR.anchoredPosition = positions[i];
            dotR.sizeDelta = new Vector2(dotSize, dotSize);
        }
    }

    void AddCornerBracketAlert(GameObject parent, string name, bool left, bool top)
    {
        float len = 80, t = 12;
        var h = new GameObject(name + "_H", typeof(RectTransform));
        h.transform.SetParent(parent.transform, false);
        var hI = h.AddComponent<Image>(); hI.color = COL_ALERT;
        var hR = h.GetComponent<RectTransform>();
        hR.anchorMin = hR.anchorMax = new Vector2(left ? 0 : 1, top ? 1 : 0);
        hR.pivot = new Vector2(left ? 0 : 1, top ? 1 : 0);
        hR.anchoredPosition = Vector2.zero;
        hR.sizeDelta = new Vector2(left ? len : -len, t);

        var v = new GameObject(name + "_V", typeof(RectTransform));
        v.transform.SetParent(parent.transform, false);
        var vI = v.AddComponent<Image>(); vI.color = COL_ALERT;
        var vR = v.GetComponent<RectTransform>();
        vR.anchorMin = vR.anchorMax = new Vector2(left ? 0 : 1, top ? 1 : 0);
        vR.pivot = new Vector2(left ? 0 : 1, top ? 1 : 0);
        vR.anchoredPosition = Vector2.zero;
        vR.sizeDelta = new Vector2(t, top ? -len : len);
    }
}

internal static class ComponentExtensions
{
    public static T GetOrAddComponent<T>(this GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }
}

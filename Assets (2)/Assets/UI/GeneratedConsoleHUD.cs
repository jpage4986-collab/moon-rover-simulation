using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityStandardAssets.Vehicles.Car;
using MoonRover.Driver;
using MoonRover.FeedBack;
using MoonRover.Navigation;
using MoonRover.Platform;
using MoonRover.UI;
using MoonRover.Vision;

/// <summary>
/// Modular lunar-rover console HUD.
/// The generated atlas is used as a set of proportion-preserving windows rather than
/// as one stretched background. Live map/camera/telemetry components stay functional.
/// </summary>
public sealed class GeneratedConsoleHUD : MonoBehaviour
{
    [Header("Generated art")]
    public Texture2D modularAtlas;
    public Texture2D radarReplacement;
    public Texture2D modeFrame;
    public Texture2D telemetryFrame;
    public Texture2D mapFrame;
    public Texture2D hazCamFrame;
    public Texture2D statusFrame;
    public Texture2D driveModeKeyboardIcon;
    public Texture2D driveModeJoystickIcon;
    public Texture2D driveModeSteeringWheelIcon;
    public Font uiFont;
    public Font techFont;
    public GameObject rover;

    private const int ATLAS_WIDTH = 1672;
    private const int ATLAS_HEIGHT = 941;
    private Transform hudRoot;
    private Text centerSpeedText;
    private Rigidbody centerSpeedRigidbody;
    private bool built;

    private static readonly Color Cyan = new Color(0.45f, 0.90f, 1f, 1f);
    private static readonly Color CyanDim = new Color(0.32f, 0.68f, 0.78f, 1f);
    private static readonly Color Text = new Color(0.82f, 0.94f, 0.96f, 1f);
    private static readonly Color Dim = new Color(0.46f, 0.68f, 0.72f, 1f);
    private static readonly Color Green = new Color(0.45f, 1f, 0.62f, 1f);
    private static readonly Color Amber = new Color(1f, 0.72f, 0.22f, 1f);

    // The crop coordinates are in the generated atlas' top-left coordinate system.
    private static readonly Rect RadarCrop = new Rect(34, 24, 350, 350);
    private static readonly Rect WaveCrop = new Rect(397, 23, 584, 244);
    private static readonly Rect MapFrameCrop = new Rect(988, 22, 530, 356);
    private static readonly Rect RoverTelemetryCrop = new Rect(39, 393, 572, 214);
    private static readonly Rect SuspensionCrop = new Rect(628, 393, 347, 222);
    private static readonly Rect ViewportCrop = new Rect(35, 613, 290, 270);
    private static readonly Rect LinkCrop = new Rect(325, 636, 250, 122);
    private static readonly Rect ModeCrop = new Rect(596, 638, 385, 126);
    private static readonly Rect LargeViewportCrop = new Rect(985, 388, 538, 453);

    /// <summary>Called by the editor generator so the scene shows the replacement immediately.</summary>
    public void BuildForEditor()
    {
        Build();
    }

    private void Start()
    {
        Build();
    }

    private void Build()
    {
        if (built && hudRoot != null)
        {
            BindRuntimeComponents();
            return;
        }

        // Preserve the layout authored in the scene.  Previously Start()
        // always deleted GeneratedHUD_ModularRoot and rebuilt every child from
        // hard-coded coordinates, which discarded any Inspector adjustments
        // as soon as Play was pressed.
        if (hudRoot == null)
            hudRoot = transform.Find("GeneratedHUD_ModularRoot");
        if (hudRoot != null)
        {
            built = true;
            BindRuntimeComponents();
            return;
        }

        if (modularAtlas == null)
        {
            Debug.LogWarning("[GeneratedConsoleHUD] modularAtlas 未绑定，保留空 HUD。");
            return;
        }

        RemoveExistingHudRoot();
        hudRoot = new GameObject("GeneratedHUD_ModularRoot", typeof(RectTransform)).transform;
        hudRoot.SetParent(transform, false);
        var hudRect = hudRoot.GetComponent<RectTransform>();
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.pivot = new Vector2(0.5f, 0.5f);
        hudRect.offsetMin = Vector2.zero;
        hudRect.offsetMax = Vector2.zero;

        CreateTopBar();
        CreateLeftRail();
        CreateRightRail();
        CreateCenterTelemetry();
        CreateAlertOverlay();
        BindRuntimeComponents();
        built = true;
    }

    private void RemoveExistingHudRoot()
    {
        var old = transform.Find("GeneratedHUD_ModularRoot");
        if (old == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(old.gameObject);
        else Destroy(old.gameObject);
#else
        Destroy(old.gameObject);
#endif
    }

    private void CreateTopBar()
    {
        var title = AddText("ConsoleTitle", "LUNAR ROVER  /  SURFACE OPERATIONS", 22, Cyan,
            TextAnchor.MiddleLeft, new Vector2(34, -24), new Vector2(500, 34), hudRoot);
        title.fontStyle = FontStyle.Bold;
        AddLine("TopRule", new Vector2(34, -63), new Vector2(720, 2), new Color(Cyan.r, Cyan.g, Cyan.b, 0.48f), hudRoot);

        // Fixed transparent heading tape. The tape does not rotate; its world
        // ticks and cardinal labels scroll with the rover heading instead.
        var tapeObject = new GameObject("DirectionTape", typeof(RectTransform));
        tapeObject.transform.SetParent(hudRoot, false);
        var tape = tapeObject.GetComponent<RectTransform>();
        tape.anchorMin = tape.anchorMax = new Vector2(0.5f, 1f);
        tape.pivot = new Vector2(0.5f, 1f);
        tape.anchoredPosition = new Vector2(0f, -16f);
        tape.sizeDelta = new Vector2(760f, 92f);

        AddLine("DirectionTapeRule", new Vector2(0, -32), new Vector2(760, 2),
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.34f), tape);
        var tickMarks = new List<RectTransform>();
        for (int i = 0; i < 25; i++)
        {
            float height = i % 3 == 0 ? 18f : (i % 2 == 0 ? 12f : 8f);
            AddLine("DirectionTick" + i, Vector2.zero, new Vector2(i % 3 == 0 ? 3f : 2f, height),
                new Color(Cyan.r, Cyan.g, Cyan.b, i % 3 == 0 ? 0.86f : 0.52f), tape);
            var tick = tape.Find("DirectionTick" + i).GetComponent<RectTransform>();
            PlaceCentered(tick, new Vector2(0f, 8f), tick.sizeDelta);
            tickMarks.Add(tick);
        }
        var labels = new List<Text>();
        var labelAngles = new List<float>();
        string[] compassLabels = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        for (int i = 0; i < compassLabels.Length; i++)
        {
            var label = AddText("CompassLabel" + i, compassLabels[i], 16,
                i == 0 ? Text : Dim, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(56, 22), tape);
            PlaceCentered(label.rectTransform, Vector2.zero, new Vector2(56, 22));
            labels.Add(label);
            labelAngles.Add(i * 45f);
        }

        AddLine("DirectionCenterCaret", new Vector2(378, -17), new Vector2(4, 22), Amber, tape);
        var headingValue = AddText("HeadingValue", "N  0°", 18, Cyan, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(160, 24), tape);
        PlaceCentered(headingValue.rectTransform, new Vector2(0f, -34f), new Vector2(160, 24));

        var headingRuntime = tapeObject.AddComponent<HeadingTapeRuntime>();
        headingRuntime.rover = ResolveCar() != null ? ResolveCar().transform : null;
        headingRuntime.tickMarks = tickMarks.ToArray();
        headingRuntime.directionLabels = labels.ToArray();
        headingRuntime.labelWorldAngles = labelAngles.ToArray();
        headingRuntime.tapeHalfWidth = 380f;
        headingRuntime.pixelsPerDegree = 3.8f;

        var link = AddText("TopLink", "LINK  ONLINE", 18, Green, TextAnchor.MiddleRight,
            new Vector2(-210, -24), new Vector2(170, 30), hudRoot, right: true);
        link.fontStyle = FontStyle.Bold;
        AddText("TopTime", "SIM / LOCAL", 16, Dim, TextAnchor.MiddleRight,
            new Vector2(-34, -52), new Vector2(170, 24), hudRoot, right: true);
    }

    private void CreateLeftRail()
    {
        // Keep the original left-side radar module.  The supplied auxiliary
        // image remains in the project as an optional asset, but is not used
        // in the default console layout.
        AddPanelSurface("TerrainTopDownFrame", new Vector2(28, -82), new Vector2(300, 300), hudRoot);
        var terrainTopDownImage = AddRawImage("TerrainTopDownImage", new Vector2(28, -82), new Vector2(300, 300), hudRoot);
        terrainTopDownImage.color = Color.white;
        var topDown = terrainTopDownImage.gameObject.AddComponent<TopDownTerrainCamera>();
        topDown.target = terrainTopDownImage;
        topDown.rover = ResolveCar() != null ? ResolveCar().transform : null;

        AddText("RadarTitle", "TERRAIN / TOP VIEW", 18, Text, TextAnchor.MiddleLeft,
            new Vector2(44, -92), new Vector2(190, 28), hudRoot);
        AddText("RadarSub", "OVERHEAD CAMERA  /  LIVE", 14, Dim, TextAnchor.MiddleLeft,
            new Vector2(44, -120), new Vector2(180, 24), hudRoot);
        AddText("AttitudePitch", "俯仰  0.0°", 14, Dim, TextAnchor.MiddleLeft,
            new Vector2(52, -398), new Vector2(120, 22), hudRoot);
        AddText("AttitudeRoll", "侧倾  0.0°", 14, Dim, TextAnchor.MiddleLeft,
            new Vector2(190, -398), new Vector2(120, 22), hudRoot);

        // Use a clean frame here.  ModeCrop contains the old baked icons, so
        // drawing the new mode icons on top of it creates the double-image
        // seen in the previous build.
        if (modeFrame != null)
            AddGeneratedFrame("ModeWindow", modeFrame, new Vector2(24, -410), new Vector2(344, 112), hudRoot);
        else
            AddAtlas("ModeWindow", ModeCrop, new Vector2(24, -410), new Vector2(344, 112), hudRoot);
        AddText("ModeTitle", "DRIVE MODE", 15, Text, TextAnchor.MiddleLeft,
            new Vector2(42, -420), new Vector2(160, 20), hudRoot);
        AddText("ModeValue", "模式：手柄", 14, Cyan, TextAnchor.MiddleRight,
            new Vector2(204, -420), new Vector2(142, 22), hudRoot);
        AddDriveModeIcon("DriveModeKeyboardIcon", driveModeKeyboardIcon, new Vector2(82, -458), hudRoot);
        AddDriveModeIcon("DriveModeJoystickIcon", driveModeJoystickIcon, new Vector2(172, -458), hudRoot);
        AddDriveModeIcon("DriveModeSteeringWheelIcon", driveModeSteeringWheelIcon, new Vector2(262, -458), hudRoot);

        if (telemetryFrame != null)
        {
            var telemetryImage = AddRawImage("TelemetryWindow", new Vector2(24, -545), new Vector2(344, 129), hudRoot);
            telemetryImage.texture = telemetryFrame;
            telemetryImage.color = Color.white;
        }
        else
        {
            AddPanelSurface("TelemetryWindow", new Vector2(24, -545), new Vector2(344, 129), hudRoot);
        }
        AddTelemetryChrome(hudRoot);
        AddText("TelemetryTitle", "ROVER TELEMETRY", 14, Text, TextAnchor.MiddleLeft,
            new Vector2(42, -555), new Vector2(260, 20), hudRoot);
        AddLine("TelemetryRule", new Vector2(42, -582), new Vector2(308, 1),
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f), hudRoot);
        var speed = AddText("SpeedText", "SPEED  0.0 km/h", 20, Cyan, TextAnchor.MiddleLeft,
            new Vector2(42, -590), new Vector2(250, 26), hudRoot);
        AddText("DistanceText", "RANGE  0.0 m", 14, Dim, TextAnchor.MiddleLeft,
            new Vector2(42, -622), new Vector2(150, 22), hudRoot);
        var fv = AddText("FvText", "Fv  0.00", 14, Green, TextAnchor.MiddleLeft,
            new Vector2(218, -622), new Vector2(130, 22), hudRoot);

        AddAtlas("SuspensionWindow", SuspensionCrop, new Vector2(24, -695), new Vector2(344, 220), hudRoot);
        AddText("SuspensionTitle", "SUSPENSION / 4-WHEEL", 16, Text, TextAnchor.MiddleLeft,
            new Vector2(42, -704), new Vector2(240, 24), hudRoot);
        var slipBars = new Image[4];
        var slipTexts = new Text[4];
        string[] wheelNames = { "FL", "FR", "RL", "RR" };
        for (int i = 0; i < 4; i++)
        {
            float x = 76 + (i % 2) * 130;
            float y = -787 - (i / 2) * 57;
            slipBars[i] = AddFillBar("WheelBar" + i, new Vector2(x, y), new Vector2(86, 15), Green, hudRoot);
            slipTexts[i] = AddText("WheelLabel" + i, wheelNames[i] + "  0%", 13, Dim,
                TextAnchor.MiddleLeft, new Vector2(x, y - 20), new Vector2(90, 20), hudRoot);
        }
    }

    private void CreateRightRail()
    {
        // Compact initial right column: each data source has its own bounded
        // panel, so an uninitialized RawImage can never wash across the HUD.
        if (mapFrame != null)
            AddGeneratedFrame("MapFrame", mapFrame, new Vector2(-30, -76), new Vector2(300, 300), hudRoot, right: true);
        else
            AddPanelSurface("MapFrame", new Vector2(-30, -76), new Vector2(300, 300), hudRoot, right: true);
        var map = AddRawImage("MapImage", new Vector2(-50, -116), new Vector2(260, 260), hudRoot, right: true);
        map.color = new Color(0.02f, 0.08f, 0.10f, 0.78f);
        // Keep the map fixed to its own square viewport.  FitInParent would
        // use the full-screen hudRoot and expand this RawImage across the HUD.
        map.rectTransform.anchorMin = map.rectTransform.anchorMax = new Vector2(1f, 1f);
        map.rectTransform.pivot = new Vector2(1f, 1f);
        map.rectTransform.anchoredPosition = new Vector2(-50f, -116f);
        map.rectTransform.sizeDelta = new Vector2(260f, 260f);
        AddText("MapTitle", "NAV / TERRAIN MAP", 18, Text, TextAnchor.MiddleLeft,
            new Vector2(-50, -94), new Vector2(250, 26), hudRoot, right: true);
        AddText("GpsText", "GPS  12 SAT", 14, Dim, TextAnchor.MiddleLeft,
            new Vector2(-170, -388), new Vector2(120, 22), hudRoot, right: true);
        AddText("WaypointText", "WAYPOINT  —", 14, Dim, TextAnchor.MiddleLeft,
            new Vector2(-40, -388), new Vector2(120, 22), hudRoot, right: true);

        // Restore the underbody/HazCam viewport as a fixed-size module.
        if (hazCamFrame != null)
            AddGeneratedFrame("HazCamFrame", hazCamFrame, new Vector2(-30, -406), new Vector2(300, 300), hudRoot, right: true);
        else
            AddPanelSurface("HazCamFrame", new Vector2(-30, -406), new Vector2(300, 300), hudRoot, right: true);
        var cam = AddRawImage("CamImage", new Vector2(-50, -446), new Vector2(260, 260), hudRoot, right: true);
        cam.color = Color.white;
        // ChassisCamera renders a square texture; keep the complete square
        // image inside this bounded viewport.
        cam.uvRect = new Rect(0f, 0f, 1f, 1f);
        if (statusFrame != null)
            AddGeneratedFrame("StatusFrame", statusFrame, new Vector2(-30, -714), new Vector2(300, 190), hudRoot, right: true);
        else
            AddPanelSurface("StatusFrame", new Vector2(-30, -714), new Vector2(300, 190), hudRoot, right: true);
        AddText("CamTitle", "HAZCAM / UNDERBODY", 16, Text, TextAnchor.MiddleLeft,
            new Vector2(-50, -418), new Vector2(240, 24), hudRoot, right: true);

        var platform = AddText("PlatformStatus", "PLATFORM  未连接", 16, Amber, TextAnchor.MiddleLeft,
            new Vector2(-50, -726), new Vector2(250, 24), hudRoot, right: true);
        AddText("PlatformPitch", "PITCH  0.0°", 14, Dim, TextAnchor.MiddleLeft,
            new Vector2(-170, -756), new Vector2(120, 22), hudRoot, right: true);
        AddText("PlatformRoll", "ROLL  0.0°", 14, Dim, TextAnchor.MiddleLeft,
            new Vector2(-40, -756), new Vector2(120, 22), hudRoot, right: true);
        AddText("PlatformFPS", "BASE  50 Hz", 14, Green, TextAnchor.MiddleLeft,
            new Vector2(-50, -786), new Vector2(150, 22), hudRoot, right: true);
        AddText("CommsText", "SIGNAL  ▰▰▰▰▰   20 ms", 13, Green, TextAnchor.MiddleLeft,
            new Vector2(-50, -812), new Vector2(250, 22), hudRoot, right: true);
        AddText("BatteryText", "BAT  100.0%   28.8V   0.0A", 13, Cyan, TextAnchor.MiddleLeft,
            new Vector2(-50, -838), new Vector2(250, 22), hudRoot, right: true);
        AddText("EventLogText", "EVENT LOG  /  SYSTEM READY", 11, Dim, TextAnchor.MiddleLeft,
            new Vector2(-50, -864), new Vector2(250, 20), hudRoot, right: true);
    }

    private void CreateCenterTelemetry()
    {
        var centerSpeed = AddText("CenterSpeedText", "0.0", 144, Cyan,
            TextAnchor.MiddleCenter, Vector2.zero, new Vector2(340, 170), hudRoot);
        PlaceCentered(centerSpeed.rectTransform, new Vector2(-58f, -330f), new Vector2(340f, 170f));
        centerSpeedText = centerSpeed;
        var centerSpeedUnit = AddText("CenterSpeedUnitText", "km/h", 40, Dim,
            TextAnchor.MiddleLeft, Vector2.zero, new Vector2(110, 60), hudRoot);
        PlaceCentered(centerSpeedUnit.rectTransform, new Vector2(174f, -330f), new Vector2(110f, 60f));

        // A quiet center marker leaves the actual driving view unobstructed.
        AddLine("CenterReticleH", new Vector2(934, -475), new Vector2(52, 1), new Color(Cyan.r, Cyan.g, Cyan.b, 0.32f), hudRoot);
        AddLine("CenterReticleV", new Vector2(959, -450), new Vector2(1, 52), new Color(Cyan.r, Cyan.g, Cyan.b, 0.32f), hudRoot);
        AddText("CenterHint", "MANUAL CONTROL", 13, new Color(Cyan.r, Cyan.g, Cyan.b, 0.52f),
            TextAnchor.MiddleCenter, new Vector2(900, -520), new Vector2(120, 22), hudRoot);
    }

    private void CreateAlertOverlay()
    {
        var overlay = new GameObject("AEBAlertOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(hudRoot, false);
        var image = overlay.GetComponent<Image>();
        image.color = new Color(1f, 0.08f, 0.04f, 0f);
        image.raycastTarget = false;
        var rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var alert = AddText("AlertText", string.Empty, 34, new Color(1f, 0.30f, 0.24f, 1f),
            TextAnchor.MiddleCenter, new Vector2(760, -82), new Vector2(400, 42), hudRoot);
        alert.gameObject.SetActive(false);

        var car = ResolveCar();
        if (car != null)
        {
            var manual = GetOrAdd<ManualDriverWithAEB>(car.gameObject);
            manual.aebOverlayImage = image;
            manual.aebAlertText = alert;
        }
    }

    private void CreateRadarDynamics()
    {
        var sweepObject = new GameObject("RadarSweep", typeof(RectTransform), typeof(Image), typeof(RadarSweepEffect));
        sweepObject.transform.SetParent(hudRoot, false);
        var sweepRect = sweepObject.GetComponent<RectTransform>();
        Place(sweepRect, new Vector2(178, -232), new Vector2(2, 126), false);
        sweepRect.pivot = new Vector2(0.5f, 0f);
        var sweepImage = sweepObject.GetComponent<Image>();
        sweepImage.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.78f);
        sweepImage.raycastTarget = false;

        var pulseObject = new GameObject("RadarPulse", typeof(RectTransform), typeof(Image));
        pulseObject.transform.SetParent(hudRoot, false);
        var pulseRect = pulseObject.GetComponent<RectTransform>();
        Place(pulseRect, new Vector2(178, -232), new Vector2(8, 8), false);
        pulseRect.pivot = new Vector2(0.5f, 0.5f);
        var pulseImage = pulseObject.GetComponent<Image>();
        pulseImage.color = Green;
        pulseImage.raycastTarget = false;

        var effect = sweepObject.GetComponent<RadarSweepEffect>();
        effect.sweep = sweepRect;
        effect.pulse = pulseRect;
        effect.scanner = FindObjectOfType<Scan>();
    }

    private void CreateWaveformDynamics()
    {
        var waveObject = new GameObject("LiveWaveform", typeof(RectTransform), typeof(RawImage), typeof(DynamicWaveform));
        waveObject.transform.SetParent(hudRoot, false);
        var rect = waveObject.GetComponent<RectTransform>();
        Place(rect, new Vector2(414, -850), new Vector2(640, 82), false);
        var raw = waveObject.GetComponent<RawImage>();
        raw.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f);
        raw.raycastTarget = false;
        var wave = waveObject.GetComponent<DynamicWaveform>();
        wave.target = raw;
        wave.scanner = FindObjectOfType<Scan>();
    }

    private void BindRuntimeComponents()
    {
        var car = ResolveCar();
        if (car == null) return;
        var root = hudRoot != null ? hudRoot : transform;
        centerSpeedRigidbody = car.CarRigidbody != null ? car.CarRigidbody : car.GetComponent<Rigidbody>();
        // When the scene-authored HUD is reused, CreateCenterTelemetry is not
        // called, so recover the text reference from the existing hierarchy.
        centerSpeedText = FindText(root, "CenterSpeedText");

        var hud = GetOrAdd<HUDDashboard>(car.gameObject);
        hud.batteryText = FindText(root, "BatteryText") ?? FindText(root, "SpeedText");
        hud.gpsText = FindText(root, "GpsText");
        hud.compassText = FindText(root, "HeadingValue");
        hud.commsText = FindText(root, "CommsText");
        hud.waypointText = FindText(root, "WaypointText");
        hud.eventLogText = FindText(root, "EventLogText");

        var moveInfo = FindObjectOfType<Move_info>();
        if (moveInfo == null) moveInfo = GetOrAdd<Move_info>(car.gameObject);
        moveInfo.enabled = true;
        moveInfo.carRigidbody = centerSpeedRigidbody;
        moveInfo.terrainRadar = FindObjectOfType<Scan>();
        moveInfo.speedText = FindText(root, "SpeedText");
        moveInfo.centerSpeedText = FindText(root, "CenterSpeedText");
        moveInfo.distanceText = FindText(root, "DistanceText");
        moveInfo.fvText = FindText(root, "FvText");

        var attitude = GetOrAdd<MiniAttitudeIndicator>(car.gameObject);
        attitude.attitudeImage = AddRawImageIfMissing("AttitudeImage", Vector2.zero, new Vector2(360, 360), root);
        PlaceCentered(attitude.attitudeImage.rectTransform, Vector2.zero, new Vector2(360, 360));
        attitude.attitudeImage.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.78f);
        attitude.pitchText = FindText(root, "AttitudePitch");
        attitude.rollText = FindText(root, "AttitudeRoll");

        var wheels = GetOrAdd<WheelSlipIndicator>(car.gameObject);
        wheels.slipBars = new Image[4];
        wheels.slipTexts = new Text[4];
        for (int i = 0; i < 4; i++)
        {
            wheels.slipBars[i] = FindImage(root, "WheelBar" + i);
            wheels.slipTexts[i] = FindText(root, "WheelLabel" + i);
        }

        var terrainProfile = GetOrAdd<TerrainProfile>(car.gameObject);
        // Keep the sampling component available for future use, but hide the
        // old tiny Terrain Profile viewport from the driving HUD.
        terrainProfile.profileImage = null;
        terrainProfile.labelText = null;

        var manual = GetOrAdd<ManualDriverWithAEB>(car.gameObject);
        manual.g29StatusText = FindText(root, "ModeValue");

        var platform = FindObjectOfType<MotionPlatformController>();
        if (platform != null)
        {
            platform.statusText = FindText(root, "PlatformStatus");
            platform.pitchText = FindText(root, "PlatformPitch");
            platform.rollText = FindText(root, "PlatformRoll");
            platform.motionFPSText = FindText(root, "PlatformFPS");
        }

        var mapObject = FindOrCreateChild("ExplorationMap", root);
        var mapSystem = GetOrAdd<ExplorationMap>(mapObject);
        mapSystem.rover = car.transform;
        mapSystem.mapImage = FindRawImage(root, "MapImage");
        mapSystem.arrowTransform = EnsureMapArrow(mapSystem.mapImage);
        mapSystem.scanner = FindObjectOfType<Scan>() ?? GetOrAdd<Scan>(new GameObject("Scan_Radar"));

        var camera = GetOrAdd<ChassisCamera>(car.gameObject);
        camera.camImage = FindRawImage(root, "CamImage");
        camera.enabled = true;
    }

    private void Update()
    {
        if (centerSpeedText == null || centerSpeedRigidbody == null)
        {
            // Allow references to recover after a scene reload or script
            // recompilation without rebuilding the user's authored layout.
            if (hudRoot != null) BindRuntimeComponents();
            if (centerSpeedText == null || centerSpeedRigidbody == null) return;
        }
        centerSpeedText.text = (centerSpeedRigidbody.velocity.magnitude * 3.6f).ToString("F1");
    }

    private CarController ResolveCar()
    {
        if (rover != null)
        {
            var assigned = rover.GetComponent<CarController>();
            if (assigned != null) return assigned;
        }
        return FindObjectOfType<CarController>();
    }

    private GameObject FindOrCreateChild(string name, Transform parent)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private RectTransform EnsureMapArrow(RawImage mapImage)
    {
        if (mapImage == null) return null;
        var arrow = mapImage.transform.Find("MapArrow");
        if (arrow == null)
        {
            var go = new GameObject("MapArrow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(mapImage.transform, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 0.24f, 0.18f, 1f);
            image.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(9, 9);
            return rect;
        }
        return arrow.GetComponent<RectTransform>();
    }

    private Text AddText(string name, string value, int size, Color color, TextAnchor alignment,
        Vector2 position, Vector2 sizeDelta, Transform parent, bool right = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.text = value;
        text.font = SelectFont(value);
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0.06f, 0.08f, 0.8f);
        outline.effectDistance = new Vector2(1, -1);
        Place(go.GetComponent<RectTransform>(), position, sizeDelta, right);
        return text;
    }

    private Font SelectFont(string value)
    {
        bool cjk = !string.IsNullOrEmpty(value) && HasCjk(value);
        if (cjk && uiFont != null) return uiFont;
        if (!cjk && techFont != null) return techFont;
        if (uiFont != null) return uiFont;
        return Font.CreateDynamicFontFromOSFont(cjk ? "Microsoft YaHei" : "Arial", 32);
    }

    private static bool HasCjk(string value)
    {
        for (int i = 0; i < value.Length; i++)
            if (value[i] >= 0x2E80 && value[i] <= 0x9FFF) return true;
        return false;
    }

    private RawImage AddAtlas(string name, Rect crop, Vector2 position, Vector2 sizeDelta,
        Transform parent, bool right = false)
    {
        var image = AddRawImage(name, position, sizeDelta, parent, right);
        image.texture = modularAtlas;
        image.uvRect = new Rect(
            crop.x / ATLAS_WIDTH,
            (ATLAS_HEIGHT - crop.y - crop.height) / ATLAS_HEIGHT,
            crop.width / ATLAS_WIDTH,
            crop.height / ATLAS_HEIGHT);
        image.color = Color.white;
        return image;
    }

    private RawImage AddRawImage(string name, Vector2 position, Vector2 sizeDelta, Transform parent, bool right = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        Place(go.GetComponent<RectTransform>(), position, sizeDelta, right);
        return raw;
    }

    private RawImage AddGeneratedFrame(string name, Texture2D texture, Vector2 position,
        Vector2 sizeDelta, Transform parent, bool right = false)
    {
        var image = AddRawImage(name, position, sizeDelta, parent, right);
        image.texture = texture;
        image.color = Color.white;
        return image;
    }

    private void AddDriveModeIcon(string name, Texture2D texture, Vector2 position, Transform parent)
    {
        if (texture == null) return;
        var image = AddRawImage(name, position, new Vector2(100f, 100f), parent);
        image.texture = texture;
        image.color = Color.white;
    }

    private Image AddPanelSurface(string name, Vector2 position, Vector2 sizeDelta, Transform parent, bool right = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.008f, 0.028f, 0.038f, 0.45f);
        image.raycastTarget = false;
        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.70f);
        outline.effectDistance = new Vector2(1f, -1f);
        Place(go.GetComponent<RectTransform>(), position, sizeDelta, right);
        return image;
    }

    private void AddTelemetryChrome(Transform parent)
    {
        var edge = new Color(Cyan.r, Cyan.g, Cyan.b, 0.78f);
        var softEdge = new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f);
        var dimEdge = new Color(Cyan.r, Cyan.g, Cyan.b, 0.16f);

        // Outer frame: the same thin, engineered line language as the original HUD.
        AddLine("TelemetryTopEdge", new Vector2(36, -546), new Vector2(320, 1), edge, parent);
        AddLine("TelemetryBottomEdge", new Vector2(36, -673), new Vector2(320, 1), edge, parent);
        AddLine("TelemetryLeftEdge", new Vector2(28, -552), new Vector2(1, 115), softEdge, parent);
        AddLine("TelemetryRightEdge", new Vector2(363, -552), new Vector2(1, 115), softEdge, parent);

        // Cut-corner brackets and small top-right status marker.
        AddLine("TelemetryCornerTLH", new Vector2(28, -552), new Vector2(22, 1), edge, parent);
        AddLine("TelemetryCornerTLV", new Vector2(28, -552), new Vector2(1, 18), edge, parent);
        AddLine("TelemetryCornerTRH", new Vector2(341, -552), new Vector2(22, 1), edge, parent);
        AddLine("TelemetryCornerTRV", new Vector2(362, -552), new Vector2(1, 18), edge, parent);
        AddLine("TelemetryCornerBLH", new Vector2(28, -655), new Vector2(22, 1), softEdge, parent);
        AddLine("TelemetryCornerBLV", new Vector2(28, -656), new Vector2(1, 17), softEdge, parent);
        AddLine("TelemetryCornerBRH", new Vector2(341, -655), new Vector2(22, 1), softEdge, parent);
        AddLine("TelemetryCornerBRV", new Vector2(362, -656), new Vector2(1, 17), softEdge, parent);

        AddLine("TelemetryHeaderRail", new Vector2(42, -578), new Vector2(88, 1), softEdge, parent);
        AddLine("TelemetryDataRail", new Vector2(42, -619), new Vector2(308, 1), dimEdge, parent);
        AddLine("TelemetryBottomRail", new Vector2(78, -665), new Vector2(236, 1), dimEdge, parent);
    }

    private RawImage AddRawImageIfMissing(string name, Vector2 position, Vector2 sizeDelta, Transform parent)
    {
        var found = FindRawImage(parent, name);
        return found != null ? found : AddRawImage(name, position, sizeDelta, parent);
    }

    private static void AddAspect(RawImage image, float aspect)
    {
        var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = aspect;
    }

    private Image AddFillBar(string name, Vector2 position, Vector2 sizeDelta, Color color, Transform parent)
    {
        // A dark track makes the live length change readable even when the
        // current value is close to zero; WheelSlipIndicator owns the fill.
        var trackObject = new GameObject(name + "Track", typeof(RectTransform), typeof(Image));
        trackObject.transform.SetParent(parent, false);
        var track = trackObject.GetComponent<Image>();
        track.color = new Color(0.03f, 0.12f, 0.15f, 0.88f);
        track.raycastTarget = false;
        Place(trackObject.GetComponent<RectTransform>(), position, sizeDelta, false);

        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillAmount = 0f;
        image.color = color;
        image.raycastTarget = false;
        Place(go.GetComponent<RectTransform>(), position, sizeDelta, false);
        return image;
    }

    private void AddLine(string name, Vector2 position, Vector2 sizeDelta, Color color, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        Place(go.GetComponent<RectTransform>(), position, sizeDelta, false);
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 sizeDelta, bool right)
    {
        if (right)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
        }
        rect.sizeDelta = sizeDelta;
    }

    private static void PlaceCentered(RectTransform rect, Vector2 position, Vector2 sizeDelta)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        if (go == null) return null;
        var result = go.GetComponent<T>();
        return result != null ? result : go.AddComponent<T>();
    }

    private static Text FindText(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var text in root.GetComponentsInChildren<Text>(true))
            if (text.gameObject.name == name) return text;
        return null;
    }

    private static RawImage FindRawImage(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var image in root.GetComponentsInChildren<RawImage>(true))
            if (image.gameObject.name == name) return image;
        return null;
    }

    private static Image FindImage(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var image in root.GetComponentsInChildren<Image>(true))
            if (image.gameObject.name == name) return image;
        return null;
    }
}

/// <summary>
/// Keeps the HUD tape fixed on screen while scrolling world bearings beneath
/// the center marker, like an overhead game compass.
/// </summary>
public sealed class HeadingTapeRuntime : MonoBehaviour
{
    public Transform rover;
    public RectTransform[] tickMarks;
    public Text[] directionLabels;
    public float[] labelWorldAngles;
    public float tapeHalfWidth = 380f;
    public float pixelsPerDegree = 3.8f;

    private void Start()
    {
        if (rover == null)
        {
            var car = FindObjectOfType<CarController>();
            if (car != null) rover = car.transform;
        }
    }

    private void Update()
    {
        if (rover == null) return;
        float heading = rover.eulerAngles.y;

        if (tickMarks != null)
        {
            for (int i = 0; i < tickMarks.Length; i++)
            {
                if (tickMarks[i] == null) continue;
                float worldAngle = i * 15f;
                float delta = Mathf.DeltaAngle(heading, worldAngle);
                tickMarks[i].anchoredPosition = new Vector2(delta * pixelsPerDegree, 8f);
                tickMarks[i].gameObject.SetActive(Mathf.Abs(delta) <= tapeHalfWidth / pixelsPerDegree + 8f);
            }
        }

        if (directionLabels != null && labelWorldAngles != null)
        {
            for (int i = 0; i < directionLabels.Length && i < labelWorldAngles.Length; i++)
            {
                if (directionLabels[i] == null) continue;
                float delta = Mathf.DeltaAngle(heading, labelWorldAngles[i]);
                directionLabels[i].rectTransform.anchoredPosition = new Vector2(delta * pixelsPerDegree, -12f);
                directionLabels[i].gameObject.SetActive(Mathf.Abs(delta) <= tapeHalfWidth / pixelsPerDegree + 15f);
            }
        }
    }
}

/// <summary>Simple live overhead camera for the terrain window.</summary>
public sealed class TopDownTerrainCamera : MonoBehaviour
{
    public RawImage target;
    public Transform rover;
    public float height = 35f;
    public float orthographicSize = 18f;
    public int textureSize = 512;

    private Camera overheadCamera;
    private RenderTexture renderTexture;

    private void Start()
    {
        ResolveRover();
        EnsureCamera();
    }

    private void LateUpdate()
    {
        ResolveRover();
        EnsureCamera();
        if (overheadCamera == null || rover == null) return;

        overheadCamera.transform.position = rover.position + Vector3.up * height;
        // Keep looking straight down, but roll the image 180 degrees so the
        // overhead view is not displayed upside down.
        overheadCamera.transform.rotation = Quaternion.Euler(90f, 0f, 180f);
        if (target != null && target.texture != renderTexture)
            target.texture = renderTexture;
    }

    private void ResolveRover()
    {
        if (rover != null) return;
        var car = FindObjectOfType<CarController>();
        if (car != null) rover = car.transform;
    }

    private void EnsureCamera()
    {
        if (overheadCamera == null)
        {
            var go = new GameObject("Terrain_Overhead_Camera");
            overheadCamera = go.AddComponent<Camera>();
            overheadCamera.orthographic = true;
            overheadCamera.orthographicSize = orthographicSize;
            overheadCamera.nearClipPlane = 0.1f;
            overheadCamera.farClipPlane = 200f;
            overheadCamera.clearFlags = CameraClearFlags.Skybox;
            overheadCamera.cullingMask = ~LayerMask.GetMask("UI");
            overheadCamera.allowHDR = false;
            overheadCamera.allowMSAA = true;
        }

        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32);
            renderTexture.name = "Terrain_Overhead_RenderTexture";
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.Create();
            overheadCamera.targetTexture = renderTexture;
        }

        if (target != null && target.texture != renderTexture)
            target.texture = renderTexture;
    }

    private void OnDestroy()
    {
        if (overheadCamera != null && overheadCamera.targetTexture == renderTexture)
            overheadCamera.targetTexture = null;
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
        if (overheadCamera != null) Destroy(overheadCamera.gameObject);
    }
}

/// <summary>Terrain pitch plus top-down radar view driven by Scan data.</summary>
public sealed class TerrainPitchDisplay : MonoBehaviour
{
    public RawImage target;
    public Transform rover;
    public Scan scanner;
    public Text pitchText;
    public Text pointsText;
    public int textureSize = 256;
    public float updateInterval = 0.12f;
    public float lookAhead = 12f;
    public float radarRadius = 15f;

    private Texture2D texture;
    private Terrain terrain;
    private float timer;

    private void Start()
    {
        ResolveReferences();
        texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        if (target != null) target.texture = texture;
        Render();
    }

    private void Update()
    {
        ResolveReferences();
        timer += Time.deltaTime;
        if (timer < updateInterval) return;
        timer = 0f;
        Render();
    }

    private void ResolveReferences()
    {
        if (rover == null)
        {
            var car = FindObjectOfType<CarController>();
            if (car != null) rover = car.transform;
        }
        if (scanner == null) scanner = FindObjectOfType<Scan>();
        if (terrain == null) terrain = Terrain.activeTerrain;
    }

    private float SampleGround(Vector3 position)
    {
        if (terrain == null) return position.y;
        return terrain.SampleHeight(position) + terrain.transform.position.y;
    }

    private void Render()
    {
        if (texture == null) return;

        var pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color(0.01f, 0.05f, 0.07f, 0.18f);

        int cx = textureSize / 2;
        int cy = 142;
        int maxRadius = 102;
        Color grid = new Color(0.45f, 0.90f, 1f, 0.28f);
        Color bright = new Color(0.45f, 0.90f, 1f, 0.90f);

        // Top-down range rings and axes.
        for (int r = 30; r <= maxRadius; r += 36) DrawCircle(pixels, cx, cy, r, grid);
        DrawLine(pixels, cx - maxRadius, cy, cx + maxRadius, cy, grid);
        DrawLine(pixels, cx, cy - maxRadius, cx, cy + maxRadius, grid);

        float pitch = 0f;
        if (rover != null)
        {
            Vector3 start = rover.position + rover.forward * 1.5f;
            Vector3 end = rover.position + rover.forward * lookAhead;
            float startHeight = SampleGround(start);
            float endHeight = SampleGround(end);
            pitch = Mathf.Atan2(endHeight - startHeight, lookAhead - 1.5f) * Mathf.Rad2Deg;

            // Compact terrain profile strip along the lower edge of the view.
            int profileY = 31;
            int previousY = profileY;
            for (int x = 18; x <= textureSize - 18; x++)
            {
                float distance = (x - 18f) / (textureSize - 36f) * lookAhead;
                Vector3 sample = rover.position + rover.forward * distance;
                float height = SampleGround(sample);
                float normalized = Mathf.Clamp((height - startHeight) / 3f, -1f, 1f);
                int y = profileY + Mathf.RoundToInt(normalized * 15f);
                if (x > 18) DrawLine(pixels, x - 1, previousY, x, y, bright);
                previousY = y;
            }
            DrawLine(pixels, 18, profileY, textureSize - 18, profileY, grid);
        }

        // Radar returns supplied by Scan, expressed in the rover's local X/Z frame.
        int pointCount = 0;
        if (scanner != null && rover != null && scanner.PhysicalPoints != null)
        {
            for (int i = 0; i < scanner.PhysicalPoints.Count; i++)
            {
                Vector3 relative = scanner.PhysicalPoints[i] - rover.position;
                float localX = Vector3.Dot(relative, rover.right);
                float localZ = Vector3.Dot(relative, rover.forward);
                float distance = new Vector2(localX, localZ).magnitude;
                if (distance > radarRadius) continue;

                int x = cx + Mathf.RoundToInt(localX / radarRadius * maxRadius);
                int y = cy + Mathf.RoundToInt(localZ / radarRadius * maxRadius);
                Color pointColor = distance < 7f ? new Color(0.45f, 1f, 0.62f, 1f) :
                    new Color(1f, 0.72f, 0.22f, 1f);
                DrawDot(pixels, x, y, 3, pointColor);
                pointCount++;
            }
        }

        // Rover marker at the origin, pointing toward the top of the display.
        DrawLine(pixels, cx, cy - 10, cx - 8, cy + 8, bright);
        DrawLine(pixels, cx, cy - 10, cx + 8, cy + 8, bright);
        DrawLine(pixels, cx - 8, cy + 8, cx + 8, cy + 8, bright);
        DrawDot(pixels, cx, cy, 3, new Color(1f, 0.32f, 0.22f, 1f));

        texture.SetPixels(pixels);
        texture.Apply();
        if (pitchText != null)
            pitchText.text = string.Format("AHEAD PITCH  {0:+0.0;-0.0;0.0}°", pitch);
        if (pointsText != null)
            pointsText.text = string.Format("RADAR POINTS  {0}", pointCount);
    }

    private void DrawCircle(Color[] pixels, int cx, int cy, int radius, Color color)
    {
        const int segments = 96;
        int previousX = cx + radius;
        int previousY = cy;
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            int x = cx + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
            int y = cy + Mathf.RoundToInt(Mathf.Sin(angle) * radius);
            DrawLine(pixels, previousX, previousY, x, y, color);
            previousX = x;
            previousY = y;
        }
    }

    private void DrawDot(Color[] pixels, int x, int y, int radius, Color color)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
                if (dx * dx + dy * dy <= radius * radius) SetPixel(pixels, x + dx, y + dy, color);
    }

    private void DrawLine(Color[] pixels, int x0, int y0, int x1, int y1, Color color)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        if (steps == 0) { SetPixel(pixels, x0, y0, color); return; }
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            SetPixel(pixels, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)),
                Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), color);
        }
    }

    private void SetPixel(Color[] pixels, int x, int y, Color color)
    {
        if (x < 0 || x >= textureSize || y < 0 || y >= textureSize) return;
        pixels[y * textureSize + x] = color;
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }
}

/// <summary>Small animated radar sweep; the actual terrain scan remains in Scan.cs.</summary>
public sealed class RadarSweepEffect : MonoBehaviour
{
    public RectTransform sweep;
    public RectTransform pulse;
    public Scan scanner;
    public float rotationsPerSecond = 0.08f;

    private float angle;

    private void Start()
    {
        if (scanner == null) scanner = FindObjectOfType<Scan>();
    }

    private void Update()
    {
        angle = (angle + 360f * rotationsPerSecond * Time.deltaTime) % 360f;
        if (sweep != null) sweep.localRotation = Quaternion.Euler(0f, 0f, -angle);
        if (pulse != null)
        {
            float scanValue = scanner != null ? Mathf.Clamp01(scanner.Fv / 30f) : 0f;
            float scale = 1.0f + Mathf.Sin(Time.time * 4f) * 0.22f + scanValue * 0.2f;
            pulse.localScale = new Vector3(scale, scale, 1f);
        }
    }
}

/// <summary>Live, lightweight waveform overlay driven by Scan.Fv and time.</summary>
public sealed class DynamicWaveform : MonoBehaviour
{
    public RawImage target;
    public Scan scanner;
    public int textureWidth = 320;
    public int textureHeight = 64;
    public float updateInterval = 0.06f;

    private Texture2D texture;
    private float timer;

    private void Start()
    {
        if (scanner == null) scanner = FindObjectOfType<Scan>();
        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        if (target != null) target.texture = texture;
        Render();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < updateInterval) return;
        timer = 0f;
        Render();
    }

    private void Render()
    {
        if (texture == null) return;
        var pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0f, 0f, 0f, 0f);

        float fv = scanner != null ? Mathf.Clamp01(scanner.Fv / 30f) : 0.06f;
        float time = Time.time;
        int center = textureHeight / 2;
        for (int x = 0; x < textureWidth; x++)
        {
            float t = x / (float)(textureWidth - 1);
            float noise = Mathf.PerlinNoise(t * 8f, time * 0.7f) - 0.5f;
            float signal = Mathf.Sin(t * 28f + time * 4f) * (0.12f + fv * 0.38f);
            signal += Mathf.Sin(t * 83f - time * 7f) * (0.04f + fv * 0.08f);
            int y = Mathf.Clamp(Mathf.RoundToInt(center + (noise * 0.45f + signal) * textureHeight), 1, textureHeight - 2);
            for (int dy = -1; dy <= 1; dy++)
                pixels[(y + dy) * textureWidth + x] = new Color(0.45f, 0.90f, 1f, 0.9f);
            for (int py = y + 2; py < textureHeight; py++)
                pixels[py * textureWidth + x] = new Color(0.45f, 0.90f, 1f, 0.03f);
        }
        texture.SetPixels(pixels);
        texture.Apply();
    }
}

using UnityEngine;

[CreateAssetMenu(fileName = "RoverConfig", menuName = "MoonRover/RoverConfig")]
public class RoverConfig : ScriptableObject
{
    [Header("AEB 紧急制动设置")]
    public float aebSlopeThreshold = 25f;
    public float aebCliffThreshold = 35f;
    public float aebObstacleDistance = 15f;
    public float aebDetectDistance = 4f;
    public float dangerSteepness = 30f;

    [Header("AI 导航设置")]
    public int feelerCount = 15;
    public float lookAheadDistance = 10f;
    public float maxSteerAngle = 25f;
    public float stuckDetectTime = 2.5f;
    public float reverseTime = 3f;
    public float slopeGreenThreshold = 8f;
    public float slopeYellowThreshold = 22f;
    public float slopePenaltyWeight = 4f;
    public float targetWeight = 1f;
    public float decisionInterval = 0.5f;
    public float hysteresisBonus = 0.2f;

    [Header("路径规划")]
    public float pathDensifyInterval = 1f;
    public float waypointTolerance = 2.5f;

    [Header("雷达扫描")]
    public int scanRings = 12;
    public int scanRaysPerRing = 45;
    public float scanMinRadius = 4f;
    public float scanMaxRadius = 35f;
    public float scanFovAngle = 360f;
    public float scanRadarHeightOffset = 2.5f;

    [Header("遥测仪表盘")]
    public float pitchRollWarning = 30f;
    public float bumpinessGreenThreshold = 5f;
    public float bumpinessYellowThreshold = 10f;

    [Header("轨迹预测")]
    public float predictionDistance = 10f;
    public int trajectoryPoints = 30;
    public float wheelBase = 2.5f;
    public float trackWidth = 1.6f;
    public float trajectorySmoothSpeed = 8f;

    [Header("探索地图")]
    public int mapResolution = 1024;
    public float contourInterval = 2f;
    public float majorContour = 10f;
    public float mapUpdateInterval = 0.1f;
}

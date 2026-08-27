using UnityEngine;

[CreateAssetMenu(fileName = "SensorConfig", menuName = "MoonRover/SensorConfig")]
public class SensorConfig : ScriptableObject
{
    [Header("扫描范围")]
    public float maxRadius = 35f;
    public float minRadius = 4f;
    public float fovAngle = 360f;

    [Header("扫描精度")]
    public int ringCount = 12;
    public int raysPerRing = 45;

    [Header("坡度分级阈值")]
    public float slopeGreenThreshold = 8f;
    public float slopeYellowThreshold = 22f;
}

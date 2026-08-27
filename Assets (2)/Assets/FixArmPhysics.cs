using UnityEngine;

public class FixArmPhysics : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== FixArmPhysics ===");
        
        // 获取所有子物体的ArticulationBody
        var bodies = GetComponentsInChildren<ArticulationBody>(true);
        Debug.Log($"Found {bodies.Length} ArticulationBodies");
        
        foreach (var body in bodies)
        {
            Debug.Log($"Fixing: {body.gameObject.name}");
            
            // 禁用重力
            body.useGravity = false;
            
            // 如果是根关节，设为Fixed并最大化刚度
            if (body.isRoot)
            {
                Debug.Log($"  -> isRoot, setting Fixed joint");
                body.jointType = ArticulationJointType.FixedJoint;
                
                var drive = body.xDrive;
                drive.stiffness = float.MaxValue;
                drive.damping = float.MaxValue;
                body.xDrive = drive;
            }
        }
        
        Debug.Log("=== Fix Complete ===");
    }
}

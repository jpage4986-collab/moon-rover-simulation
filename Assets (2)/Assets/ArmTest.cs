using UnityEngine;

public class ArmTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== Arm Test Started ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"Parent: {transform.parent?.name ?? "None"}");
        
        var articulationBodies = GetComponentsInChildren<ArticulationBody>();
        Debug.Log($"Found {articulationBodies.Length} ArticulationBody components:");
        
        foreach (var ab in articulationBodies)
        {
            Debug.Log($"  - {ab.gameObject.name}");
            Debug.Log($"    jointType: {ab.jointType}");
            Debug.Log($"    isRoot: {ab.isRoot}");
            Debug.Log($"    useGravity: {ab.useGravity}");
        }
        
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Debug.Log($"Rigidbody: isKinematic={rb.isKinematic}, useGravity={rb.useGravity}");
        }
        else
        {
            Debug.Log("No Rigidbody found");
        }
        
        Transform root = transform.root;
        Debug.Log($"Root: {root.name}");
        Debug.Log("=== Test Complete ===");
    }
    
    void Update()
    {
        if (Time.frameCount % 60 == 0)
        {
            Transform root = transform.root;
            Debug.Log($"[{Time.time:F1}] Root={root.name} pos={root.position}, This={name} pos={transform.position}");
        }
    }
}

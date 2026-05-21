using UnityEngine;

public class CombineStaticMeshes : MonoBehaviour
{
    void Start()
    {
        // Combine all static meshes in children of this object
        StaticBatchingUtility.Combine(gameObject);
    }
}

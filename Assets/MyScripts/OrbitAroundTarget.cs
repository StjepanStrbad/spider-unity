using UnityEngine;

/// <summary>
/// Attach this script to the object you want to ORBIT.
/// Assign the target (the object to orbit around) in the Inspector.
/// </summary>
public class OrbitAroundTarget : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The object this GameObject will orbit around.")]
    public Transform target;

    [Header("Orbit Settings")]
    [Tooltip("Distance from the target.")]
    public float radius = 5f;

    [Tooltip("Orbit speed in degrees per second.")]
    public float orbitSpeed = 45f;

    [Tooltip("Starting angle offset in degrees (0 = positive X axis).")]
    public float startAngle = 0f;

    // Internal angle tracker
    private float currentAngle;

    private void Start()
    {
        currentAngle = startAngle;

        if (target == null)
        {
            Debug.LogWarning($"[OrbitAroundTarget] No target assigned on '{gameObject.name}'. Script disabled.");
            enabled = false;
        }
    }

    private void Update()
    {
        // Advance the angle
        currentAngle += orbitSpeed * Time.deltaTime;

        // Keep angle in [0, 360) to avoid float drift over time
        if (currentAngle >= 360f) currentAngle -= 360f;
        if (currentAngle < 0f) currentAngle += 360f;

        // Calculate new position on the horizontal circle around the target
        float rad = currentAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
        transform.position = target.position + offset;

        // Always face the target (only rotates on Y axis — horizontal look)
        Vector3 directionToTarget = target.position - transform.position;
        if (directionToTarget != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(directionToTarget);
            // Zero out X and Z so we only rotate horizontally
            // Add 180° to flip the forward axis toward the target
            lookRotation = Quaternion.Euler(0f, lookRotation.eulerAngles.y + 180f, 0f);
            transform.rotation = lookRotation;
        }
    }

    /// <summary>
    /// Visualise the orbit path in the Scene view for easy setup.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        Gizmos.color = Color.cyan;
        int segments = 64;
        Vector3 prev = target.position + new Vector3(Mathf.Cos(0f), 0f, Mathf.Sin(0f)) * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * 2f * Mathf.PI;
            Vector3 next = target.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        // Draw current radius line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(target.position, transform.position);
    }
}
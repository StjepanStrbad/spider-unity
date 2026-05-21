using UnityEngine;

public class PipeCircularMover : MonoBehaviour
{
    [Header("Pipe Settings")]
    public Transform pipeCenter;        // Assign the pipe's center axis transform
    public Vector3 pipeAxis = Vector3.forward; // Axis the pipe runs along

    [Header("Movement Settings")]
    public float radius = 2f;           // Radius of the pipe
    public float speed = 1f;            // How fast this saw orbits
    public bool clockwise = true;       // Direction of orbit
    [Range(0f, 360f)]
    public float startAngle = 0f;       // Starting position on circumference
    [Header("Position Along Pipe")]
    public float pipeOffset = 0f; // Move along the pipe axis
    private float currentAngle;

    void Start()
    {
        currentAngle = startAngle;
    }

    void Update()
    {
        // Increment angle based on speed and direction
        float direction = clockwise ? 1f : -1f;
        currentAngle += direction * speed * Time.deltaTime * 60f;

        // Keep angle within 0-360
        currentAngle = currentAngle % 360f;

        // Calculate position on circumference
        float angleRad = currentAngle * Mathf.Deg2Rad;

        // Build two axes perpendicular to the pipe axis
        Vector3 perpAxis1 = Vector3.Cross(pipeAxis, Vector3.up).normalized;
        if (perpAxis1.sqrMagnitude < 0.01f)
            perpAxis1 = Vector3.Cross(pipeAxis, Vector3.right).normalized;
        Vector3 perpAxis2 = Vector3.Cross(pipeAxis, perpAxis1).normalized;

        // Calculate the position on the circle
        Vector3 offset = (perpAxis1 * Mathf.Cos(angleRad) +
                         perpAxis2 * Mathf.Sin(angleRad)) * radius;

        // Move to position
        transform.position = pipeCenter.position + offset + pipeAxis.normalized * pipeOffset;

        // Rotate saw to face inward toward pipe center
        Vector3 inward = (pipeCenter.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(pipeAxis, inward);
    }
}
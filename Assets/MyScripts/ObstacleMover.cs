using UnityEngine;

public class ObstacleMover : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveDistance = 3f;   // How far up and down it travels
    public float moveSpeed = 1.5f;      // How fast it cycles

    // Starting position is remembered on spawn
    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // Sine wave goes between -1 and 1 smoothly forever
        // Multiplying by moveDistance sets how far it travels
        float offset = -Mathf.Abs(Mathf.Sin(Time.time * moveSpeed)) * moveDistance;

        transform.position = startPosition + Vector3.up * offset;
    }
}
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    // ── Inspector settings ───────────────────────────────
    [Header("Checkpoint Type")]
    public bool isFinalCheckpoint = false;  // Tick ONLY on the last checkpoint
    public int nextSceneIndex = -1;         // Set this if isFinalCheckpoint = true

    // ── Private state ────────────────────────────────────
    private bool activated = false;

    // ────────────────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (activated) return;
        if (!other.CompareTag("Player")) return;

        activated = true;

        // Tell the visual to update
        GetComponent<CheckpointVisual>()?.Activate();

        if (isFinalCheckpoint)
        {
            if (CheckpointManager.Instance != null)
                CheckpointManager.Instance.ReachFinalCheckpoint(nextSceneIndex);
        }
        else
        {
            if (CheckpointManager.Instance != null)
                CheckpointManager.Instance.SetRespawnPoint(transform.position);
        }
    }

    // ── Resets checkpoint so it can be re-activated ──────
    public void ResetCheckpoint()
    {
        activated = false;
        GetComponent<CheckpointVisual>()?.Deactivate();
    }
}
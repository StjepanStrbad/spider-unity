using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckpointManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────
    public static CheckpointManager Instance;

    // ── In-memory respawn state ──────────────────────────
    // These are NOT saved to disk — lost on game quit by design
    private Vector3 currentRespawnPoint;
    private bool hasCheckpoint = false;

    // ── Level Complete screen reference ─────────────────
    // Assigned at runtime by LevelCompleteUI when the scene loads
    [HideInInspector]
    public LevelCompleteUI levelCompleteUI;

    // ────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ── Called by normal mid-level Checkpoint objects ────
    public void SetRespawnPoint(Vector3 position)
    {
        currentRespawnPoint = position;
        hasCheckpoint = true;
        Debug.Log("[CheckpointManager] Respawn point set: " + position);
    }

    // ── Returns respawn position, falls back to scene start
    public Vector3 GetRespawnPoint(Vector3 fallback)
    {
        return hasCheckpoint ? currentRespawnPoint : fallback;
    }

    // ── Resets in-memory checkpoint when a new level loads
    public void ResetForNewLevel()
    {
        currentRespawnPoint = Vector3.zero;
        hasCheckpoint = false;
    }

    // ── Called by the FINAL checkpoint in a level ────────
    public void ReachFinalCheckpoint(int nextSceneIndex)
    {
        if (LevelStatsTracker.Instance != null)
            LevelStatsTracker.Instance.StopTimer();

        UnlockNextLevel(nextSceneIndex);

        // Fallback: find the UI in the scene if it never registered.
        if (levelCompleteUI == null)
            levelCompleteUI = FindFirstObjectByType<LevelCompleteUI>(FindObjectsInactive.Include);

        if (levelCompleteUI != null)
        {
            levelCompleteUI.SetNextSceneIndex(nextSceneIndex);
            levelCompleteUI.Show();
        }
        else
        {
            Debug.LogWarning("[CheckpointManager] No LevelCompleteUI in this scene. " +
                             "Add a UIDocument GameObject with LevelComplete.uxml AND a " +
                             "LevelCompleteUI component on it.");
        }
    }

    // ── Saves next level index to disk ───────────────────
    private void UnlockNextLevel(int nextSceneIndex)
    {
        GameSaveData data = SaveSystem.Load() ?? new GameSaveData();

        // Only update if this is genuinely a new furthest level
        if (nextSceneIndex > data.unlockedLevelIndex)
        {
            data.unlockedLevelIndex = nextSceneIndex;
            SaveSystem.Save(data);
            Debug.Log("[CheckpointManager] Level " + nextSceneIndex + " unlocked.");
        }
    }

    // ── Loads the next level ─────────────────────────────
    // Called by the Level Complete screen's Enter Next Level button
    public void LoadNextLevel(int nextSceneIndex)
    {
        ResetForNewLevel();
        LoadingScreen.Load(nextSceneIndex);
    }
}
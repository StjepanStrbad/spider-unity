using UnityEngine;

public class LevelStatsTracker : MonoBehaviour
{
    // ── Singleton so any script can reach it easily ─────
    public static LevelStatsTracker Instance;

    // ── Live stats for this level session ───────────────
    private float levelTimer = 0f;
    private bool timerRunning = false;
    private int levelDeaths = 0;

    // ────────────────────────────────────────────────────
    private void Awake()
    {
        // Only one tracker per scene
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Timer starts the moment the level loads
        StartTimer();
    }

    private void Update()
    {
        // Tick the timer every frame while it's running
        if (timerRunning)
            levelTimer += Time.deltaTime;
    }


    public void StartTimer()
    {
        levelTimer = 0f;
        timerRunning = true;
    }

    public void StopTimer()
    {
        timerRunning = false;
    }

    
    public void RegisterDeath()
    {
        levelDeaths++;
    }

    // ── Getters ──────────────────────────────────────────

    public int GetLevelDeaths()
    {
        return levelDeaths;
    }

    public float GetLevelTimeRaw()
    {
        return levelTimer;
    }

    // Returns time formatted as  2:07  or  14:32
    public string GetLevelTimeFormatted()
    {
        int minutes = Mathf.FloorToInt(levelTimer / 60f);
        int seconds = Mathf.FloorToInt(levelTimer % 60f);
        return string.Format("{0}:{1:00}", minutes, seconds);
    }
}
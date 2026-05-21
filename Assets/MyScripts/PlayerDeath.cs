using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class PlayerDeath : MonoBehaviour
{
    // ── Inspector references ─────────────────────────────
    [Header("Death Screen UI")]
    [Tooltip("UIDocument pointing at DeathScreen.uxml. Place on its own GameObject in the scene.")]
    public UIDocument deathScreenDoc;

    [Header("Explosion")]
    public GameObject explosionPrefab;
    public float explosionScale = 2f;
    public float explosionForwardOffset = 1f;

    [Header("Timing")]
    public float explosionDuration = 0.1f;
    public float respawnDelay = 0.2f;

    // ── Private state ────────────────────────────────────
    private bool isDead = false;
    private Vector3 sceneStartPosition;

    private VisualElement _deathRoot;
    private Label _totalDeathsLabel;
    private Button _respawnBtn, _restartBtn, _menuBtn;

    // ────────────────────────────────────────────────────
    private void Start()
    {
        sceneStartPosition = transform.position;
        WireDeathScreen();

        if (CheckpointManager.Instance != null)
            CheckpointManager.Instance.ResetForNewLevel();
    }

    private void OnDestroy()
    {
        if (_respawnBtn != null) _respawnBtn.clicked -= OnRespawnAtCheckpoint;
        if (_restartBtn != null) _restartBtn.clicked -= OnRestartLevel;
        if (_menuBtn    != null) _menuBtn.clicked    -= OnMainMenu;
    }

    private void WireDeathScreen()
    {
        if (deathScreenDoc == null)
        {
            Debug.LogWarning("[PlayerDeath] Death Screen Doc field is empty — " +
                             "drag the DeathScreenUI GameObject into it.");
            return;
        }

        var docRoot = deathScreenDoc.rootVisualElement;
        if (docRoot == null)
        {
            Debug.LogError("[PlayerDeath] DeathScreenUI has no rootVisualElement. " +
                           "Check that its UIDocument Source Asset points to DeathScreen.uxml.");
            return;
        }

        _deathRoot        = docRoot.Q("root") ?? docRoot;
        _totalDeathsLabel = docRoot.Q<Label>("TotalDeathsValue");
        _respawnBtn       = docRoot.Q<Button>("RespawnButton");
        _restartBtn       = docRoot.Q<Button>("RestartButton");
        _menuBtn          = docRoot.Q<Button>("MainMenuButton");

        if (_respawnBtn != null) _respawnBtn.clicked += OnRespawnAtCheckpoint;
        if (_restartBtn != null) _restartBtn.clicked += OnRestartLevel;
        if (_menuBtn    != null) _menuBtn.clicked    += OnMainMenu;

        if (_respawnBtn == null || _restartBtn == null || _menuBtn == null ||
            _totalDeathsLabel == null)
        {
            Debug.LogWarning(
                $"[PlayerDeath] Some UI elements not found in DeathScreen UXML. " +
                $"respawn={(_respawnBtn != null)}, restart={(_restartBtn != null)}, " +
                $"menu={(_menuBtn != null)}, deaths={(_totalDeathsLabel != null)}. " +
                $"Check that the UIDocument's Source Asset is DeathScreen.uxml.");
        }
        else
        {
            Debug.Log("[PlayerDeath] Death screen wired in scene '" + gameObject.scene.name + "'.");
        }
    }

    // ── Death trigger ────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DeathZone"))
            Die();
    }

    // ── Core death sequence ──────────────────────────────
    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (LevelStatsTracker.Instance != null)
            LevelStatsTracker.Instance.RegisterDeath();

        RegisterDeathToSaveFile();
        SetSpiderVisible(false);

        SimpleArcJump arcJump = GetComponent<SimpleArcJump>();
        if (arcJump != null) arcJump.ResetJump();

        GetComponent<GroundStick>().enabled = false;

        SpawnExplosion();
        StartCoroutine(FreezeAfterExplosion());
    }

    private void RegisterDeathToSaveFile()
    {
        GameSaveData data = SaveSystem.Load() ?? new GameSaveData();
        data.totalDeaths++;
        SaveSystem.Save(data);
        Debug.Log("[PlayerDeath] Total deaths: " + data.totalDeaths);
    }

    private void SpawnExplosion()
    {
        if (explosionPrefab == null) return;

        Camera mainCam = Camera.main;
        Vector3 spawnPos = transform.position
            + mainCam.transform.forward * explosionForwardOffset;

        GameObject explosion = Instantiate(
            explosionPrefab, spawnPos, Quaternion.identity);
        explosion.transform.localScale *= explosionScale;
    }

    private IEnumerator FreezeAfterExplosion()
    {
        yield return new WaitForSecondsRealtime(explosionDuration);

        Time.timeScale = 0f;

        if (_totalDeathsLabel != null)
        {
            GameSaveData data = SaveSystem.Load();
            _totalDeathsLabel.text = data != null ? data.totalDeaths.ToString() : "0";
        }

        if (_deathRoot != null) _deathRoot.RemoveFromClassList("hidden");
    }

    // ════════════════════════════════════════════════════
    // BUTTON CALLBACKS
    // ════════════════════════════════════════════════════

    public void OnRespawnAtCheckpoint()
    {
        Time.timeScale = 1f;
        if (_deathRoot != null) _deathRoot.AddToClassList("hidden");
        StartCoroutine(DelayedRespawn());
    }

    public void OnRestartLevel()
    {
        Time.timeScale = 1f;
        LoadingScreen.Load(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnMainMenu()
    {
        Time.timeScale = 1f;
        LoadingScreen.Load(0);
    }

    // ── Respawn sequence ─────────────────────────────────
    private IEnumerator DelayedRespawn()
    {
        // Use real-time so a leftover Time.timeScale = 0 cannot deadlock the coroutine.
        yield return new WaitForSecondsRealtime(respawnDelay);

        Vector3 respawnPos = (CheckpointManager.Instance != null)
            ? CheckpointManager.Instance.GetRespawnPoint(sceneStartPosition)
            : sceneStartPosition;

        // Stop any momentum first so physics can't drag us back.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Move root, rigidbody, and the GroundStick body together.
        transform.position = respawnPos;
        if (rb != null) rb.position = respawnPos;

        GroundStick gs = GetComponent<GroundStick>();
        if (gs != null) gs.Teleport(respawnPos);

        SetSpiderVisible(true);

        // One physics tick so any pending forces are flushed before we re-enable controls.
        yield return new WaitForFixedUpdate();

        // Re-assert position — covers the edge case of something nudging us mid-tick.
        transform.position = respawnPos;
        if (rb != null)
        {
            rb.position        = respawnPos;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (gs != null) gs.enabled = true;

        SimpleArcJump arcJump = GetComponent<SimpleArcJump>();
        if (arcJump != null) arcJump.ResetJump();

        isDead = false;
    }

    private void SetSpiderVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
            r.enabled = visible;
    }
}

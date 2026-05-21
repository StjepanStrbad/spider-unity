using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit controller for the Level Complete overlay.
/// Attach to a GameObject that has a UIDocument pointing at LevelComplete.uxml.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LevelCompleteUI : MonoBehaviour
{
    private VisualElement _visualRoot;
    private Label _deathsLabel;
    private Label _timeLabel;
    private Button _nextBtn, _replayBtn, _menuBtn;
    private int _cachedNextSceneIndex = -1;

    private void Start()
    {
        // Register first — so the final checkpoint can call Show() on us
        // even if UI setup below errors out for any reason.
        if (CheckpointManager.Instance != null)
            CheckpointManager.Instance.levelCompleteUI = this;
        else
            Debug.LogWarning("[LevelCompleteUI] No CheckpointManager found. " +
                             "Start the game from MainMenu, or make sure LevelOne has a " +
                             "CheckpointManager GameObject.");

        // Setup runs in Start (not OnEnable) so UIDocument has finished
        // building the visual tree before we query into it.
        var doc = GetComponent<UIDocument>();
        var docRoot = doc.rootVisualElement;
        if (docRoot == null)
        {
            Debug.LogError("[LevelCompleteUI] UIDocument has no rootVisualElement. " +
                           "Check that Source Asset on the UIDocument points to LevelComplete.uxml.");
            return;
        }

        _visualRoot  = docRoot.Q("root") ?? docRoot;
        _deathsLabel = docRoot.Q<Label>("DeathsValue");
        _timeLabel   = docRoot.Q<Label>("TimeValue");
        _nextBtn     = docRoot.Q<Button>("NextLevelButton");
        _replayBtn   = docRoot.Q<Button>("ReplayButton");
        _menuBtn     = docRoot.Q<Button>("MainMenuButton");

        if (_nextBtn   != null) _nextBtn.clicked   += OnEnterNextLevel;
        if (_replayBtn != null) _replayBtn.clicked += OnReplayLevel;
        if (_menuBtn   != null) _menuBtn.clicked   += OnMainMenu;

        if (_nextBtn == null || _replayBtn == null || _menuBtn == null ||
            _deathsLabel == null || _timeLabel == null)
        {
            Debug.LogWarning(
                $"[LevelCompleteUI] Some UI elements not found in UXML. " +
                $"next={(_nextBtn != null)}, replay={(_replayBtn != null)}, menu={(_menuBtn != null)}, " +
                $"deaths={(_deathsLabel != null)}, time={(_timeLabel != null)}. " +
                $"Check that UIDocument's Source Asset is LevelComplete.uxml.");
        }
        else
        {
            Debug.Log("[LevelCompleteUI] Ready in scene '" + gameObject.scene.name + "'.");
        }
    }

    private void OnDisable()
    {
        if (_nextBtn   != null) _nextBtn.clicked   -= OnEnterNextLevel;
        if (_replayBtn != null) _replayBtn.clicked -= OnReplayLevel;
        if (_menuBtn   != null) _menuBtn.clicked   -= OnMainMenu;
    }

    // ── Called by CheckpointManager.ReachFinalCheckpoint()
    public void Show()
    {
        if (LevelStatsTracker.Instance != null)
        {
            if (_deathsLabel != null)
                _deathsLabel.text = LevelStatsTracker.Instance.GetLevelDeaths().ToString();
            if (_timeLabel != null)
                _timeLabel.text = LevelStatsTracker.Instance.GetLevelTimeFormatted();
        }

        if (_visualRoot != null) _visualRoot.RemoveFromClassList("hidden");
        Time.timeScale = 0f;
    }

    public void SetNextSceneIndex(int index) => _cachedNextSceneIndex = index;

    // ── Button callbacks ─────────────────────────────────

    public void OnEnterNextLevel()
    {
        Time.timeScale = 1f;
        if (CheckpointManager.Instance != null)
            CheckpointManager.Instance.LoadNextLevel(_cachedNextSceneIndex);
        else
            Debug.LogWarning("[LevelCompleteUI] No CheckpointManager found.");
    }

    public void OnReplayLevel()
    {
        Time.timeScale = 1f;
        LoadingScreen.Load(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnMainMenu()
    {
        Time.timeScale = 1f;
        LoadingScreen.Load(0);
    }
}

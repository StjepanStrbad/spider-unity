using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Persistent loading overlay. Place ONE GameObject with this script
/// and a UIDocument (Source Asset = LoadingScreen.uxml) in the MainMenu
/// scene. It DontDestroyOnLoad's itself, so every subsequent scene
/// transition routed through LoadingScreen.Load(...) shows the overlay
/// until the new scene is fully ready.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance;

    [Tooltip("The loading overlay stays visible at least this long, so it never just flashes on fast loads.")]
    public float minVisibleSeconds = 0.6f;

    [Tooltip("If the panel looks unstyled at runtime, drag LoadingScreen.uss into this field.")]
    public StyleSheet fallbackStyleSheet;

    private VisualElement _root;
    private VisualElement _progressFill;
    private Label _progressLabel;
    private Label _statusLabel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        var doc = GetComponent<UIDocument>();
        var docRoot = doc != null ? doc.rootVisualElement : null;
        if (docRoot == null)
        {
            Debug.LogError("[LoadingScreen] UIDocument has no rootVisualElement. " +
                           "Check that Source Asset points to LoadingScreen.uxml.");
            return;
        }

        _root          = docRoot.Q("root") ?? docRoot;
        _progressFill  = docRoot.Q("ProgressFill");
        _progressLabel = docRoot.Q<Label>("ProgressLabel");
        _statusLabel   = docRoot.Q<Label>("StatusText");

        // Defensive: if the UXML didn't load any stylesheet, apply the fallback.
        if (docRoot.styleSheets.count == 0)
        {
            if (fallbackStyleSheet != null)
            {
                docRoot.styleSheets.Add(fallbackStyleSheet);
            }
            else
            {
                Debug.LogWarning("[LoadingScreen] No stylesheet attached — the panel will look unstyled. " +
                                 "Drag LoadingScreen.uss into the 'Fallback Style Sheet' field on this component.");
            }
        }

        Hide();
    }

    /// <summary>Static entry point used everywhere — handles a missing instance gracefully.</summary>
    public static void Load(int sceneIndex)
    {
        if (Instance != null)
            Instance.StartCoroutine(Instance.LoadRoutine(sceneIndex));
        else
            SceneManager.LoadScene(sceneIndex);
    }

    private IEnumerator LoadRoutine(int sceneIndex)
    {
        Time.timeScale = 1f;          // make sure the coroutine ticks
        Show();
        SetProgress(0f);
        yield return null;            // let the overlay paint a frame

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneIndex);
        op.allowSceneActivation = false;

        float startTime = Time.realtimeSinceStartup;

        // Unity caps op.progress at 0.9 until allowSceneActivation is true.
        while (op.progress < 0.9f)
        {
            SetProgress(op.progress / 0.9f * 0.9f);
            yield return null;
        }

        // Keep the overlay visible for at least minVisibleSeconds so it's not a single-frame flash.
        while (Time.realtimeSinceStartup - startTime < minVisibleSeconds)
        {
            SetProgress(Mathf.Lerp(0.9f, 0.99f,
                (Time.realtimeSinceStartup - startTime) / minVisibleSeconds));
            yield return null;
        }

        SetProgress(1f);
        yield return new WaitForSecondsRealtime(0.1f);

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // Give the new scene a frame to wire itself up, then hide.
        yield return null;
        Hide();
    }

    private void Show()
    {
        // Inline style — works even if the .hidden CSS rule never loaded.
        if (_root != null) _root.style.display = DisplayStyle.Flex;
    }

    private void Hide()
    {
        if (_root != null) _root.style.display = DisplayStyle.None;
    }

    private void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (_progressFill != null)
            _progressFill.style.width = new Length(t * 100f, LengthUnit.Percent);
        if (_progressLabel != null)
            _progressLabel.text = Mathf.RoundToInt(t * 100f) + "%";
    }
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit controller for the Main Menu.
/// Attach to a GameObject that has a UIDocument pointing at MainMenu.uxml.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuUIToolkit : MonoBehaviour
{
    public enum MenuButton { NewGame = 0, Continue = 1, Quit = 2 }

    [Header("Scene indices")]
    [Tooltip("Scene index loaded when New Game is pressed.")]
    public int newGameSceneIndex = 1;

    public event Action<MenuButton> HoverEnter;
    public event Action<MenuButton> HoverExit;

    private Button _newGameBtn;
    private Button _continueBtn;
    private Button _quitBtn;
    private Label _status;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[MainMenuUIToolkit] UIDocument has no rootVisualElement. " +
                           "Make sure Source Asset of UIDocument points to MainMenu.uxml.");
            return;
        }

        _newGameBtn  = root.Q<Button>("NewGameButton");
        _continueBtn = root.Q<Button>("ContinueButton");
        _quitBtn     = root.Q<Button>("QuitButton");
        _status      = root.Q<Label>("StatusText");

        if (_newGameBtn  != null) _newGameBtn .clicked += OnNewGame;
        if (_continueBtn != null) _continueBtn.clicked += OnContinue;
        if (_quitBtn     != null) _quitBtn    .clicked += OnQuit;

        Wire(_newGameBtn,  MenuButton.NewGame,  "BOOT  >>  NEW SESSION");
        Wire(_continueBtn, MenuButton.Continue, "RESUME  >>  LAST CHECKPOINT");
        Wire(_quitBtn,     MenuButton.Quit,     "TERMINATE  >>  SHUTDOWN");

        if (_continueBtn != null) _continueBtn.SetEnabled(SaveSystem.HasSave());
    }

    private void OnDisable()
    {
        if (_newGameBtn  != null) _newGameBtn .clicked -= OnNewGame;
        if (_continueBtn != null) _continueBtn.clicked -= OnContinue;
        if (_quitBtn     != null) _quitBtn    .clicked -= OnQuit;
    }

    public Button GetButton(MenuButton id) => id switch
    {
        MenuButton.NewGame  => _newGameBtn,
        MenuButton.Continue => _continueBtn,
        MenuButton.Quit     => _quitBtn,
        _ => null
    };

    private void Wire(Button btn, MenuButton id, string statusText)
    {
        if (btn == null) return;

        btn.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (_status != null) _status.text = statusText;
            HoverEnter?.Invoke(id);
        });
        btn.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            if (_status != null) _status.text = "AWAITING INPUT";
            HoverExit?.Invoke(id);
        });
        btn.RegisterCallback<FocusInEvent>(_ =>
        {
            if (_status != null) _status.text = statusText;
            HoverEnter?.Invoke(id);
        });
        btn.RegisterCallback<FocusOutEvent>(_ =>
        {
            if (_status != null) _status.text = "AWAITING INPUT";
            HoverExit?.Invoke(id);
        });
    }

    public void OnNewGame()
    {
        SaveSystem.DeleteSave();
        LoadingScreen.Load(newGameSceneIndex);
    }

    public void OnContinue()
    {
        GameSaveData data = SaveSystem.Load();
        if (data == null) return;
        LoadingScreen.Load(data.unlockedLevelIndex);
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

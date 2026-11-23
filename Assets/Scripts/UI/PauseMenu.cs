using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause menu: Esc toggles it. While paused the game is frozen and the music gets the low pass filter.
/// The options panel (volumes) is the same one used by the main menu (OptionsMenu).
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Panels")]
    [SerializeField] private GameObject pausePanel;     // dark background, shown while paused
    [SerializeField] private GameObject mainContent;    // title + Resume / Options / Main menu buttons
    [SerializeField] private GameObject optionsPanel;   // volume sliders

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button backButton;         // inside the options panel
    [SerializeField] private Button pauseButton;        // HUD button (top right) that opens the pause menu
    [SerializeField] private Button helpButton;         // "?" on the pause page: opens the help pop-up

    [Header("Help")]
    [SerializeField] private HelpPopup helpPopup;       // "How to play" pop-up (also shown when a game starts)

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
        if (backButton != null) backButton.onClick.AddListener(CloseOptions);
        if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);
        if (helpButton != null) helpButton.onClick.AddListener(OpenHelp);
    }

    private void Start()
    {
        SetPaused(false);

        // Pressing Play in the main menu: explain the controls before the level starts
        if (HelpPopup.ShowOnNextLevelStart && helpPopup != null)
        {
            HelpPopup.ShowOnNextLevelStart = false;
            helpPopup.Open();
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (helpPopup != null && helpPopup.IsOpen)
            helpPopup.Close();  // Esc closes the help pop-up first
        else if (IsPaused && optionsPanel != null && optionsPanel.activeSelf)
            CloseOptions();     // Esc inside options goes back to the pause menu
        else
            SetPaused(!IsPaused);
    }

    public void Resume() => SetPaused(false);
    public void Pause() => SetPaused(true);
    public void TogglePause() => SetPaused(!IsPaused);

    public void OpenOptions()
    {
        if (mainContent != null) mainContent.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (mainContent != null) mainContent.SetActive(true);
    }

    /// <summary>Shows the help pop-up over the pause menu; closing it goes back to the pause menu.</summary>
    public void OpenHelp()
    {
        if (helpPopup == null) return;
        if (pausePanel != null) pausePanel.SetActive(false);
        helpPopup.Open(() =>
        {
            if (IsPaused && pausePanel != null) pausePanel.SetActive(true);
        });
    }

    public void GoToMainMenu()
    {
        SetPaused(false);
        SceneTransition.Load(mainMenuScene);
    }

    private void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        if (pausePanel != null) pausePanel.SetActive(paused);
        CloseOptions();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetLowPass(paused);
            AudioManager.Instance.SetSfxPaused(paused);
        }
    }

    private void OnDestroy()
    {
        // Never leave the game frozen or muffled when the scene changes
        Time.timeScale = 1f;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetLowPass(false);
            AudioManager.Instance.SetSfxPaused(false);
        }
    }
}

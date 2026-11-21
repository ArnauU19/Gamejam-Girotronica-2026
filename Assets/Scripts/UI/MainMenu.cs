using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu: Play / Options / Quit. The options panel itself is handled by OptionsMenu.
/// References are assigned by Girotronica > Build Main Menu In Open Scene.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Scene loaded by the Play button. It must be in the build scene list.")]
    public string firstLevelScene = "Level1";

    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject optionsPanel;

    [Header("Buttons")]
    public Button playButton;
    public Button optionsButton;
    public Button quitButton;
    public Button backButton;

    private void Awake()
    {
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
        if (backButton != null) backButton.onClick.AddListener(CloseOptions);
    }

    private void Start()
    {
        CloseOptions();
        if (AudioManager.Instance != null) AudioManager.Instance.playMainMenuSong();
    }

    private void Update()
    {
        // Esc closes the options panel
        if (optionsPanel != null && optionsPanel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseOptions();
        }
    }

    public void Play()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.playLevelSong();
        SceneManager.LoadScene(firstLevelScene);
    }

    public void OpenOptions()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// End scene ("Gràcies per jugar"): the button goes back to the main menu with the paper transition.
/// Built by Girotronica > UI > Build End Credits UI (Open Scene).
/// </summary>
public class EndCreditsMenu : MonoBehaviour
{
    public string mainMenuScene = "MainMenu";
    public Button menuButton;

    void Awake()
    {
        if (menuButton != null) menuButton.onClick.AddListener(GoToMainMenu);
    }

    void Start()
    {
        Time.timeScale = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.playMainMenuSong();
    }

    public void GoToMainMenu() => SceneTransition.Load(mainMenuScene);
}

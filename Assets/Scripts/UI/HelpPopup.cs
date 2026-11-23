using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "How to play" pop-up: a paper sheet over a dark background with a few short pages
/// (a looping clip + one sentence each) and arrows to move between them.
///  - Opens by itself at the start of the first level when the player presses Play in the main menu.
///  - Opens from the "?" button of the pause menu.
/// While it is open the game is frozen (Time.timeScale = 0). Lives in the PauseMenu prefab;
/// built by Girotronica > UI > Add Help + Language UI.
/// </summary>
public class HelpPopup : MonoBehaviour
{
    /// <summary>Set by the main menu Play button: the next level shows the pop-up when it starts.</summary>
    public static bool ShowOnNextLevelStart;

    [Tooltip("Root shown / hidden (dark background + sheet).")]
    public GameObject panel;
    [Tooltip("One object per page, in order.")]
    public GameObject[] pages;
    public Button previousButton;
    public Button nextButton;
    public Button closeButton;
    [Tooltip("Big button shown on the last page.")]
    public Button gotItButton;
    [Tooltip("\"1 / 4\" page counter (optional).")]
    public Text pageCounter;

    public bool IsOpen => panel != null && panel.activeSelf;

    int page;
    float timeScaleBefore = 1f;
    Action onClosed;

    void Awake()
    {
        if (previousButton != null) previousButton.onClick.AddListener(() => ShowPage(page - 1));
        if (nextButton != null) nextButton.onClick.AddListener(() => ShowPage(page + 1));
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (gotItButton != null) gotItButton.onClick.AddListener(Close);
    }

    /// <summary>Shows the pop-up from the first page and freezes the game. onClosed runs when it is closed.</summary>
    public void Open(Action whenClosed = null)
    {
        if (panel == null) return;
        onClosed = whenClosed;
        if (!panel.activeSelf) timeScaleBefore = Time.timeScale;
        Time.timeScale = 0f;
        panel.SetActive(true);
        ShowPage(0);
    }

    public void Close()
    {
        if (!IsOpen) return;
        panel.SetActive(false);
        Time.timeScale = timeScaleBefore;
        var callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    public void ShowPage(int index)
    {
        int n = pages != null ? pages.Length : 0;
        if (n == 0) return;
        page = Mathf.Clamp(index, 0, n - 1);
        for (int i = 0; i < n; i++)
            if (pages[i] != null) pages[i].SetActive(i == page);

        bool last = page == n - 1;
        if (previousButton != null) SetButtonVisible(previousButton, page > 0);
        if (nextButton != null) SetButtonVisible(nextButton, !last);
        if (gotItButton != null) SetButtonVisible(gotItButton, last);
        if (pageCounter != null) pageCounter.text = (page + 1) + " / " + n;
    }

    // The Button is on the "Frame" child of the wobbling container that also holds the halftone shadow
    static void SetButtonVisible(Button b, bool visible)
    {
        var root = b.transform.parent != null && b.transform.parent.GetComponent<Canvas>() == null ? b.transform.parent : b.transform;
        root.gameObject.SetActive(visible);
    }

    void OnDestroy()
    {
        if (IsOpen) Time.timeScale = 1f;
    }
}

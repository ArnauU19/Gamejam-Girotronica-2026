using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One per level scene.
///  - Holds the character during a countdown (the player can already place rulers), then releases it.
///  - When the goal is reached, loads the next scene of the build scene list (File > Build Profiles > Scene List).
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Countdown")]
    [Min(0f)] public float countdownSeconds = 3f;
    public Text countdownText;

    [Header("Level end")]
    [Tooltip("Seconds between reaching the goal and loading the next scene (only if the character has no Animator; " +
             "otherwise the next scene loads when the Win animation finished its loops).")]
    public float nextLevelDelay = 1.5f;
    [Tooltip("Safety limit (seconds) when waiting for the Win animation loops.")]
    public float maxWinWait = 10f;

    [Header("References")]
    [Tooltip("If empty, the SurfaceWalker of the scene is used.")]
    public SurfaceWalker walker;

    /// <summary>True while the character must wait for the countdown.</summary>
    public bool HoldsCharacter => !LevelStarted;
    public bool LevelStarted { get; private set; }
    public bool LevelCompleted { get; private set; }

    public event Action OnLevelStarted;
    public event Action OnLevelCompleted;

    void Awake()
    {
        Instance = this;
        if (walker == null) walker = FindAnyObjectByType<SurfaceWalker>();
        SetText("");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    IEnumerator Start()
    {
        // Countdown: 3, 2, 1
        float remaining = countdownSeconds;
        while (remaining > 0f)
        {
            SetText(Mathf.CeilToInt(remaining).ToString());
            yield return null;
            remaining -= Time.deltaTime;
        }

        LevelStarted = true;
        Debug.Log("[LevelManager] Countdown finished, level started.", this);
        if (walker != null) walker.BeginLevel();
        OnLevelStarted?.Invoke();

        SetText("");   // no "go" message: the text just disappears when the countdown ends
    }

    /// <summary>Called by LevelGoal when the character reaches the end.</summary>
    public void CompleteLevel()
    {
        if (LevelCompleted) return;
        LevelCompleted = true;   // no "level complete" message, the Win animation plays and the next level loads
        OnLevelCompleted?.Invoke();
        StartCoroutine(LoadNextScene());
    }

    IEnumerator LoadNextScene()
    {
        // Wait for the Win animation to loop its times (CharacterAnimator.winLoops); without an
        // animated character, just wait nextLevelDelay.
        var anim = walker != null ? walker.GetComponent<CharacterAnimator>() : null;
        if (anim != null && anim.HasAnimator)
        {
            float timeout = Time.time + maxWinWait;
            yield return new WaitUntil(() => anim.WinLoopsDone || Time.time > timeout);
        }
        else
        {
            yield return new WaitForSeconds(nextLevelDelay);
        }

        int current = SceneManager.GetActiveScene().buildIndex;
        if (current < 0)
        {
            Debug.LogWarning("[LevelManager] This scene is not in the build scene list, cannot know the next level.", this);
            yield break;
        }

        int next = current + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log($"[LevelManager] Loading next scene (build index {next}).", this);
            SceneTransition.Load(next);
        }
        else
        {
            Debug.Log("[LevelManager] Last level of the scene list reached.", this);
        }
    }

    void SetText(string value)
    {
        if (countdownText == null) return;
        countdownText.text = value;
        countdownText.enabled = !string.IsNullOrEmpty(value);
    }
}

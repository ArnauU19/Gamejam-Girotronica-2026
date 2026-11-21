using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Paper transition between scenes: a crumpled sheet falls and unfolds over the screen (the paper frames
/// of Art/Effects and extras), the next scene loads behind it, then the sheet crumples away again.
///
/// Use SceneTransition.Load(name) / Load(buildIndex) instead of SceneManager.LoadScene. The prefab lives
/// in Resources/SceneTransition (made by Girotronica > UI > Apply Notebook UI). Without it, scenes just load.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public CanvasGroup group;
    [Tooltip("Paper-coloured full screen image that hides the scene while it loads.")]
    public Image cover;
    [Tooltip("Image that shows the crumpled paper frames.")]
    public Image paper;
    [Tooltip("From a small paper ball to the unfolded sheet.")]
    public Sprite[] frames;
    public float frameTime = 0.055f;
    public float revealTime = 0.45f;

    static SceneTransition instance;
    bool busy;

    public static bool IsBusy => instance != null && instance.busy;

    public static void Load(string sceneName) =>
        Run(() => SceneManager.LoadSceneAsync(sceneName), () => SceneManager.LoadScene(sceneName));

    public static void Load(int buildIndex) =>
        Run(() => SceneManager.LoadSceneAsync(buildIndex), () => SceneManager.LoadScene(buildIndex));

    static void Run(Func<AsyncOperation> loadAsync, Action loadNow)
    {
        var t = GetInstance();
        if (t == null) { loadNow(); return; }
        if (t.busy) return;
        t.StartCoroutine(t.Play(loadAsync));
    }

    static SceneTransition GetInstance()
    {
        if (instance != null) return instance;
        var prefab = Resources.Load<SceneTransition>("SceneTransition");
        if (prefab == null) return null;
        instance = Instantiate(prefab);
        instance.name = "SceneTransition";
        DontDestroyOnLoad(instance.gameObject);
        instance.Show(false);
        return instance;
    }

    void Show(bool on)
    {
        if (group != null)
        {
            group.alpha = on ? 1f : 0f;
            group.blocksRaycasts = on;
        }
        if (paper != null) paper.enabled = on;
    }

    void SetCover(float a)
    {
        if (cover == null) return;
        var c = cover.color;
        c.a = a;
        cover.color = c;
    }

    void SetPaper(float framePos, float alpha)
    {
        if (paper == null || frames == null || frames.Length == 0) return;
        int i = Mathf.Clamp(Mathf.RoundToInt(framePos), 0, frames.Length - 1);
        paper.sprite = frames[i];
        var c = paper.color;
        c.a = alpha;
        paper.color = c;
    }

    IEnumerator Play(Func<AsyncOperation> loadAsync)
    {
        busy = true;
        Show(true);
        SetCover(0f);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(Sfx.Whoosh);

        // Cover: the paper ball falls and unfolds, the screen fades to paper during the last frames.
        int n = frames != null ? frames.Length : 0;
        for (int i = 0; i < n; i++)
        {
            SetPaper(i, 1f);
            SetCover(Mathf.Clamp01((i - (n - 4)) / 3f));
            yield return new WaitForSecondsRealtime(frameTime);
        }
        SetCover(1f);
        yield return null;

        Time.timeScale = 1f;
        var op = loadAsync();
        while (op != null && !op.isDone) yield return null;
        yield return null;

        // Reveal: the sheet crumples back while the paper cover fades away.
        for (float t = 0f; t < revealTime; t += Time.unscaledDeltaTime)
        {
            float f = t / revealTime;
            SetCover(1f - f);
            SetPaper((n - 1) * (1f - f), 1f - f * f);
            yield return null;
        }

        Show(false);
        busy = false;
    }
}

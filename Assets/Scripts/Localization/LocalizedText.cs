using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the text of a Loc key on the Text of this object and updates it when the language changes.
/// Texts that are wider than their box in some language are shrunk a little so they still fit.
/// </summary>
[RequireComponent(typeof(Text))]
public class LocalizedText : MonoBehaviour
{
    public string key;
    [Tooltip("Optional value for {0} (e.g. the level number).")]
    public string arg;
    [Tooltip("Shrink the font when the text is wider than its box (only for single-line texts).")]
    public bool shrinkToFit = true;
    [Range(0.3f, 1f)] public float minScale = 0.6f;

    Text text;
    int baseFontSize;

    void Init()
    {
        if (text != null) return;
        text = GetComponent<Text>();
        baseFontSize = text.fontSize;
    }

    void OnEnable()
    {
        Loc.Changed += Apply;
        Apply();
    }

    void OnDisable() => Loc.Changed -= Apply;

    public void Apply()
    {
        Init();
        if (string.IsNullOrEmpty(key)) return;
        text.text = string.IsNullOrEmpty(arg) ? Loc.Get(key) : Loc.Format(key, arg);
        Fit();
    }

    void Fit()
    {
        text.fontSize = baseFontSize;
        if (!shrinkToFit || text.resizeTextForBestFit || text.horizontalOverflow != HorizontalWrapMode.Overflow) return;
        float width = text.rectTransform.rect.width;
        if (width <= 1f) return;
        int min = Mathf.Max(8, Mathf.RoundToInt(baseFontSize * minScale));
        while (text.fontSize > min && text.preferredWidth > width) text.fontSize--;
    }
}

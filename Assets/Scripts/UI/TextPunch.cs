using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Countdown / level messages: every time the text changes it "punches" (elastic scale), and a speech
/// bubble behind it is shown only while the text is visible, sized for short (numbers) or long messages.
/// </summary>
[RequireComponent(typeof(Text))]
public class TextPunch : MonoBehaviour
{
    [Tooltip("Speech bubble drawn behind the text (optional).")]
    public Image bubble;
    public Vector2 shortBubbleSize = new Vector2(460f, 380f);
    public Vector2 longBubbleSize = new Vector2(1180f, 440f);
    [Tooltip("Texts up to this length use the short bubble.")]
    public int shortLength = 4;

    public float punch = 0.45f;
    public float damping = 7f;
    public float frequency = 16f;

    Text text;
    string last;
    float time = 10f;

    void Awake() => text = GetComponent<Text>();

    void LateUpdate()
    {
        string current = text.text;
        if (current != last)
        {
            last = current;
            if (!string.IsNullOrEmpty(current)) time = 0f;
            if (bubble != null)
                bubble.rectTransform.sizeDelta = current != null && current.Length <= shortLength ? shortBubbleSize : longBubbleSize;
        }

        bool visible = text.enabled && !string.IsNullOrEmpty(current);
        if (bubble != null && bubble.enabled != visible) bubble.enabled = visible;

        time += Time.unscaledDeltaTime;
        float s = 1f + punch * Mathf.Exp(-damping * time) * Mathf.Cos(frequency * time);
        transform.localScale = new Vector3(s, s, 1f);
        if (bubble != null)
        {
            float b = 1f + 0.5f * (s - 1f);
            bubble.transform.localScale = new Vector3(b, b, 1f);
        }
    }
}

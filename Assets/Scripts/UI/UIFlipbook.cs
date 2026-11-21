using UnityEngine;
using UnityEngine.UI;

/// <summary>Plays a list of sprites on a UI Image in a loop (unscaled time). Used for the running girl in the main menu.</summary>
[RequireComponent(typeof(Image))]
public class UIFlipbook : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 10f;

    Image image;
    float time;

    void Awake() => image = GetComponent<Image>();

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        time += Time.unscaledDeltaTime;
        int i = Mathf.FloorToInt(time * framesPerSecond) % frames.Length;
        if (image.sprite != frames[i]) image.sprite = frames[i];
    }
}

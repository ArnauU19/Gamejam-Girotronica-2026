using UnityEngine;

/// <summary>
/// The game is designed for 16:9. On narrower screens (16:10 laptops, 4:3, a resized browser) the fixed
/// orthographic height would cut the sides, so the camera zooms out just enough to keep the whole
/// 16:9 area visible. Wider screens keep the designed size. Created automatically, works in every scene.
/// </summary>
public class AspectFit : MonoBehaviour
{
    const float DesignAspect = 16f / 9f;

    Camera cam;
    float baseSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var go = new GameObject(nameof(AspectFit));
        DontDestroyOnLoad(go);
        go.AddComponent<AspectFit>();
    }

    void LateUpdate()
    {
        var c = Camera.main;
        if (c == null || !c.orthographic || Screen.height == 0) return;
        if (c != cam) { cam = c; baseSize = c.orthographicSize; }   // new scene: remember its designed size

        float aspect = (float)Screen.width / Screen.height;
        cam.orthographicSize = baseSize * Mathf.Max(1f, DesignAspect / aspect);
    }
}

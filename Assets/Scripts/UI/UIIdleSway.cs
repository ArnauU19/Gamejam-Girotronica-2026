using UnityEngine;

/// <summary>Slow hand-held sway (rotation + bob) for titles and paper bits. Unscaled time.</summary>
public class UIIdleSway : MonoBehaviour
{
    public float angle = 1.2f;
    public float bob = 4f;
    public float period = 4f;
    [Tooltip("Shifts the phase so several objects don't move in sync.")]
    public float phase;

    Vector3 basePos;
    float baseAngle;

    void Awake()
    {
        basePos = transform.localPosition;
        baseAngle = Mathf.DeltaAngle(0f, transform.localEulerAngles.z);
    }

    void Update()
    {
        float t = (Time.unscaledTime / Mathf.Max(0.01f, period) + phase) * Mathf.PI * 2f;
        transform.localPosition = basePos + new Vector3(0f, Mathf.Sin(t) * bob, 0f);
        transform.localRotation = Quaternion.Euler(0f, 0f, baseAngle + Mathf.Sin(t * 0.5f + 1f) * angle);
    }
}

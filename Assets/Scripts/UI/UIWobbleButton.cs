using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hand-drawn button feel: grows and wiggles a little on hover / selection, squashes when pressed.
/// Uses unscaled time so it also works in the pause menu (Time.timeScale = 0).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIWobbleButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler, IPointerClickHandler
{
    public float hoverScale = 1.06f;
    public float pressScale = 0.95f;
    [Tooltip("Degrees of the little wiggle when the pointer enters.")]
    public float wiggleAngle = 4f;
    [Tooltip("On hover the button straightens this fraction of its resting tilt.")]
    [Range(0f, 1f)] public float straighten = 0.6f;
    public float speed = 14f;
    public bool clickSound = true;

    Vector3 baseScale;
    float baseAngle;
    bool hover, pressed, selected, initialized;
    float wiggleTime = 10f;
    Selectable selectable;

    void Init()
    {
        if (initialized) return;
        initialized = true;
        baseScale = transform.localScale;
        baseAngle = Mathf.DeltaAngle(0f, transform.localEulerAngles.z);
        selectable = GetComponent<Selectable>();
    }

    void Awake() => Init();

    void OnDisable()
    {
        Init();
        hover = pressed = selected = false;
        wiggleTime = 10f;
        transform.localScale = baseScale;
        transform.localRotation = Quaternion.Euler(0f, 0f, baseAngle);
    }

    bool Interactable => selectable == null || selectable.IsInteractable();

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool active = (hover || selected) && Interactable;

        float target = pressed && Interactable ? pressScale : active ? hoverScale : 1f;
        float k = 1f - Mathf.Exp(-speed * dt);
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * target, k);

        wiggleTime += dt;
        float wiggle = wiggleAngle * Mathf.Exp(-7f * wiggleTime) * Mathf.Sin(wiggleTime * 32f);
        float angle = baseAngle * (active ? 1f - straighten : 1f) + wiggle;
        float current = Mathf.LerpAngle(transform.localEulerAngles.z, angle, k);
        transform.localRotation = Quaternion.Euler(0f, 0f, current);
    }

    void StartWiggle()
    {
        if (Interactable) wiggleTime = 0f;
    }

    public void OnPointerEnter(PointerEventData e) { hover = true; StartWiggle(); }
    public void OnPointerExit(PointerEventData e) { hover = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnSelect(BaseEventData e) { selected = true; StartWiggle(); }
    public void OnDeselect(BaseEventData e) { selected = false; }

    public void OnPointerClick(PointerEventData e)
    {
        if (!clickSound || !Interactable) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(Sfx.Grab);
    }
}

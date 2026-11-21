using UnityEngine;

/// <summary>
/// Small trigger collider placed in front of the character (child of it).
/// - Touches a ruler    -> the ruler is blocking the way and the character dies.
/// - Touches an "eraser" -> the character turns around (its direction flips).
/// It sits above the feet, so a ruler correctly placed under the feet never touches it.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FrontSensor : MonoBehaviour
{
    public SurfaceWalker walker;
    [Tooltip("Objects with this tag make the character turn around when the sensor touches them.")]
    public string eraserTag = "eraser";
    [Tooltip("Seconds during which the same eraser cannot flip the character again.")]
    public float eraserCooldown = 0.25f;

    bool eraserTagValid;
    Collider2D lastEraser;
    float lastEraserTime = -10f;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
        walker = GetComponentInParent<SurfaceWalker>();
    }

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (walker == null) walker = GetComponentInParent<SurfaceWalker>();

        // Keep the sensor on the side the character walks to.
        if (walker != null)
        {
            Vector3 p = transform.localPosition;
            p.x = Mathf.Abs(p.x) * (walker.facing < 0 ? -1f : 1f);
            transform.localPosition = p;
        }

        // CompareTag with a tag missing from the Tag Manager logs errors, so check it once.
        try { GameObject.FindGameObjectsWithTag(eraserTag); eraserTagValid = !string.IsNullOrEmpty(eraserTag); }
        catch (UnityException)
        {
            eraserTagValid = false;
            Debug.LogWarning($"[FrontSensor] Tag '{eraserTag}' is not defined in the Tag Manager: erasers are disabled.", this);
        }
    }

    public bool IsEraser(Collider2D c) => eraserTagValid && c != null && c.CompareTag(eraserTag);

    void OnTriggerEnter2D(Collider2D other)
    {
        if (walker == null) return;

        if (IsEraser(other))
        {
            // Flipping mirrors this sensor to the other side; the cooldown avoids a second flip
            // if it lands inside the same (big) eraser.
            if (other == lastEraser && Time.time - lastEraserTime < eraserCooldown) return;
            lastEraser = other;
            lastEraserTime = Time.time;
            walker.FlipDirection($"front sensor hit eraser '{other.name}'");
            return;
        }

        walker.OnFrontSensorTouched(other);
    }
}

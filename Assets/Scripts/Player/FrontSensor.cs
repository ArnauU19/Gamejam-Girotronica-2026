using UnityEngine;

/// <summary>
/// Small trigger collider placed in front of the character (child of it).
/// - Touches a ruler    -> the ruler is blocking the way and the character dies.
/// - Erasers are ignored here: the SurfaceWalker checks them itself from the feet to the head
///   (bounce on the pink rubber, die on the metal end), so small erasers on the floor are found too.
/// It sits above the feet, so a ruler correctly placed under the feet never touches it.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FrontSensor : MonoBehaviour
{
    public SurfaceWalker walker;
    [Tooltip("Objects with this tag are erasers: ignored here (the SurfaceWalker handles them).")]
    public string eraserTag = "eraser";

    bool eraserTagValid;

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

        // Erasers are handled by the SurfaceWalker (CheckEraserAhead)
        if (IsEraser(other) || other.GetComponent<EraserPart>() != null) return;

        walker.OnFrontSensorTouched(other);
    }
}

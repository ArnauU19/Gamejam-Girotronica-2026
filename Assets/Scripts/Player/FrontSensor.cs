using UnityEngine;

/// <summary>
/// Small trigger collider placed in front of the character (child of it).
/// If it touches a ruler, the ruler is blocking the way and the character dies.
/// It sits above the feet, so a ruler correctly placed under the feet never touches it.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FrontSensor : MonoBehaviour
{
    public SurfaceWalker walker;

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
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (walker != null) walker.OnFrontSensorTouched(other);
    }
}

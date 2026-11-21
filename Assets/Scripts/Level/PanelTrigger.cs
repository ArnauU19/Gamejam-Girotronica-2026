using UnityEngine;

/// <summary>
/// Trigger zone placed at the entrance or the exit of a Panel.
/// Needs a Collider2D marked as "Is Trigger".
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PanelTrigger : MonoBehaviour
{
    public enum Kind { Enter, Exit }

    public Kind kind = Kind.Enter;
    [Tooltip("If empty, the Panel in the parents is used.")]
    public Panel panel;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
        panel = GetComponentInParent<Panel>();
    }

    void Awake()
    {
        if (panel == null) panel = GetComponentInParent<Panel>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var walker = other.GetComponentInParent<SurfaceWalker>();
        // Only the body counts (not the FrontSensor or other child colliders).
        if (walker == null || panel == null || other != walker.BodyCollider) return;

        if (kind == Kind.Enter) panel.PlayerEntered(walker);
        else panel.PlayerExited(walker);
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = kind == Kind.Enter ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.offset, box.size);
    }
}

using UnityEngine;

/// <summary>
/// The trigger area of a Panel: a BoxCollider2D (Is Trigger) tagged "panel", as big as the panel.
/// The character is INSIDE a panel while its body touches one of these areas (2D sprite) and
/// OUTSIDE when it touches none (3D sprite). The detection itself is done by SurfaceWalker using the tag.
/// Put it on a child of the Panel ("PanelArea") and use "Fit To Panel Frame" to size it.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class PanelTrigger : MonoBehaviour
{
    public const string PanelTag = "panel";

    void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        try { gameObject.tag = PanelTag; }
        catch (UnityException) { Debug.LogWarning($"[PanelTrigger] Tag '{PanelTag}' does not exist yet.", this); }
        FitToPanelFrame();
    }

    /// <summary>Sizes the box to the Panel's "Frame" LineRenderer (the drawn border of the panel).</summary>
    [ContextMenu("Fit To Panel Frame")]
    public void FitToPanelFrame()
    {
        var panel = GetComponentInParent<Panel>();
        var frameT = panel != null ? panel.transform.Find("Frame") : null;
        var frame = frameT != null ? frameT.GetComponent<LineRenderer>() : null;
        if (frame == null || frame.positionCount == 0)
        {
            Debug.LogWarning("[PanelTrigger] No 'Frame' LineRenderer found in the Panel to fit to.", this);
            return;
        }

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        for (int i = 0; i < frame.positionCount; i++)
        {
            Vector3 p = frame.GetPosition(i);
            if (!frame.useWorldSpace) p = frameT.TransformPoint(p);
            Vector2 local = transform.InverseTransformPoint(p);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        var box = GetComponent<BoxCollider2D>();
        box.offset = (min + max) * 0.5f;
        box.size = max - min;
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.offset, box.size);
    }
}

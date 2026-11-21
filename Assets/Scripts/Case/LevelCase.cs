using System.Collections.Generic;
using UnityEngine;

// The pencil case: the area where the pieces wait at the start of the level.
// Pieces are placed by hand in the scene, at their real size; the ones inside the case shrink when the level starts.
// Taking a piece out brings it back to its real size; putting it back in shrinks it again.
// The area is this object's Collider2D if it has one, otherwise the bounds of its renderer (the sprite).
public class LevelCase : MonoBehaviour
{
    [SerializeField, Range(0.1f, 1f)] private float pieceScale = 0.5f;   // size of the pieces inside the case
    [SerializeField] private float scaleTime = 0.12f;                   // seconds to shrink / grow

    public float PieceScale => pieceScale;
    public float ScaleTime => scaleTime;

    private static readonly List<LevelCase> all = new List<LevelCase>();
    private Collider2D areaCollider;
    private Renderer areaRenderer;

    private void Awake()
    {
        areaCollider = GetComponent<Collider2D>();
        areaRenderer = GetComponent<Renderer>();
    }

    private void OnEnable() { all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    public bool Contains(Vector2 point)
    {
        if (areaCollider != null) return areaCollider.OverlapPoint(point);
        if (areaRenderer != null)
        {
            Bounds b = areaRenderer.bounds;
            return point.x >= b.min.x && point.x <= b.max.x && point.y >= b.min.y && point.y <= b.max.y;
        }
        return false;
    }

    /// <summary>The case that contains this point, or null.</summary>
    public static LevelCase At(Vector2 point)
    {
        foreach (LevelCase c in all)
            if (c.Contains(point)) return c;
        return null;
    }
}

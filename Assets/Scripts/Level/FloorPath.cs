using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A floor defined by a list of points.
/// - Between two points the floor is a straight segment (horizontal, diagonal, vertical... any angle).
/// - A point with bendRadius > 0 becomes a rounded bend instead of a sharp corner.
/// The component generates the EdgeCollider2D (physics) and the LineRenderer (visual) from the points.
///
/// Editing (all changes are kept when the level starts):
/// - Select the floor and drag the point handles in the Scene view (or edit the list in the Inspector).
/// - You can also edit the EdgeCollider2D directly ("Edit Collider"): that edit is kept and the line follows it.
/// Nothing is regenerated when entering Play mode: the level uses exactly what was saved.
/// Points are in local space of this GameObject.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(EdgeCollider2D))]
public class FloorPath : MonoBehaviour
{
    [System.Serializable]
    public struct Point
    {
        public Vector2 position;
        [Tooltip("0 = sharp corner. > 0 = rounded bend with (approximately) this radius.")]
        [Min(0f)] public float bendRadius;

        public Point(float x, float y, float bendRadius = 0f)
        {
            position = new Vector2(x, y);
            this.bendRadius = bendRadius;
        }
    }

    public List<Point> points = new List<Point>();
    [Range(2, 32)] public int segmentsPerBend = 12;
    [Min(0.01f)] public float lineWidth = 0.15f;

    // Hashes of what was last generated, to know what changed (saved with the scene).
    [SerializeField, HideInInspector] int builtSettingsHash;
    [SerializeField, HideInInspector] int builtColliderHash;

    readonly List<Vector2> path = new List<Vector2>();

    /// <summary>True when the collider was edited by hand after the last generation from the points.</summary>
    public bool ColliderEditedByHand
    {
        get
        {
            var col = GetComponent<EdgeCollider2D>();
            return col != null && builtColliderHash != 0 && HashPoints(col.points) != builtColliderHash;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // OnValidate also runs when the scene loads / Play starts: only regenerate if the
        // FloorPath settings really changed, so hand-made collider edits are never overwritten.
        if (Application.isPlaying || HashSettings() == builtSettingsHash) return;

        // Floors created before this version have no hash: adopt what is saved (collider included)
        // instead of regenerating it.
        bool legacy = builtSettingsHash == 0;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (legacy) AdoptCurrentCollider();
            else Rebuild();
        };
    }

    void AdoptCurrentCollider()
    {
        var col = GetComponent<EdgeCollider2D>();
        ApplyToLine(col.points);
        builtSettingsHash = HashSettings();
        builtColliderHash = HashPoints(col.points);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void Update()
    {
        // Edit mode only: if the collider was edited by hand, make the line follow it.
        if (Application.isPlaying) return;
        var col = GetComponent<EdgeCollider2D>();
        if (col == null || builtColliderHash == 0) return;
        int h = HashPoints(col.points);
        if (h == builtColliderHash) return;
        ApplyToLine(col.points);
        builtColliderHash = h;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    /// <summary>Regenerates the collider and the line from the points.</summary>
    public void Rebuild()
    {
        BuildPath(path);
        if (path.Count < 2) return;

        var col = GetComponent<EdgeCollider2D>();
        col.SetPoints(path);
        ApplyToLine(path);

        builtSettingsHash = HashSettings();
        builtColliderHash = HashPoints(col.points);
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(col);
            UnityEditor.EditorUtility.SetDirty(this);
            var line = GetComponent<LineRenderer>();
            if (line != null) UnityEditor.EditorUtility.SetDirty(line);
        }
#endif
    }

    /// <summary>Replaces the points with the current collider shape (sharp corners).</summary>
    public void ImportPointsFromCollider()
    {
        var col = GetComponent<EdgeCollider2D>();
        points.Clear();
        foreach (var p in col.points) points.Add(new Point(p.x, p.y));
        Rebuild();
    }

    void ApplyToLine(IList<Vector2> pts)
    {
        var line = GetComponent<LineRenderer>();
        if (line == null) return;
        line.useWorldSpace = false;
        line.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++) line.SetPosition(i, pts[i]);
        line.startWidth = line.endWidth = lineWidth;
    }

    int HashSettings()
    {
        unchecked
        {
            int h = 17;
            foreach (var p in points)
                h = h * 31 + Mathf.RoundToInt(p.position.x * 1000f) * 7 + Mathf.RoundToInt(p.position.y * 1000f) * 13 +
                    Mathf.RoundToInt(p.bendRadius * 1000f);
            h = h * 31 + segmentsPerBend;
            h = h * 31 + Mathf.RoundToInt(lineWidth * 1000f);
            return h == 0 ? 1 : h;
        }
    }

    static int HashPoints(IList<Vector2> pts)
    {
        unchecked
        {
            int h = 23;
            foreach (var p in pts)
                h = h * 31 + Mathf.RoundToInt(p.x * 1000f) * 7 + Mathf.RoundToInt(p.y * 1000f) * 13;
            return h == 0 ? 1 : h;
        }
    }

    void BuildPath(List<Vector2> result)
    {
        result.Clear();
        int n = points.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = points[i].position;
            float r = points[i].bendRadius;

            // End points and sharp corners are added as they are.
            if (i == 0 || i == n - 1 || r <= 0f)
            {
                result.Add(p);
                continue;
            }

            Vector2 prev = points[i - 1].position;
            Vector2 next = points[i + 1].position;
            Vector2 inDir = (p - prev).normalized;
            Vector2 outDir = (next - p).normalized;
            float turn = Vector2.Angle(inDir, outDir);
            if (turn < 0.5f)
            {
                result.Add(p);
                continue;
            }

            // Distance from the corner where the curve starts/ends (same as a circle fillet).
            float t = r * Mathf.Tan(turn * 0.5f * Mathf.Deg2Rad);
            t = Mathf.Min(t, Vector2.Distance(prev, p) * 0.5f, Vector2.Distance(p, next) * 0.5f);
            Vector2 a = p - inDir * t;
            Vector2 b = p + outDir * t;

            // Quadratic bezier with the corner as control point -> smooth, tangent-continuous bend.
            for (int s = 0; s <= segmentsPerBend; s++)
            {
                float u = s / (float)segmentsPerBend;
                result.Add((1 - u) * (1 - u) * a + 2 * (1 - u) * u * p + u * u * b);
            }
        }
    }
}

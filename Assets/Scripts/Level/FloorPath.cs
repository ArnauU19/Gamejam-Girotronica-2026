using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A floor defined by a list of points.
/// - Between two points the floor is a straight segment (horizontal, diagonal, vertical... any angle).
/// - A point with bendRadius > 0 becomes a rounded bend instead of a sharp corner.
/// The component generates the EdgeCollider2D (physics) and the LineRenderer (visual) from the points.
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

    readonly List<Vector2> path = new List<Vector2>();

    void OnEnable() => Rebuild();

#if UNITY_EDITOR
    void OnValidate()
    {
        // Changing other components inside OnValidate is not allowed, so defer it.
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Rebuild(); };
    }
#endif

    /// <summary>Regenerates the collider and the line from the points.</summary>
    public void Rebuild()
    {
        BuildPath(path);
        if (path.Count < 2) return;

        GetComponent<EdgeCollider2D>().SetPoints(path);

        var line = GetComponent<LineRenderer>();
        if (line != null)
        {
            line.useWorldSpace = false;
            line.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++) line.SetPosition(i, path[i]);
            line.startWidth = line.endWidth = lineWidth;
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

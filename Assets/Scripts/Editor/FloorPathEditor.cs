using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene-view editing for FloorPath: drag the circles to move points.
/// Inspector buttons to add/remove points and to sync with a hand-edited collider.
/// </summary>
[CustomEditor(typeof(FloorPath))]
public class FloorPathEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var path = (FloorPath)target;

        EditorGUILayout.HelpBox("Drag the white circles in the Scene view to move the floor points. " +
                                "Changes are kept when the level starts.", MessageType.None);
        if (path.ColliderEditedByHand)
            EditorGUILayout.HelpBox("The Edge Collider was edited by hand: the floor uses that shape " +
                                    "(the points list is not in sync).", MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add point"))
            {
                Undo.RecordObject(path, "Add floor point");
                Vector2 last = path.points.Count > 0 ? path.points[^1].position : Vector2.zero;
                Vector2 dir = path.points.Count > 1 ? (last - path.points[^2].position).normalized : Vector2.right;
                path.points.Add(new FloorPath.Point(last.x + dir.x * 2f, last.y + dir.y * 2f));
                RebuildWithUndo(path);
            }
            using (new EditorGUI.DisabledScope(path.points.Count <= 2))
            {
                if (GUILayout.Button("Remove last point"))
                {
                    Undo.RecordObject(path, "Remove floor point");
                    path.points.RemoveAt(path.points.Count - 1);
                    RebuildWithUndo(path);
                }
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Regenerate from points")) RebuildWithUndo(path);
            if (GUILayout.Button("Import points from collider"))
            {
                Undo.RecordObject(path, "Import floor points");
                RecordGenerated(path);
                path.ImportPointsFromCollider();
            }
        }
    }

    void OnSceneGUI()
    {
        var path = (FloorPath)target;
        var t = path.transform;
        for (int i = 0; i < path.points.Count; i++)
        {
            var p = path.points[i];
            Vector3 world = t.TransformPoint(p.position);
            float size = HandleUtility.GetHandleSize(world) * 0.08f;

            Handles.color = Color.white;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(path, "Move floor point");
                p.position = t.InverseTransformPoint(moved);
                path.points[i] = p;
                RebuildWithUndo(path);
            }
            Handles.Label(world + Vector3.up * size * 2f, i.ToString());
        }
    }

    static void RecordGenerated(FloorPath path)
    {
        Undo.RecordObject(path.GetComponent<EdgeCollider2D>(), "Floor");
        var line = path.GetComponent<LineRenderer>();
        if (line != null) Undo.RecordObject(line, "Floor");
    }

    static void RebuildWithUndo(FloorPath path)
    {
        RecordGenerated(path);
        path.Rebuild();
    }
}

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Helpers to build levels with the panel prefabs (Assets/Prefabs/Level).
/// Girotronica > Level > Measure Selected: select two objects (e.g. the SnapLeft / SnapRight points of
/// two panels) and it prints the distance and the ruler length in cm that fills that gap exactly.
/// </summary>
public static class LevelTools
{
    const float UnitsPerCm = 0.746f; // same as the Ruler prefab (RulerBuilder.unitsPerCm)

    [MenuItem("Girotronica/Level/Measure Selected (distance + ruler cm)")]
    static void Measure()
    {
        var sel = Selection.transforms;
        if (sel.Length != 2)
        {
            EditorUtility.DisplayDialog("Measure", "Select exactly two objects (for example two snap points).", "OK");
            return;
        }
        Vector2 a = sel[0].position, b = sel[1].position;
        float d = Vector2.Distance(a, b);
        var ls = Object.FindAnyObjectByType<LevelScale>();
        float scale = ls != null ? ls.scale : 1f;   // levels drawn smaller have smaller centimetres
        string msg = $"{sel[0].name} -> {sel[1].name}\n" +
                     $"Distance: {d:F3} units  (dx {Mathf.Abs(b.x - a.x):F3}, dy {Mathf.Abs(b.y - a.y):F3})\n" +
                     $"Ruler that fills it: Length Cm = {d / (UnitsPerCm * scale):F2}" + (scale != 1f ? $"  (level scale {scale:F3})" : "");
        Debug.Log("[LevelTools] " + msg.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Measure", msg, "OK");
    }

    [MenuItem("Girotronica/Level/Measure Selected (distance + ruler cm)", true)]
    static bool MeasureValidate() => Selection.transforms.Length == 2;
}
#endif

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Size of the level (on LevelBase). A level with more panels is drawn smaller: set "Scale" and
/// the panels and the character get that same scale, so they keep their proportion:
///  - every panel (objects with a Panel component) -> scale
///  - the character -> characterScale * scale (its walking distances, speed and gravity too, in play)
/// The pieces (grabbable objects), camera, desk, pencil case, kill zones and UI are never changed.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(-500)]   // before SurfaceWalker.Awake
public class LevelScale : MonoBehaviour
{
    [Tooltip("1 = Level1 size (two panels side by side). Smaller for levels with more panels.")]
    [Range(0.2f, 1.5f)] public float scale = 1f;

    [Tooltip("The character of the level.")]
    public SurfaceWalker character;
    [Tooltip("Scale of the character at level scale 1.")]
    public float characterScale = 0.8f;

    void Awake()
    {
        if (Application.isPlaying) ScaleWalker();
    }

    // Walking distances and speeds are in world units: they follow the level scale.
    void ScaleWalker()
    {
        if (character == null || Mathf.Approximately(scale, 1f)) return;
        float k = scale;
        character.speed *= k;
        character.footOffset *= k;
        character.bodyHalfWidth *= k;
        character.groundSnapDistance *= k;
        character.edgeTolerance *= k;
        character.rulerClearance *= k;
        character.rulerSnapSpeed *= k;
        character.gravity *= k;
        character.maxFallSpeed *= k;
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Application.isPlaying || !gameObject.scene.IsValid()) return;
        bool changed = false;

        foreach (var p in FindObjectsByType<Panel>(FindObjectsInactive.Include))
            if (p.gameObject.scene == gameObject.scene) changed |= SetScale(p.transform, scale);

        if (character != null) changed |= SetScale(character.transform, characterScale * scale);

        if (changed) EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    static bool SetScale(Transform t, float s)
    {
        Vector3 target = new Vector3(s, s, t.localScale.z);
        if ((t.localScale - target).sqrMagnitude < 1e-8f) return false;
        Undo.RecordObject(t, "Level scale");
        t.localScale = target;
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        return true;
    }
#endif
}

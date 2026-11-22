#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// TEMPORARY: run once from "Girotronica > TEMP Unscale Pieces", then delete this file.
// The level scale no longer touches the grabbable pieces: puts them back to their own size in every scene with a LevelScale.
//  - rulers: pieceScale back to 1, keeping the same length on screen (lengthCm * pieceScale),
//    except Level6 / Level9 that get back the 20 cm of Level6r / Level9r
//  - other pieces: back to the size they had in Level5r..Level9r (1 elsewhere)
public static class PieceScaleFix
{
    [MenuItem("Girotronica/TEMP Unscale Pieces")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<LevelScale>() == null) continue;
            bool changed = false;

            foreach (var rb in Object.FindObjectsByType<RulerBuilder>(FindObjectsInactive.Include))
            {
                if (Mathf.Approximately(rb.pieceScale, 1f)) continue;
                rb.lengthCm = (name == "Level6" || name == "Level9") ? 20f : Mathf.Round(rb.lengthCm * rb.pieceScale * 1000f) / 1000f;
                rb.pieceScale = 1f;
                rb.Rebuild();
                PrefabUtility.RecordPrefabInstancePropertyModifications(rb);
                Debug.Log($"[PieceScaleFix] {name}: {rb.name} -> {rb.lengthCm} cm");
                changed = true;
            }

            var others = Object.FindObjectsByType<DraggableObject>(FindObjectsInactive.Include)
                .Where(d => d.GetComponent<RulerBuilder>() == null).OrderByDescending(d => d.transform.position.y).ToList();
            for (int i = 0; i < others.Count; i++)
            {
                float s = Original(name, i, others[i].name);
                var t = others[i].transform;
                if (Mathf.Approximately(t.localScale.x, s) && Mathf.Approximately(t.localScale.y, s)) continue;
                t.localScale = new Vector3(s, s, t.localScale.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                Debug.Log($"[PieceScaleFix] {name}: {others[i].name} -> scale {s}");
                changed = true;
            }

            if (changed) EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        Debug.Log("[PieceScaleFix] Done.");
    }

    // Size of each non-ruler piece in Level5r..Level9r (pieces sorted from top to bottom)
    static float Original(string scene, int index, string piece)
    {
        switch (scene)
        {
            case "Level5": return 1.1f;
            case "Level6": return 1.1f;
            case "Level7": return 0.8f;
            case "Level8": return index == 0 ? 0.83f : 0.68f;
            case "Level9": return piece.StartsWith("Escuadra") ? 0.732f : 1f;
            default: return 1f;
        }
    }
}
#endif

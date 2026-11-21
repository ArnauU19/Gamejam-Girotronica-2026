using UnityEditor;
using UnityEngine;

/// <summary>
/// Makes sure the tags used by the gameplay code exist in the Tag Manager
/// (runs automatically every time scripts are compiled, on every team member's machine).
/// </summary>
[InitializeOnLoad]
public static class ProjectTagsSetup
{
    static readonly string[] RequiredTags = { "ruler", "killZone", "panel", "eraser" };

    static ProjectTagsSetup()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;

        var so = new SerializedObject(assets[0]);
        var tags = so.FindProperty("tags");
        bool changed = false;

        foreach (var tag in RequiredTags)
        {
            bool found = false;
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tag) { found = true; break; }
            if (found) continue;

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            changed = true;
            Debug.Log($"[ProjectTagsSetup] Added tag '{tag}'.");
        }

        if (changed)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorApplication.delayCall += AssetDatabase.SaveAssets; // write ProjectSettings/TagManager.asset
        }
    }
}

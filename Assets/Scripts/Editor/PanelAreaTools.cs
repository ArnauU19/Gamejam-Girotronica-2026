using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Panel areas: one trigger tagged "panel", as big as the panel, per Panel (see PanelTrigger).
/// Menu: Girotronica > Update Panel Areas (Open Scene)
///   - removes the old EnterTrigger / ExitTrigger objects (old detection system),
///   - adds (or refits) a "PanelArea" child to every Panel, sized to its Frame.
/// </summary>
public static class PanelAreaTools
{
    const string AreaName = "PanelArea";
    static readonly string[] OldTriggerNames = { "EnterTrigger", "ExitTrigger" };

    [MenuItem("Girotronica/Update Panel Areas (Open Scene)")]
    public static void UpdateOpenScene()
    {
        Undo.SetCurrentGroupName("Update Panel Areas");
        int group = Undo.GetCurrentGroup();

        var panels = Resources.FindObjectsOfTypeAll<Panel>()
            .Where(p => p.gameObject.scene.IsValid() && p.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
            .ToList();

        int removed = 0;
        foreach (var panel in panels)
        {
            foreach (var oldName in OldTriggerNames)
            {
                Transform old;
                while ((old = panel.transform.Find(oldName)) != null)
                {
                    Undo.DestroyObjectImmediate(old.gameObject);
                    removed++;
                }
            }
            CreatePanelArea(panel.transform);
        }

        Undo.CollapseUndoOperations(group);
        TestPanelBuilder.SaveScene();
        Debug.Log($"[PanelAreaTools] {panels.Count} panel(s) updated with a '{AreaName}' trigger, " +
                  $"{removed} old Enter/Exit trigger(s) removed. Scene saved.");
    }

    /// <summary>Creates (or refits) the "PanelArea" child of a panel. The panel's Frame must exist.</summary>
    public static GameObject CreatePanelArea(Transform panel)
    {
        var t = panel.Find(AreaName);
        GameObject area;
        if (t != null) area = t.gameObject;
        else
        {
            area = new GameObject(AreaName);
            Undo.RegisterCreatedObjectUndo(area, "Create " + AreaName);
            area.transform.SetParent(panel, false);
        }

        Undo.RecordObject(area, "Setup " + AreaName);
        area.tag = PanelTrigger.PanelTag;
        var box = TestPanelBuilder.GetOrAdd<BoxCollider2D>(area);
        box.isTrigger = true;
        var trigger = TestPanelBuilder.GetOrAdd<PanelTrigger>(area);
        trigger.FitToPanelFrame();
        return area;
    }
}

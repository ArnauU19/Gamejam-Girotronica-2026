using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menus:
///  - Girotronica > Add Level UI (Canvas + Countdown) To Open Scene
///  - Girotronica > Update Build Scene List (Levels)
/// </summary>
public static class LevelUIBuilder
{
    [MenuItem("Girotronica/Add Level UI (Canvas + Countdown) To Open Scene")]
    public static void AddToOpenSceneMenu()
    {
        Undo.SetCurrentGroupName("Add Level UI");
        int group = Undo.GetCurrentGroup();
        var manager = AddToOpenScene();
        Undo.CollapseUndoOperations(group);
        TestPanelBuilder.SaveScene();
        Selection.activeGameObject = manager.gameObject;
        Debug.Log("[LevelUIBuilder] Canvas + countdown + LevelManager ready, scene saved.");
    }

    /// <summary>Creates (or reuses) the LevelManager, the Canvas and the countdown text. Idempotent.</summary>
    public static LevelManager AddToOpenScene()
    {
        // --- LevelManager
        var manager = Object.FindAnyObjectByType<LevelManager>();
        if (manager == null)
        {
            var go = new GameObject("LevelManager");
            Undo.RegisterCreatedObjectUndo(go, "Create LevelManager");
            manager = go.AddComponent<LevelManager>();
        }

        // --- Canvas (screen space overlay, scales with a 1920x1080 reference)
        var canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null || canvasGo.GetComponent<Canvas>() == null)
        {
            canvasGo = new GameObject("Canvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            canvasGo.layer = LayerMask.NameToLayer("UI");
        }
        var canvas = TestPanelBuilder.GetOrAdd<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = TestPanelBuilder.GetOrAdd<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        TestPanelBuilder.GetOrAdd<GraphicRaycaster>(canvasGo);

        // --- Countdown text (big number in the middle of the screen)
        var textT = canvasGo.transform.Find("CountdownText");
        GameObject textGo;
        if (textT != null) textGo = textT.gameObject;
        else
        {
            textGo = new GameObject("CountdownText", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(textGo, "Create CountdownText");
            textGo.transform.SetParent(canvasGo.transform, false);
            textGo.layer = canvasGo.layer;
        }
        var rt = (RectTransform)textGo.transform;
        Undo.RecordObject(rt, "Setup CountdownText");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(1200f, 320f);

        var text = TestPanelBuilder.GetOrAdd<Text>(textGo);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 220;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = new Color(0.12f, 0.12f, 0.12f);
        text.raycastTarget = false; // never block clicks/drags on the rulers
        text.text = "5";
        var outline = TestPanelBuilder.GetOrAdd<Outline>(textGo);
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(5f, -5f);

        Undo.RecordObject(manager, "Setup LevelManager");
        manager.countdownText = text;
        manager.countdownSeconds = 5f;
        if (manager.walker == null) manager.walker = Object.FindAnyObjectByType<SurfaceWalker>();
        return manager;
    }

    [MenuItem("Girotronica/Add Bottom Kill Zone To Open Scene")]
    public static void AddBottomKillZoneMenu()
    {
        Undo.SetCurrentGroupName("Add Bottom Kill Zone");
        var zone = AddBottomKillZone();
        TestPanelBuilder.SaveScene();
        Selection.activeGameObject = zone;
        Debug.Log("[LevelUIBuilder] Bottom kill zone ready, scene saved.");
    }

    /// <summary>
    /// Wide trigger tagged "killZone" just below what the camera shows: falling out of the level
    /// respawns the character. Idempotent.
    /// </summary>
    public static GameObject AddBottomKillZone()
    {
        var zone = GameObject.Find("KillZone_Bottom");
        if (zone == null)
        {
            zone = new GameObject("KillZone_Bottom");
            Undo.RegisterCreatedObjectUndo(zone, "Create KillZone_Bottom");
        }
        Undo.RecordObject(zone, "Setup KillZone_Bottom");
        zone.tag = "killZone";

        var cam = Camera.main;
        float halfH = cam != null && cam.orthographic ? cam.orthographicSize : 7f;
        float halfW = halfH * 16f / 9f;
        Vector3 c = cam != null ? cam.transform.position : Vector3.zero;

        Undo.RecordObject(zone.transform, "Setup KillZone_Bottom");
        zone.transform.SetPositionAndRotation(new Vector3(c.x, c.y - halfH - 2f, 0f), Quaternion.identity);
        zone.transform.localScale = Vector3.one;
        var box = TestPanelBuilder.GetOrAdd<BoxCollider2D>(zone);
        box.isTrigger = true;
        box.offset = Vector2.zero;
        box.size = new Vector2(halfW * 2f + 20f, 2f);
        return zone;
    }

    /// <summary>
    /// Puts every Assets/Scenes/LevelN.unity in the build scene list, ordered by N, so
    /// "next scene" = next level. Other scenes already in the list are kept but disabled.
    /// </summary>
    [MenuItem("Girotronica/Update Build Scene List (Levels)")]
    public static void UpdateBuildSceneList()
    {
        var levels = Directory.GetFiles("Assets/Scenes", "Level*.unity")
            .Select(p => p.Replace('\\', '/'))
            .Select(p => (path: p, match: Regex.Match(Path.GetFileNameWithoutExtension(p), @"^Level(\d+)$")))
            .Where(x => x.match.Success)
            .OrderBy(x => int.Parse(x.match.Groups[1].Value))
            .Select(x => x.path)
            .ToList();

        var list = levels.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
        foreach (var s in EditorBuildSettings.scenes)
            if (!levels.Contains(s.path)) list.Add(new EditorBuildSettingsScene(s.path, false));

        EditorBuildSettings.scenes = list.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("[LevelUIBuilder] Build scene list: " + string.Join(" -> ", levels.Select(Path.GetFileNameWithoutExtension)));
    }
}

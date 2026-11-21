using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Girotronica > Build Test Panel In Open Scene
/// Builds (or rebuilds) a test panel and sets up the player in the open scene.
/// Everything is registered in Undo, so Ctrl+Z reverts it.
/// </summary>
public static class TestPanelBuilder
{
    const string CapsuleSpriteGuid = "207ee8102dd4143d288186ef0be518ee"; // sprite used by the original Capsule

    [MenuItem("Girotronica/Build Test Panel In Open Scene")]
    public static void Build()
    {
        var oldPanel = GameObject.Find("Panel_01");
        if (oldPanel != null && !EditorUtility.DisplayDialog("Rebuild Panel_01?",
                "Panel_01 already exists. Rebuilding deletes it (and anything you added inside it) " +
                "and recreates the test panel. You can undo with Ctrl+Z.", "Rebuild", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Build Test Panel");
        int undoGroup = Undo.GetCurrentGroup();

        // --- Remove the old placeholder floor and a previous test panel.
        if (oldPanel != null) Undo.DestroyObjectImmediate(oldPanel);
        var oldFloor = GameObject.Find("Floor");
        if (oldFloor != null && oldFloor.transform.parent == null) Undo.DestroyObjectImmediate(oldFloor);

        Material lineMat = LoadLineMaterial();

        // --- Panel root
        var panelGo = new GameObject("Panel_01");
        Undo.RegisterCreatedObjectUndo(panelGo, "Create Panel");
        panelGo.AddComponent<Panel>();

        // Panel frame (visual only)
        var frame = CreateChild("Frame", panelGo.transform);
        var frameLine = frame.AddComponent<LineRenderer>();
        SetupLine(frameLine, lineMat, 0.1f, Color.black);
        frameLine.loop = true;
        frameLine.positionCount = 4;
        frameLine.SetPositions(new Vector3[]
        {
            new Vector3(-8f, -4f), new Vector3(7f, -4f), new Vector3(7f, 4.5f), new Vector3(-8f, 4.5f)
        });

        // Floor: straight -> diagonal down -> straight -> 90 degree bend up -> exits through the top
        var floor = CreateChild("Floor", panelGo.transform);
        floor.AddComponent<EdgeCollider2D>();
        SetupLine(floor.AddComponent<LineRenderer>(), lineMat, 0.15f, new Color(0.15f, 0.15f, 0.15f));
        var path = floor.AddComponent<FloorPath>();
        path.points = new List<FloorPath.Point>
        {
            new FloorPath.Point(-10f,  1f),        // start (outside the panel, left)
            new FloorPath.Point( -4f,  1f, 0.5f),  // end of first straight -> diagonal down
            new FloorPath.Point( -1f, -2f, 1f),    // end of diagonal -> straight
            new FloorPath.Point(  5f, -2f, 2f),    // 90 degree rounded bend going up
            new FloorPath.Point(  5f,  4.5f),      // end of the floor at the top border of the panel
        };
        path.Rebuild();

        // Enter / Exit triggers
        CreateTrigger("EnterTrigger", panelGo.transform, new Vector2(-8f, 1.75f), new Vector2(0.4f, 2.5f), PanelTrigger.Kind.Enter);
        CreateTrigger("ExitTrigger", panelGo.transform, new Vector2(4.5f, 5f), new Vector2(2f, 0.4f), PanelTrigger.Kind.Exit);

        // --- Player (always starts INSIDE the panel)
        var player = FindPlayer();
        if (player == null)
        {
            player = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(player, "Create Player");
        }
        Undo.RecordObject(player.transform, "Setup Player");
        player.transform.SetPositionAndRotation(new Vector3(-6.5f, 1.7f, 0f), Quaternion.identity);
        SetupPlayer(player, panelGo.GetComponent<Panel>());

        // --- Camera
        var cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam, "Setup Camera");
            Undo.RecordObject(cam.transform, "Setup Camera");
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(-0.5f, 0.5f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.96f, 0.95f, 0.9f);
        }

        Undo.CollapseUndoOperations(undoGroup);
        SaveScene();
        Selection.activeGameObject = panelGo;
        Debug.Log("[TestPanelBuilder] Test panel built and scene saved.");
    }

    /// <summary>
    /// Non-destructive: only (re)configures the character (components + front sensor).
    /// Keeps its position, name and sprite.
    /// </summary>
    [MenuItem("Girotronica/Setup Character (walker + front sensor)")]
    public static void SetupCharacterMenu()
    {
        var player = FindPlayer();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Setup Character", "No object named Character, Player or Capsule in the scene.", "OK");
            return;
        }
        Undo.SetCurrentGroupName("Setup Character");
        int undoGroup = Undo.GetCurrentGroup();
        SetupPlayer(player, FindClosestPanel(player.transform.position));
        Undo.CollapseUndoOperations(undoGroup);
        SaveScene();
        Selection.activeGameObject = player;
        Debug.Log($"[TestPanelBuilder] Character '{player.name}' set up and scene saved.");
    }

    static void SetupPlayer(GameObject player, Panel startPanel)
    {
        Undo.RecordObject(player.transform, "Setup Player");
        player.transform.localScale = new Vector3(0.5f, 0.5f, 1f); // capsule 0.5 x 1 units

        var sr = GetOrAdd<SpriteRenderer>(player);
        if (sr.sprite == null) sr.sprite = LoadCapsuleSprite();
        sr.color = new Color(1f, 0.55f, 0.1f);
        sr.sortingOrder = 10;

        var rb = GetOrAdd<Rigidbody2D>(player);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.None;

        var capsule = GetOrAdd<CapsuleCollider2D>(player);
        capsule.direction = CapsuleDirection2D.Vertical;
        capsule.size = new Vector2(1f, 2f);
        capsule.offset = Vector2.zero;
        capsule.isTrigger = true; // movement is done with raycasts; trigger only for panel zones

        var walker = GetOrAdd<SurfaceWalker>(player);
        walker.footOffset = 0.55f;   // half height (0.5) + half floor line width
        walker.bodyHalfWidth = 0.25f;

        // Small "eye" so the rotation/direction is visible
        var eyeT = player.transform.Find("Eye");
        if (eyeT == null)
        {
            var eye = CreateChild("Eye", player.transform);
            eye.transform.localPosition = new Vector3(0.25f, 0.5f, 0f);
            eye.transform.localScale = new Vector3(0.25f, 0.25f, 1f);
            var eyeSr = eye.AddComponent<SpriteRenderer>();
            eyeSr.sprite = sr.sprite;
            eyeSr.color = Color.black;
            eyeSr.sortingOrder = 11;
        }

        // Front sensor: small trigger just in front of the body, ABOVE the feet
        // (local units of the character: body is 1 x 2, feet at y = -1).
        var sensorT = player.transform.Find("FrontSensor");
        GameObject sensorGo = sensorT != null ? sensorT.gameObject : CreateChild("FrontSensor", player.transform);
        Undo.RecordObject(sensorGo.transform, "Setup Front Sensor");
        sensorGo.transform.localPosition = new Vector3(0.6f, 0.2f, 0f);
        sensorGo.transform.localRotation = Quaternion.identity;
        sensorGo.transform.localScale = Vector3.one;
        var sensorBox = GetOrAdd<BoxCollider2D>(sensorGo);
        sensorBox.isTrigger = true;
        sensorBox.offset = Vector2.zero;
        sensorBox.size = new Vector2(0.2f, 1.2f); // covers from 0.6 above the feet to near the head
        var sensor = GetOrAdd<FrontSensor>(sensorGo);
        sensor.walker = walker;

        if (startPanel != null) walker.startPanel = startPanel;
    }

    static GameObject FindPlayer()
    {
        var walker = Object.FindAnyObjectByType<SurfaceWalker>();
        if (walker != null) return walker.gameObject;
        var go = GameObject.Find("Player");
        if (go == null) go = GameObject.Find("Character");
        if (go == null) go = GameObject.Find("Capsule");
        return go;
    }

    static Panel FindClosestPanel(Vector3 pos)
    {
        Panel best = null;
        float bestDist = float.MaxValue;
        foreach (var p in Resources.FindObjectsOfTypeAll<Panel>())
        {
            if (!p.gameObject.scene.IsValid()) continue; // skip prefabs/assets
            float d = (p.transform.position - pos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    static void SaveScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static GameObject CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static void CreateTrigger(string name, Transform parent, Vector2 pos, Vector2 size, PanelTrigger.Kind kind)
    {
        var go = CreateChild(name, parent);
        go.transform.localPosition = pos;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
        var trigger = go.AddComponent<PanelTrigger>();
        trigger.kind = kind;
        trigger.panel = parent.GetComponent<Panel>();
    }

    static void SetupLine(LineRenderer line, Material mat, float width, Color color)
    {
        line.sharedMaterial = mat;
        line.useWorldSpace = false;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.sortingOrder = 5;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null)
        {
            Undo.RecordObject(c, "Setup " + typeof(T).Name);
            return c;
        }
        return Undo.AddComponent<T>(go);
    }

    static Material LoadLineMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        if (mat == null) mat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        return mat;
    }

    static Sprite LoadCapsuleSprite()
    {
        string path = AssetDatabase.GUIDToAssetPath(CapsuleSpriteGuid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}

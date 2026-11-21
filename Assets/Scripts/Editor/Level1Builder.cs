using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Menu: Girotronica > Build Level 1
/// Level 1: two panels (Start and Finish) separated by a gap. The player drags the ruler
/// from the case into the gap so the character can cross to the goal.
/// </summary>
public static class Level1Builder
{
    const string ScenePath = "Assets/Scenes/Level1.unity";
    const string RulerPrefabPath = "Assets/Prefabs/Ruler.prefab";
    const string CharacterPrefabPath = "Assets/Prefabs/Character.prefab";
    const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
    const string SquareSpriteGuid = "311925a002f4447b3a28927169b83ea6"; // same square sprite used by Albert's case

    static readonly string[] BuiltObjects = { "Panel_Start", "Panel_Finish", "Character", "Drag Manager", "Case", "Ruler" };

    [MenuItem("Girotronica/Build Level 1")]
    public static void Build()
    {
        // Open Level1 (asks to save the current scene if it has changes).
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var existing = BuiltObjects.Select(GameObject.Find).Where(g => g != null && g.transform.parent == null).ToList();
        if (existing.Count > 0 && !EditorUtility.DisplayDialog("Rebuild Level 1?",
                "Level 1 already has: " + string.Join(", ", existing.Select(g => g.name)) +
                ".\nRebuilding replaces them. You can undo with Ctrl+Z.", "Rebuild", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Build Level 1");
        int undoGroup = Undo.GetCurrentGroup();
        foreach (var go in existing) Undo.DestroyObjectImmediate(go);

        Material lineMat = TestPanelBuilder.LoadLineMaterial();

        // ---------------------------------------------------------------- Start panel
        // Floor: straight -> diagonal down -> straight, ends at the right border (exit).
        var start = BuildPanel("Panel_Start", lineMat,
            frameMin: new Vector2(-12f, -3.5f), frameMax: new Vector2(-2f, 4f),
            floor: new List<FloorPath.Point>
            {
                new FloorPath.Point(-11.5f,  1.5f),
                new FloorPath.Point( -7.5f,  1.5f, 0.6f),
                new FloorPath.Point( -5f,   -1f,   0.6f),
                new FloorPath.Point( -2f,   -1f),
            },
            enterPos: new Vector2(-12f, 2f), enterSize: new Vector2(0.4f, 3f),
            exitPos: new Vector2(-1.7f, -0.5f), exitSize: new Vector2(0.4f, 2f));

        // ---------------------------------------------------------------- Finish panel
        // Same floor height as the start exit, 4 units to the right (the ruler is ~4.9 long).
        var finish = BuildPanel("Panel_Finish", lineMat,
            frameMin: new Vector2(2f, -3.5f), frameMax: new Vector2(12f, 4f),
            floor: new List<FloorPath.Point>
            {
                new FloorPath.Point( 2f,   -1f),
                new FloorPath.Point(11.5f, -1f),
            },
            enterPos: new Vector2(2.2f, -0.5f), enterSize: new Vector2(0.4f, 2f),
            exitPos: new Vector2(11.8f, -0.5f), exitSize: new Vector2(0.4f, 2f));

        // Goal flag inside the finish panel
        var goal = TestPanelBuilder.CreateChild("Goal", finish.transform);
        goal.transform.localPosition = new Vector3(9.5f, 0f, 0f);
        var goalBox = goal.AddComponent<BoxCollider2D>();
        goalBox.isTrigger = true;
        goalBox.size = new Vector2(0.6f, 2f);
        goal.AddComponent<LevelGoal>();
        var pole = TestPanelBuilder.CreateChild("Pole", goal.transform);
        var poleLine = pole.AddComponent<LineRenderer>();
        TestPanelBuilder.SetupLine(poleLine, lineMat, 0.08f, Color.black);
        poleLine.positionCount = 2;
        poleLine.SetPositions(new[] { new Vector3(0f, -1f), new Vector3(0f, 1.2f) });
        var flag = TestPanelBuilder.CreateChild("Flag", goal.transform);
        var flagLine = flag.AddComponent<LineRenderer>();
        TestPanelBuilder.SetupLine(flagLine, lineMat, 0.5f, new Color(0.2f, 0.75f, 0.3f));
        flagLine.numCapVertices = 0;
        flagLine.positionCount = 2;
        flagLine.SetPositions(new[] { new Vector3(0.04f, 0.95f), new Vector3(0.8f, 0.95f) });

        // ---------------------------------------------------------------- Character
        var character = InstantiateCharacter();
        character.transform.SetPositionAndRotation(new Vector3(-10.5f, 2.1f, 0f), Quaternion.identity);
        var walker = character.GetComponent<SurfaceWalker>();
        Undo.RecordObject(walker, "Setup walker");
        walker.startPanel = start.GetComponent<Panel>(); // always starts inside the Start panel
        walker.speed = 2f;                               // time to place the ruler before the edge
        PrefabUtility.RecordPrefabInstancePropertyModifications(walker);

        // ---------------------------------------------------------------- Drag system (Albert)
        var dragGo = new GameObject("Drag Manager");
        Undo.RegisterCreatedObjectUndo(dragGo, "Create Drag Manager");
        var drag = dragGo.AddComponent<DragManager>();
        var so = new SerializedObject(drag);
        so.FindProperty("pointerPosition").objectReferenceValue = FindAction("UI", "Point");
        so.FindProperty("click").objectReferenceValue = FindAction("UI", "Click");
        so.FindProperty("rightClick").objectReferenceValue = FindAction("UI", "RightClick");
        so.FindProperty("cam").objectReferenceValue = Camera.main;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Case with the ruler inside (below the finish panel, out of the character's way)
        var caseGo = new GameObject("Case");
        Undo.RegisterCreatedObjectUndo(caseGo, "Create Case");
        caseGo.transform.position = new Vector3(7f, -5.5f, 0f);
        caseGo.transform.localScale = new Vector3(6f, 1.6f, 1f);
        var caseSr = caseGo.AddComponent<SpriteRenderer>();
        caseSr.sprite = LoadSprite(SquareSpriteGuid);
        caseSr.color = new Color(0.2754717f, 0f, 0f, 1f);
        caseSr.sortingOrder = 0;
        caseGo.AddComponent<LevelCase>();

        var rulerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RulerPrefabPath);
        var ruler = (GameObject)PrefabUtility.InstantiatePrefab(rulerPrefab);
        Undo.RegisterCreatedObjectUndo(ruler, "Create Ruler");
        ruler.transform.SetPositionAndRotation(new Vector3(7f, -5.5f, 0f), Quaternion.Euler(0f, 0f, 90f)); // lying flat

        // ---------------------------------------------------------------- Camera
        var cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam, "Setup Camera");
            Undo.RecordObject(cam.transform, "Setup Camera");
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.96f, 0.95f, 0.9f);
        }

        // ---------------------------------------------------------------- UI (countdown) + level flow
        LevelUIBuilder.AddToOpenScene();
        LevelUIBuilder.AddBottomKillZone();

        Undo.CollapseUndoOperations(undoGroup);
        TestPanelBuilder.SaveScene();
        Selection.activeGameObject = ruler;
        Debug.Log("[Level1Builder] Level 1 built and saved. Drag the ruler from the case into the gap between the panels.");
    }

    static GameObject BuildPanel(string name, Material lineMat, Vector2 frameMin, Vector2 frameMax,
        List<FloorPath.Point> floor, Vector2 enterPos, Vector2 enterSize, Vector2 exitPos, Vector2 exitSize)
    {
        var panelGo = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(panelGo, "Create " + name);
        panelGo.AddComponent<Panel>();

        var frame = TestPanelBuilder.CreateChild("Frame", panelGo.transform);
        var frameLine = frame.AddComponent<LineRenderer>();
        TestPanelBuilder.SetupLine(frameLine, lineMat, 0.1f, Color.black);
        frameLine.loop = true;
        frameLine.positionCount = 4;
        frameLine.SetPositions(new[]
        {
            new Vector3(frameMin.x, frameMin.y), new Vector3(frameMax.x, frameMin.y),
            new Vector3(frameMax.x, frameMax.y), new Vector3(frameMin.x, frameMax.y)
        });

        var floorGo = TestPanelBuilder.CreateChild("Floor", panelGo.transform);
        floorGo.AddComponent<EdgeCollider2D>();
        TestPanelBuilder.SetupLine(floorGo.AddComponent<LineRenderer>(), lineMat, 0.15f, new Color(0.15f, 0.15f, 0.15f));
        var path = floorGo.AddComponent<FloorPath>();
        path.points = floor;
        path.Rebuild();

        TestPanelBuilder.CreateTrigger("EnterTrigger", panelGo.transform, enterPos, enterSize, PanelTrigger.Kind.Enter);
        TestPanelBuilder.CreateTrigger("ExitTrigger", panelGo.transform, exitPos, exitSize, PanelTrigger.Kind.Exit);
        return panelGo;
    }

    /// <summary>Instantiates the Character prefab, creating it the first time (reusable in every level).</summary>
    static GameObject InstantiateCharacter()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
        if (prefab == null)
        {
            var temp = new GameObject("Character");
            TestPanelBuilder.SetupPlayer(temp, null);
            prefab = PrefabUtility.SaveAsPrefabAsset(temp, CharacterPrefabPath);
            Object.DestroyImmediate(temp);
            Debug.Log($"[Level1Builder] Created {CharacterPrefabPath}");
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Character";
        Undo.RegisterCreatedObjectUndo(instance, "Create Character");
        return instance;
    }

    static InputActionReference FindAction(string map, string action)
    {
        var reference = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
            .OfType<InputActionReference>()
            .FirstOrDefault(r => r.action != null && r.action.actionMap != null &&
                                 r.action.actionMap.name == map && r.action.name == action);
        if (reference == null) Debug.LogError($"[Level1Builder] Input action {map}/{action} not found in {InputActionsPath}");
        return reference;
    }

    static Sprite LoadSprite(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }
}

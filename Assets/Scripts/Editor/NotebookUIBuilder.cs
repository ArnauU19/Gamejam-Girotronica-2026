using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the "comic notebook" UI of the game:
///  - Main menu: the open notebook (llibreta) on the desk; title + comic panel the girl runs out of on the
///    left page, the menu / options on the right page.
///  - Pause menu prefab (Assets/Prefabs/UI/PauseMenu.prefab): torn notebook page with tape, used by every level.
///  - Level HUD: countdown inside a speech bubble, "Nivel N" label, pause button.
///  - Scene transition prefab (Resources/SceneTransition): crumpled paper frames.
///
/// Girotronica > UI > Apply Notebook UI (All Scenes) does everything and saves the scenes.
/// </summary>
public static partial class NotebookUIBuilder
{
    public const string GameTitle = "RULER GAME";
    const string FontsFolder = "Assets/Art/UI/Fonts";
    const string PausePrefabPath = "Assets/Prefabs/UI/PauseMenu.prefab";
    const string TransitionPrefabPath = "Assets/Resources/SceneTransition.prefab";
    const string MainMenuScene = "Assets/Scenes/MainMenu.unity";
    const string MenuArtPath = "Assets/Art/menu_ui.png";

    static Font marker, hand;

    // ================================================================== menus

    [MenuItem("Girotronica/UI/Apply Notebook UI (All Scenes)")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[NotebookUI] Stop Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;

        NotebookUIArt.GenerateAll();
        PrepareSourceArt();
        BuildTransitionPrefab();
        BuildPausePrefab();

        if (File.Exists(MainMenuScene))
        {
            EditorSceneManager.OpenScene(MainMenuScene);
            BuildMainMenu();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        foreach (var path in LevelScenes())
        {
            EditorSceneManager.OpenScene(path);
            SetupLevel();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
        Debug.Log("[NotebookUI] Main menu, pause menu, level HUDs and scene transition updated.");
    }

    [MenuItem("Girotronica/UI/Rebuild Main Menu (Open Scene)")]
    public static void BuildMainMenuMenu()
    {
        BuildMainMenu();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    [MenuItem("Girotronica/UI/Setup Level UI (Open Scene)")]
    public static void SetupLevelMenu()
    {
        BuildPausePrefab();
        SetupLevel();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static string[] LevelScenes() =>
        Directory.GetFiles("Assets/Scenes", "Level*.unity")
            .Select(p => p.Replace('\\', '/'))
            .Where(p => Regex.IsMatch(Path.GetFileNameWithoutExtension(p), @"^Level\d+$"))
            .OrderBy(p => int.Parse(Regex.Match(p, @"Level(\d+)").Groups[1].Value))
            .ToArray();

    // ================================================================== main menu

    public static void BuildMainMenu()
    {
        LoadFonts();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "MainMenuCanvas") Object.DestroyImmediate(root);
        EnsureEventSystem();

        var canvas = NewCanvas("MainMenuCanvas", 0);
        var ct = canvas.transform;

        PrepareSourceArt();

        // ---- wood desk (full screen)
        var desk = Img(ct, "Desk", LoadSprite(System.IO.File.Exists(DeskArt.WoodPath) ? DeskArt.WoodPath : "Assets/Art/Captura de pantalla 2026-11-21 173050.png"), new Color(0.9f, 0.88f, 0.85f));
        Stretch(desk.rectTransform);
        var fitter = desk.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = desk.sprite != null ? desk.sprite.rect.width / desk.sprite.rect.height : 16f / 9f;

        // ---- illustration: covers the right part of the screen, full height, torn edge on its left side
        var artSprite = LoadSprite(MenuArtPath);
        var art = Img(ct, "MenuArt", artSprite, Color.white);
        var artRt = art.rectTransform;
        artRt.anchorMin = new Vector2(1, 0);
        artRt.anchorMax = new Vector2(1, 1);
        artRt.pivot = new Vector2(1, 0.5f);
        artRt.anchoredPosition = Vector2.zero;
        artRt.sizeDelta = new Vector2(1000, 0);
        var artFit = art.gameObject.AddComponent<AspectRatioFitter>();
        artFit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        artFit.aspectRatio = artSprite != null ? artSprite.rect.width / artSprite.rect.height : 2000f / 2160f;

        // ---- left column: title (always visible) + swappable content
        var left = Rect(ct, "LeftColumn");
        left.anchorMin = new Vector2(0, 0);
        left.anchorMax = new Vector2(0, 1);
        left.pivot = new Vector2(0, 0.5f);
        left.anchoredPosition = Vector2.zero;
        left.sizeDelta = new Vector2(920, 0);

        var title = Txt(left, "Title", GameTitle, marker, 140, NotebookUIArt.Paper, TextAnchor.MiddleCenter);
        TopBand(title.rectTransform, -90, 190);
        title.rectTransform.offsetMin = new Vector2(60, title.rectTransform.offsetMin.y);
        title.rectTransform.offsetMax = new Vector2(-40, title.rectTransform.offsetMax.y);
        title.horizontalOverflow = HorizontalWrapMode.Wrap;
        title.verticalOverflow = VerticalWrapMode.Truncate;
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 50;
        title.resizeTextMaxSize = 140;
        var titleInk = title.gameObject.AddComponent<Outline>();
        titleInk.effectColor = NotebookUIArt.Ink;
        titleInk.effectDistance = new Vector2(6, -6);
        var titleDrop = title.gameObject.AddComponent<Shadow>();
        titleDrop.effectColor = new Color(0, 0, 0, 0.45f);
        titleDrop.effectDistance = new Vector2(12, -14);
        title.transform.localRotation = Quaternion.Euler(0, 0, -3f);
        title.gameObject.AddComponent<UIIdleSway>().bob = 4f;
        var titleLine = Img(left, "TitleUnderline", NotebookUIArt.Get(NotebookUIArt.Underline), NotebookUIArt.Ink, Image.Type.Sliced);
        titleLine.rectTransform.anchorMin = titleLine.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        titleLine.rectTransform.sizeDelta = new Vector2(600, 44);
        titleLine.rectTransform.anchoredPosition = new Vector2(20, -300);
        titleLine.transform.localRotation = Quaternion.Euler(0, 0, -3f);

        // Main content: the buttons, straight on the wood
        var mainPanel = Rect(left, "MainPanel");
        Stretch(mainPanel);
        var play = MakeButton(mainPanel, "PlayButton", "Jugar", new Vector2(0, 40), new Vector2(480, 124), -2f, 68);
        var options = MakeButton(mainPanel, "OptionsButton", "Opcions", new Vector2(0, -120), new Vector2(480, 124), 1.5f, 62);
        var quit = MakeButton(mainPanel, "QuitButton", "Sortir", new Vector2(0, -280), new Vector2(480, 124), -1f, 62);
        var footer = Txt(left, "Footer", "Game Jam Girotrònica 2026", hand, 32, NotebookUIArt.Paper, TextAnchor.MiddleCenter);
        BottomBand(footer.rectTransform, 30, 50);
        var footerInk = footer.gameObject.AddComponent<Outline>();
        footerInk.effectColor = new Color(NotebookUIArt.Ink.r, NotebookUIArt.Ink.g, NotebookUIArt.Ink.b, 0.8f);
        footerInk.effectDistance = new Vector2(2, -2);

        // Options content: same place, on a taped paper sheet so the labels read on the wood
        var optionsPanel = Rect(left, "OptionsPanel");
        Stretch(optionsPanel);
        var sheet = Img(optionsPanel, "Sheet", NotebookUIArt.Get(NotebookUIArt.PaperPage), Color.white, Image.Type.Sliced);
        Place(sheet.rectTransform, new Vector2(0, -150), new Vector2(720, 680));
        sheet.transform.localRotation = Quaternion.Euler(0, 0, -1.5f);
        Tape(sheet.rectTransform, new Vector2(-280, 320), 35f);
        Tape(sheet.rectTransform, new Vector2(280, 320), -35f);
        var sheetContent = Rect(sheet.rectTransform, "Content");
        Stretch(sheetContent);
        sheetContent.offsetMin = new Vector2(60, 40);
        sheetContent.offsetMax = new Vector2(-60, -30);
        var back = BuildOptions(sheetContent, 560f, 112f);
        InstallLanguageRow(sheetContent.GetComponent<OptionsMenu>(), MainMenuOptionsLayout);
        ResizeMainMenuSheet(sheet.rectTransform);
        optionsPanel.gameObject.SetActive(false);

        // ---- wire MainMenu
        var menu = Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include);
        if (menu == null) menu = new GameObject("MainMenu").AddComponent<MainMenu>();
        Undo.RecordObject(menu, "Wire MainMenu");
        menu.mainPanel = mainPanel.gameObject;
        menu.optionsPanel = optionsPanel.gameObject;
        menu.playButton = play;
        menu.optionsButton = options;
        menu.quitButton = quit;
        menu.backButton = back;
        EditorUtility.SetDirty(menu);

        var cam = Camera.main;
        if (cam != null) cam.backgroundColor = new Color(0.23f, 0.16f, 0.11f);
        Selection.activeGameObject = canvas.gameObject;
    }

    // ================================================================== end credits

    [MenuItem("Girotronica/UI/Build End Credits UI (Open Scene)")]
    public static void BuildEndCreditsMenu()
    {
        BuildEndCredits();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    /// <summary>
    /// "Gràcies per jugar" on the left of the screen + a button back to the main menu.
    /// Only UI: the wood desk and the girl are scene objects of EndCredits.
    /// </summary>
    public static void BuildEndCredits()
    {
        LoadFonts();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "EndCreditsCanvas") Object.DestroyImmediate(root);
        EnsureEventSystem();

        var canvas = NewCanvas("EndCreditsCanvas", 0);
        var left = Rect(canvas.transform, "LeftColumn");
        left.anchorMin = new Vector2(0, 0);
        left.anchorMax = new Vector2(0, 1);
        left.pivot = new Vector2(0, 0.5f);
        left.anchoredPosition = Vector2.zero;
        left.sizeDelta = new Vector2(1000, 0);

        var thanks = Txt(left, "Thanks", "Gràcies\nper jugar", marker, 160, NotebookUIArt.Paper, TextAnchor.MiddleCenter);
        Place(thanks.rectTransform, new Vector2(0, 130), new Vector2(900, 440));
        thanks.lineSpacing = 0.9f;
        var ink = thanks.gameObject.AddComponent<Outline>();
        ink.effectColor = NotebookUIArt.Ink;
        ink.effectDistance = new Vector2(6, -6);
        var drop = thanks.gameObject.AddComponent<Shadow>();
        drop.effectColor = new Color(0, 0, 0, 0.45f);
        drop.effectDistance = new Vector2(12, -14);
        thanks.transform.localRotation = Quaternion.Euler(0, 0, -3f);
        thanks.gameObject.AddComponent<UIIdleSway>().bob = 5f;

        var button = MakeButton(left, "MainMenuButton", "Tornar al menú", new Vector2(0, -300), new Vector2(520, 128), -1.5f, 62);

        var end = canvas.gameObject.AddComponent<EndCreditsMenu>();
        end.menuButton = button;
        Selection.activeGameObject = canvas.gameObject;
    }

    /// <summary>Volume rows + back button. Adds OptionsMenu to the panel. Returns the back button.</summary>
    static Button BuildOptions(RectTransform panel, float width, float rowStep)
    {
        Heading(panel, "Opcions", -1.5f);
        var opt = panel.gameObject.AddComponent<OptionsMenu>();
        opt.masterSlider = VolumeRow(panel, "MasterRow", "Volum general", rowStep, width, out opt.masterValueText);
        opt.musicSlider = VolumeRow(panel, "MusicRow", "Música", 0f, width, out opt.musicValueText);
        opt.sfxSlider = VolumeRow(panel, "SfxRow", "Efectes de so", -rowStep, width, out opt.sfxValueText);
        return MakeButton(panel, "BackButton", "Tornar", new Vector2(0, -rowStep * 2f - 30f), new Vector2(340, 104), 1.5f, 54);
    }

    static Slider VolumeRow(RectTransform parent, string name, string label, float y, float width, out Text value)
    {
        var row = Rect(parent, name);
        Place(row, new Vector2(0, y), new Vector2(width, 110));

        var l = Txt(row, "Label", label, hand, 44, NotebookUIArt.Ink, TextAnchor.MiddleLeft);
        Anchor(l.rectTransform, new Vector2(0, 0.5f), new Vector2(0.75f, 1f));
        value = Txt(row, "Value", "100%", hand, 40, NotebookUIArt.Orange, TextAnchor.MiddleRight);
        Anchor(value.rectTransform, new Vector2(0.7f, 0.5f), new Vector2(1f, 1f));

        var sliderRt = Rect(row, "Slider");
        Anchor(sliderRt, new Vector2(0, 0.05f), new Vector2(1, 0.45f));
        var slider = sliderRt.gameObject.AddComponent<Slider>();

        var bg = Img(sliderRt, "Background", NotebookUIArt.Get(NotebookUIArt.SliderTrack), Color.white, Image.Type.Sliced);
        bg.raycastTarget = true;   // click anywhere on the track to set the value
        Stretch(bg.rectTransform);
        var fillArea = Rect(sliderRt, "Fill Area");
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(6, 0);
        fillArea.offsetMax = new Vector2(-6, 0);
        var fill = Img(fillArea, "Fill", NotebookUIArt.Get(NotebookUIArt.SliderFill), NotebookUIArt.Yellow, Image.Type.Sliced);
        fill.rectTransform.sizeDelta = new Vector2(0, 0);
        var handleArea = Rect(sliderRt, "Handle Slide Area");
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(20, 0);
        handleArea.offsetMax = new Vector2(-20, 0);
        var handle = Img(handleArea, "Handle", NotebookUIArt.Get(NotebookUIArt.SliderHandle), Color.white);
        handle.preserveAspect = true;
        handle.raycastTarget = true;
        handle.rectTransform.sizeDelta = new Vector2(64, 24);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.colors = ButtonColors();
        return slider;
    }

    // ================================================================== pause menu prefab

    public static GameObject BuildPausePrefab()
    {
        LoadFonts();
        Directory.CreateDirectory(Path.GetDirectoryName(PausePrefabPath));

        var canvas = NewCanvas("PauseMenu", 20);
        var root = canvas.gameObject;
        var ct = canvas.transform;

        var pauseButton = MakeButton(ct, "PauseButton", "II", Vector2.zero, new Vector2(104, 96), 3f, 52, marker);
        var pbRt = (RectTransform)pauseButton.transform.parent;
        pbRt.anchorMin = pbRt.anchorMax = new Vector2(1, 1);
        pbRt.anchoredPosition = new Vector2(-84, -76);

        var pausePanel = Rect(ct, "PausePanel");
        Stretch(pausePanel);
        var dim = Img(pausePanel, "Dim", null, new Color(0.08f, 0.06f, 0.05f, 0.62f));
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);

        var page = Img(pausePanel, "Page", NotebookUIArt.Get(NotebookUIArt.PaperPage), Color.white, Image.Type.Sliced);
        Place(page.rectTransform, new Vector2(0, -10), new Vector2(800, 900));
        page.transform.localRotation = Quaternion.Euler(0, 0, -1.5f);
        var pageRt = page.rectTransform;
        Tape(pageRt, new Vector2(-300, 420), 35f);
        Tape(pageRt, new Vector2(300, 420), -35f);

        var main = Rect(pageRt, "MainContent");
        Stretch(main);
        main.offsetMin = new Vector2(60, 60);
        main.offsetMax = new Vector2(-60, -60);
        Heading(main, "Pausa", -2f, 120);
        var resume = MakeButton(main, "ResumeButton", "Tornar al joc", new Vector2(0, 90), new Vector2(560, 112), -1.5f, 54);
        var optionsB = MakeButton(main, "OptionsButton", "Opcions", new Vector2(0, -50), new Vector2(560, 112), 1.2f, 54);
        var menuB = MakeButton(main, "MainMenuButton", "Sortir al menú", new Vector2(0, -190), new Vector2(560, 112), -0.8f, 54);
        var hint = Txt(main, "Hint", "Prem Esc per continuar", hand, 32, NotebookUIArt.Pencil, TextAnchor.MiddleCenter);
        BottomBand(hint.rectTransform, 0, 50);

        var options = Rect(pageRt, "OptionsPanel");
        Stretch(options);
        options.offsetMin = new Vector2(70, 60);
        options.offsetMax = new Vector2(-70, -60);
        var back = BuildOptions(options, 600f, 140f);
        options.gameObject.SetActive(false);
        pausePanel.gameObject.SetActive(false);

        var pm = root.AddComponent<PauseMenu>();
        var so = new SerializedObject(pm);
        so.FindProperty("pausePanel").objectReferenceValue = pausePanel.gameObject;
        so.FindProperty("mainContent").objectReferenceValue = main.gameObject;
        so.FindProperty("optionsPanel").objectReferenceValue = options.gameObject;
        so.FindProperty("resumeButton").objectReferenceValue = resume;
        so.FindProperty("optionsButton").objectReferenceValue = optionsB;
        so.FindProperty("mainMenuButton").objectReferenceValue = menuB;
        so.FindProperty("backButton").objectReferenceValue = back;
        so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        InstallPauseExtras(root);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PausePrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ================================================================== level HUD

    public static void SetupLevel()
    {
        LoadFonts();
        var scene = SceneManager.GetActiveScene();

        // Pause menu: replace the old scene-built one with the prefab
        foreach (var root in scene.GetRootGameObjects())
        {
            bool isPrefab = PrefabUtility.GetCorrespondingObjectFromSource(root) != null &&
                            AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root)) == PausePrefabPath;
            if (isPrefab || root.name == "PauseCanvas" || root.GetComponentInChildren<PauseMenu>(true) != null)
                Object.DestroyImmediate(root);
        }
        var pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefabPath);
        if (pausePrefab != null) PrefabUtility.InstantiatePrefab(pausePrefab, scene);
        EnsureEventSystem();

        // Countdown in a speech bubble
        var manager = Object.FindAnyObjectByType<LevelManager>();
        if (manager == null || manager.countdownText == null) manager = LevelUIBuilder.AddToOpenScene();
        var so = new SerializedObject(manager);
        so.FindProperty("countdownSeconds").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var text = manager.countdownText;
        var hud = text.canvas != null ? text.canvas.transform : text.transform.parent;
        text.font = marker;
        text.fontSize = 150;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 40;
        text.resizeTextMaxSize = 150;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.color = NotebookUIArt.Ink;
        text.alignment = TextAnchor.MiddleCenter;
        var trt = text.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 0.5f);
        // Shifted right so it looks centred over the play area (the pencil case takes the left side).
        trt.anchoredPosition = new Vector2(130, -170);
        trt.sizeDelta = new Vector2(1000, 200);
        text.color = NotebookUIArt.Paper;
        text.text = "3";
        foreach (var fx in text.GetComponents<Shadow>()) Object.DestroyImmediate(fx);
        var ink = text.gameObject.AddComponent<Outline>();
        ink.effectColor = NotebookUIArt.Ink;
        ink.effectDistance = new Vector2(5, -5);
        var drop = text.gameObject.AddComponent<Shadow>();
        drop.effectColor = new Color(0, 0, 0, 0.45f);
        drop.effectDistance = new Vector2(10, -12);

        // No speech bubble any more (removes the one from older builds)
        var oldBubble = hud.Find("CountdownBubble");
        if (oldBubble != null) Object.DestroyImmediate(oldBubble.gameObject);
        var punch = text.GetComponent<TextPunch>();
        if (punch == null) punch = text.gameObject.AddComponent<TextPunch>();
        punch.bubble = null;

        // "Nivell N" label on a taped paper tag
        var oldTag = hud.Find("LevelTag");
        if (oldTag != null) Object.DestroyImmediate(oldTag.gameObject);
        var m = Regex.Match(scene.name, @"\d+");
        var tag = Rect(hud, "LevelTag");
        tag.anchorMin = tag.anchorMax = new Vector2(0, 1);
        tag.anchoredPosition = new Vector2(170, -76);
        tag.sizeDelta = new Vector2(270, 96);
        tag.localRotation = Quaternion.Euler(0, 0, -2.5f);
        Halftone(tag, new Vector2(8, -8));
        var tagFrame = Img(tag, "Frame", NotebookUIArt.Get(NotebookUIArt.ButtonFrame), Color.white, Image.Type.Sliced);
        Stretch(tagFrame.rectTransform);
        var tagText = Txt(tag, "Text", m.Success ? "Nivell " + m.Value : scene.name, marker, 50, NotebookUIArt.Ink, TextAnchor.MiddleCenter);
        Stretch(tagText.rectTransform);
        Tape(tag, new Vector2(0, 48), 4f, 0.8f);
        tag.SetAsFirstSibling();
    }

    // ================================================================== scene transition prefab

    static void BuildTransitionPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(TransitionPrefabPath));
        var canvas = NewCanvas("SceneTransition", 1000);
        var root = canvas.gameObject;
        var group = root.AddComponent<CanvasGroup>();

        var cover = Img(canvas.transform, "Cover", null, new Color(NotebookUIArt.Paper.r, NotebookUIArt.Paper.g, NotebookUIArt.Paper.b, 0f));
        cover.raycastTarget = true;
        Stretch(cover.rectTransform);
        var paper = Img(canvas.transform, "Paper", null, Color.white);
        Stretch(paper.rectTransform);
        paper.rectTransform.offsetMin = new Vector2(-260, -200);
        paper.rectTransform.offsetMax = new Vector2(260, 200);
        paper.enabled = false;

        const string fx = "Assets/Art/Effects and extras/";
        var names = new[]
        {
            "papers1ReRe2-removebg-preview", "papers2ReRe2_nobg", "papers3ReRe2_nobg", "papers4ReRe2_nobg",
            "papers5ReRe2_nobg", "papers6ReRe2_nobg", "papers7ReRe2"
        };
        var t = root.AddComponent<SceneTransition>();
        t.group = group;
        t.cover = cover;
        t.paper = paper;
        t.frames = names.Select(n => LoadSprite(fx + n + ".png")).Where(s => s != null).ToArray();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        PrefabUtility.SaveAsPrefabAsset(root, TransitionPrefabPath);
        Object.DestroyImmediate(root);
    }

    // ================================================================== source art import settings

    static void PrepareSourceArt()
    {
        // Big menu images: import them at full resolution.
        foreach (var path in new[] { "Assets/Art/llibreta.png", MenuArtPath })
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && imp.maxTextureSize < 4096)
            {
                imp.maxTextureSize = 4096;
                imp.SaveAndReimport();
            }
        }
    }

    // ================================================================== building blocks

    static void LoadFonts()
    {
        marker = AssetDatabase.LoadAssetAtPath<Font>($"{FontsFolder}/PermanentMarker-Regular.ttf");
        hand = AssetDatabase.LoadAssetAtPath<Font>($"{FontsFolder}/PatrickHand-Regular.ttf");
        var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (marker == null) marker = fallback;
        if (hand == null) hand = fallback;
    }

    static Sprite LoadSprite(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();

    static Canvas NewCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
    }

    static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Color color, Image.Type type = Image.Type.Simple)
    {
        var img = Rect(parent, name).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = type;
        img.raycastTarget = false;
        return img;
    }

    static Text Txt(Transform parent, string name, string value, Font font, int size, Color color, TextAnchor align)
    {
        var t = Rect(parent, name).gameObject.AddComponent<Text>();
        t.text = value;
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void TopBand(RectTransform rt, float y, float height)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, height);
    }

    static void BottomBand(RectTransform rt, float y, float height)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, height);
    }

    /// <summary>Marker heading at the top of a page with an orange scribble under it.</summary>
    static void Heading(RectTransform parent, string text, float angle, int size = 90)
    {
        var t = Txt(parent, "Heading", text, marker, size, NotebookUIArt.Ink, TextAnchor.MiddleCenter);
        TopBand(t.rectTransform, -10, size + 40);
        t.transform.localRotation = Quaternion.Euler(0, 0, angle);
        var line = Img(parent, "HeadingUnderline", NotebookUIArt.Get(NotebookUIArt.Underline), NotebookUIArt.Orange, Image.Type.Sliced);
        line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        line.rectTransform.anchoredPosition = new Vector2(0, -(size + 46));
        line.rectTransform.sizeDelta = new Vector2(Mathf.Max(220, text.Length * size * 0.55f), 32);
        line.transform.localRotation = Quaternion.Euler(0, 0, angle);
    }

    static void Halftone(RectTransform parent, Vector2 offset)
    {
        var h = Img(parent, "HalftoneShadow", NotebookUIArt.Get(NotebookUIArt.Halftone), new Color(NotebookUIArt.Ink.r, NotebookUIArt.Ink.g, NotebookUIArt.Ink.b, 0.55f), Image.Type.Tiled);
        Stretch(h.rectTransform);
        h.rectTransform.offsetMin = offset + new Vector2(4, 4);
        h.rectTransform.offsetMax = offset - new Vector2(4, 4);
        h.transform.SetAsFirstSibling();
    }

    static void Tape(RectTransform parent, Vector2 pos, float angle, float scale = 1f)
    {
        var t = Img(parent, "Tape", NotebookUIArt.Get(NotebookUIArt.Tape), Color.white);
        Place(t.rectTransform, pos, new Vector2(176, 56) * scale);
        t.transform.localRotation = Quaternion.Euler(0, 0, angle);
    }

    static ColorBlock ButtonColors()
    {
        var c = ColorBlock.defaultColorBlock;
        c.normalColor = Color.white;
        c.highlightedColor = NotebookUIArt.Yellow;
        c.selectedColor = NotebookUIArt.Yellow;
        c.pressedColor = new Color(0.97f, 0.70f, 0.45f);
        c.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        return c;
    }

    /// <summary>
    /// Comic-panel button: halftone shadow + ink frame (tinted yellow like a highlighter on hover) + handwritten label.
    /// Returns the Button (on the "Frame"); its parent is the container that wobbles.
    /// </summary>
    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, float angle, int fontSize, Font font = null)
    {
        var root = Rect(parent, name);
        Place(root, pos, size);
        root.localRotation = Quaternion.Euler(0, 0, angle);
        Halftone(root, new Vector2(10, -10));

        var frame = Img(root, "Frame", NotebookUIArt.Get(NotebookUIArt.ButtonFrame), Color.white, Image.Type.Sliced);
        frame.raycastTarget = true;
        Stretch(frame.rectTransform);
        var button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.colors = ButtonColors();
        var nav = button.navigation;
        nav.mode = Navigation.Mode.Vertical;
        button.navigation = nav;

        var text = Txt(frame.transform, "Label", label, font != null ? font : hand, fontSize, NotebookUIArt.Ink, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(10, 4);
        text.rectTransform.offsetMax = new Vector2(-10, 0);

        var wobble = frame.gameObject.AddComponent<UIWobbleButton>();
        wobble.straighten = 0f;
        return button;
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Post-jam additions to the notebook UI (without rebuilding the existing menus):
///  - Language row ("&lt; English &gt;") in the options panels of the main menu and of the pause menu.
///  - "?" button on the pause page + the "How to play" pop-up (HelpPopup) inside the PauseMenu prefab.
/// Girotronica > UI > Add Help + Language UI does both and saves. It can be run again (it replaces its own objects).
/// The tutorial clips are sprite sheets: Assets/Art/UI/Tutorial/&lt;clip&gt;.png + &lt;clip&gt;.sheet.txt
/// ("columns rows frames frameWidth frameHeight fps"); the sheet is sliced into one sprite per frame on import.
/// </summary>
public static partial class NotebookUIBuilder
{
    const string TutorialFolder = "Assets/Art/UI/Tutorial";

    // y of: language, master, music, sfx rows and back button (options content panel coordinates)
    static readonly float[] MainMenuOptionsLayout = { 155f, 57f, -53f, -163f, -280f };
    static readonly float[] PauseOptionsLayout = { 200f, 95f, -25f, -145f, -290f };

    struct HelpPageDef
    {
        public string name, key, clip, mouse;
        public HelpPageDef(string name, string key, string clip, string mouse) { this.name = name; this.key = key; this.clip = clip; this.mouse = mouse; }
    }

    static readonly HelpPageDef[] HelpPages =
    {
        new HelpPageDef("Grab", "helpGrab", "grab", "mouse_left"),
        new HelpPageDef("Rotate", "helpRotate", "rotate", "mouse_right"),
        new HelpPageDef("Bridge", "helpBridge", "ruler", null),
        new HelpPageDef("Bounce", "helpBounce", "eraser", null),
    };

    [MenuItem("Girotronica/UI/Add Help + Language UI (Pause prefab + Main Menu)")]
    public static void ApplyPostJam()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[NotebookUI] Stop Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;
        LoadFonts();
        PrepareTutorialArt();

        // Pause menu prefab (used by every level)
        var root = PrefabUtility.LoadPrefabContents(PausePrefabPath);
        InstallPauseExtras(root);
        PrefabUtility.SaveAsPrefabAsset(root, PausePrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        // Main menu options panel
        if (File.Exists(MainMenuScene))
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScene);
            var options = Object.FindAnyObjectByType<OptionsMenu>(FindObjectsInactive.Include);
            if (options != null)
            {
                InstallLanguageRow(options, MainMenuOptionsLayout);
                var sheet = options.transform.parent as RectTransform;
                if (sheet != null && sheet.name == "Sheet") ResizeMainMenuSheet(sheet);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            else Debug.LogWarning("[NotebookUI] No OptionsMenu found in the main menu.");
        }

        if (!string.IsNullOrEmpty(previous) && File.Exists(previous) && previous != SceneManager.GetActiveScene().path)
            EditorSceneManager.OpenScene(previous);
        Debug.Log("[NotebookUI] Language selector + help pop-up added (PauseMenu prefab and main menu).");
    }

    // ================================================================== language row

    /// <summary>The main menu options sheet grows to fit the language row.</summary>
    static void ResizeMainMenuSheet(RectTransform sheet)
    {
        sheet.anchoredPosition = new Vector2(sheet.anchoredPosition.x, -160f);
        sheet.sizeDelta = new Vector2(sheet.sizeDelta.x, 760f);
        foreach (Transform child in sheet)
            if (child.name == "Tape")
            {
                var rt = (RectTransform)child;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 360f);
            }
    }

    static void InstallLanguageRow(OptionsMenu options, float[] layout)
    {
        if (options == null) return;
        LoadFonts();
        var panel = (RectTransform)options.transform;
        var old = panel.Find("LanguageRow");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // move the existing rows down to make room
        string[] names = { "MasterRow", "MusicRow", "SfxRow", "BackButton" };
        float width = 560f;
        for (int i = 0; i < names.Length; i++)
        {
            var rt = panel.Find(names[i]) as RectTransform;
            if (rt == null) continue;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, layout[i + 1]);
            if (names[i] == "MasterRow") width = rt.sizeDelta.x;
        }

        var row = Rect(panel, "LanguageRow");
        Place(row, new Vector2(0, layout[0]), new Vector2(width, 80));
        row.SetSiblingIndex(Mathf.Max(0, (panel.Find("MasterRow")?.GetSiblingIndex() ?? 0)));

        var label = Txt(row, "Label", Loc.Get("language"), hand, 44, NotebookUIArt.Ink, TextAnchor.MiddleLeft);
        Anchor(label.rectTransform, new Vector2(0, 0), new Vector2(0.42f, 1f));
        label.gameObject.AddComponent<LocalizedText>().key = "language";

        var selector = Rect(row, "Selector");
        Anchor(selector, new Vector2(0.42f, 0), new Vector2(1f, 1f));
        float selW = width * 0.58f;
        var prev = MakeButton(selector, "PrevButton", "<", new Vector2(-selW / 2f + 36f, 0), new Vector2(68, 66), 3f, 40, marker);
        var next = MakeButton(selector, "NextButton", ">", new Vector2(selW / 2f - 36f, 0), new Vector2(68, 66), -3f, 40, marker);
        foreach (var b in new[] { prev, next })
        {
            var nav = b.navigation;
            nav.mode = Navigation.Mode.Automatic;
            b.navigation = nav;
        }
        var value = Txt(selector, "Value", Loc.LanguageName(Loc.Current), hand, 44, NotebookUIArt.Orange, TextAnchor.MiddleCenter);
        Stretch(value.rectTransform);
        value.rectTransform.offsetMin = new Vector2(76, 0);
        value.rectTransform.offsetMax = new Vector2(-76, 0);

        var ls = row.gameObject.AddComponent<LanguageSelector>();
        ls.valueText = value;
        ls.previousButton = prev;
        ls.nextButton = next;
        EditorUtility.SetDirty(ls);
    }

    // ================================================================== pause menu: "?" + pop-up

    static void InstallPauseExtras(GameObject root)
    {
        LoadFonts();
        var pm = root.GetComponent<PauseMenu>();
        var page = root.transform.Find("PausePanel/Page") as RectTransform;
        var main = page != null ? page.Find("MainContent") as RectTransform : null;
        var options = root.GetComponentInChildren<OptionsMenu>(true);
        InstallLanguageRow(options, PauseOptionsLayout);

        Button help = null;
        if (main != null)
        {
            var old = main.Find("HelpButton");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            help = MakeButton(main, "HelpButton", "?", new Vector2(270, 305), new Vector2(96, 90), 4f, 56, marker);
        }

        var popup = BuildHelpPopup(root.transform);

        if (pm != null)
        {
            var so = new SerializedObject(pm);
            so.FindProperty("helpButton").objectReferenceValue = help;
            so.FindProperty("helpPopup").objectReferenceValue = popup;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static HelpPopup BuildHelpPopup(Transform canvas)
    {
        var old = canvas.Find("HelpPopup");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var rootRt = Rect(canvas, "HelpPopup");
        Stretch(rootRt);
        rootRt.SetAsLastSibling();
        var popup = rootRt.gameObject.AddComponent<HelpPopup>();

        var panel = Rect(rootRt, "Panel");
        Stretch(panel);
        var dim = Img(panel, "Dim", null, new Color(0.08f, 0.06f, 0.05f, 0.7f));
        dim.raycastTarget = true;
        Stretch(dim.rectTransform);

        var sheet = Img(panel, "Sheet", NotebookUIArt.Get(NotebookUIArt.PaperPage), Color.white, Image.Type.Sliced);
        Place(sheet.rectTransform, new Vector2(0, -10), new Vector2(1240, 900));
        sheet.transform.localRotation = Quaternion.Euler(0, 0, -1f);
        Tape(sheet.rectTransform, new Vector2(-480, 420), 35f);
        Tape(sheet.rectTransform, new Vector2(480, 420), -35f);

        var content = Rect(sheet.rectTransform, "Content");
        Stretch(content);
        content.offsetMin = new Vector2(70, 60);
        content.offsetMax = new Vector2(-70, -60);

        Heading(content, Loc.Get("helpTitle"), -1.5f, 90);
        var heading = content.Find("Heading");
        if (heading != null) heading.gameObject.AddComponent<LocalizedText>().key = "helpTitle";

        var close = MakeButton(content, "CloseButton", "X", new Vector2(500, 330), new Vector2(84, 80), 4f, 46, marker);

        // pages
        var pages = new GameObject[HelpPages.Length];
        for (int i = 0; i < HelpPages.Length; i++)
            pages[i] = BuildHelpPage(content, HelpPages[i]).gameObject;

        var prev = MakeButton(content, "PrevButton", "<", new Vector2(-440, 48), new Vector2(96, 96), 3f, 56, marker);
        var next = MakeButton(content, "NextButton", ">", new Vector2(440, 48), new Vector2(96, 96), -3f, 56, marker);
        var gotIt = MakeButton(content, "GotItButton", Loc.Get("helpGotIt"), new Vector2(374, -345), new Vector2(380, 104), -1.5f, 54);
        gotIt.GetComponentInChildren<Text>(true).gameObject.AddComponent<LocalizedText>().key = "helpGotIt";
        var counter = Txt(content, "PageCounter", "1 / " + pages.Length, hand, 36, NotebookUIArt.Pencil, TextAnchor.MiddleLeft);
        Place(counter.rectTransform, new Vector2(-420, -340), new Vector2(200, 50));

        popup.panel = panel.gameObject;
        popup.pages = pages;
        popup.previousButton = prev;
        popup.nextButton = next;
        popup.closeButton = close;
        popup.gotItButton = gotIt;
        popup.pageCounter = counter;
        panel.gameObject.SetActive(false);
        EditorUtility.SetDirty(popup);
        return popup;
    }

    static RectTransform BuildHelpPage(RectTransform content, HelpPageDef def)
    {
        var page = Rect(content, "Page" + def.name);
        Stretch(page);

        // the clip, on a white sticker with a halftone shadow
        var clipRoot = Rect(page, "ClipSticker");
        Place(clipRoot, new Vector2(0, 48), new Vector2(620, 336));
        clipRoot.localRotation = Quaternion.Euler(0, 0, def.name.Length % 2 == 0 ? -1f : 1f);
        Halftone(clipRoot, new Vector2(10, -10));
        var frame = Img(clipRoot, "Frame", NotebookUIArt.Get(NotebookUIArt.ButtonFrame), Color.white, Image.Type.Sliced);
        Stretch(frame.rectTransform);
        var clip = Img(clipRoot, "Clip", null, Color.white);
        Stretch(clip.rectTransform);
        clip.rectTransform.offsetMin = new Vector2(22, 20);
        clip.rectTransform.offsetMax = new Vector2(-22, -20);
        clip.preserveAspect = true;
        var frames = LoadClip(def.clip, out float clipFps);
        if (frames.Length > 0) clip.sprite = frames[0];
        else clip.color = new Color(1, 1, 1, 0);
        var flip = clip.gameObject.AddComponent<UIFlipbook>();
        flip.frames = frames;
        flip.framesPerSecond = clipFps;

        if (!string.IsNullOrEmpty(def.mouse))
        {
            var mouse = Img(clipRoot, "Mouse", LoadSprite($"{TutorialFolder}/{def.mouse}.png"), Color.white);
            mouse.preserveAspect = true;
            var mrt = mouse.rectTransform;
            // bottom-left corner, inside the white sticker (Kenney Input Prompts icons, CC0)
            mrt.anchorMin = mrt.anchorMax = new Vector2(0, 0);
            mrt.anchoredPosition = new Vector2(66, 62);
            mrt.sizeDelta = new Vector2(76, 76);
            mouse.transform.localRotation = Quaternion.Euler(0, 0, -6f);
            // the icon is white: an ink outline makes it read on the white sticker
            var ink = mouse.gameObject.AddComponent<Outline>();
            ink.effectColor = NotebookUIArt.Ink;
            ink.effectDistance = new Vector2(3f, -3f);
        }

        var caption = Txt(page, "Caption", Loc.Get(def.key), hand, 46, NotebookUIArt.Ink, TextAnchor.MiddleCenter);
        Place(caption.rectTransform, new Vector2(0, -228), new Vector2(940, 104));
        caption.horizontalOverflow = HorizontalWrapMode.Wrap;
        caption.verticalOverflow = VerticalWrapMode.Truncate;
        caption.resizeTextForBestFit = true;
        caption.resizeTextMinSize = 26;
        caption.resizeTextMaxSize = 46;
        caption.lineSpacing = 0.9f;
        caption.gameObject.AddComponent<LocalizedText>().key = def.key;
        return page;
    }

    struct SheetInfo { public int cols, rows, count, w, h; public float fps; }

    static bool ReadSheetInfo(string clip, out SheetInfo info)
    {
        info = default;
        string txt = $"{TutorialFolder}/{clip}.sheet.txt";
        if (!File.Exists(txt)) return false;
        var v = File.ReadAllText(txt).Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        if (v.Length < 6) return false;
        info.cols = int.Parse(v[0]); info.rows = int.Parse(v[1]); info.count = int.Parse(v[2]);
        info.w = int.Parse(v[3]); info.h = int.Parse(v[4]);
        info.fps = float.Parse(v[5], System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Frames of a clip (sprites sliced from its sheet, in order) and its frames per second.</summary>
    static Sprite[] LoadClip(string clip, out float fps)
    {
        fps = 10f;
        string sheet = $"{TutorialFolder}/{clip}.png";
        if (!File.Exists(sheet) || !ReadSheetInfo(clip, out var info)) return new Sprite[0];
        fps = info.fps;
        return AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>()
            .Where(s => s.name.StartsWith(clip + "_")).OrderBy(s => s.name).ToArray();
    }

    /// <summary>
    /// Tutorial art import: the clip sheets are sliced into one sprite per frame (clip_000, clip_001...),
    /// the icons are single sprites. No mipmaps, crunch compressed (small download for the web build).
    /// </summary>
    static void PrepareTutorialArt()
    {
        if (!Directory.Exists(TutorialFolder)) return;
        foreach (var path in Directory.GetFiles(TutorialFolder, "*.png", SearchOption.TopDirectoryOnly).Select(p => p.Replace('\\', '/')))
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            string clip = Path.GetFileNameWithoutExtension(path);
            bool isSheet = ReadSheetInfo(clip, out var info);

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = isSheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.crunchedCompression = true;
            imp.compressionQuality = 60;

            if (isSheet)
            {
                int texH = info.rows * info.h;
                var factory = new SpriteDataProviderFactories();
                factory.Init();
                var dp = factory.GetSpriteEditorDataProviderFromObject(imp);
                dp.InitSpriteEditorDataProvider();
                var rects = new List<SpriteRect>();
                for (int i = 0; i < info.count; i++)
                {
                    int c = i % info.cols, r = i / info.cols;
                    rects.Add(new SpriteRect
                    {
                        name = $"{clip}_{i:D3}",
                        rect = new Rect(c * info.w, texH - (r + 1) * info.h, info.w, info.h),
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        spriteID = GUID.Generate(),
                    });
                }
                dp.SetSpriteRects(rects.ToArray());
                var ids = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
                ids?.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
                dp.Apply();
            }
            imp.SaveAndReimport();
        }
        AssetDatabase.Refresh();
    }
}

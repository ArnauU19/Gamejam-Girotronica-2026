using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Desk art of the "real world" around the comic panels, drawn procedurally:
///  - wood_desk.png    : stylised cartoon wood (flat orange planks with long grain streaks and a few knots).
///  - pencil_case.png  : open 2D metal tin pencil case seen from above (vertical, for the case area of the levels).
///
/// Girotronica > Art > Apply Wood Desk + Metal Pencil Case (All Scenes) generates them and swaps them into the
/// Level scenes (background sprite + LevelCase sprite) and the main menu desk, keeping each object's world size,
/// so the case area (LevelCase uses the renderer bounds) and the level layout do not change.
/// </summary>
public static class DeskArt
{
    public const string Folder = "Assets/Art/Desk";
    public const string WoodPath = Folder + "/wood_desk.png";
    public const string CasePath = Folder + "/pencil_case.png";
    const string OldBackgroundPath = "Assets/Art/Captura de pantalla 2026-11-21 173050.png";

    static readonly Color Ink = new Color(0.11f, 0.11f, 0.13f);
    const int DeskOrder = -10;
    const int CaseOrder = -5;

    // ================================================================== menu

    [MenuItem("Girotronica/Art/Generate Wood Desk + Metal Pencil Case")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        NotebookUIArt.SaveSprite(WoodPath, DrawWood(2048, 1152), Vector4.zero);
        NotebookUIArt.SaveSprite(CasePath, DrawCase(), Vector4.zero);
        AssetDatabase.SaveAssets();
        Debug.Log("[DeskArt] wood_desk.png and pencil_case.png generated in " + Folder);
    }

    [MenuItem("Girotronica/Art/Apply Wood Desk + Metal Pencil Case (All Scenes)")]
    public static void ApplyAll()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[DeskArt] Stop Play mode first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = SceneManager.GetActiveScene().path;

        Generate();
        var wood = AssetDatabase.LoadAssetAtPath<Sprite>(WoodPath);
        var tin = AssetDatabase.LoadAssetAtPath<Sprite>(CasePath);
        var old = new HashSet<Sprite>(AssetDatabase.LoadAllAssetsAtPath(OldBackgroundPath).OfType<Sprite>());

        var scenes = Directory.GetFiles("Assets/Scenes", "*.unity")
            .Select(p => p.Replace('\\', '/'))
            .Where(p => Regex.IsMatch(Path.GetFileNameWithoutExtension(p), @"^(Level\d+|MainMenu)$"));
        foreach (var path in scenes)
        {
            EditorSceneManager.OpenScene(path);
            int n = ApplyToOpenScene(old, wood, tin);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[DeskArt] {Path.GetFileName(path)}: {n} object(s) updated.");
        }

        if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
    }

    static int ApplyToOpenScene(HashSet<Sprite> oldBackground, Sprite wood, Sprite tin)
    {
        var scene = SceneManager.GetActiveScene();
        int count = 0;

        foreach (var sr in Resources.FindObjectsOfTypeAll<SpriteRenderer>().Where(r => r.gameObject.scene == scene))
        {
            // Explicit draw order: desk (-10) < pencil case (-5) < pieces / panels / character (0 and up).
            // The pieces are 3D meshes on order 0; with the case also on 0 the draw order was a coin toss.
            if (sr.GetComponent<LevelCase>() != null) { Swap(sr, tin); sr.sortingOrder = CaseOrder; count++; }
            else if (sr.sprite == wood || (sr.sprite != null && oldBackground.Contains(sr.sprite)))
            {
                if (sr.sprite != wood) Swap(sr, wood);
                Undo.RecordObject(sr, "Desk order");
                sr.sortingOrder = DeskOrder;
                count++;
            }
        }

        foreach (var img in Resources.FindObjectsOfTypeAll<Image>().Where(i => i.gameObject.scene == scene))
        {
            if (img.sprite == null || !oldBackground.Contains(img.sprite)) continue;
            Undo.RecordObject(img, "Wood desk");
            img.sprite = wood;
            var fitter = img.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                Undo.RecordObject(fitter, "Wood desk");
                fitter.aspectRatio = wood.rect.width / wood.rect.height;
            }
            count++;
        }
        return count;
    }

    /// <summary>Changes the sprite keeping the object's current world size.</summary>
    static void Swap(SpriteRenderer sr, Sprite sprite)
    {
        if (sprite == null) return;
        Vector3 lossy = sr.transform.lossyScale;
        Vector2 oldSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        oldSize = new Vector2(oldSize.x * Mathf.Abs(lossy.x), oldSize.y * Mathf.Abs(lossy.y));
        Vector2 newSize = sprite.bounds.size;

        Undo.RecordObject(sr, "Swap sprite");
        Undo.RecordObject(sr.transform, "Swap sprite");
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.color = Color.white;

        var s = sr.transform.localScale;
        float parentX = Mathf.Abs(lossy.x / (s.x == 0f ? 1f : s.x));
        float parentY = Mathf.Abs(lossy.y / (s.y == 0f ? 1f : s.y));
        s.x = Mathf.Sign(s.x == 0f ? 1f : s.x) * oldSize.x / (newSize.x * parentX);
        s.y = Mathf.Sign(s.y == 0f ? 1f : s.y) * oldSize.y / (newSize.y * parentY);
        sr.transform.localScale = s;
        EditorUtility.SetDirty(sr);
    }

    // ================================================================== wood

    static readonly Color WoodLight = new Color(0.96f, 0.72f, 0.45f);
    static readonly Color WoodMid = new Color(0.93f, 0.63f, 0.35f);
    static readonly Color WoodDark = new Color(0.88f, 0.54f, 0.27f);
    static readonly Color WoodStreak = new Color(0.82f, 0.47f, 0.22f);
    static readonly Color WoodKnot = new Color(0.66f, 0.36f, 0.16f);
    static readonly Color WoodSeam = new Color(0.52f, 0.28f, 0.13f);

    struct Knot { public float x, y, sx, sy; }

    static NotebookUIArt.Raster DrawWood(int w, int h)
    {
        var r = new NotebookUIArt.Raster(w, h);
        const int planks = 4;
        float plankH = h / (float)planks;

        // A few knots, never on a seam
        var knots = new List<Knot>();
        for (int p = 0; p < planks; p++)
            if (NotebookUIArt.Hash(p, 5, 77) > 0.35f)
                knots.Add(new Knot
                {
                    x = Mathf.Lerp(0.12f, 0.88f, NotebookUIArt.Hash(p, 6, 77)) * w,
                    y = (p + Mathf.Lerp(0.35f, 0.65f, NotebookUIArt.Hash(p, 7, 77))) * plankH,
                    sx = Mathf.Lerp(26f, 42f, NotebookUIArt.Hash(p, 8, 77)),
                    sy = Mathf.Lerp(9f, 14f, NotebookUIArt.Hash(p, 9, 77))
                });

        const int ss = 2;   // supersampling for clean stripe edges
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Color acc = Color.clear;
            for (int sy = 0; sy < ss; sy++)
            for (int sx = 0; sx < ss; sx++)
                acc += WoodAt(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss, plankH, knots);
            acc /= ss * ss;

            // soft vignette for depth
            float nx = x / (float)w * 2f - 1f, ny = y / (float)h * 2f - 1f;
            float v = 1f - 0.16f * (nx * nx * 0.7f + ny * ny);
            r.Set(x, y, new Color(acc.r * v, acc.g * v, acc.b * v, 1f));
        }
        return r;
    }

    static Color WoodAt(float x, float y, float plankH, List<Knot> knots)
    {
        int plank = Mathf.FloorToInt(y / plankH);
        float v = y - plank * plankH;

        // Seams between planks: dark groove + light lip under it
        if (v < 3.5f && plank > 0) return WoodSeam;
        if (v < 6f && plank > 0) return Color.Lerp(WoodSeam, WoodDark, 0.6f);
        if (v > plankH - 2.5f) return Color.Lerp(WoodLight, Color.white, 0.25f);

        // Long wavy grain
        float s1 = NotebookUIArt.Hash(plank, 1, 9) * 6.28f, s2 = NotebookUIArt.Hash(plank, 2, 9) * 6.28f;
        float g = v
                  + 16f * Mathf.Sin(x * 0.0019f + s1)
                  + 7f * Mathf.Sin(x * 0.0061f + s2 + v * 0.004f)
                  + 34f * NotebookUIArt.ValueNoise(x / 520f, plank * 3.1f, 13);

        // Grain bends around knots; knot itself is a dark core with rings
        foreach (var k in knots)
        {
            float dx = (x - k.x) / k.sx, dy = (y - k.y) / k.sy;
            float d2 = dx * dx + dy * dy;
            if (d2 < 1f) return WoodKnot;
            float e = Mathf.Sqrt(d2);
            if (Mathf.Abs(e - 1.75f) < 0.16f || Mathf.Abs(e - 2.6f) < 0.12f) return WoodStreak;
            g += 26f * Mathf.Exp(-d2 * 0.12f) * Mathf.Sign(dy == 0f ? 1f : dy);
        }

        float wobble = 10f * NotebookUIArt.ValueNoise(g / 45f, plank * 2.3f, 21);
        int band = Mathf.FloorToInt((g + wobble) / 21f);
        float pick = NotebookUIArt.Hash(band, plank, 31);

        // Some bands are long darker streaks that fade in and out along x
        if (pick > 0.72f && NotebookUIArt.ValueNoise(x / 260f, band * 1.7f, 41) > 0.5f) return WoodStreak;
        if (pick > 0.5f) return WoodDark;
        if (pick > 0.2f) return WoodMid;
        return WoodLight;
    }

    // ================================================================== pencil case

    static NotebookUIArt.Raster DrawCase()
    {
        const int w = 420, h = 1640;
        var r = new NotebookUIArt.Raster(w, h);
        var outerRect = new Rect(18, 26, 384, 1596);
        var outer = NotebookUIArt.RoundRect(outerRect, 150f, 3f);

        // drop shadow on the desk
        var shadow = new NotebookUIArt.Raster(w, h);
        shadow.FillPoly(NotebookUIArt.Offset(outer, new Vector2(9, -13)), new Color(0, 0, 0, 0.42f));
        shadow.Blur(8, 3);
        r.Over(shadow);

        // outer body: brushed metal with a bright vertical sheen on the left and a thin one on the right
        r.FillPoly(outer, Color.white, (x, y) =>
        {
            float t = (x - outerRect.xMin) / outerRect.width;
            float l = 0.74f + 0.24f * Gauss(t, 0.22f, 0.11f) + 0.14f * Gauss(t, 0.80f, 0.05f) - 0.20f * t * t
                      + 0.025f * Mathf.Sin(y * 0.9f + x * 0.05f) * 0.3f;
            return Metal(l);
        });
        r.Stroke(NotebookUIArt.RoundRect(Inset(outerRect, 9), 141f, 3f), true, 2.5f, new Color(1, 1, 1, 0.55f));

        // inner wall of the tray (lit from the top left: darker on the top / right walls)
        var wallRect = Inset(outerRect, 26);
        var wall = NotebookUIArt.RoundRect(wallRect, 124f, 3f);
        r.FillPoly(wall, Color.white, (x, y) =>
        {
            float t = (x - wallRect.xMin) / wallRect.width;
            float u = (y - wallRect.yMin) / wallRect.height;
            return Metal(0.30f + 0.16f * (1f - t) + 0.06f * (1f - u));
        });

        // tray floor with diagonal sheen streaks
        var floorRect = Inset(outerRect, 46);
        var floor = NotebookUIArt.RoundRect(floorRect, 104f, 3f);
        r.FillPoly(floor, Color.white, (x, y) =>
        {
            float t = (x - floorRect.xMin) / floorRect.width;
            float band = Mathf.Repeat((x * 0.8f + y * 0.35f) / 300f, 1f);
            float sheen = 0.10f * Gauss(band, 0.15f, 0.05f) + 0.05f * Gauss(band, 0.32f, 0.03f);
            return Metal(0.40f + 0.05f * (1f - t) + sheen * 0.8f);
        });

        // embossed panel in the floor (bevel: dark top-left edge, light bottom-right edge)
        var panelRect = new Rect(105, 230, 210, 1180);
        var panel = NotebookUIArt.RoundRect(panelRect, 80f, 3f);
        r.FillPoly(panel, Color.white, (x, y) => Metal(0.46f + 0.04f * (1f - (x - panelRect.xMin) / panelRect.width)));
        r.Stroke(NotebookUIArt.Offset(panel, new Vector2(-2, 2)), true, 3.5f, new Color(0.25f, 0.26f, 0.29f, 0.45f));
        r.Stroke(NotebookUIArt.Offset(panel, new Vector2(2, -2)), true, 3f, new Color(1, 1, 1, 0.35f));

        // hinges of the lid on the left rim
        foreach (float hy in new[] { 430f, 1230f })
        {
            var hinge = NotebookUIArt.RoundRect(new Rect(6, hy, 26, 74), 6f, 15f);
            r.FillPoly(hinge, Color.white, (x, y) => Metal(0.62f + 0.25f * Gauss((x - 6f) / 26f, 0.35f, 0.2f)));
            r.Stroke(hinge, true, 2.5f, Ink);
        }

        // comic ink outlines
        r.Stroke(floor, true, 2f, new Color(Ink.r, Ink.g, Ink.b, 0.35f));
        r.Stroke(wall, true, 3.5f, new Color(Ink.r, Ink.g, Ink.b, 0.85f));
        r.Stroke(outer, true, 7f, Ink);

        // cartoon glints
        r.StrokeTapered(Line(new Vector2(31, 1150), new Vector2(31, 1330)), 1f, 6f, 1f, new Color(1, 1, 1, 0.9f));
        r.StrokeTapered(Line(new Vector2(31, 1360), new Vector2(31, 1390)), 1f, 5f, 1f, new Color(1, 1, 1, 0.9f));
        r.StrokeTapered(Line(new Vector2(390, 300), new Vector2(390, 420)), 1f, 4f, 1f, new Color(1, 1, 1, 0.7f));
        return r;
    }

    static Color Metal(float l)
    {
        l = Mathf.Clamp01(l);
        return new Color(l * 0.96f, l * 0.98f, Mathf.Min(1f, l * 1.03f), 1f);
    }

    static float Gauss(float t, float c, float s) => Mathf.Exp(-((t - c) / s) * ((t - c) / s));

    static Rect Inset(Rect r, float d) => new Rect(r.xMin + d, r.yMin + d, r.width - 2f * d, r.height - 2f * d);

    static List<Vector2> Line(Vector2 a, Vector2 b)
    {
        var pts = new List<Vector2>();
        for (int i = 0; i <= 12; i++) pts.Add(Vector2.Lerp(a, b, i / 12f));
        return pts;
    }
}

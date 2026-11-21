using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the hand-drawn UI sprites (ink frames, torn paper, halftone, tape, speech bubble, slider parts)
/// into Assets/Art/UI/Generated. Everything is drawn procedurally so it can be regenerated / tweaked here.
/// </summary>
public static class NotebookUIArt
{
    public const string Folder = "Assets/Art/UI/Generated";

    // Palette taken from the art: notebook paper, pencil/ink lines, the girl's orange and yellow, the cover blue.
    public static readonly Color Ink = new Color(0.11f, 0.11f, 0.13f);
    public static readonly Color Pencil = new Color(0.30f, 0.30f, 0.33f);
    public static readonly Color Paper = new Color(0.965f, 0.94f, 0.86f);
    public static readonly Color PageCream = new Color(0.91f, 0.87f, 0.74f);
    public static readonly Color Orange = new Color(0.80f, 0.40f, 0.17f);
    public static readonly Color Yellow = new Color(1f, 0.89f, 0.50f);
    public static readonly Color CoverBlue = new Color(0.39f, 0.40f, 0.88f);

    public const string PaperPage = "ui_paper_page";
    public const string ButtonFrame = "ui_button_frame";
    public const string PanelFrame = "ui_panel_frame";
    public const string Halftone = "ui_halftone";
    public const string Tape = "ui_tape";
    public const string Bubble = "ui_bubble";
    public const string SliderTrack = "ui_slider_track";
    public const string SliderFill = "ui_slider_fill";
    public const string SliderHandle = "ui_slider_handle";
    public const string Underline = "ui_underline";
    public const string SoftShadow = "ui_soft_shadow";

    public static Sprite Get(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

    [MenuItem("Girotronica/UI/Generate Notebook UI Sprites")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Folder);
        Save(PaperPage, DrawPaperPage(), new Vector4(110, 110, 110, 110));
        Save(ButtonFrame, DrawFrame(256, 128, 14, 5.5f, 7f, 11), new Vector4(40, 40, 40, 40));
        Save(PanelFrame, DrawFrame(256, 256, 14, 8f, 5f, 23), new Vector4(48, 48, 48, 48));
        Save(Halftone, DrawHalftone(), Vector4.zero, TextureWrapMode.Repeat);
        Save(Tape, DrawTape(), Vector4.zero);
        Save(Bubble, DrawBubble(), Vector4.zero);
        Save(SliderTrack, DrawSliderTrack(), new Vector4(18, 14, 18, 14));
        Save(SliderFill, DrawSliderFill(), new Vector4(16, 12, 16, 12));
        Save(SliderHandle, DrawHandle(), Vector4.zero);
        Save(Underline, DrawUnderline(), new Vector4(40, 0, 40, 0));
        Save(SoftShadow, DrawSoftShadow(), new Vector4(56, 56, 56, 56));
        AssetDatabase.SaveAssets();
        Debug.Log("[NotebookUIArt] UI sprites generated in " + Folder);
    }

    static void Save(string name, Raster r, Vector4 border, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        string path = $"{Folder}/{name}.png";
        File.WriteAllBytes(path, r.EncodePNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100;
        imp.spriteBorder = border;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = wrap;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    // ------------------------------------------------------------------ sprites

    static Raster DrawPaperPage()
    {
        var r = new Raster(512, 512);
        var poly = TornRect(new Rect(26, 30, 456, 452), 8f, 101);

        var shadow = new Raster(512, 512);
        shadow.FillPoly(Offset(poly, new Vector2(7, -9)), new Color(0, 0, 0, 0.5f));
        shadow.Blur(6, 3);
        r.Over(shadow);

        r.FillPoly(poly, Paper, (x, y) =>
        {
            float g = 1f - 0.035f * Hash(x, y, 7) - 0.04f * ValueNoise(x / 70f, y / 70f, 3);
            return new Color(g, g, g * 0.995f, 1f);
        });
        r.Stroke(poly, true, 1.6f, new Color(0.55f, 0.5f, 0.42f, 0.45f));
        return r;
    }

    static Raster DrawFrame(int w, int h, float inset, float width, float overshoot, int seed)
    {
        var r = new Raster(w, h);
        var rect = new Rect(inset, inset, w - inset * 2f, h - inset * 2f);
        var fill = new List<Vector2>();
        fill.AddRange(Line(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), 0.8f, seed));
        fill.AddRange(Line(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), 0.8f, seed + 1));
        fill.AddRange(Line(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), 0.8f, seed + 2));
        fill.AddRange(Line(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), 0.8f, seed + 3));
        r.FillPoly(fill, Color.white, (x, y) => { float g = 1f - 0.025f * Hash(x, y, seed); return new Color(g, g, g, 1f); });
        SketchRect(r, rect, width, overshoot, seed);
        return r;
    }

    static void SketchRect(Raster r, Rect rect, float width, float overshoot, int seed)
    {
        var c = new[]
        {
            new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin),
            new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax)
        };
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = c[i], b = c[(i + 1) % 4];
            Vector2 d = (b - a).normalized;
            float o1 = overshoot * (0.4f + Hash(i, seed, 1)), o2 = overshoot * (0.4f + Hash(i, seed, 2));
            var main = Line(a - d * o1, b + d * o2, 1.1f, seed + i * 7);
            r.StrokeTapered(main, width * 0.55f, width, width * 0.7f, Ink);
            Vector2 n = new Vector2(-d.y, d.x) * (Hash(i, seed, 3) * 2.4f - 1.2f);
            var thin = Line(a - d * o2 * 0.6f + n, b + d * o1 * 0.8f + n, 1.6f, seed + i * 13 + 5);
            r.StrokeTapered(thin, 0.6f, 1.5f, 0.6f, new Color(Ink.r, Ink.g, Ink.b, 0.55f));
        }
    }

    static Raster DrawHalftone()
    {
        var r = new Raster(24, 24);
        var centers = new[] { new Vector2(12, 12), new Vector2(0, 0), new Vector2(24, 0), new Vector2(0, 24), new Vector2(24, 24) };
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float best = 99f;
            foreach (var cc in centers) best = Mathf.Min(best, Vector2.Distance(p, cc));
            float a = Mathf.Clamp01(4.4f - best + 0.5f);
            if (a > 0f) r.Blend(x, y, Color.white, a);
        }
        return r;
    }

    static Raster DrawTape()
    {
        var r = new Raster(176, 56);
        var poly = new List<Vector2>();
        for (int i = 0; i <= 8; i++) poly.Add(new Vector2(14 + (i % 2 == 0 ? 0 : 7) + Hash(i, 3, 1) * 3f, 10 + i * 4.5f));
        for (int i = 0; i <= 8; i++) poly.Add(new Vector2(160 - (i % 2 == 0 ? 0 : 7) - Hash(i, 5, 1) * 3f, 46 - i * 4.5f));
        poly.Reverse();
        r.FillPoly(poly, new Color(0.93f, 0.87f, 0.68f, 0.82f), (x, y) =>
        {
            float g = 1f - 0.06f * ValueNoise(x / 9f, y / 40f, 9) - 0.03f * Hash(x, y, 4);
            return new Color(g, g, g, 0.9f + 0.1f * ValueNoise(x / 20f, y / 20f, 2));
        });
        r.Stroke(poly, true, 1.2f, new Color(0.6f, 0.52f, 0.36f, 0.35f));
        return r;
    }

    static Raster DrawBubble()
    {
        var r = new Raster(512, 400);
        var c = new Vector2(262, 222);
        var pts = new List<Vector2>();
        const float gapStart = 222f, gapEnd = 246f;
        for (float a = gapEnd; a < gapStart + 360f; a += 3f)
        {
            float rad = a * Mathf.Deg2Rad;
            float wob = 1f + 0.012f * (ValueNoise(a / 25f, 0, 17) * 2f - 1f);
            pts.Add(c + new Vector2(Mathf.Cos(rad) * 226f, Mathf.Sin(rad) * 148f) * wob);
        }
        pts.Add(new Vector2(96, 18));
        var shadow = new Raster(512, 400);
        shadow.FillPoly(Offset(pts, new Vector2(10, -10)), new Color(0, 0, 0, 0.3f));
        shadow.Blur(4, 2);
        r.Over(shadow);
        r.FillPoly(pts, Color.white);
        r.Stroke(pts, true, 8.5f, Ink);
        var inner = new List<Vector2>();
        foreach (var p in pts) inner.Add(p + new Vector2(1.5f, 2f));
        r.Stroke(inner, true, 1.6f, new Color(Ink.r, Ink.g, Ink.b, 0.4f));
        return r;
    }

    static Raster DrawSliderTrack()
    {
        var r = new Raster(128, 40);
        var rr = RoundRect(new Rect(6, 10, 116, 20), 10f);
        r.FillPoly(rr, new Color(0.88f, 0.84f, 0.73f), (x, y) => { float g = 1f - 0.05f * Hash(x, y, 2); return new Color(g, g, g, 1f); });
        r.Stroke(Wobble(rr, 0.6f, 31), true, 2.6f, Pencil);
        return r;
    }

    static Raster DrawSliderFill()
    {
        var r = new Raster(128, 36);
        var rr = RoundRect(new Rect(4, 8, 120, 20), 9f);
        r.FillPoly(rr, Color.white, (x, y) =>
        {
            float a = 0.78f + 0.22f * ValueNoise(x / 26f, y / 2.5f, 5);
            return new Color(1f, 1f, 1f, a);
        });
        return r;
    }

    static Raster DrawHandle()
    {
        var r = new Raster(64, 64);
        var pts = new List<Vector2>();
        for (int a = 0; a < 360; a += 6)
        {
            float rad = a * Mathf.Deg2Rad;
            float rr = 21f + 0.9f * (ValueNoise(a / 40f, 0, 41) * 2f - 1f);
            pts.Add(new Vector2(32, 32) + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * rr);
        }
        var shadow = new Raster(64, 64);
        shadow.FillPoly(Offset(pts, new Vector2(2.5f, -3f)), new Color(0, 0, 0, 0.35f));
        shadow.Blur(2, 2);
        r.Over(shadow);
        r.FillPoly(pts, Color.white);
        r.Stroke(pts, true, 4.5f, Ink);
        return r;
    }

    static Raster DrawUnderline()
    {
        var r = new Raster(256, 40);
        var a = new List<Vector2>();
        var b = new List<Vector2>();
        for (int i = 0; i <= 40; i++)
        {
            float t = i / 40f;
            a.Add(new Vector2(Mathf.Lerp(12, 244, t), 24 - 6f * Mathf.Sin(t * Mathf.PI) + 1.2f * Mathf.Sin(t * 19f)));
            b.Add(new Vector2(Mathf.Lerp(34, 226, t), 13 - 3f * Mathf.Sin(t * Mathf.PI * 0.8f) + Mathf.Sin(t * 13f)));
        }
        r.StrokeTapered(a, 3f, 9f, 4f, Color.white);
        r.StrokeTapered(b, 1.5f, 5f, 2f, Color.white);
        return r;
    }

    static Raster DrawSoftShadow()
    {
        var r = new Raster(160, 160);
        r.FillPoly(RoundRect(new Rect(32, 32, 96, 96), 14f), Color.white);
        r.Blur(10, 3);
        return r;
    }

    // ------------------------------------------------------------------ shapes

    static List<Vector2> Line(Vector2 a, Vector2 b, float amp, int seed, float step = 6f)
    {
        var pts = new List<Vector2>();
        float len = Vector2.Distance(a, b);
        int n = Mathf.Max(2, Mathf.CeilToInt(len / step));
        Vector2 d = (b - a) / len;
        Vector2 nrm = new Vector2(-d.y, d.x);
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n;
            float off = amp * (ValueNoise(t * len / 38f, 0.5f, seed) * 2f - 1f);
            pts.Add(Vector2.Lerp(a, b, t) + nrm * off);
        }
        return pts;
    }

    static List<Vector2> TornRect(Rect r, float jag, int seed)
    {
        var pts = new List<Vector2>();
        var corners = new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) };
        int k = 0;
        for (int e = 0; e < 4; e++)
        {
            Vector2 a = corners[e], b = corners[(e + 1) % 4];
            Vector2 d = (b - a).normalized;
            Vector2 inward = new Vector2(-d.y, d.x);
            float len = Vector2.Distance(a, b);
            for (float s = 0; s < len; s += 3f, k++)
            {
                float j = jag * (0.55f * ValueNoise(k / 9f, e, seed) + 0.45f * Hash(k, e, seed));
                pts.Add(a + d * s + inward * j);
            }
        }
        return pts;
    }

    static List<Vector2> RoundRect(Rect r, float radius, float stepDeg = 10f)
    {
        var pts = new List<Vector2>();
        var centers = new[]
        {
            new Vector2(r.xMax - radius, r.yMin + radius), new Vector2(r.xMax - radius, r.yMax - radius),
            new Vector2(r.xMin + radius, r.yMax - radius), new Vector2(r.xMin + radius, r.yMin + radius)
        };
        float start = -90f;
        for (int c = 0; c < 4; c++, start += 90f)
            for (float a = start; a <= start + 90f; a += stepDeg)
                pts.Add(centers[c] + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * radius);
        return pts;
    }

    static List<Vector2> Wobble(List<Vector2> pts, float amp, int seed)
    {
        var o = new List<Vector2>(pts.Count);
        for (int i = 0; i < pts.Count; i++)
            o.Add(pts[i] + new Vector2(ValueNoise(i / 4f, 0, seed) * 2f - 1f, ValueNoise(i / 4f, 9, seed) * 2f - 1f) * amp);
        return o;
    }

    static List<Vector2> Offset(List<Vector2> pts, Vector2 o)
    {
        var r = new List<Vector2>(pts.Count);
        foreach (var p in pts) r.Add(p + o);
        return r;
    }

    // ------------------------------------------------------------------ noise

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    static float ValueNoise(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Mathf.Lerp(Hash(xi, yi, seed), Hash(xi + 1, yi, seed), fx);
        float b = Mathf.Lerp(Hash(xi, yi + 1, seed), Hash(xi + 1, yi + 1, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    // ------------------------------------------------------------------ raster

    class Raster
    {
        public readonly int W, H;
        readonly Color[] px;

        public Raster(int w, int h)
        {
            W = w; H = h;
            px = new Color[w * h];
        }

        public void Blend(int x, int y, Color c, float cov)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            float a = c.a * cov;
            if (a <= 0f) return;
            int i = y * W + x;
            Color d = px[i];
            float oa = a + d.a * (1f - a);
            Color o = (c * a + d * d.a * (1f - a)) / oa;
            o.a = oa;
            px[i] = o;
        }

        public void Over(Raster top)
        {
            for (int i = 0; i < px.Length; i++)
                if (top.px[i].a > 0f) Blend(i % W, i / W, new Color(top.px[i].r, top.px[i].g, top.px[i].b, 1f), top.px[i].a);
        }

        public void Stroke(IList<Vector2> pts, bool closed, float width, Color c) =>
            StrokeInternal(pts, closed, _ => width, c);

        public void StrokeTapered(IList<Vector2> pts, float w0, float wMid, float w1, Color c) =>
            StrokeInternal(pts, false, t => t < 0.5f ? Mathf.Lerp(w0, wMid, Mathf.SmoothStep(0, 1, t * 2f)) : Mathf.Lerp(wMid, w1, Mathf.SmoothStep(0, 1, t * 2f - 1f)), c);

        void StrokeInternal(IList<Vector2> pts, bool closed, Func<float, float> widthAt, Color c)
        {
            var cov = new float[W * H];
            int n = pts.Count;
            int segs = closed ? n : n - 1;
            for (int s = 0; s < segs; s++)
            {
                Vector2 a = pts[s], b = pts[(s + 1) % n];
                float t0 = s / (float)segs, t1 = (s + 1) / (float)segs;
                float maxW = Mathf.Max(widthAt(t0), widthAt(t1)) * 0.5f + 1.5f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - maxW));
                int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + maxW));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - maxW));
                int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + maxW));
                Vector2 ab = b - a;
                float l2 = Mathf.Max(1e-5f, ab.sqrMagnitude);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float u = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                    float d = Vector2.Distance(p, a + ab * u);
                    float hw = widthAt(Mathf.Lerp(t0, t1, u)) * 0.5f;
                    float v = Mathf.Clamp01(hw - d + 0.5f);
                    int i = y * W + x;
                    if (v > cov[i]) cov[i] = v;
                }
            }
            for (int i = 0; i < cov.Length; i++)
                if (cov[i] > 0f) Blend(i % W, i / W, c, cov[i]);
        }

        public void FillPoly(IList<Vector2> poly, Color c, Func<int, int, Color> shade = null)
        {
            const int sub = 4;
            var cov = new float[W * H];
            var xs = new List<float>();
            int n = poly.Count;
            for (int y = 0; y < H; y++)
            for (int k = 0; k < sub; k++)
            {
                float sy = y + (k + 0.5f) / sub;
                xs.Clear();
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = poly[i], b = poly[(i + 1) % n];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy))
                        xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int i = 0; i + 1 < xs.Count; i += 2)
                {
                    float xa = Mathf.Max(0f, xs[i]), xb = Mathf.Min(W, xs[i + 1]);
                    for (int x = Mathf.FloorToInt(xa); x < Mathf.CeilToInt(xb) && x < W; x++)
                    {
                        float overlap = Mathf.Min(x + 1, xb) - Mathf.Max(x, xa);
                        if (overlap > 0f) cov[y * W + x] += overlap / sub;
                    }
                }
            }
            for (int i = 0; i < cov.Length; i++)
            {
                if (cov[i] <= 0f) continue;
                Color col = c;
                if (shade != null)
                {
                    Color m = shade(i % W, i / W);
                    col = new Color(c.r * m.r, c.g * m.g, c.b * m.b, c.a * m.a);
                }
                Blend(i % W, i / W, col, Mathf.Clamp01(cov[i]));
            }
        }

        /// <summary>Box blur (repeated) of the whole image, colour weighted by alpha.</summary>
        public void Blur(int radius, int passes)
        {
            var buf = new Color[px.Length];
            for (int p = 0; p < passes; p++)
            {
                BlurDir(px, buf, radius, true);
                BlurDir(buf, px, radius, false);
            }
        }

        void BlurDir(Color[] src, Color[] dst, int r, bool horizontal)
        {
            int len = horizontal ? W : H, lines = horizontal ? H : W;
            for (int l = 0; l < lines; l++)
            for (int i = 0; i < len; i++)
            {
                Color sum = Color.clear;
                float aSum = 0f;
                int count = 0;
                for (int k = -r; k <= r; k++)
                {
                    int j = i + k;
                    count++;
                    if (j < 0 || j >= len) continue;
                    Color s = horizontal ? src[l * W + j] : src[j * W + l];
                    sum += new Color(s.r, s.g, s.b, 0f) * s.a;
                    aSum += s.a;
                }
                Color o = aSum > 0f ? sum / aSum : Color.clear;
                o.a = aSum / count;
                if (horizontal) dst[l * W + i] = o; else dst[i * W + l] = o;
            }
        }

        public byte[] EncodePNG()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return bytes;
        }
    }
}

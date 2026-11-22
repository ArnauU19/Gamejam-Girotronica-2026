using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the ruler from its length: the plastic body stretches (9-sliced) but the marks do NOT:
/// a longer ruler gets more centimetres, more millimetre ticks and more numbers.
/// The collider and the four snap tips are moved to the ends of the ruler too.
///
/// Change "Length Cm" to make it longer or shorter. If someone scales the ruler with the Scale tool
/// in the editor, the scale is turned into length / width automatically (the ruler is never stretched).
///
/// The ruler stands vertical (length along local Y), like the old prefab. The drawn parts are created
/// by code and never saved (they are rebuilt whenever the ruler is enabled).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class RulerBuilder : MonoBehaviour
{
    [Header("Size")]
    [Min(1f)] public float lengthCm = 10f;
    [Tooltip("World units per centimetre (at the ruler's real size).")]
    [Min(0.05f)] public float unitsPerCm = 0.746f;
    [Tooltip("Width of the ruler in world units.")]
    [Min(0.2f)] public float width = 1f;
    [Tooltip("Plastic before the 0 mark and after the last mark (world units).")]
    [Min(0f)] public float endMargin = 0.4f;
    [Tooltip("Size of the whole ruler, marks included (set by LevelScale in levels drawn smaller). 1 = real size.")]
    [Min(0.05f)] public float pieceScale = 1f;

    [Header("Art (Assets/Art/Pieces)")]
    [SerializeField] private Sprite body;
    [SerializeField] private Sprite shadow;
    [SerializeField] private Sprite shadowSoft;
    [SerializeField] private Sprite outline;
    [SerializeField] private Sprite cmCell;
    [SerializeField] private Sprite endTick;
    [SerializeField] private Sprite glint;
    [SerializeField] private Sprite[] numbers;
    [SerializeField] private float bodyMargin = 0.2f;     // transparent margin drawn around the body sprite
    [SerializeField] private float shadowMargin = 0.7f;   // same for shadows / outline
    [SerializeField] private float artCm = 0.75f;         // width of one cm cell in the art
    [SerializeField] private float artWidth = 1f;         // ruler width the art was drawn for

    [Header("Logic parts")]
    [SerializeField] private BoxCollider2D box;
    [SerializeField] private Transform tipA, tipB, tipC, tipD;   // A,B top; C,D bottom; A,C left

    /// <summary>Length in world units, with the level scale applied.</summary>
    public float LengthUnits => lengthCm * unitsPerCm * pieceScale;

    private GameObject generated;
    private bool dirty;

    private void OnEnable()
    {
        FoldScale();
        Rebuild();
    }

    private void OnDisable() { Clear(); }

#if UNITY_EDITOR
    private void OnValidate() { dirty = true; }
#endif

    private void Update()
    {
        if (Application.isPlaying) return;
        if (FoldScale()) dirty = true;
        if (dirty) { dirty = false; Rebuild(); }
    }

    /// <summary>Edit mode only: a scaled ruler becomes a longer / wider ruler instead.</summary>
    private bool FoldScale()
    {
        if (Application.isPlaying) return false;
        Vector3 s = transform.localScale;
        if (Mathf.Approximately(s.x, 1f) && Mathf.Approximately(s.y, 1f)) return false;
        lengthCm = Mathf.Max(1f, lengthCm * Mathf.Abs(s.y));
        width = Mathf.Max(0.2f, width * Mathf.Abs(s.x));
        transform.localScale = new Vector3(1f, 1f, s.z);
        return true;
    }

    private void Clear()
    {
        if (generated == null) return;
        if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
        generated = null;
    }

    public void Rebuild()
    {
        Clear();
        // Everything is built at real size (L, W) and the drawn parts are then scaled by pieceScale
        float L = lengthCm * unitsPerCm;
        float W = width;
        float k = pieceScale;

        // ---- logic: collider + tips at the corners
        if (box != null) { box.size = new Vector2(W * k, L * k); box.offset = Vector2.zero; }
        SetTip(tipA, -W * k / 2f, L * k / 2f);
        SetTip(tipB, W * k / 2f, L * k / 2f);
        SetTip(tipC, -W * k / 2f, -L * k / 2f);
        SetTip(tipD, W * k / 2f, -L * k / 2f);

        if (body == null) return;

        // ---- drawn parts. "Art" space is horizontal (x = along the ruler), rotated 90 degrees onto local Y.
        generated = new GameObject("Ruler art (generated)");
        generated.hideFlags = HideFlags.HideAndDontSave;
        generated.transform.SetParent(transform, false);
        generated.layer = gameObject.layer;
        generated.transform.localScale = new Vector3(k, k, 1f);

        Transform art = generated.transform;
        var visualGo = NewChild("Visual", art);
        visualGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Transform visual = visualGo.transform;

        var shadowRoot = NewChild("Shadows", art);
        shadowRoot.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        SpriteRenderer sSoft = Sliced("ShadowSoft", shadowRoot.transform, shadowSoft, L + 2 * shadowMargin, W + 2 * shadowMargin, 0);
        SpriteRenderer sSharp = Sliced("Shadow", shadowRoot.transform, shadow, L + 2 * shadowMargin, W + 2 * shadowMargin, 1);
        // each shadow is moved on its own by PieceLook, so give them their own parents
        sSoft.transform.SetParent(art, true); sSharp.transform.SetParent(art, true);
        if (Application.isPlaying) Destroy(shadowRoot); else DestroyImmediate(shadowRoot);

        var outlineGo = NewChild("Outline", visual);
        SpriteRenderer ring = Sliced("Ring", outlineGo.transform, outline, L + 2 * shadowMargin, W + 2 * shadowMargin, 2);
        outlineGo.SetActive(false);

        Sliced("Body", visual, body, L + 2 * bodyMargin, W + 2 * bodyMargin, 3);

        // marks: one cell per cm, hanging from the top edge (art space)
        float sx = unitsPerCm / artCm;
        float x0 = -L / 2f + endMargin;
        int cells = Mathf.FloorToInt((L - 2f * endMargin) / unitsPerCm + 0.0001f);
        float topY = W / 2f - artWidth;
        for (int i = 0; i < cells; i++)
        {
            var c = Simple("cm" + i, visual, cmCell, 4);
            c.transform.localPosition = new Vector3(x0 + i * unitsPerCm, topY, 0f);
            c.transform.localScale = new Vector3(sx, 1f, 1f);
        }
        var last = Simple("cmEnd", visual, endTick, 4);
        last.transform.localPosition = new Vector3(x0 + cells * unitsPerCm, topY, 0f);

        for (int i = 0; i <= cells && numbers != null && i < numbers.Length; i++)
        {
            var n = Simple("n" + i, visual, numbers[i], 4);
            n.transform.localPosition = new Vector3(x0 + i * unitsPerCm + 0.012f, W / 2f - 0.42f, 0f);
        }

        // shines, between the numbers, in the lower half
        if (glint != null && cells >= 2)
        {
            var g1 = Simple("glint1", visual, glint, 5);
            g1.transform.localPosition = new Vector3(x0 + 0.5f * unitsPerCm, -W / 2f + 0.36f, 0f);
            g1.transform.localScale = Vector3.one * 0.75f;
            if (cells >= 5)
            {
                var g2 = Simple("glint2", visual, glint, 5);
                g2.transform.localPosition = new Vector3(x0 + (cells - 1.5f) * unitsPerCm, -W / 2f + 0.36f, 0f);
                g2.transform.localScale = Vector3.one * 0.6f;
            }
        }

        var look = GetComponent<PieceLook>();
        if (look != null) look.Bind(visual, sSharp, sSoft, outlineGo, new[] { ring });
    }

    private void SetTip(Transform t, float x, float y)
    {
        if (t == null) return;
        t.localPosition = new Vector3(x, y, t.localPosition.z);
        t.localScale = Vector3.one;
    }

    private GameObject NewChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private SpriteRenderer Simple(string name, Transform parent, Sprite sprite, int order)
    {
        var r = NewChild(name, parent).AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        return r;
    }

    private SpriteRenderer Sliced(string name, Transform parent, Sprite sprite, float w, float h, int order)
    {
        var r = Simple(name, parent, sprite, order);
        r.drawMode = SpriteDrawMode.Sliced;
        r.size = new Vector2(w, h);
        return r;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class DraggableObject : MonoBehaviour
{
    [SerializeField] private SnapTip[] tips;
    public SnapTip[] Tips => tips;

    [Header("Rotation feel")]
    [SerializeField] private float rotateDuration = 0.25f;      // total time of the animation
    [SerializeField] private float overshootDegrees = 12f;      // how far it goes past the target
    [SerializeField, Range(0.1f, 0.9f)] private float overshootSplit = 0.65f; // % of time spent going to the overshoot

    // Grab shake and hover outline settings are global: they live in the DragManager
    private static DragManager Cfg => DragManager.Instance;
    private float shakeDegrees => Cfg != null ? Cfg.ShakeDegrees : 1.5f;
    private float shakeSpeed => Cfg != null ? Cfg.ShakeSpeed : 18f;
    private float shakeBlendTime => Cfg != null ? Cfg.ShakeBlendTime : 0.12f;
    private Color outlineColor => Cfg != null ? Cfg.OutlineColor : new Color(1f, 0.92f, 0.35f, 1f);
    private float outlineWidth => Cfg != null ? Cfg.OutlineWidth : 0.04f;
    private float outlineDepthPush => Cfg != null ? Cfg.OutlineDepthPush : 0.02f;

    private Coroutine rotateRoutine;
    private float currentAngle;
    private float targetAngle;
    private Collider2D col;
    private bool isGrabbed;

    // Grab shake: baseAngle is the real angle of the piece; the shake is a small offset on top of it
    private bool shaking;
    private float baseAngle;
    private float shakeWeight;
    private float shakeSeed;

    // Hover outline: copies of the model behind it, shifted in 8 directions
    private readonly List<GameObject> outlineRoots = new List<GameObject>();
    private Material outlineMat;
    private readonly List<SpriteRenderer> spriteOutlines = new List<SpriteRenderer>();
    private bool outlineBuilt;

    // Pencil case: fullScale is the size the piece has in the scene; inside the case it is multiplied by the case's pieceScale
    private Vector3 fullScale;
    private float scaleFactor = 1f;
    private float scaleTime = 0.12f;
    private float caseScale = 0.5f;
    private LevelCase currentCase;
    public bool InCase => currentCase != null;
    public float ScaleFactor => scaleFactor;

    private readonly List<SnapZone> usedZones = new();

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        shakeSeed = Random.value * 100f;
    }

    private void Start()
    {
        fullScale = transform.localScale;
        EnsureOutline();                                  // built at full size so the width matches outside the case
        SetCase(LevelCase.At(transform.position), true);  // pieces placed inside the case start small
    }

    // ---------- Pencil case ----------
    /// <summary>Tells the piece which case it is over (null = outside). It shrinks / grows towards the new size.</summary>
    public void SetCase(LevelCase c, bool instant = false)
    {
        currentCase = c;
        if (c != null) { scaleTime = c.ScaleTime; caseScale = c.PieceScale; }
        if (instant) ApplyScale(TargetScale);
    }

    private float TargetScale => currentCase != null ? currentCase.PieceScale : 1f;

    private void Update()
    {
        float target = TargetScale;
        if (Mathf.Approximately(scaleFactor, target)) return;
        float speed = Mathf.Max(0.01f, Mathf.Abs(1f - caseScale)) / Mathf.Max(0.0001f, scaleTime);
        ApplyScale(Mathf.MoveTowards(scaleFactor, target, speed * Time.deltaTime));
    }

    private void ApplyScale(float f)
    {
        scaleFactor = f;
        transform.localScale = fullScale * f;
    }

    private void OnDisable()
    {
        // If the object is disabled mid-rotation, the coroutine dies:
        // finish the rotation instantly so the state stays clean.
        if (rotateRoutine != null)
        {
            SetZ(targetAngle);
            rotateRoutine = null;
        }
        isGrabbed = false;
        shaking = false;
        SetHovered(false);
        RefreshCollider();
    }

    public void OccupyZone(SnapZone zone)
    {
        zone.Occupant = this;
        usedZones.Add(zone);
    }

    public void ReleaseZones()
    {
        foreach (var z in usedZones) z.Occupant = null;
        usedZones.Clear();
    }

    public void OnGrab()
    {
        Debug.Log("Me tienes");
        isGrabbed = true;
        baseAngle = transform.eulerAngles.z;
        shakeWeight = 0f;
        shaking = true;
        RefreshCollider();
    }

    public void OnRelease()
    {
        Debug.Log("Me sueltas");
        isGrabbed = false;
        EndShake();
        RefreshCollider();
    }

    public void RotateRightClick()
    {
        if (rotateRoutine == null)
            currentAngle = targetAngle = shaking ? baseAngle : transform.eulerAngles.z; // without the shake offset
        else
            StopCoroutine(rotateRoutine);

        targetAngle += 90f;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(Sfx.Whoosh);
        rotateRoutine = StartCoroutine(RotateRoutine(targetAngle));
        RefreshCollider(); // rotateRoutine is set now -> collider off
    }

    private IEnumerator RotateRoutine(float target)
    {
        float start = currentAngle;
        float peak = target + overshootDegrees;

        // Phase 1: fast push past the target (ease-out)
        float d1 = rotateDuration * overshootSplit;
        for (float t = 0f; t < d1; t += Time.deltaTime)
        {
            float k = t / d1;
            k = 1f - (1f - k) * (1f - k);
            SetZ(Mathf.LerpUnclamped(start, peak, k));
            yield return null;
        }

        // Phase 2: settle back onto the target (smooth)
        float d2 = rotateDuration * (1f - overshootSplit);
        for (float t = 0f; t < d2; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / d2);
            SetZ(Mathf.LerpUnclamped(peak, target, k));
            yield return null;
        }

        SetZ(target);
        rotateRoutine = null;
        RefreshCollider(); // rotation finished -> collider back on (unless grabbed)
    }

    private void SetZ(float z)
    {
        currentAngle = z;
        baseAngle = z;
        Vector3 e = transform.eulerAngles;
        transform.eulerAngles = new Vector3(e.x, e.y, z);
    }

    // ---------- Grab shake ----------
    private void LateUpdate()
    {
        if (!shaking || rotateRoutine != null)
        {
            shakeWeight = 0f;   // while rotating the rotation owns the angle; the shake fades back in afterwards
            return;
        }

        shakeWeight = Mathf.MoveTowards(shakeWeight, 1f, Time.unscaledDeltaTime / Mathf.Max(0.0001f, shakeBlendTime));
        float n = Mathf.PerlinNoise(Time.time * shakeSpeed, shakeSeed) * 2f - 1f;
        SetAngleRaw(baseAngle + n * shakeDegrees * shakeWeight);
    }

    /// <summary>Stops the shake and puts the piece back on its real angle. Call it before snapping.</summary>
    public void EndShake()
    {
        if (!shaking) return;
        shaking = false;
        shakeWeight = 0f;
        if (rotateRoutine == null) SetAngleRaw(baseAngle);
    }

    private void SetAngleRaw(float z)
    {
        Vector3 e = transform.eulerAngles;
        transform.eulerAngles = new Vector3(e.x, e.y, z);
    }

    // ---------- Hover outline ----------
    // The pieces are 3D models (MeshRenderer children). The outline is made of 8 copies of each mesh,
    // shifted a little in the screen plane and pushed behind the piece, painted with a flat color.
    // If a piece only has an enabled SpriteRenderer, 8 copies of the sprite are used instead.
    public void SetHovered(bool hovered)
    {
        if (hovered)
        {
            EnsureOutline();
            ApplyOutlineColor();
        }
        foreach (GameObject root in outlineRoots)
            if (root != null && root.activeSelf != hovered)
                root.SetActive(hovered);
    }

    private void EnsureOutline()
    {
        if (outlineBuilt) return;
        outlineBuilt = true;
        BuildOutline();
    }

    private void BuildOutline()
    {
        // Collect first: the outline objects must not be outlined themselves
        MeshFilter[] meshes = GetComponentsInChildren<MeshFilter>(true);
        SpriteRenderer spriteSrc = GetComponent<SpriteRenderer>();

        Material mat = null;
        foreach (MeshFilter mf in meshes)
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;
            if (mat == null) mat = CreateOutlineMaterial();
            if (mat == null) break;
            BuildMeshOutline(mf, mr, mat);
        }

        if (spriteSrc != null && spriteSrc.enabled && spriteSrc.sprite != null)
            BuildSpriteOutline(spriteSrc);
    }

    private Material CreateOutlineMaterial()
    {
        // OutlineMat (Assets/Resources) is a plain unlit material; it is loaded this way so builds include it
        Material baseMat = Resources.Load<Material>("OutlineMat");
        if (baseMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) { Debug.LogWarning("[DraggableObject] OutlineMat not found in Resources: no hover outline.", this); return null; }
            baseMat = new Material(sh);
        }

        Material mat = new Material(baseMat);   // own copy so each piece can have its own color
        outlineMat = mat;
        ApplyOutlineColor();
        return mat;
    }

    private void ApplyOutlineColor()
    {
        Color c = outlineColor;
        if (outlineMat != null)
        {
            if (outlineMat.HasProperty("_BaseColor")) outlineMat.SetColor("_BaseColor", c);
            if (outlineMat.HasProperty("_Color")) outlineMat.SetColor("_Color", c);
        }
        foreach (SpriteRenderer sr in spriteOutlines)
            if (sr != null) sr.color = c;
    }

    private void BuildMeshOutline(MeshFilter mf, MeshRenderer mr, Material mat)
    {
        var root = new GameObject("Outline");
        root.transform.SetParent(mf.transform, false);
        root.layer = mf.gameObject.layer;

        // Away from the camera, by the thickness of the model, so the copies never poke through the real piece
        Camera cam = Camera.main;
        Vector3 away = cam != null ? cam.transform.forward : Vector3.forward;
        Vector3 push = away * (mr.bounds.size.z * 0.5f + outlineDepthPush);

        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            var go = new GameObject($"o{i}");
            go.transform.SetParent(root.transform, false);
            go.layer = root.layer;
            go.transform.position = mf.transform.position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * outlineWidth + push;

            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        root.SetActive(false);
        outlineRoots.Add(root);
    }

    private void BuildSpriteOutline(SpriteRenderer src)
    {
        var root = new GameObject("Outline");
        root.transform.SetParent(transform, false);
        root.layer = gameObject.layer;

        // The piece can be scaled differently in x and y: compensate so the outline has the same width everywhere
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(ls.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(ls.y));

        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            var go = new GameObject($"o{i}");
            go.transform.SetParent(root.transform, false);
            go.layer = gameObject.layer;
            go.transform.localPosition = new Vector3(Mathf.Cos(a) * outlineWidth / sx, Mathf.Sin(a) * outlineWidth / sy, 0f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = src.sprite;
            sr.sharedMaterial = src.sharedMaterial;
            sr.color = outlineColor;
            spriteOutlines.Add(sr);
            sr.flipX = src.flipX;
            sr.flipY = src.flipY;
            sr.drawMode = src.drawMode;
            if (src.drawMode != SpriteDrawMode.Simple) sr.size = src.size;
            sr.sortingLayerID = src.sortingLayerID;
            sr.sortingOrder = src.sortingOrder - 1;
        }

        root.SetActive(false);
        outlineRoots.Add(root);
    }

    // The collider is only on when the object is neither grabbed nor rotating
    private void RefreshCollider()
    {
        col.enabled = !isGrabbed && rotateRoutine == null;
    }
}
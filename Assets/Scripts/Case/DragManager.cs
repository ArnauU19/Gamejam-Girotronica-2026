using System.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class DragManager : MonoBehaviour
{
    [SerializeField] private InputActionReference pointerPosition;
    [SerializeField] private InputActionReference click;
    [SerializeField] private InputActionReference rightClick;
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask draggablelayers = ~0;

    [Header("Grab shake (all pieces)")]
    [SerializeField] private float shakeDegrees = 1.5f;         // max tilt while held
    [SerializeField] private float shakeSpeed = 18f;            // how fast it trembles
    [SerializeField] private float shakeBlendTime = 0.12f;      // fade-in of the shake (after grabbing / rotating)

    [Header("Hover outline (all pieces)")]
    [SerializeField] private Color outlineColor = new Color(1f, 0.92f, 0.35f, 1f);
    [SerializeField] private float outlineWidth = 0.04f;        // world units
    [SerializeField] private float outlineDepthPush = 0.02f;    // extra distance behind the model (3D pieces)

    public static DragManager Instance { get; private set; }
    public float ShakeDegrees => shakeDegrees;
    public float ShakeSpeed => shakeSpeed;
    public float ShakeBlendTime => shakeBlendTime;
    public Color OutlineColor => outlineColor;
    public float OutlineWidth => outlineWidth;
    public float OutlineDepthPush => outlineDepthPush;

    private DraggableObject currentGrabbedObject;
    private Vector3 offset;

    private void Awake()
    {
        Instance = this;
        if (cam == null) cam = Camera.main;
    }

    private void OnEnable()
    {
        pointerPosition.action.Enable();
        click.action.Enable();
        rightClick.action.Enable();

        click.action.performed += OnClick;
        rightClick.action.performed += OnRightClick;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        click.action.performed -= OnClick;
        rightClick.action.performed -= OnRightClick;
    }

    void OnClick(InputAction.CallbackContext context)
    {
        if (context.ReadValueAsButton())
            OnClickStarted();
        else
            OnClickCanceled();
    }

    void OnClickStarted()
    {
        Vector3 world = GetPointerWorldPos();
        DraggableObject top = GetTopDraggableAt(world);
        if (top == null) return;

        currentGrabbedObject = top;
        offset = currentGrabbedObject.transform.position - world;
        // offset stored at full size: it follows the piece when it shrinks / grows, so the piece stays under the cursor
        float f = Mathf.Max(0.0001f, currentGrabbedObject.ScaleFactor);
        offset.x /= f;
        offset.y /= f;
        currentGrabbedObject.OnGrab();
        PlaySfx(Sfx.Grab);
        // baseRot=currentGrabbedObject.transform.rotation;
        // isShaking=true;
        // if(grabEffect==null)grabEffect= StartCoroutine(grabShake());

    }

    void OnClickCanceled()
    {
        if (currentGrabbedObject == null) return;

        currentGrabbedObject.EndShake();   // back to the real angle before snapping
        // Dropped inside the case: it stays there (small) and does not snap to anything
        bool snapped = false;
        if (currentGrabbedObject.InCase) currentGrabbedObject.ReleaseZones();
        else snapped = TrySnap(currentGrabbedObject);
        PlaySfx(Sfx.Release);
        currentGrabbedObject.OnRelease();
        // currentGrabbedObject.transform.rotation=baseRot;

        currentGrabbedObject = null;

       /*  isShaking=false;
        StopCoroutine(grabEffect);


        grabEffect= null; */

        // baseRot=quaternion.identity;
    }
    Quaternion rotationDestiny= Quaternion.identity;
    bool isShaking=false;
    // Quaternion baseRot= Quaternion.identity;
    IEnumerator grabShake()
    {
        quaternion rotbase=currentGrabbedObject.transform.rotation;
        float off= 2f;
        while (isShaking)
        {
            rotationDestiny=rotbase* Quaternion.Euler(0,0,off);
            yield return new WaitForSecondsRealtime(0.2f);
            off=off*-1;
        }
        currentGrabbedObject.transform.rotation=rotbase;
        // yield return null;
    }

    void OnRightClick(InputAction.CallbackContext context)
    {
        if (context.ReadValueAsButton())
        {
            if(currentGrabbedObject != null){
                // StopCoroutine(grabEffect);
                // isShaking=false;
                // currentGrabbedObject.transform.rotation=baseRot;
                currentGrabbedObject.RotateRightClick();
                // baseRot=currentGrabbedObject.transform.rotation;
                // isShaking=true;
                // if(grabEffect==null)grabEffect= StartCoroutine(grabShake());


            }
        }
    }
    Coroutine grabEffect=null;
    void Update()
    {
        UpdateHover();
        if (currentGrabbedObject != null){
            Vector3 pointer = GetPointerWorldPos();
            currentGrabbedObject.SetCase(LevelCase.At(pointer));   // shrinks over the case, grows outside it
            float f = currentGrabbedObject.ScaleFactor;
            currentGrabbedObject.transform.position = pointer + new Vector3(offset.x * f, offset.y * f, offset.z);
            // StartCoroutine
            // currentGrabbedObject.transform.eulerAngles=rotationDestiny*Time.deltaTime;
            if(isShaking) currentGrabbedObject.transform.rotation = Quaternion.Lerp(
            currentGrabbedObject.transform.rotation,
            rotationDestiny,
            Time.deltaTime * 15f);
        }
    }

    // Outline on the piece under the pointer (or on the one being held)
    DraggableObject hovered;

    void UpdateHover()
    {
        DraggableObject target = null;
        if (Time.timeScale > 0f)    // no hover while the game is paused
            target = currentGrabbedObject != null ? currentGrabbedObject : GetTopDraggableAt(GetPointerWorldPos());

        if (target == hovered) return;
        if (hovered != null) hovered.SetHovered(false);
        hovered = target;
        if (hovered != null) hovered.SetHovered(true);
    }

    bool TrySnap(DraggableObject obj)
    {
        obj.ReleaseZones();

        SnapTip bestTip = null;
        SnapZone bestZone = null;
        float bestDist = float.MaxValue;

        foreach(SnapTip tip in obj.Tips)
        {
            foreach(SnapZone zone in tip.ZonesInside)
            {
                if (!zone.isFree) continue;

                float d = Vector2.Distance(tip.transform.position, zone.Position);
                if(d < bestDist)
                {
                    bestDist = d;
                    bestTip = tip;
                    bestZone = zone;
                }
            }
        }

        if (bestZone == null) return false;

        Vector3 delta = bestZone.Position - bestTip.transform.position;
        delta.z = 0;
        obj.transform.position += delta;
        obj.OccupyZone(bestZone);
        return true;
    }

    void PlaySfx(Sfx id)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(id);
    }

    Vector3 GetPointerWorldPos()
    {
        Vector3 pos = pointerPosition.action.ReadValue<Vector2>();
        pos.z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(pos);
    }

    DraggableObject GetTopDraggableAt(Vector3 world)
    {
        Collider2D[] hits = Physics2D.OverlapPointAll(world, draggablelayers);
        DraggableObject best = null;
        int bestOrder = int.MinValue;

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out DraggableObject d)) continue;

            int order = 0;
            if (hit.TryGetComponent(out SpriteRenderer sr))
                order = sr.sortingLayerID * 1000 + sr.sortingOrder;

            if (order >= bestOrder)
            {
                bestOrder = order;
                best = d;
            }
        }
        return best;
    }
}

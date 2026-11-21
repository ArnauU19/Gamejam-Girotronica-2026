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

    private DraggableObject currentGrabbedObject;
    private Vector3 offset;

    private void Awake()
    {
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
        currentGrabbedObject.OnGrab();
        PlaySfx(Sfx.Grab);
        // baseRot=currentGrabbedObject.transform.rotation;
        // isShaking=true;
        // if(grabEffect==null)grabEffect= StartCoroutine(grabShake());

    }

    void OnClickCanceled()
    {
        if (currentGrabbedObject == null) return;

        bool snapped = TrySnap(currentGrabbedObject);
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
        if (currentGrabbedObject != null){
            currentGrabbedObject.transform.position = GetPointerWorldPos() + offset;
            // StartCoroutine
            // currentGrabbedObject.transform.eulerAngles=rotationDestiny*Time.deltaTime;
            if(isShaking) currentGrabbedObject.transform.rotation = Quaternion.Lerp(
            currentGrabbedObject.transform.rotation,
            rotationDestiny,
            Time.deltaTime * 15f);
        }
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

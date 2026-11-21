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
    }

    void OnClickCanceled()
    {
        if (currentGrabbedObject == null) return;

        currentGrabbedObject.OnRelease();
        currentGrabbedObject = null;
    }

    void OnRightClick(InputAction.CallbackContext context)
    {
        if (context.ReadValueAsButton())
        {
            if(currentGrabbedObject != null)
                currentGrabbedObject.RotateRightClick();
        }
    }
    void Update()
    {
        if (currentGrabbedObject != null)
            currentGrabbedObject.transform.position = GetPointerWorldPos() + offset;
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

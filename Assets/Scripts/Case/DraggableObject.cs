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

    private Coroutine rotateRoutine;
    private float currentAngle;
    private float targetAngle;
    private Collider2D col;
    private bool isGrabbed;

    private readonly List<SnapZone> usedZones = new();

    private void Awake() => col = GetComponent<Collider2D>();

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
        RefreshCollider();
    }

    public void OnRelease()
    {
        Debug.Log("Me sueltas");
        isGrabbed = false;
        RefreshCollider();
    }

    public void RotateRightClick()
    {
        if (rotateRoutine == null)
            currentAngle = targetAngle = transform.eulerAngles.z;
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
        Vector3 e = transform.eulerAngles;
        transform.eulerAngles = new Vector3(e.x, e.y, z);
    }

    // The collider is only on when the object is neither grabbed nor rotating
    private void RefreshCollider()
    {
        col.enabled = !isGrabbed && rotateRoutine == null;
    }
}
using System.Collections;
using UnityEngine;

/// <summary>
/// Eraser (goma): when the character bumps into it, it does a bouncy squash & stretch that gets
/// smaller and smaller until it is still again (a damped spring on the scale).
/// Put it on the eraser object (the one with the collider tagged "eraser").
/// </summary>
public class EraserScript : MonoBehaviour
{
    [Tooltip("Object that is squashed and stretched. If empty, this object.")]
    public Transform target;

    [Header("Bounce")]
    [Tooltip("How much it squashes at the start (0.2 = 20%).")]
    [Range(0f, 0.5f)] public float amplitude = 0.18f;
    [Tooltip("Wobbles per second.")]
    public float frequency = 6f;
    [Tooltip("How fast the wobble dies out. Higher = becomes still sooner.")]
    public float damping = 5f;
    [Tooltip("Seconds until it is completely still.")]
    public float duration = 1f;
    [Tooltip("Seconds during which a new hit does not restart the bounce.")]
    public float cooldown = 0.2f;

    Vector3 baseScale;
    Coroutine routine;
    float lastBounceTime = -10f;

    void Awake()
    {
        if (target == null) target = transform;
        baseScale = target.localScale;
    }

    // The character's colliders are triggers, so this is the usual call.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<SurfaceWalker>() != null) Bounce();
    }

    // In case the eraser is hit by a solid collider of the character.
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.GetComponentInParent<SurfaceWalker>() != null) Bounce();
    }

    /// <summary>Starts (or restarts) the bouncy squash & stretch.</summary>
    public void Bounce()
    {
        if (Time.time - lastBounceTime < cooldown) return;
        lastBounceTime = Time.time;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(Sfx.Boing);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(BounceRoutine());
    }

    IEnumerator BounceRoutine()
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // Damped oscillation: starts squashed (hit from the side), then stretches, smaller each time.
            float s = amplitude * Mathf.Exp(-damping * t) * Mathf.Cos(2f * Mathf.PI * frequency * t);
            // Narrower <-> taller, keeping roughly the same area.
            target.localScale = new Vector3(baseScale.x * (1f - s), baseScale.y * (1f + s), baseScale.z);
            yield return null;
        }

        target.localScale = baseScale;
        routine = null;
    }

    void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        if (target != null) target.localScale = baseScale;
    }
}

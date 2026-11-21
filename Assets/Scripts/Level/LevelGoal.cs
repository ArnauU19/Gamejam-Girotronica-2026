using UnityEngine;

/// <summary>
/// Trigger placed in the finish panel. When the character's body reaches it the level is complete.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LevelGoal : MonoBehaviour
{
    public bool Completed { get; private set; }

    void Reset() => GetComponent<Collider2D>().isTrigger = true;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (Completed) return;
        var walker = other.GetComponentInParent<SurfaceWalker>();
        if (walker == null || other != walker.BodyCollider || walker.CurrentState == SurfaceWalker.State.Dead) return;

        Completed = true;
        Debug.Log($"[Level] LEVEL COMPLETE ({gameObject.scene.name})", this);
        walker.ReachGoal();
        if (LevelManager.Instance != null) LevelManager.Instance.CompleteLevel(); // loads the next scene
    }
}

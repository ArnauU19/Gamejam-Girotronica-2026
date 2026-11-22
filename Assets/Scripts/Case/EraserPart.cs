using UnityEngine;

/// <summary>
/// One solid part of the eraser (goma), on its own child collider.
///  - Bounce (the pink rubber): the character bounces off it and turns around.
///  - Deadly (the metal ferrule): the character crashes into it and dies (Dizzy), like hitting a ruler.
/// The character (SurfaceWalker) never walks on top of these parts: they are always obstacles.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EraserPart : MonoBehaviour
{
    public enum Kind { Bounce, Deadly }
    public Kind kind = Kind.Bounce;

    EraserScript eraser;
    public EraserScript Eraser => eraser != null ? eraser : (eraser = GetComponentInParent<EraserScript>());
}

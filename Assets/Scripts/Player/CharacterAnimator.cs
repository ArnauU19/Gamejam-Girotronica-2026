using UnityEngine;

/// <summary>
/// Drives the character Animator (Assets/Art/Animacions/Character.controller) from the SurfaceWalker.
///
/// Parameters:
///  - Outside (bool)    : the body is not touching any "panel" trigger.
///  - Fall    (bool)    : state Falling.
///  - Glide   (bool)    : sliding = on the steep ruler that flipped the direction (walker.IsSliding).
///                        Running on any other ruler plays Run. The SPRITE (not the character) rotates
///                        so it lies along the slope (sprite up = ruler normal).
///  - Dizzy   (trigger) : frontal crash into a ruler. Plays once (DizzyDone), then the walker respawns.
///  - Win     (trigger) : reached the goal. WinLoopsDone becomes true after the Win clip looped winLoops times.
///
/// The controller has no transitions back from Fall / Glide / Dizzy to Run, so this script returns to the
/// Run state itself when those animations are over (harmless if such transitions are added later).
/// SurfaceWalker adds this component automatically if the character has an Animator in its children.
/// </summary>
[RequireComponent(typeof(SurfaceWalker))]
public class CharacterAnimator : MonoBehaviour
{
    [Tooltip("If empty, the Animator in the children is used.")]
    public Animator animator;
    [Tooltip("Object rotated while gliding. If empty, the Animator's object.")]
    public Transform sprite;

    [Header("Win")]
    [Min(1)] public int winLoops = 4;
    public string winState = "Win";

    [Header("States used to go back to running")]
    public string runInsideState = "Run_m";
    public string runOutsideState = "Run_nm";
    public string[] fallStates = { "Fall_m", "Fall_nm" };
    public string[] glideStates = { "Glide_m", "Glide_nm" };
    public string[] dizzyStates = { "Dizzy_m", "Dizzy_nm" };

    [Header("Glide")]
    [Tooltip("Degrees per second the sprite rotates to follow the slope.")]
    public float glideRotationSpeed = 720f;

    /// <summary>True once the Win animation has looped winLoops times (the LevelManager waits for it).</summary>
    public bool WinLoopsDone { get; private set; }
    /// <summary>True once the Dizzy animation has played once after a frontal crash (the walker then respawns).</summary>
    public bool DizzyDone { get; private set; }
    public bool HasAnimator => animator != null;

    static readonly int OutsideId = Animator.StringToHash("Outside");
    static readonly int FallId = Animator.StringToHash("Fall");
    static readonly int GlideId = Animator.StringToHash("Glide");
    static readonly int DizzyId = Animator.StringToHash("Dizzy");
    static readonly int WinId = Animator.StringToHash("Win");

    SurfaceWalker walker;
    int winSfxPlayed;   // Eat sounds already played during this Win animation (one per loop)

    void Awake()
    {
        walker = GetComponent<SurfaceWalker>();
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (sprite == null && animator != null) sprite = animator.transform;
    }

    void OnEnable()
    {
        walker.StateChanged += OnStateChanged;
        walker.FrontalCollision += OnFrontalCollision;
    }

    void OnDisable()
    {
        walker.StateChanged -= OnStateChanged;
        walker.FrontalCollision -= OnFrontalCollision;
    }

    void OnStateChanged(SurfaceWalker.State prev, SurfaceWalker.State next)
    {
        if (animator == null) return;
        if (next == SurfaceWalker.State.Finished)
        {
            WinLoopsDone = false;
            winSfxPlayed = 0;
            animator.SetTrigger(WinId);
        }
    }

    void OnFrontalCollision()
    {
        if (animator == null) return;
        DizzyDone = false;
        animator.SetTrigger(DizzyId);
    }

    void LateUpdate()
    {
        if (animator == null) return;

        var state = walker.CurrentState;
        bool falling = state == SurfaceWalker.State.Falling;
        bool gliding = walker.IsSliding; // only the slide after a steep-ruler flip; other rulers = running

        animator.SetBool(OutsideId, walker.OutPanel);
        animator.SetBool(FallId, falling);
        animator.SetBool(GlideId, gliding);

        UpdateSpriteRotation(gliding);

        var info = animator.GetCurrentAnimatorStateInfo(0);
        if (info.IsName(winState))
        {
            // One Eat sound at the start of each loop of the Win animation
            int loop = Mathf.FloorToInt(info.normalizedTime);
            while (winSfxPlayed <= loop && winSfxPlayed < winLoops)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(Sfx.Eat);
                winSfxPlayed++;
            }

            // Hold the last frame once the Win clip has looped winLoops times.
            if (!WinLoopsDone && info.normalizedTime >= winLoops)
            {
                WinLoopsDone = true;
                animator.speed = 0f;
            }
            return;
        }

        // Dizzy played once -> the walker can respawn.
        if (!DizzyDone && IsAny(info, dizzyStates) && info.normalizedTime >= 1f) DizzyDone = true;

        if (!animator.IsInTransition(0)) ReturnToRunWhenOver(info, falling, gliding);
    }

    /// <summary>While gliding the sprite lies along the slope (its up = slope normal); otherwise straight.</summary>
    void UpdateSpriteRotation(bool gliding)
    {
        if (sprite == null || sprite == transform) return;
        float angle = gliding ? Vector2.SignedAngle(transform.up, walker.SurfaceNormal) : 0f;
        sprite.localRotation = Quaternion.RotateTowards(sprite.localRotation, Quaternion.Euler(0f, 0f, angle),
            glideRotationSpeed * Time.deltaTime);
    }

    void ReturnToRunWhenOver(AnimatorStateInfo info, bool falling, bool gliding)
    {
        bool over =
            (IsAny(info, fallStates) && !falling) ||
            (IsAny(info, glideStates) && !gliding) ||
            (IsAny(info, dizzyStates) && info.normalizedTime >= 1f);
        if (over) animator.Play(walker.OutPanel ? runOutsideState : runInsideState, 0, 0f);
    }

    static bool IsAny(AnimatorStateInfo info, string[] names)
    {
        foreach (var n in names)
            if (info.IsName(n)) return true;
        return false;
    }
}

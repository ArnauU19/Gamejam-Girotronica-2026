using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Character that walks forward automatically.
///
/// States (every change goes through SetState, so animations can hook into StateChanged):
///  - Walking : glued to the floor, following its normal.
///  - OnRuler : on a bridge (collider tagged "ruler"). Keeps the direction and gravity it had when it
///              left the floor. Outside the panels, if the ground ray hits a ruler whose normal differs
///              more than slideFlipAngle (50°) from the character's up:
///                - it is the ruler that caught a fall -> the direction flips and the character slides on
///                  it (IsSliding -> "Glide" animation);
///                - it was reached from the ground (floor or another ruler) -> crash: dies (Dizzy).
///              Any other ruler: it just runs.
///  - Falling : no support. Falls with the gravity of the last floor it touched.
///  - Dead    : the front sensor crashed into a ruler (the Dizzy animation plays once, then respawn)
///              or the character touched a "killZone" collider (instant respawn).
///              The level countdown does not restart.
///  - Finished: reached the LevelGoal.
///  - Waiting : before the LevelManager countdown ends.
///
/// Panels: every panel has a trigger tagged "panel" as big as the panel (PanelTrigger).
///  - Body touching a panel trigger (enter / stay) -> inside : "2D" sprite object active.
///  - Body touching none (exit)                    -> outside: "3D" sprite object active.
///  - Outside a panel the character never changes its angle.
///
/// Animations: CharacterAnimator (added automatically if there is an Animator in the children)
/// turns these states and events into the Animator parameters Outside / Fall / Glide / Dizzy / Win.
///
/// Setup: Rigidbody2D (Dynamic, Gravity Scale 0) + body Collider2D marked as Is Trigger,
/// a FrontSensor child, and the "2D" / "3D" sprite children.
/// Unity gravity is NOT used: this script applies its own gravity direction.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SurfaceWalker : MonoBehaviour
{
    // New states go at the END so values already saved in scenes/prefabs keep their meaning.
    public enum State { Walking, Falling, OnRuler, Dead, Finished, Waiting }

    [Header("Movement")]
    public float speed = 3f;
    [Tooltip("1 = walks to its right, -1 = walks to its left (relative to its own up).")]
    public int facing = 1;
    [Tooltip("Degrees per second the body rotates to match the floor (only inside panels).")]
    public float turnSpeed = 720f;

    [Header("Ground detection")]
    public LayerMask groundMask = ~0;
    [Tooltip("Distance from the center of the character to its feet.")]
    public float footOffset = 0.55f;
    [Tooltip("Half of the character width, used to detect walls in front.")]
    public float bodyHalfWidth = 0.25f;
    [Tooltip("Extra distance below the feet where we still consider we are on the floor.")]
    public float groundSnapDistance = 0.4f;
    [Tooltip("Surfaces whose normal differs more than this from the current up are treated as walls.")]
    [Range(0f, 90f)] public float wallAngle = 60f;
    [Tooltip("Distance the character keeps going straight after losing support before it starts falling.")]
    public float edgeTolerance = 0.3f;

    [Header("Rulers (bridges)")]
    public string rulerTag = "ruler";
    [Tooltip("Extra distance beyond the feet (along gravity) where a ruler is still caught. " +
             "Gives clearance for bridges that are not placed perfectly.")]
    public float rulerClearance = 0.35f;
    [Tooltip("Speed (units/s) used to settle the feet onto the ruler surface.")]
    public float rulerSnapSpeed = 8f;
    [Tooltip("Outside the panels: if the angle between a ruler's normal and the character's up is bigger " +
             "than this, the direction flips.")]
    [Range(0f, 90f)] public float slideFlipAngle = 50f;

    [Header("Panels")]
    public string panelTag = "panel";
    [Tooltip("Sprite object shown inside a panel. If empty, the child named \"2D\" is used.")]
    public GameObject sprite2D;
    [Tooltip("Sprite object shown outside the panels. If empty, the child named \"3D\" is used.")]
    public GameObject sprite3D;

    [Header("Death / respawn")]
    [Tooltip("Colliders with this tag kill the character on contact.")]
    public string killZoneTag = "killZone";
    [Tooltip("Optional. If empty, the character respawns where it was at the start of the level.")]
    public Transform spawnPoint;
    [Tooltip("Safety limit (seconds) waiting for the Dizzy animation before respawning.")]
    public float maxDizzyWait = 3f;

    [Header("Gravity (custom)")]
    public float gravity = 25f;
    public float maxFallSpeed = 20f;
    [Tooltip("How fast the sideways speed is lost while falling.")]
    public float airDrag = 1.5f;
    public Vector2 startGravityDir = Vector2.down;

    [Header("State (read only)")]
    [SerializeField] State currentState = State.Falling;
    [Tooltip("True while the character is outside every panel.")]
    [SerializeField] bool outPanel = false;

    [Header("Debug")]
    public bool logStateChanges = true;

    /// <summary>Raised on every state change (old, new). Use it to drive animations.</summary>
    public event Action<State, State> StateChanged;

    /// <summary>Raised when the front sensor crashes into a ruler (the character dies). Drives "Dizzy".</summary>
    public event Action FrontalCollision;

    /// <summary>True while sliding: on the steep ruler that flipped the direction. Drives "Glide".</summary>
    public bool IsSliding => sliding && currentState == State.OnRuler;

    public State CurrentState => currentState;
    public bool OutPanel => outPanel;
    public Collider2D BodyCollider { get; private set; }
    /// <summary>Normal of the ruler under the character while OnRuler (used to tilt the glide sprite).</summary>
    public Vector2 SurfaceNormal => surfaceNormal;

    enum Filter { Any, FloorOnly, RulerOnly }

    Rigidbody2D rb;
    FrontSensor frontSensor;
    ContactFilter2D contactFilter;
    readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    readonly HashSet<Collider2D> panelsTouched = new HashSet<Collider2D>();
    bool panelsChanged;

    Vector2 gravityDir;
    Vector2 lastGroundNormal;
    Vector2 surfaceNormal = Vector2.up;
    Vector2 velocity;
    float unsupportedDistance;
    Collider2D flipLatch; // ruler that already flipped the direction (flip only once per ruler)
    bool sliding;         // on that steep ruler after the flip
    Collider2D caughtByFall; // ruler that caught the last fall (only that one can be a slide)
    float lastEraserBounceTime = -10f;

    Vector2 spawnPosition;
    float spawnRotation;
    int startFacing = 1;
    float lastRespawnTime = -1f;
    bool killZoneTagValid, panelTagValid;

    /// <summary>Direction of movement while on a ruler / falling (the direction it had when leaving the floor).</summary>
    Vector2 ExitDirection => Forward(-gravityDir);

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // we do our own gravity
        BodyCollider = GetComponent<Collider2D>();
        frontSensor = GetComponentInChildren<FrontSensor>(true);

        // Animations: add the Animator driver if the character has an Animator and nobody added it yet.
        if (GetComponent<CharacterAnimator>() == null && GetComponentInChildren<Animator>(true) != null)
            gameObject.AddComponent<CharacterAnimator>();
        contactFilter = new ContactFilter2D { useTriggers = false };

        // Levels built with the panel prefabs: the start panel brings a PlayerSpawn -> start there.
        if (spawnPoint == null)
        {
            var spawn = FindAnyObjectByType<PlayerSpawn>();
            if (spawn != null) spawnPoint = spawn.transform;
        }
        if (spawnPoint != null)
        {
            Vector3 p = spawnPoint.position;
            transform.SetPositionAndRotation(new Vector3(p.x, p.y, transform.position.z), Quaternion.Euler(0f, 0f, spawnPoint.eulerAngles.z));
            rb.position = p;
            rb.rotation = spawnPoint.eulerAngles.z;
        }
        spawnPosition = rb.position;
        spawnRotation = rb.rotation;
        startFacing = facing < 0 ? -1 : 1;

        if (sprite2D == null) sprite2D = transform.Find("2D")?.gameObject;
        if (sprite3D == null) sprite3D = transform.Find("3D")?.gameObject;

        killZoneTagValid = TagExists(killZoneTag);
        panelTagValid = TagExists(panelTag);
    }

    void Start()
    {
        gravityDir = startGravityDir.sqrMagnitude > 0f ? startGravityDir.normalized : Vector2.down;
        lastGroundNormal = -gravityDir;
        SetOutPanel(false); // we always start inside a panel

        // Wait for the level countdown if there is a LevelManager; otherwise start right away
        // (lands on the floor below on the first steps).
        var level = LevelManager.Instance;
        currentState = level != null && !level.LevelStarted ? State.Waiting : State.Falling;
    }

    /// <summary>Called by the LevelManager when the countdown ends.</summary>
    public void BeginLevel()
    {
        if (currentState == State.Waiting) SetState(State.Falling, "level started");
    }

    void FixedUpdate()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        float dt = Time.fixedDeltaTime;

        if (panelsChanged)
        {
            panelsChanged = false;
            SetOutPanel(panelsTouched.Count == 0, playSound: true);
        }

        switch (currentState)
        {
            case State.Walking: Walk(dt); break;
            case State.OnRuler: WalkOnRuler(dt); break;
            case State.Falling: Fall(dt); break;
        }
    }

    void SetState(State next, string reason)
    {
        if (next == currentState) return;
        State prev = currentState;
        currentState = next;
        if (logStateChanges)
            Debug.Log($"[SurfaceWalker] {prev} -> {next} ({reason}) outPanel={outPanel} gravity={gravityDir}", this);
        UpdateStepsSound(next);
        StateChanged?.Invoke(prev, next);
    }

    // Footsteps loop only while walking on the floor or on a ruler. Falling, crash (Dead),
    // eating (Finished) and waiting stop it.
    void UpdateStepsSound(State state)
    {
        if (AudioManager.Instance == null) return;
        if (state == State.Walking || state == State.OnRuler) AudioManager.Instance.PlayLoop(Sfx.Steps);
        else AudioManager.Instance.StopLoop(Sfx.Steps);
    }

    void OnDisable()
    {
        // The AudioManager survives scene changes: never leave the loop playing
        if (AudioManager.Instance != null) AudioManager.Instance.StopLoop(Sfx.Steps);
    }

    // ------------------------------------------------------------------ Walking (floor)

    void Walk(float dt)
    {
        Vector2 up = lastGroundNormal;
        Vector2 forward = Forward(up);
        Vector2 pos = rb.position;
        float step = speed * dt;

        // 0) Eraser in front (any height, from the feet to the head): bounce on the rubber, die on the metal.
        if (CheckEraserAhead(forward, up, bodyHalfWidth + step + 0.05f)) return;

        // 1) Wall in front (sharp concave corner): climb onto it. Rulers are never walls to climb, and
        //    erasers neither (the front sensor makes the character turn around when it touches them).
        if (Cast(pos, forward, bodyHalfWidth + step + 0.05f, Filter.FloorOnly, out var wall) &&
            Vector2.Angle(wall.normal, up) > wallAngle &&
            (frontSensor == null || !frontSensor.IsEraser(wall.collider)))
        {
            lastGroundNormal = wall.normal;
            rb.MovePosition(wall.point + wall.normal * footOffset);
            RotateTowards(wall.normal, dt);
            return;
        }

        // 2) Move forward along the floor.
        Vector2 target = pos + forward * step;

        // 3) Floor under our feet -> stick to it and follow its normal.
        if (Cast(target, -up, footOffset + groundSnapDistance, Filter.FloorOnly, out var ground))
        {
            unsupportedDistance = 0f;
            lastGroundNormal = ground.normal;
            rb.MovePosition(ground.point + ground.normal * footOffset);
            RotateTowards(ground.normal, dt);
            return;
        }

        // 4) The floor ended. A ruler within clearance? -> go straight onto it.
        gravityDir = -up;
        if (Cast(target, gravityDir, footOffset + rulerClearance, Filter.RulerOnly, out _))
        {
            rb.MovePosition(target);
            EnterRuler("floor ended, ruler found");
            return;
        }

        // 5) Nothing: keep going straight a little (tolerance), then fall.
        rb.MovePosition(target);
        unsupportedDistance += step;
        if (unsupportedDistance > edgeTolerance) StartFalling("floor ended");
    }

    // ------------------------------------------------------------------ On ruler (bridge)

    void WalkOnRuler(float dt)
    {
        Vector2 up = -gravityDir;
        Vector2 pos = rb.position;
        if (CheckEraserAhead(ExitDirection, up, bodyHalfWidth + speed * dt + 0.05f)) return;
        Vector2 target = pos + ExitDirection * (speed * dt);

        float reach = footOffset + Mathf.Max(rulerClearance, groundSnapDistance);
        if (Cast(target, gravityDir, reach, Filter.Any, out var hit))
        {
            if (!IsRuler(hit.collider))
            {
                // Reached a normal floor (next panel): walk on it again following its normal.
                if (Vector2.Angle(hit.normal, up) < wallAngle && hit.distance <= footOffset + groundSnapDistance)
                {
                    flipLatch = null;
                    sliding = false;
                    lastGroundNormal = hit.normal;
                    unsupportedDistance = 0f;
                    rb.MovePosition(hit.point + hit.normal * footOffset);
                    SetState(State.Walking, $"reached floor '{hit.collider.name}'");
                    return;
                }
            }
            else
            {
                unsupportedDistance = 0f;
                surfaceNormal = hit.normal;

                // Outside the panels: a steep ruler (normal vs our up > slideFlipAngle) flips the
                // direction. Only once per ruler, otherwise it would flip every frame.
                float angle = Vector2.Angle(hit.normal, up);
                if (outPanel && angle > slideFlipAngle && flipLatch != hit.collider)
                {
                    // Only a steep ruler the character FELL onto is a slide. Reaching it from the ground
                    // (floor or another ruler) is a crash.
                    if (hit.collider != caughtByFall)
                    {
                        Die($"ran from the ground onto steep ruler '{hit.collider.name}' ({angle:F0}°)", dizzy: true);
                        return;
                    }

                    flipLatch = hit.collider;
                    sliding = true;
                    SetFacing(-facing, $"steep ruler '{hit.collider.name}' ({angle:F0}° > {slideFlipAngle:F0}°)");
                    rb.MovePosition(pos); // do not step into it this frame
                    return;
                }

                // Sliding = still on the steep ruler that flipped the direction. Any other ruler: running.
                sliding = flipLatch != null && hit.collider == flipLatch;

                // Settle the feet onto the ruler, only along the gravity axis (never following its normal).
                float gap = hit.distance - footOffset;
                target += gravityDir * Mathf.Clamp(gap, -rulerSnapSpeed * dt, rulerSnapSpeed * dt);
                rb.MovePosition(target);
                RotateTowards(up, dt);
                return;
            }
        }

        // No ruler under the feet: keep going straight a little, then fall.
        flipLatch = null;
        sliding = false;
        rb.MovePosition(target);
        RotateTowards(up, dt);
        unsupportedDistance += speed * dt;
        if (unsupportedDistance > edgeTolerance) StartFalling("ruler ended");
    }

    /// <param name="fellOnto">The ruler that caught a fall (null when coming from the ground).</param>
    void EnterRuler(string reason, Collider2D fellOnto = null)
    {
        caughtByFall = fellOnto;
        lastGroundNormal = -gravityDir;
        surfaceNormal = -gravityDir;
        velocity = Vector2.zero;
        unsupportedDistance = 0f;
        sliding = false;
        SetState(State.OnRuler, reason);
    }

    /// <summary>Turns the character around (used by the FrontSensor when it touches an eraser).</summary>
    public void FlipDirection(string reason)
    {
        if (currentState == State.Dead || currentState == State.Finished || currentState == State.Waiting) return;

        // While falling, the sideways speed is reversed too.
        if (currentState == State.Falling)
        {
            Vector2 along = Vector2.Dot(velocity, gravityDir) * gravityDir;
            velocity = along - (velocity - along);
        }
        SetFacing(-facing, reason);
    }

    /// <summary>Changes the walking direction and mirrors the children (sprites, front sensor).</summary>
    void SetFacing(int newFacing, string reason)
    {
        newFacing = newFacing < 0 ? -1 : 1;
        if (newFacing == (facing < 0 ? -1 : 1)) return;
        facing = newFacing;

        foreach (Transform child in transform)
        {
            Vector3 p = child.localPosition;
            p.x = -p.x;
            child.localPosition = p;
        }
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.flipX = !sr.flipX;

        if (logStateChanges) Debug.Log($"[SurfaceWalker] Direction inverted, facing = {facing} ({reason})", this);
    }

    // ------------------------------------------------------------------ Falling

    void Fall(float dt)
    {
        // Split velocity in "along gravity" and "sideways".
        float along = Vector2.Dot(velocity, gravityDir);
        Vector2 sideways = velocity - along * gravityDir;

        along = Mathf.Min(along + gravity * dt, maxFallSpeed);
        sideways *= Mathf.Exp(-airDrag * dt);
        velocity = sideways + along * gravityDir;

        Vector2 delta = velocity * dt;
        Vector2 pos = rb.position;

        // Erasers while falling: sideways like when walking; landing on top of one is a crash.
        if (sideways.sqrMagnitude > 0.0001f &&
            CheckEraserAhead(sideways.normalized, -gravityDir, bodyHalfWidth + sideways.magnitude * dt + 0.05f)) return;
        if (EraserBelow(pos, footOffset + Mathf.Max(0f, Vector2.Dot(delta, gravityDir)), out var below))
        {
            Die($"fell onto eraser '{below.name}'", dizzy: true);
            return;
        }

        // Move sideways first, then look along gravity.
        float fallDist = Mathf.Max(0f, Vector2.Dot(delta, gravityDir));
        Vector2 origin = pos + (delta - gravityDir * Vector2.Dot(delta, gravityDir));
        if (Cast(origin, gravityDir, footOffset + fallDist + rulerClearance, Filter.Any, out var hit))
        {
            if (IsRuler(hit.collider))
            {
                // Caught by a bridge (with clearance). Feet settle on it in WalkOnRuler.
                rb.MovePosition(origin + gravityDir * Mathf.Min(fallDist, Mathf.Max(0f, hit.distance - footOffset)));
                EnterRuler($"caught by ruler '{hit.collider.name}'", hit.collider);
                return;
            }
            if (hit.distance <= footOffset + fallDist && Vector2.Dot(hit.normal, -gravityDir) > 0.5f)
            {
                Land(hit);
                return;
            }
        }

        rb.MovePosition(pos + delta);
        RotateTowards(-gravityDir, dt);
    }

    void StartFalling(string reason)
    {
        gravityDir = -lastGroundNormal;
        velocity = Forward(lastGroundNormal) * speed;
        unsupportedDistance = 0f;
        sliding = false;
        SetState(State.Falling, reason);
    }

    void Land(RaycastHit2D hit)
    {
        lastGroundNormal = hit.normal;
        rb.MovePosition(hit.point + hit.normal * footOffset);
        velocity = Vector2.zero;
        unsupportedDistance = 0f;
        SetState(State.Walking, $"landed on '{hit.collider.name}'");
    }

    // ------------------------------------------------------------------ Triggers: panels and kill zones

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsKillZone(other)) Die($"touched killZone '{other.name}'");
        else UpdatePanelContact(other);
    }

    void OnTriggerStay2D(Collider2D other) => UpdatePanelContact(other);

    void OnTriggerExit2D(Collider2D other) => UpdatePanelContact(other);

    /// <summary>
    /// Keeps the set of panel triggers touched by the BODY (child colliders like the front sensor
    /// also send trigger messages here, so the body contact is checked explicitly).
    /// Inside/outside is decided once per physics step in FixedUpdate, so an exit and an enter in the
    /// same step (e.g. after a respawn) do not make the sprite flicker.
    /// </summary>
    void UpdatePanelContact(Collider2D other)
    {
        if (!panelTagValid || other == null || !other.CompareTag(panelTag)) return;

        bool changed = other.enabled && BodyCollider.IsTouching(other)
            ? panelsTouched.Add(other)
            : panelsTouched.Remove(other);
        if (changed) panelsChanged = true;
    }

    /// <param name="playSound">Paper sound when it really walks out of / into a panel (not on start or respawn).</param>
    void SetOutPanel(bool value, bool playSound = false)
    {
        bool changed = value != outPanel;
        if (changed && playSound && AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(Sfx.Paper);
        outPanel = value;
        if (sprite2D != null) sprite2D.SetActive(!outPanel);
        if (sprite3D != null) sprite3D.SetActive(outPanel);
        if (changed && logStateChanges)
            Debug.Log($"[SurfaceWalker] {(outPanel ? "EXITED the panels (3D)" : "ENTERED a panel (2D)")}", this);
    }

    // ------------------------------------------------------------------ Death / goal

    /// <summary>Called by the FrontSensor child when it touches something.</summary>
    public void OnFrontSensorTouched(Collider2D other)
    {
        if (IsRuler(other)) Die($"front sensor hit ruler '{other.name}'", dizzy: true);
        else if (IsKillZone(other)) Die($"touched killZone '{other.name}'");
    }

    /// <summary>
    /// Dies and goes back to the spawn point (the countdown does not restart).
    /// dizzy = true (frontal crash into a ruler): the character stays where it crashed while the Dizzy
    /// animation plays once, then respawns. Otherwise (kill zone) it respawns instantly.
    /// </summary>
    public void Die(string reason, bool dizzy = false)
    {
        if (currentState == State.Dead || currentState == State.Finished) return;
        // Several colliders can report the same death in the same physics step: only the first counts.
        if (Time.fixedTime == lastRespawnTime) return;

        Debug.Log($"[SurfaceWalker] DIED ({reason}).", this);
        velocity = Vector2.zero;
        sliding = false;
        SetState(State.Dead, reason);

        if (dizzy)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(Sfx.Hit);
            FrontalCollision?.Invoke();
            StartCoroutine(RespawnAfterDizzy());
        }
        else
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(Sfx.Respawn);
            Respawn();
        }
    }

    IEnumerator RespawnAfterDizzy()
    {
        var anim = GetComponent<CharacterAnimator>();
        if (anim != null && anim.HasAnimator)
        {
            float limit = Time.time + maxDizzyWait;
            yield return new WaitUntil(() => anim.DizzyDone || Time.time > limit);
        }
        if (currentState == State.Dead) Respawn();
    }

    void Respawn()
    {
        Vector2 pos = spawnPoint != null ? (Vector2)spawnPoint.position : spawnPosition;
        float rot = spawnPoint != null ? spawnPoint.eulerAngles.z : spawnRotation;

        // Teleport (rigidbody + transform, so interpolation does not draw a trail).
        rb.position = pos;
        rb.rotation = rot;
        transform.SetPositionAndRotation(new Vector3(pos.x, pos.y, transform.position.z), Quaternion.Euler(0f, 0f, rot));

        velocity = Vector2.zero;
        unsupportedDistance = 0f;
        flipLatch = null;
        sliding = false;
        caughtByFall = null;
        SetFacing(startFacing, "respawn");
        gravityDir = startGravityDir.sqrMagnitude > 0f ? startGravityDir.normalized : Vector2.down;
        lastGroundNormal = -gravityDir;
        panelsTouched.Clear();
        panelsChanged = false;
        SetOutPanel(false); // the spawn is inside a panel; its trigger re-adds it on the next step
        lastRespawnTime = Time.fixedTime;

        // Still in the countdown -> keep waiting; otherwise go on immediately (lands on the floor).
        var level = LevelManager.Instance;
        SetState(level != null && !level.LevelStarted ? State.Waiting : State.Falling, "respawn");
    }

    /// <summary>Called by LevelGoal: the character stops, the level is complete.</summary>
    public void ReachGoal()
    {
        if (currentState == State.Dead) return;
        velocity = Vector2.zero;
        SetState(State.Finished, "reached the goal");
    }

    // ------------------------------------------------------------------ Helpers

    Vector2 Forward(Vector2 up) => new Vector2(up.y, -up.x) * (facing < 0 ? -1f : 1f);

    /// <summary>Rotates the body towards "up". Outside the panels the angle never changes.</summary>
    void RotateTowards(Vector2 up, float dt)
    {
        if (outPanel) return;
        float target = Vector2.SignedAngle(Vector2.up, up);
        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, target, turnSpeed * dt));
    }

    bool IsKillZone(Collider2D c) => killZoneTagValid && c != null && c.CompareTag(killZoneTag);

    static EraserPart EraserPartOf(Collider2D c) => c != null ? c.GetComponent<EraserPart>() : null;

    /// <summary>
    /// Looks for an eraser in front with several rays, from just above the feet to the head, so a small
    /// eraser stuck to the floor is found too. Metal part (Deadly) -> dies (Dizzy). Only the pink rubber
    /// (Bounce) -> turns around and the eraser wobbles. Returns true if something happened.
    /// </summary>
    bool CheckEraserAhead(Vector2 dir, Vector2 up, float reach)
    {
        const int rays = 5;
        EraserPart bounce = null;
        contactFilter.SetLayerMask(groundMask);
        for (int i = 0; i < rays; i++)
        {
            float h = Mathf.Lerp(-footOffset + 0.05f, footOffset * 0.9f, i / (rays - 1f));
            int count = Physics2D.Raycast(rb.position + up * h, dir, contactFilter, hits, reach);
            for (int k = 0; k < count; k++)
            {
                if (hits[k].rigidbody == rb) continue;
                EraserPart part = EraserPartOf(hits[k].collider);
                if (part == null) continue;
                if (part.kind == EraserPart.Kind.Deadly)
                {
                    Die($"crashed into the metal end of eraser '{part.name}'", dizzy: true);
                    return true;
                }
                bounce = part;
            }
        }

        // Just after a bounce the character can still be touching the rubber: do not flip back.
        if (bounce == null || Time.time - lastEraserBounceTime < 0.3f) return false;
        lastEraserBounceTime = Time.time;
        FlipDirection($"bounced on eraser '{bounce.name}'");
        if (bounce.Eraser != null) bounce.Eraser.Bounce();
        return true;
    }

    bool EraserBelow(Vector2 pos, float distance, out EraserPart part)
    {
        part = null;
        contactFilter.SetLayerMask(groundMask);
        int count = Physics2D.Raycast(pos, gravityDir, contactFilter, hits, distance);
        for (int k = 0; k < count; k++)
        {
            if (hits[k].rigidbody == rb) continue;
            part = EraserPartOf(hits[k].collider);
            if (part != null) return true;
        }
        return false;
    }

    /// <summary>A ruler is anything tagged rulerTag or carrying the Ruler component (from the drag system).</summary>
    bool IsRuler(Collider2D c)
    {
        if (c == null) return false;
        if (!string.IsNullOrEmpty(rulerTag) && c.CompareTag(rulerTag)) return true;
        return c.GetComponentInParent<Ruler>() != null;
    }

    /// <summary>CompareTag with a tag missing from the Tag Manager logs errors, so check it once.</summary>
    bool TagExists(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        try { GameObject.FindGameObjectsWithTag(tag); return true; }
        catch (UnityException)
        {
            Debug.LogWarning($"[SurfaceWalker] Tag '{tag}' is not defined in the Tag Manager.", this);
            return false;
        }
    }

    /// <summary>Closest raycast hit, ignoring triggers and ourselves, filtered by floor/ruler.</summary>
    bool Cast(Vector2 origin, Vector2 dir, float distance, Filter filter, out RaycastHit2D best)
    {
        best = default;
        contactFilter.SetLayerMask(groundMask);
        int count = Physics2D.Raycast(origin, dir, contactFilter, hits, distance);
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            if (h.rigidbody == rb) continue;
            if (IsKillZone(h.collider)) continue; // never stand on a kill zone: touching it kills
            if (EraserPartOf(h.collider) != null) continue; // erasers are obstacles, never floors (CheckEraserAhead)
            bool ruler = IsRuler(h.collider);
            if (filter == Filter.FloorOnly && ruler) continue;
            if (filter == Filter.RulerOnly && !ruler) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
            }
        }
        return best.collider != null;
    }

    void OnDrawGizmosSelected()
    {
        Vector2 pos = transform.position;
        Vector2 up = Application.isPlaying ? lastGroundNormal : (Vector2)transform.up;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pos, pos - up * (footOffset + groundSnapDistance));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawLine(pos - up * footOffset, pos - up * (footOffset + rulerClearance));
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pos, pos + Forward(up) * (bodyHalfWidth + 0.3f));
        if (Application.isPlaying && currentState == State.Falling)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(pos, pos + gravityDir * 1.5f);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Character that walks forward automatically.
///
/// States (every change goes through SetState, so animations can hook into StateChanged):
///  - Walking : inside a panel, glued to the floor, rotating with its normal.
///  - OnRuler : on a bridge (collider tagged "ruler"). It does NOT follow the ruler normal:
///              it keeps the direction and the gravity it had when it left the floor.
///  - Falling : no support. Falls with the gravity of the last floor it touched.
///  - Dead    : the front sensor touched a ruler, or the character touched a "killZone" collider.
///              It only lasts an instant: the character is teleported to the spawn point
///              (Died / Respawned events) and goes on. The level countdown does not restart.
///  - Finished: reached the LevelGoal.
///  - Waiting : before the LevelManager countdown ends.
///
/// Setup: Rigidbody2D (Dynamic, Gravity Scale 0) + body Collider2D marked as Is Trigger,
/// plus a child with a FrontSensor (small trigger collider in front of the body).
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
    [Tooltip("Degrees per second the body rotates to match the floor.")]
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

    [Header("Death / respawn")]
    [Tooltip("Colliders with this tag kill the character on contact.")]
    public string killZoneTag = "killZone";
    [Tooltip("Optional. If empty, the character respawns where it was at the start of the level.")]
    public Transform spawnPoint;

    [Header("Gravity (custom)")]
    public float gravity = 25f;
    public float maxFallSpeed = 20f;
    [Tooltip("How fast the sideways speed is lost while falling.")]
    public float airDrag = 1.5f;
    public Vector2 startGravityDir = Vector2.down;

    [Header("Panels")]
    [Tooltip("Panel where the character starts. The character always starts inside a panel.")]
    public Panel startPanel;

    [Header("State (read only)")]
    [SerializeField] State currentState = State.Falling;
    [Tooltip("True while the character is outside every panel.")]
    [SerializeField] bool outPanel = false;

    [Header("Debug")]
    public bool logStateChanges = true;

    /// <summary>Raised on every state change (old, new). Use it to drive animations.</summary>
    public event Action<State, State> StateChanged;

    public State CurrentState => currentState;
    public bool OutPanel => outPanel;
    public Vector2 GravityDir { get; private set; }
    public Vector2 LastGroundNormal { get; private set; }
    /// <summary>Direction of movement while on a ruler / falling (the direction it had when leaving the floor).</summary>
    public Vector2 ExitDirection => Forward(-GravityDir);
    public Panel CurrentPanel { get; private set; }
    public Collider2D BodyCollider { get; private set; }
    public int Deaths { get; private set; }

    /// <summary>Raised when the character dies (reason). Respawned follows right after.</summary>
    public event Action<string> Died;
    public event Action Respawned;

    enum Filter { Any, FloorOnly, RulerOnly }

    Rigidbody2D rb;
    Vector2 spawnPosition;
    float spawnRotation;
    float lastRespawnTime = -1f;
    bool killZoneTagValid;
    Vector2 velocity;
    float unsupportedDistance;
    ContactFilter2D contactFilter;
    readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // we do our own gravity
        BodyCollider = GetComponent<Collider2D>();
        contactFilter = new ContactFilter2D { useTriggers = false };
        spawnPosition = rb.position;
        spawnRotation = rb.rotation;

        // CompareTag with a tag missing from the Tag Manager logs errors, so check it once.
        try { GameObject.FindGameObjectsWithTag(killZoneTag); killZoneTagValid = !string.IsNullOrEmpty(killZoneTag); }
        catch (UnityException)
        {
            killZoneTagValid = false;
            Debug.LogWarning($"[SurfaceWalker] Tag '{killZoneTag}' is not defined in the Tag Manager: kill zones are disabled.", this);
        }
    }

    void Start()
    {
        GravityDir = startGravityDir.sqrMagnitude > 0f ? startGravityDir.normalized : Vector2.down;
        LastGroundNormal = -GravityDir;

        // We always start inside a panel.
        outPanel = false;
        CurrentPanel = startPanel;

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

        switch (currentState)
        {
            case State.Walking: Walk(dt); break;
            case State.OnRuler: WalkOnRuler(dt); break;
            case State.Falling: Fall(dt); break;
            case State.Waiting:
            case State.Dead:
            case State.Finished: break;
        }
    }

    // ------------------------------------------------------------------ State

    void SetState(State next, string reason)
    {
        if (next == currentState) return;
        State prev = currentState;
        currentState = next;
        if (logStateChanges)
            Debug.Log($"[SurfaceWalker] {prev} -> {next} ({reason}) outPanel={outPanel} gravity={GravityDir}", this);
        StateChanged?.Invoke(prev, next);
    }

    // ------------------------------------------------------------------ Walking (panel floor)

    void Walk(float dt)
    {
        Vector2 up = LastGroundNormal;
        Vector2 forward = Forward(up);
        Vector2 pos = rb.position;
        float step = speed * dt;

        // 1) Wall in front (sharp concave corner): climb onto it. Rulers are never walls to climb.
        if (Cast(pos, forward, bodyHalfWidth + step + 0.05f, Filter.FloorOnly, out var wall) &&
            Vector2.Angle(wall.normal, up) > wallAngle)
        {
            LastGroundNormal = wall.normal;
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
            LastGroundNormal = ground.normal;
            rb.MovePosition(ground.point + ground.normal * footOffset);
            RotateTowards(ground.normal, dt);
            return;
        }

        // 4) The floor ended. A ruler within clearance? -> go straight onto it.
        GravityDir = -up;
        if (Cast(target, GravityDir, footOffset + rulerClearance, Filter.RulerOnly, out _))
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
        // Direction and gravity are the ones of the exit: no rotation, no normal following.
        Vector2 up = -GravityDir;
        Vector2 forward = ExitDirection;
        Vector2 target = rb.position + forward * (speed * dt);

        float reach = footOffset + Mathf.Max(rulerClearance, groundSnapDistance);
        if (Cast(target, GravityDir, reach, Filter.Any, out var hit))
        {
            if (!IsRuler(hit.collider))
            {
                // Reached a normal floor (next panel): walk on it again following its normal.
                if (Vector2.Angle(hit.normal, up) < wallAngle && hit.distance <= footOffset + groundSnapDistance)
                {
                    LastGroundNormal = hit.normal;
                    unsupportedDistance = 0f;
                    rb.MovePosition(hit.point + hit.normal * footOffset);
                    SetState(State.Walking, $"reached floor '{hit.collider.name}'");
                    return;
                }
            }
            else
            {
                // Settle the feet onto the ruler, only along the gravity axis.
                unsupportedDistance = 0f;
                float gap = hit.distance - footOffset;
                target += GravityDir * Mathf.Clamp(gap, -rulerSnapSpeed * dt, rulerSnapSpeed * dt);
                rb.MovePosition(target);
                RotateTowards(up, dt);
                return;
            }
        }

        // No ruler under the feet: keep going straight a little, then fall.
        rb.MovePosition(target);
        RotateTowards(up, dt);
        unsupportedDistance += speed * dt;
        if (unsupportedDistance > edgeTolerance) StartFalling("ruler ended");
    }

    void EnterRuler(string reason)
    {
        LastGroundNormal = -GravityDir;
        velocity = Vector2.zero;
        unsupportedDistance = 0f;
        SetState(State.OnRuler, reason);
    }

    // ------------------------------------------------------------------ Falling

    void Fall(float dt)
    {
        // Split velocity in "along gravity" and "sideways".
        float along = Vector2.Dot(velocity, GravityDir);
        Vector2 sideways = velocity - along * GravityDir;

        along = Mathf.Min(along + gravity * dt, maxFallSpeed);
        sideways *= Mathf.Exp(-airDrag * dt);
        velocity = sideways + along * GravityDir;

        Vector2 delta = velocity * dt;
        Vector2 pos = rb.position;

        // Move sideways first, then look along gravity.
        float fallDist = Mathf.Max(0f, Vector2.Dot(delta, GravityDir));
        Vector2 origin = pos + (delta - GravityDir * Vector2.Dot(delta, GravityDir));
        if (Cast(origin, GravityDir, footOffset + fallDist + rulerClearance, Filter.Any, out var hit))
        {
            if (IsRuler(hit.collider))
            {
                // Caught by a bridge (with clearance). Feet settle on it in WalkOnRuler.
                rb.MovePosition(origin + GravityDir * Mathf.Min(fallDist, Mathf.Max(0f, hit.distance - footOffset)));
                EnterRuler($"caught by ruler '{hit.collider.name}'");
                return;
            }
            if (hit.distance <= footOffset + fallDist && Vector2.Dot(hit.normal, -GravityDir) > 0.5f)
            {
                Land(hit);
                return;
            }
        }

        rb.MovePosition(pos + delta);
        RotateTowards(-GravityDir, dt);
    }

    void StartFalling(string reason)
    {
        GravityDir = -LastGroundNormal;
        velocity = Forward(LastGroundNormal) * speed;
        unsupportedDistance = 0f;
        SetState(State.Falling, reason);
    }

    void Land(RaycastHit2D hit)
    {
        LastGroundNormal = hit.normal;
        rb.MovePosition(hit.point + hit.normal * footOffset);
        velocity = Vector2.zero;
        unsupportedDistance = 0f;
        SetState(State.Walking, $"landed on '{hit.collider.name}'");
    }

    // ------------------------------------------------------------------ Death

    /// <summary>Called by the FrontSensor child when it touches something.</summary>
    public void OnFrontSensorTouched(Collider2D other)
    {
        if (IsRuler(other)) Die($"front sensor hit ruler '{other.name}'");
        else if (IsKillZone(other)) Die($"touched killZone '{other.name}'");
    }

    /// <summary>Any collider of the character (body or sensor) touching a killZone kills it.</summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsKillZone(other)) Die($"touched killZone '{other.name}'");
    }

    /// <summary>Dies and is instantly teleported to the spawn point. The countdown does not restart.</summary>
    public void Die(string reason)
    {
        if (currentState == State.Dead || currentState == State.Finished) return;
        // Several colliders can report the same death in the same physics step: only the first counts.
        if (Time.fixedTime == lastRespawnTime) return;

        Deaths++;
        Debug.Log($"[SurfaceWalker] DIED ({reason}). Respawning. Deaths = {Deaths}", this);
        velocity = Vector2.zero;
        SetState(State.Dead, reason);
        Died?.Invoke(reason);
        Respawn();
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
        GravityDir = startGravityDir.sqrMagnitude > 0f ? startGravityDir.normalized : Vector2.down;
        LastGroundNormal = -GravityDir;
        outPanel = false;
        CurrentPanel = startPanel;
        lastRespawnTime = Time.fixedTime;

        // Still in the countdown -> keep waiting; otherwise go on immediately (lands on the floor).
        var level = LevelManager.Instance;
        SetState(level != null && !level.LevelStarted ? State.Waiting : State.Falling, "respawn");
        Respawned?.Invoke();
    }

    /// <summary>Called by LevelGoal: the character stops, the level is complete.</summary>
    public void ReachGoal()
    {
        if (currentState == State.Dead) return;
        velocity = Vector2.zero;
        SetState(State.Finished, "reached the goal");
    }

    // ------------------------------------------------------------------ Panels

    public void OnEnteredPanel(Panel panel)
    {
        CurrentPanel = panel;
        outPanel = false;
        // TODO: change the character depending on the panel.
    }

    public void OnExitedPanel(Panel panel)
    {
        // Ignore an exit of a panel we already left (e.g. next panel's enter fired first).
        if (CurrentPanel != null && CurrentPanel != panel) return;
        CurrentPanel = null;
        outPanel = true;
        // Nothing else to do here: when the floor ends, the walker goes onto a ruler or falls
        // with the gravity of the last floor. TODO: change the character depending on the panel.
    }

    // ------------------------------------------------------------------ Helpers

    Vector2 Forward(Vector2 up) => new Vector2(up.y, -up.x) * (facing < 0 ? -1f : 1f);

    bool IsKillZone(Collider2D c) => killZoneTagValid && c != null && c.CompareTag(killZoneTag);

    /// <summary>A ruler is anything tagged rulerTag or carrying the Ruler component (from the drag system).</summary>
    bool IsRuler(Collider2D c)
    {
        if (c == null) return false;
        if (!string.IsNullOrEmpty(rulerTag) && c.CompareTag(rulerTag)) return true;
        return c.GetComponentInParent<Ruler>() != null;
    }

    void RotateTowards(Vector2 up, float dt)
    {
        float target = Vector2.SignedAngle(Vector2.up, up);
        rb.MoveRotation(Mathf.MoveTowardsAngle(rb.rotation, target, turnSpeed * dt));
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
        Vector2 up = Application.isPlaying ? LastGroundNormal : (Vector2)transform.up;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pos, pos - up * (footOffset + groundSnapDistance));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawLine(pos - up * footOffset, pos - up * (footOffset + rulerClearance));
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pos, pos + Forward(up) * (bodyHalfWidth + 0.3f));
        if (Application.isPlaying && currentState == State.Falling)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(pos, pos + GravityDir * 1.5f);
        }
    }
}

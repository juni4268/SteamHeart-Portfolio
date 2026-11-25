using System.Collections.Generic;
using UnityEngine;

public enum EnemyBChargePhase { Idle, Preparing, Walking, Returning }

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyFrameMovement), typeof(EnemyHealth))]
public sealed class EnemyBChargeController : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField, Min(0f)] private float detectionRange = 6f;
    [SerializeField, Min(0f)] private float detectionHeight = 1.5f;

    [Header("Attack")]
    [SerializeField, Min(1)] private int attackDamage = 1;
    [Tooltip("Hitbox center in enemy-local coordinates. X mirrors with the charge direction.")]
    [SerializeField] private Vector2 attackPointOffset;
    [SerializeField] private Vector2 attackBoxSize = Vector2.one;
    [SerializeField] private LayerMask playerLayer;

    [Header("Animator Parameters")]
    [SerializeField] private string chargeParameter = "IsCharging";
    [SerializeField] private string returnParameter = "ReturnRequested";
    [SerializeField] private string finishCycleParameter = "FinishCharge";

    [Header("Walls")]
    [SerializeField] private LayerMask wallLayer = 1 << 7;

    private Animator animator;
    private EnemyFrameMovement movement;
    private Rigidbody2D body;
    private EnemyHealth health;
    private EnemySensor sensor;
    private EnemyAlertIndicator alertIndicator;
    private Transform player;
    private int direction;
    private float walkTime;
    private int finishAfterLoop = -1;
    private bool chargeRequested;
    private bool returnRequested;
    private readonly List<RaycastHit2D> wallHits = new List<RaycastHit2D>();

    public EnemyBChargePhase Phase { get; private set; }
    public bool IsExecuting => chargeRequested || Phase != EnemyBChargePhase.Idle;
    public Vector3 AttackHitboxCenter => transform.TransformPoint(attackPointOffset);

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        movement = GetComponent<EnemyFrameMovement>();
        body = GetComponent<Rigidbody2D>();
        movement.ConstrainHorizontalStep = ConstrainMovementAgainstWalls;
        health = GetComponent<EnemyHealth>();
        sensor = new EnemySensor(transform);
        alertIndicator = GetComponent<EnemyAlertIndicator>();
        health.ControlLockChanged += HandleControlLockChanged;
        health.Died += HandleDeath;
    }

    private void OnDestroy()
    {
        if (movement != null) movement.ConstrainHorizontalStep = null;
        if (health == null) return;
        health.ControlLockChanged -= HandleControlLockChanged;
        health.Died -= HandleDeath;
    }

    private void Update()
    {
        if (health.IsDead || HitStopManager.IsActive) return;
        if (!IsExecuting)
        {
            if (health.IsControlLocked) return;
            if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (sensor.IsTargetVisible(player, detectionRange, detectionHeight, wallLayer))
                TryStart(sensor.DirectionTo(player, 0f));
        }
        else if (Phase == EnemyBChargePhase.Walking)
        {
            CheckAttackDamage();
        }
    }

    public bool TryStart(int moveDirection)
    {
        if (!isActiveAndEnabled || IsExecuting || moveDirection == 0 || health.IsDead
            || health.IsControlLocked || HitStopManager.IsActive || animator == null || !animator.isActiveAndEnabled)
            return false;

        FaceDirection(moveDirection);
        chargeRequested = true;
        ResetCycle();
        animator.SetBool(chargeParameter, true);
        alertIndicator?.Show();
        return true;
    }

    private void FaceDirection(int moveDirection)
    {
        direction = moveDirection > 0 ? 1 : -1;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    private void ResetCycle()
    {
        returnRequested = false;
        finishAfterLoop = -1;
        animator.SetBool(returnParameter, false);
        animator.SetBool(finishCycleParameter, false);
    }

    private void FixedUpdate()
    {
        if (Phase == EnemyBChargePhase.Walking) UpdateMovementControl();
    }

    internal void NotifyPhaseEntered(EnemyBChargePhase phase, float normalizedTime)
    {
        if (!isActiveAndEnabled) return;
        EnemyBChargePhase previousPhase = Phase;
        Phase = phase;
        if (phase == EnemyBChargePhase.Preparing && previousPhase == EnemyBChargePhase.Returning)
        {
            // Once engaged, start each new charge without another detection-range check.
            if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
            int nextDirection = sensor.DirectionTo(player, 0f);
            if (nextDirection != 0) FaceDirection(nextDirection);
            ResetCycle();
        }
        else if (phase == EnemyBChargePhase.Walking)
        {
            walkTime = normalizedTime;
            UpdateMovementControl();
        }
        else if (phase == EnemyBChargePhase.Returning)
        {
            PauseMovement();
        }
        else if (phase == EnemyBChargePhase.Idle && previousPhase != EnemyBChargePhase.Idle)
        {
            Cancel();
        }
    }

    internal void NotifyPhaseUpdated(EnemyBChargePhase phase, float normalizedTime)
    {
        if (!isActiveAndEnabled || phase != Phase || phase != EnemyBChargePhase.Walking) return;
        walkTime = normalizedTime;
        // Arm the end transition during the extra cycle. Animator owns the exit boundary.
        if (finishAfterLoop >= 0 && Mathf.FloorToInt(walkTime) >= finishAfterLoop - 1)
            animator.SetBool(finishCycleParameter, true);
    }

    internal void NotifyPhaseExited(EnemyBChargePhase phase)
    {
        if (phase != EnemyBChargePhase.Walking) return;
        PauseMovement();
    }

    private void CheckAttackDamage()
    {
        if (Phase != EnemyBChargePhase.Walking || finishAfterLoop >= 0 || returnRequested || HitStopManager.IsActive) return;
        Vector3 center = AttackHitboxCenter;
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, attackBoxSize, 0f, playerLayer))
        {
            PlayerHealth target = hit.GetComponentInParent<PlayerHealth>();
            if (target == null || !target.TryTakeDamage(new DamageInfo(attackDamage, center))) continue;
            // Finish this cycle, then play one complete additional cycle.
            finishAfterLoop = Mathf.FloorToInt(walkTime) + 2;
            return;
        }
    }

    private void HandleControlLockChanged(bool locked)
    {
        // Keep the animation and its curve sampling alive without cancelling knockback velocity.
        if (locked) movement.Suspend();
        else if (isActiveAndEnabled && Phase == EnemyBChargePhase.Walking) UpdateMovementControl();
    }

    private void HandleDeath() => enabled = false;

    private void UpdateMovementControl()
    {
        if (health.IsDead || health.IsControlLocked || HitStopManager.IsActive)
        {
            movement.Suspend();
            return;
        }
        if (returnRequested) movement.Stop();
        else movement.Move(direction);
    }

    private void PauseMovement()
    {
        if (health.IsControlLocked) movement.Suspend();
        else movement.Stop();
    }

    private void OnCollisionEnter2D(Collision2D collision) => CheckWall(collision);
    private void OnCollisionStay2D(Collision2D collision) => CheckWall(collision);

    private float ConstrainMovementAgainstWalls(float step)
    {
        if (!isActiveAndEnabled || Phase != EnemyBChargePhase.Walking) return step;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(wallLayer);
        wallHits.Clear();
        body.Cast(Vector2.right * Mathf.Sign(step), filter, wallHits, Mathf.Abs(step));
        float allowedDistance = Mathf.Abs(step);
        bool blocked = false;
        foreach (RaycastHit2D hit in wallHits)
        {
            if (Mathf.Abs(hit.normal.x) < 0.5f) continue;
            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance));
            blocked = true;
        }
        if (blocked) RequestWallReturn();
        return Mathf.Sign(step) * allowedDistance;
    }

    private void CheckWall(Collision2D collision)
    {
        if (Phase != EnemyBChargePhase.Walking || returnRequested
            || (wallLayer.value & (1 << collision.gameObject.layer)) == 0) return;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Mathf.Abs(collision.GetContact(i).normal.x) < 0.5f) continue;
            RequestWallReturn();
            return;
        }
    }

    internal void RequestWallReturn()
    {
        if (Phase != EnemyBChargePhase.Walking) return;
        returnRequested = true;
        movement.Stop();
        animator.SetBool(returnParameter, true);
    }

    private void Cancel()
    {
        chargeRequested = false;
        returnRequested = false;
        finishAfterLoop = -1;
        Phase = EnemyBChargePhase.Idle;
        PauseMovement();
        if (animator == null) return;
        animator.SetBool(chargeParameter, false);
        animator.SetBool(returnParameter, false);
        animator.SetBool(finishCycleParameter, false);
    }

    private void OnDisable() => Cancel();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(detectionRange * 2f, detectionHeight * 2f, 0f));
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(AttackHitboxCenter, attackBoxSize);
    }
}

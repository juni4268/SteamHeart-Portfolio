using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Patrols horizontally at a fixed height until a pattern takes control.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Boss2Controller), typeof(Rigidbody2D))]
public sealed class Boss2BodyMovement : MonoBehaviour
{
    [SerializeField] private Collider2D movementCollider;
    [SerializeField] private LayerMask obstacleLayers;
    [Tooltip("World-space left and right boundaries. The entire movement collider stays inside them.")]
    [FormerlySerializedAs("horizontalRange")]
    [SerializeField] private Vector2 mapBounds = new Vector2(-4f, 5f);
    [Tooltip("Minimum and maximum distance from the boss's current position for each move.")]
    [SerializeField] private Vector2 moveDistanceRange = new Vector2(1.5f, 3f);
    [SerializeField, Min(0.01f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0.01f)] private float accelerationDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float decelerationDuration = 0.35f;
    [SerializeField, Min(0f)] private float initialDelay = 0.8f;
    [SerializeField] private Vector2 waitDurationRange = new Vector2(1f, 1.5f);
    [Header("Main Pattern")]
    [SerializeField, Min(0.01f)] private float patternStopDuration = 0.25f;
    [SerializeField] private Vector2 postPatternDelayRange = new Vector2(0.5f, 0.8f);

    private const float WallClearance = 0.02f;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[16];
    private Boss2Controller owner;
    private Rigidbody2D body;
    private RigidbodyConstraints2D originalConstraints;
    private bool patrolling;
    private bool paused;
    private bool waiting;
    private bool mainPatternHeld;
    private bool mainPatternStopped;
    private float patrolHeight;
    private float targetX;
    private float remainingWait;
    private float currentSpeed;
    private bool patternMoving;
    private bool deathMoving;
    private Vector2 patternTarget;
    private float patternSpeed;
    private Vector2 previousPatternPosition;
    private float patternBlockedTime;

    public bool IsPatrolPaused => paused;
    public Vector2 HorizontalMapBounds => mapBounds;
    public bool IsMoving => isActiveAndEnabled && patrolling && !paused && Mathf.Abs(currentSpeed) > 0.0001f;
    public float HorizontalSpeed => IsMoving ? currentSpeed : 0f;
    public bool IsFacingLeft => HorizontalSpeed < 0f;
    public float MovementBlend => IsMoving ? Mathf.Clamp01(Mathf.Abs(currentSpeed) / moveSpeed) : 0f;
    public bool IsPatternMoving => patternMoving;
    public bool IsPatternMovementBlocked { get; private set; }
    internal bool IsReadyForMainPattern => paused || (patrolling && mainPatternHeld && mainPatternStopped);

    private void OnEnable()
    {
        owner = GetComponent<Boss2Controller>();
        body = GetComponent<Rigidbody2D>();
        originalConstraints = body.constraints;
        patrolling = mainPatternHeld = mainPatternStopped = false;
        paused = false;
        currentSpeed = 0f;
    }

    private void OnDisable() => StopPatrol();

    private void FixedUpdate() => Tick(Time.fixedDeltaTime);

    private void Tick(float deltaTime)
    {
        if (deathMoving && owner.IsDead)
        {
            TickPatternMovement(deltaTime);
            return;
        }
        if (owner.CurrentState == Boss2CombatState.ReturningForGroggy)
        {
            if (HitStopManager.IsActive || Time.timeScale <= 0f) return;
            if (paused) { TickPatternMovement(deltaTime); return; }
            if (!patrolling) { patrolHeight = body.position.y; patrolling = true; }
            StopForMainPattern(deltaTime);
            return;
        }
        if (owner.CurrentState != Boss2CombatState.Fighting || owner.IsDead)
        {
            StopPatrol();
            return;
        }
        if (paused)
        {
            TickPatternMovement(deltaTime);
            return;
        }
        if (HitStopManager.IsActive || Time.timeScale <= 0f)
        {
            if (patrolling) body.linearVelocity = Vector2.zero;
            return;
        }
        TickPatrol(deltaTime);
    }

    private void TickPatternMovement(float deltaTime)
    {
        if (!patternMoving || HitStopManager.IsActive || Time.timeScale <= 0f) return;

        if (Vector2.Distance(body.position, previousPatternPosition) < 0.0001f)
            patternBlockedTime += deltaTime;
        else patternBlockedTime = 0f;
        previousPatternPosition = body.position;
        if (patternBlockedTime >= 0.5f && Vector2.Distance(body.position, patternTarget) >= 0.001f)
        {
            patternMoving = false;
            IsPatternMovementBlocked = true;
            body.linearVelocity = Vector2.zero;
            return;
        }
        Vector2 next = Vector2.MoveTowards(body.position, patternTarget, patternSpeed * deltaTime);
        body.MovePosition(next);
        if (Vector2.Distance(body.position, patternTarget) < 0.001f)
        {
            patternMoving = false;
            body.linearVelocity = Vector2.zero;
        }
    }

    private void TickPatrol(float deltaTime)
    {
        if (!patrolling)
        {
            patrolHeight = body.position.y;
            patrolling = true;
            StartWait(initialDelay);
        }

        if (owner.HasMainPattern)
        {
            StopForMainPattern(deltaTime);
            return;
        }
        if (mainPatternHeld)
        {
            mainPatternHeld = mainPatternStopped = false;
            StartWait(SampleRange(postPatternDelayRange));
        }

        if (waiting)
        {
            remainingWait = Mathf.Max(0f, remainingWait - deltaTime);
            if (remainingWait > 0f)
            {
                HoldPosition();
                return;
            }
            if (!TryChooseDestination())
            {
                StartWait(SampleRange(waitDurationRange));
                HoldPosition();
                return;
            }
            waiting = false;
        }

        TickTravel(deltaTime);
    }

    private void TickTravel(float deltaTime)
    {
        SetHorizontalConstraints();
        float remaining = targetX - body.position.x;
        float brakingAcceleration = moveSpeed / decelerationDuration;
        float desiredSpeed = Mathf.Sign(remaining) * Mathf.Min(moveSpeed,
            Mathf.Sqrt(2f * brakingAcceleration * Mathf.Abs(remaining)));
        float acceleration = Mathf.Abs(desiredSpeed) < Mathf.Abs(currentSpeed)
            ? brakingAcceleration : moveSpeed / accelerationDuration;
        float nextSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * deltaTime);
        float step = (currentSpeed + nextSpeed) * 0.5f * deltaTime;
        if (Mathf.Abs(step) >= Mathf.Abs(remaining)) step = remaining;
        bool blocked = MoveHorizontal(step);
        currentSpeed = nextSpeed;
        if (blocked || step == remaining)
        {
            StartWait(SampleRange(waitDurationRange));
        }
    }

    private void StopForMainPattern(float deltaTime)
    {
        mainPatternHeld = true;
        if (currentSpeed == 0f)
        {
            HoldPosition();
            mainPatternStopped = true;
            return;
        }
        SetHorizontalConstraints();
        float nextSpeed = Mathf.MoveTowards(currentSpeed, 0f, moveSpeed / patternStopDuration * deltaTime);
        bool blocked = MoveHorizontal((currentSpeed + nextSpeed) * 0.5f * deltaTime);
        currentSpeed = blocked ? 0f : nextSpeed;
    }

    /// <summary>Releases movement and physics constraints for a pattern-controlled move.</summary>
    public void PausePatrol()
    {
        paused = true;
        StopPatrol();
    }

    public void MoveForPattern(Vector2 destination, float speed)
    {
        if (speed <= 0f) throw new System.ArgumentOutOfRangeException(nameof(speed));
        PausePatrol();
        patternTarget = destination;
        patternSpeed = speed;
        previousPatternPosition = body.position;
        patternBlockedTime = 0f;
        IsPatternMovementBlocked = false;
        patternMoving = true;
        deathMoving = false;
    }

    public void MoveForDeath(Vector2 destination, float speed)
    {
        MoveForPattern(destination, speed);
        deathMoving = true;
    }

    /// <summary>Resumes horizontal patrol at the height reached by the pattern.</summary>
    public void ResumePatrol()
    {
        patternMoving = false;
        deathMoving = false;
        if (body != null) body.linearVelocity = Vector2.zero;
        paused = false;
    }

    private bool TryChooseDestination()
    {
        float minimum = Mathf.Max(0.01f, Mathf.Min(moveDistanceRange.x, moveDistanceRange.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(moveDistanceRange.x, moveDistanceRange.y));
        Bounds bounds = movementCollider.bounds;
        float leftSpace = bounds.min.x - Mathf.Min(mapBounds.x, mapBounds.y) - WallClearance;
        float rightSpace = Mathf.Max(mapBounds.x, mapBounds.y) - bounds.max.x - WallClearance;
        leftSpace = Mathf.Min(leftSpace, FreeDistance(Vector2.left, maximum));
        rightSpace = Mathf.Min(rightSpace, FreeDistance(Vector2.right, maximum));
        bool canLeft = leftSpace >= minimum;
        bool canRight = rightSpace >= minimum;
        if (!canLeft && !canRight) return false;
        bool left = canLeft && (!canRight || Random.value < 0.5f);
        float distance = Random.Range(minimum, Mathf.Min(maximum, left ? leftSpace : rightSpace));
        targetX = body.position.x + (left ? -distance : distance);
        return true;
    }

    private bool MoveHorizontal(float step)
    {
        Vector2 direction = step < 0f ? Vector2.left : Vector2.right;
        float distance = Mathf.Abs(step);
        Bounds bounds = movementCollider.bounds;
        float mapSpace = step < 0f
            ? bounds.min.x - Mathf.Min(mapBounds.x, mapBounds.y) - WallClearance
            : Mathf.Max(mapBounds.x, mapBounds.y) - bounds.max.x - WallClearance;
        float available = Mathf.Max(0f, Mathf.Min(mapSpace, FreeDistance(direction, distance)));
        float actualDistance = Mathf.Min(distance, available);
        body.MovePosition(new Vector2(body.position.x + direction.x * actualDistance, patrolHeight));
        return actualDistance + 0.0001f < distance;
    }

    private float FreeDistance(Vector2 direction, float distance)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(obstacleLayers);
        int count = movementCollider.Cast(direction, filter, castHits, distance + WallClearance);
        float available = distance;
        for (int i = 0; i < count; i++)
            available = Mathf.Min(available, Mathf.Max(0f, castHits[i].distance - WallClearance));
        return available;
    }

    private void StartWait(float duration)
    {
        currentSpeed = 0f;
        waiting = true;
        remainingWait = duration;
    }

    private void SetHorizontalConstraints() => body.constraints = originalConstraints |
        RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

    private void HoldPosition()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.constraints = originalConstraints | RigidbodyConstraints2D.FreezeAll;
    }

    private static float SampleRange(Vector2 range) =>
        Random.Range(Mathf.Max(0f, Mathf.Min(range.x, range.y)), Mathf.Max(0f, Mathf.Max(range.x, range.y)));

    private void StopPatrol()
    {
        if (!patrolling || body == null) return;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.constraints = originalConstraints;
        patrolling = false;
        mainPatternHeld = mainPatternStopped = false;
        currentSpeed = 0f;
    }
}

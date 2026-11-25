using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(EnemyFrameMovement))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        Idle,
        Patrol,
        Chase
    }

    [Header("Detection")]
    [SerializeField] private float aggroRange = 3.5f;
    [SerializeField] private float aggroHeight = 1f;
    [SerializeField] private float loseRange = 8f;

    [Header("Movement")]
    [SerializeField] private float patrolRange = 9f;
    [Min(0f)]
    [FormerlySerializedAs("facingDeadZone")]
    [Tooltip("Within this horizontal target distance, keep the current facing and stop chase movement.")]
    [SerializeField] private float targetHorizontalDeadZone = 0.2f;

    [Header("Random Patrol")]
    [SerializeField] private float minIdleDuration = 3f;
    [SerializeField] private float maxIdleDuration = 5f;
    [SerializeField] private float minPatrolDuration = 3f;
    [SerializeField] private float maxPatrolDuration = 10f;

    [Header("Ground / Wall Check")]
    [SerializeField] private LayerMask groundLayer = 1 << 6;
    [SerializeField] private LayerMask wallLayer = 1 << 7;
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private Transform wallCheckPoint;
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private float wallCheckDistance = 0.3f;

    private EnemyMotor motor;
    private EnemySensor sensor;
    private EnemyAttack enemyAttack;
    private EnemyAlertIndicator alertIndicator;
    private Transform player;

    private EnemyState currentState;
    private Vector2 spawnPosition;
    private float idleTimer;
    private float patrolTimer;
    private int facingDirection = 1;
    private bool canRunAI;
    private bool hasState;

    private void Start()
    {
        TryInitialize();
    }

    private bool TryInitialize()
    {
        if (canRunAI && sensor != null && motor != null) return true;

        canRunAI = false;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        enemyAttack = GetComponent<EnemyAttack>();
        alertIndicator = GetComponent<EnemyAlertIndicator>();

        if (rb == null)
        {
            Debug.LogError($"{nameof(EnemyAI)} needs a Rigidbody2D component.", this);
            enabled = false;
            return false;
        }

        rb.freezeRotation = true;
        motor = new EnemyMotor(transform, rb, GetComponent<EnemyFrameMovement>());
        EnsureSensor();

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }

        if (!hasState)
        {
            spawnPosition = transform.position;
            facingDirection = transform.localScale.x < 0f ? -1 : 1;
            SetFacingDirection(facingDirection);
            ChangeState(EnemyState.Idle);
        }

        canRunAI = true;
        return true;
    }

    private void Update()
    {
        if (!TryInitialize() || HitStopManager.IsActive) return;

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.Chase:
                UpdateChase();
                break;
        }

        UpdateMovementRequest();
    }

    private void FixedUpdate()
    {
        if (!TryInitialize()) return;

        // Recheck movement conditions before physics, including frames with multiple fixed steps.
        UpdateMovementRequest();
        motor.ApplyMovement();
    }

    private void OnDisable()
    {
        motor?.Stop();
    }

    private void ChangeState(EnemyState newState)
    {
        if (hasState && currentState == newState) return;

        currentState = newState;
        hasState = true;

        if (currentState == EnemyState.Idle)
        {
            idleTimer = Random.Range(minIdleDuration, maxIdleDuration);
            motor.Stop();
        }
        else if (currentState == EnemyState.Patrol)
        {
            patrolTimer = Random.Range(minPatrolDuration, maxPatrolDuration);
            ChooseRandomPatrolDirection();
        }
        else if (currentState == EnemyState.Chase)
        {
            alertIndicator?.Show();
        }
    }

    private void UpdateIdle()
    {
        if (CanDetectPlayer())
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f)
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    private void UpdatePatrol()
    {
        if (CanDetectPlayer())
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        patrolTimer -= Time.deltaTime;

        if (patrolTimer <= 0f || ShouldStopPatrol())
        {
            ChangeState(EnemyState.Idle);
        }
    }

    private void UpdateChase()
    {
        if (ShouldLosePlayer())
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        if (enemyAttack != null)
        {
            if (enemyAttack.IsAttacking)
            {
                return;
            }

            if (enemyAttack.IsTargetInAttackRange(player))
            {
                FaceTarget(player);

                if (enemyAttack.CanAttack())
                {
                    enemyAttack.TryAttack();
                }

                return;
            }
        }
    }

    private void UpdateMovementRequest()
    {
        int moveDirection = 0;

        if (currentState == EnemyState.Patrol)
        {
            if (!ShouldStopPatrol()) moveDirection = facingDirection;
        }
        else if (currentState == EnemyState.Chase)
        {
            bool isAttackBlockingMovement = enemyAttack != null
                && (enemyAttack.IsAttacking || enemyAttack.IsTargetInAttackRange(player));

            if (!isAttackBlockingMovement)
            {
                int playerDirection = GetDirectionToPlayer();
                if (playerDirection != 0)
                {
                    SetFacingDirection(playerDirection);
                    if (!IsLedgeAhead()) moveDirection = playerDirection;
                }
            }
        }

        motor.SetMoveRequest(moveDirection);
    }

    private EnemySensor EnsureSensor() => sensor ??= new EnemySensor(transform);

    private bool CanDetectPlayer() => EnsureSensor().IsTargetVisible(player, aggroRange, aggroHeight, wallLayer);

    private bool ShouldLosePlayer() => !EnsureSensor().IsTargetVisible(player, loseRange, aggroHeight, wallLayer);

    private int GetDirectionToPlayer() => EnsureSensor().DirectionTo(player, targetHorizontalDeadZone);

    private void ChooseRandomPatrolDirection()
    {
        int randomDirection = Random.value < 0.5f ? -1 : 1;
        float leftLimit = spawnPosition.x - patrolRange;
        float rightLimit = spawnPosition.x + patrolRange;

        if (transform.position.x <= leftLimit)
        {
            randomDirection = 1;
        }
        else if (transform.position.x >= rightLimit)
        {
            randomDirection = -1;
        }

        SetFacingDirection(randomDirection);
    }

    private bool ShouldStopPatrol()
    {
        if (IsOutsidePatrolRangeAhead()) return true;
        if (IsWallAhead()) return true;
        if (IsLedgeAhead()) return true;

        return false;
    }

    private bool IsOutsidePatrolRangeAhead()
    {
        float leftLimit = spawnPosition.x - patrolRange;
        float rightLimit = spawnPosition.x + patrolRange;

        return (transform.position.x <= leftLimit && facingDirection < 0)
            || (transform.position.x >= rightLimit && facingDirection > 0);
    }

    private void SetFacingDirection(int direction)
    {
        motor.Face(direction);
        facingDirection = motor.FacingDirection;
    }

    private void FaceTarget(Transform target) => SetFacingDirection(EnsureSensor().DirectionTo(target, targetHorizontalDeadZone));

    private bool IsWallAhead() => EnsureSensor().IsWallAhead(wallCheckPoint, facingDirection, wallCheckDistance, wallLayer);

    private bool IsWallBetweenPlayer() => EnsureSensor().IsWallBetween(player, wallLayer);

    private bool IsLedgeAhead() => EnsureSensor().IsLedgeAhead(groundCheckPoint, groundCheckDistance, groundLayer);

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        Vector3 spawnCenter = Application.isPlaying ? (Vector3)spawnPosition : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(aggroRange * 2f, aggroHeight * 2f, 0f));

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, new Vector3(loseRange * 2f, aggroHeight * 2f, 0f));

        Gizmos.color = Color.cyan;
        Vector3 patrolSize = new Vector3(patrolRange * 2f, 0.1f, 0f);
        Gizmos.DrawWireCube(spawnCenter, patrolSize);

        if (wallCheckPoint != null)
        {
            Gizmos.color = Color.magenta;
            Vector3 from = wallCheckPoint.position;
            Vector3 to = from + Vector3.right * facingDirection * wallCheckDistance;
            Gizmos.DrawLine(from, to);
        }

        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Vector3 from = groundCheckPoint.position;
            Vector3 to = from + Vector3.down * groundCheckDistance;
            Gizmos.DrawLine(from, to);
        }

        if (Application.isPlaying && player != null)
        {
            Gizmos.color = IsWallBetweenPlayer() ? Color.gray : Color.white;
            Gizmos.DrawLine(transform.position, player.position);
        }
    }
}

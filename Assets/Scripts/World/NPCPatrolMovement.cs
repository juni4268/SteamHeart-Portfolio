using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public sealed class NPCPatrolMovement : MonoBehaviour
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
    [SerializeField] private bool startsMovingRight = true;

    [Header("Random Pauses")]
    [Tooltip("다음 정지까지 이동하는 시간 범위입니다. (초)")]
    [SerializeField] private Vector2 moveDurationRange = new Vector2(2f, 5f);
    [Tooltip("멈춰 있는 시간 범위입니다. (초)")]
    [SerializeField] private Vector2 pauseDurationRange = new Vector2(0.8f, 2f);

    [Header("Obstacle Detection")]
    [SerializeField] private Transform forwardCheckPoint;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField, Min(0f)] private float detectRadius = 0.1f;

    private Rigidbody2D rb;
    private Animator animator;
    private int moveDirection;
    private bool isPaused;
    private float phaseTimeRemaining;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        moveDirection = startsMovingRight ? 1 : -1;
        UpdateFacing();
    }

    private void OnEnable()
    {
        isPaused = false;
        phaseTimeRemaining = Random.Range(moveDurationRange.x, moveDurationRange.y);
    }

    private void FixedUpdate()
    {
        phaseTimeRemaining -= Time.fixedDeltaTime;
        if (phaseTimeRemaining <= 0f)
        {
            isPaused = !isPaused;
            if (isPaused)
            {
                phaseTimeRemaining = Random.Range(pauseDurationRange.x, pauseDurationRange.y);
            }
            else
            {
                moveDirection = Random.Range(0, 2) == 0 ? -1 : 1;
                UpdateFacing();
                phaseTimeRemaining = Random.Range(moveDurationRange.x, moveDurationRange.y);
            }
        }

        if (isPaused)
        {
            StopMoving();
            return;
        }

        if (ShouldTurnAround())
        {
            moveDirection *= -1;
            UpdateFacing();
        }

        rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);
        animator.SetBool(IsMovingHash, true);
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            StopMoving();
        }
    }

    private void StopMoving()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        animator.SetBool(IsMovingHash, false);
    }

    private void UpdateFacing()
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * moveDirection;
        transform.localScale = scale;
    }

    private bool ShouldTurnAround()
    {
        if (forwardCheckPoint == null) return false;

        return Physics2D.OverlapCircle(forwardCheckPoint.position, detectRadius, obstacleLayer) != null;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        detectRadius = Mathf.Max(0f, detectRadius);
        moveDurationRange.x = Mathf.Max(0.1f, moveDurationRange.x);
        moveDurationRange.y = Mathf.Max(moveDurationRange.x, moveDurationRange.y);
        pauseDurationRange.x = Mathf.Max(0.1f, pauseDurationRange.x);
        pauseDurationRange.y = Mathf.Max(pauseDurationRange.x, pauseDurationRange.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (forwardCheckPoint == null) return;

        Gizmos.DrawWireSphere(forwardCheckPoint.position, detectRadius);
    }
}

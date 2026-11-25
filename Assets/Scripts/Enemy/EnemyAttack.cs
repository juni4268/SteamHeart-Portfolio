using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    [Header("Attack")]
    [Min(0f)]
    [SerializeField] private float attackRange = 1.2f;
    [Min(0f)]
    [SerializeField] private float attackHeight = 1f;
    [SerializeField] private float attackYOffset;
    [SerializeField] private float attackCooldown = 1f;
    [Min(1)]
    [SerializeField] private int attackDamage = 1;
    [Tooltip("Hitbox center in enemy-local coordinates. X mirrors with the enemy's facing direction.")]
    [SerializeField] private Vector2 attackPointOffset;
    [SerializeField] private Vector2 attackBoxSize = new Vector2(1f, 1f);
    [SerializeField] private LayerMask playerLayer;

    private Animator animator;
    private EnemyAnimationEventReceiver animationEventReceiver;
    private float lastAttackEndTime = -Mathf.Infinity;
    private bool isAttacking;
    private bool isHitboxActive;
    private bool hasDealtDamage;

    public bool IsAttacking => isAttacking;
    public Vector3 AttackHitboxCenter => transform.TransformPoint(attackPointOffset);

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null && animator.gameObject != gameObject)
        {
            animationEventReceiver = animator.GetComponent<EnemyAnimationEventReceiver>();
            if (animationEventReceiver == null)
            {
                animationEventReceiver = animator.gameObject.AddComponent<EnemyAnimationEventReceiver>();
            }

            animationEventReceiver.Initialize(this);
        }
    }

    private void Update()
    {
        if (isAttacking && (animator == null || !animator.isActiveAndEnabled))
        {
            CancelAttack();
            return;
        }
        if (isHitboxActive)
        {
            ApplyAttackDamage();
        }
    }

    public bool CanAttack()
    {
        return isActiveAndEnabled && !HitStopManager.IsActive && !isAttacking
            && Time.time >= lastAttackEndTime + attackCooldown;
    }

    public bool TryAttack()
    {
        if (!CanAttack()) return false;

        if (animator == null || !animator.isActiveAndEnabled)
        {
            Debug.LogWarning($"{nameof(EnemyAttack)} could not find an Animator.", this);
            return false;
        }

        isAttacking = true;
        isHitboxActive = false;
        hasDealtDamage = false;

        animator.SetTrigger("Attack");

        return true;
    }

    // Called by an Animation Event at the start of the active hit frames.
    public void BeginAttackHitbox()
    {
        if (!isActiveAndEnabled || !isAttacking) return;

        isHitboxActive = true;
        ApplyAttackDamage();
    }

    // Called by an Animation Event at the end of the active hit frames.
    public void EndAttackHitbox()
    {
        isHitboxActive = false;
    }

    // Checks for damage while the attack hitbox is active.
    public void ApplyAttackDamage()
    {
        if (!isActiveAndEnabled || HitStopManager.IsActive || !isAttacking || !isHitboxActive || hasDealtDamage) return;

        Vector3 hitboxCenter = AttackHitboxCenter;
        Collider2D[] hits = Physics2D.OverlapBoxAll(hitboxCenter, attackBoxSize, 0f, playerLayer);
        foreach (Collider2D hit in hits)
        {
            PlayerHealth playerHealth = hit.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = hit.GetComponentInParent<PlayerHealth>();
            }

            if (playerHealth == null) continue;

            playerHealth.TryTakeDamage(new DamageInfo(attackDamage, hitboxCenter));
            hasDealtDamage = true;
            return;
        }
    }

    // Called by an Animation Event near the end of the attack animation.
    public void FinishAttack()
    {
        if (!isAttacking) return;

        lastAttackEndTime = Time.time;
        isHitboxActive = false;
        isAttacking = false;
    }

    public void CancelAttack()
    {
        if (isAttacking) lastAttackEndTime = Time.time;

        isHitboxActive = false;
        isAttacking = false;
        hasDealtDamage = false;

        if (animator != null)
        {
            animator.ResetTrigger("Attack");
        }
    }

    public bool IsTargetInAttackRange(Transform target)
    {
        if (target == null) return false;

        Vector2 rangeCenter = GetAttackRangeCenter();
        float xDistance = Mathf.Abs(target.position.x - rangeCenter.x);
        float yDistance = Mathf.Abs(target.position.y - rangeCenter.y);

        return xDistance <= attackRange && yDistance <= attackHeight;
    }

    private void OnDisable()
    {
        CancelAttack();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 rangeCenter = GetAttackRangeCenter();
        Vector3 rangeSize = new Vector3(attackRange * 2f, attackHeight * 2f, 0f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(rangeCenter, rangeSize);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(AttackHitboxCenter, attackBoxSize);
    }

    private Vector2 GetAttackRangeCenter()
    {
        return (Vector2)transform.position + Vector2.up * attackYOffset;
    }
}

using UnityEngine;

public class EnemyAnimationEventReceiver : MonoBehaviour
{
    [Header("Movement Curve")]
    [Tooltip("Cumulative forward movement distance in world units. Start at 0. Use Linear tangents for continuous motion or Constant tangents for instant steps.")]
    [SerializeField] private float movementDistance;

    // Generated from the movement curve by EnemyMovementCurveSync; follows the active Clip.
    [SerializeField, HideInInspector] private float movementLoopDistance;

    private EnemyAttack enemyAttack;
    private EnemyFrameMovement frameMovement;

    public float MovementDistance => movementDistance;

    public void Initialize(EnemyAttack attack)
    {
        enemyAttack = attack;
    }

    private void Awake()
    {
        frameMovement = GetComponentInParent<EnemyFrameMovement>();
        if (enemyAttack == null)
        {
            enemyAttack = GetComponentInParent<EnemyAttack>();
        }
    }

    private void OnDidApplyAnimationProperties()
    {
        frameMovement?.ApplyMovement(movementDistance, movementLoopDistance);
    }

    public void SpawnAttackEffect(GameObject effectPrefab)
    {
        if (!isActiveAndEnabled || effectPrefab == null) return;

        Transform effect = Instantiate(effectPrefab, transform, false).transform;
        if (TryGetComponent(out SpriteRenderer visual) && visual.flipX)
        {
            Vector3 scale = effect.localScale;
            scale.x = -scale.x;
            effect.localScale = scale;
        }

        effect.SetParent(null, true);
    }

    public void ApplyAttackDamage()
    {
        enemyAttack?.ApplyAttackDamage();
    }

    public void BeginAttackHitbox()
    {
        enemyAttack?.BeginAttackHitbox();
    }

    public void EndAttackHitbox()
    {
        enemyAttack?.EndAttackHitbox();
    }

    public void FinishAttack()
    {
        enemyAttack?.FinishAttack();
    }
}

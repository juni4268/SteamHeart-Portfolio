using System.Collections;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public event System.Action<int, int, int> HealthChanged;
    public event System.Action<bool> ControlLockChanged;
    public event System.Action Died;

    [Header("Health")]
    [SerializeField, Min(1)] private int maxHp = 5;

    [Header("Reward")]
    [UnityEngine.Serialization.FormerlySerializedAs("coinReward")]
    [SerializeField, Min(0)] private int scrapReward = 1;

    [Header("Hit Knockback")]
    [Tooltip("Take damage without hit stun, knockback, hit animation, or attack interruption.")]
    [SerializeField] private bool superArmor;
    [SerializeField, Min(0f)] private float hitStunDuration = 0.2f;
    [SerializeField] private float knockbackForce = 4f;
    [SerializeField] private float knockbackUpForce = 0.5f;
    [Tooltip("Hit reaction Trigger. Leave empty to keep the current animation during knockback.")]
    [SerializeField] private string hitParameter = "Hit";

    [Header("Death Effect")]
    [SerializeField, Min(0f)] private float deathFlashDuration = 0.03f;
    [SerializeField] private CombatAnimationEffect deathEffectPrefab;
    [SerializeField] private EnemyDeathParts deathPartsPrefab;
    [SerializeField] private Transform deathEffectOrigin;

    private int currentHp;
    private bool isDead;
    private Coroutine hitStunCoroutine;
    private bool restoresAI;
    private bool isHitStunned;

    private Animator animator;
    private EnemyAI enemyAI;
    private EnemyAttack enemyAttack;
    private Rigidbody2D rb;
    private HitImpactFlash hitImpactFlash;

    public bool IsDead => isDead;
    public bool IsControlLocked => isHitStunned;
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        enemyAI = GetComponent<EnemyAI>();
        enemyAttack = GetComponent<EnemyAttack>();
        rb = GetComponent<Rigidbody2D>();
        hitImpactFlash = GetComponent<HitImpactFlash>();

        currentHp = maxHp;
    }

    public void TakeDamage(int damage) => TryTakeDamage(new DamageInfo(damage));

    public bool TryTakeDamage(DamageInfo damage)
    {
        if (!isActiveAndEnabled || isDead || damage.Amount <= 0) return false;

        int previousHp = currentHp;
        currentHp = Mathf.Max(currentHp - damage.Amount, 0);
        HealthChanged?.Invoke(previousHp, currentHp, maxHp);

        if (currentHp <= 0)
        {
            Die(damage);
        }
        else if (!superArmor)
        {
            HandleHit(damage);
        }
        return true;
    }

    private void HandleHit(DamageInfo damage)
    {
        enemyAttack?.CancelAttack();
        ApplyKnockback(damage);

        if (animator != null && !string.IsNullOrEmpty(hitParameter))
        {
            animator.SetTrigger(hitParameter);
        }
    }

    private void ApplyKnockback(DamageInfo damage)
    {
        if (rb == null) return;

        if (hitStunCoroutine != null)
        {
            StopCoroutine(hitStunCoroutine);
        }

        if (!isHitStunned)
        {
            isHitStunned = true;
            restoresAI = enemyAI != null && enemyAI.enabled;
            if (restoresAI) enemyAI.enabled = false;
            ControlLockChanged?.Invoke(true);
        }

        hitStunCoroutine = StartCoroutine(HitStunRoutine(damage));
    }

    private IEnumerator HitStunRoutine(DamageInfo damage)
    {
        int knockbackDirection = GetKnockbackDirection(damage);
        rb.linearVelocity = damage.HasKnockback ? damage.KnockbackVelocity
            : new Vector2(knockbackDirection * knockbackForce, knockbackUpForce);

        yield return new WaitForSeconds(damage.HasKnockback ? damage.ControlLockDuration : hitStunDuration);

        if (!isDead)
        {
            ReleaseHitStun(true);
        }

        hitStunCoroutine = null;
    }

    private void ReleaseHitStun(bool restoreAI)
    {
        if (!isHitStunned) return;
        isHitStunned = false;
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (restoreAI && restoresAI && enemyAI != null) enemyAI.enabled = true;
        restoresAI = false;
        ControlLockChanged?.Invoke(false);
    }

    private void StopHitStun(bool restoreAI)
    {
        if (hitStunCoroutine != null) StopCoroutine(hitStunCoroutine);
        hitStunCoroutine = null;
        ReleaseHitStun(restoreAI);
    }

    private void OnDisable()
    {
        StopHitStun(!isDead);
    }

    private int GetKnockbackDirection(DamageInfo damage)
    {
        if (damage.HasSource)
        {
            return transform.position.x >= damage.SourcePosition.x ? 1 : -1;
        }

        return transform.localScale.x >= 0f ? -1 : 1;
    }

    private void Die(DamageInfo damage)
    {
        if (isDead) return;

        isDead = true;
        Died?.Invoke();
        PlayerPermanentProgress.AddScrap(scrapReward);

        StopHitStun(false);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        if (enemyAI != null)
        {
            enemyAI.enabled = false;
        }

        if (enemyAttack != null)
        {
            enemyAttack.CancelAttack();
            enemyAttack.enabled = false;
        }

        foreach (Collider2D hitbox in GetComponentsInChildren<Collider2D>(true))
        {
            hitbox.enabled = false;
        }

        EnemyFrameMovement frameMovement = GetComponent<EnemyFrameMovement>();
        if (frameMovement != null) frameMovement.enabled = false;

        EnemyAlertIndicator alertIndicator = GetComponent<EnemyAlertIndicator>();
        if (alertIndicator != null) alertIndicator.enabled = false;

        if (animator != null) animator.enabled = false;

        int direction = damage.HasKnockback && damage.KnockbackVelocity.x != 0f
            ? (damage.KnockbackVelocity.x > 0f ? 1 : -1)
            : GetKnockbackDirection(damage);
        StartCoroutine(DeathRoutine(direction));
    }

    private IEnumerator DeathRoutine(int direction)
    {
        SpriteRenderer[] renderers = animator != null
            ? animator.GetComponentsInChildren<SpriteRenderer>(true)
            : GetComponentsInChildren<SpriteRenderer>(true);
        if (deathFlashDuration > 0f)
        {
            hitImpactFlash?.Play(1f, deathFlashDuration, 0f);
            yield return new WaitForSecondsRealtime(deathFlashDuration);
        }

        SpriteRenderer bodyRenderer = deathEffectOrigin != null
            ? deathEffectOrigin.GetComponent<SpriteRenderer>() : null;
        Vector3 position = bodyRenderer != null ? bodyRenderer.bounds.center
            : deathEffectOrigin != null ? deathEffectOrigin.position : transform.position;
        if (deathEffectPrefab != null)
        {
            // The burst owns its lifetime and survives removal of the enemy.
            Quaternion rotation = Quaternion.Euler(0f, direction < 0 ? 180f : 0f, 0f);
            CombatAnimationEffect effect = Instantiate(deathEffectPrefab, position, rotation);
            SpriteRenderer effectRenderer = effect.GetComponent<SpriteRenderer>();
            // Keep the effect's native pixels the same size as the enemy's sprite pixels.
            if (bodyRenderer != null && bodyRenderer.sprite != null
                && effectRenderer != null && effectRenderer.sprite != null)
            {
                Vector3 scale = bodyRenderer.transform.lossyScale;
                float pixelScale = effectRenderer.sprite.pixelsPerUnit / bodyRenderer.sprite.pixelsPerUnit;
                effect.transform.localScale = Vector3.Scale(effect.transform.localScale,
                    new Vector3(Mathf.Abs(scale.x) * pixelScale, Mathf.Abs(scale.y) * pixelScale, 1f));
            }
        }

        if (deathPartsPrefab != null && bodyRenderer != null && bodyRenderer.sprite != null)
        {
            EnemyDeathParts parts = Instantiate(deathPartsPrefab, position, Quaternion.identity);
            parts.Launch(bodyRenderer, direction);
        }

        // Hide in the burst's first frame; no corpse or fading body remains.
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null) renderer.enabled = false;
        }

        Destroy(gameObject);
    }

    [ContextMenu("Debug Take 1 Damage")]
    private void DebugTakeDamage()
    {
        TakeDamage(1);
    }
}

using System;
using UnityEngine;
using UnityEngine.Events;

public class BossHealth : MonoBehaviour, IDamageable
{
    private static readonly int HitTrigger = Animator.StringToHash("Hit");
    private static readonly int DeathTrigger = Animator.StringToHash("Death");

    [Header("Display")]
    [SerializeField] private string displayName = "BOSS";
    [SerializeField] private string epithet;

    [Header("Health")]
    [SerializeField, Min(1)] private int maxHp = 300;

    [Header("Reward")]
    [UnityEngine.Serialization.FormerlySerializedAs("coinReward")]
    [SerializeField, Min(0)] private int scrapReward = 1;

    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Events")]
    [SerializeField] private UnityEvent onDied = new UnityEvent();

    public event Action<int, int> HealthChanged;
    public event Action Died;

    private int currentHp;
    private bool isDead;
    private bool hasHitTrigger;
    private bool hasDeathTrigger;

    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public string DisplayName => displayName;
    public string Epithet => epithet;
    public bool IsDead => isDead;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            hasHitTrigger = HasAnimatorParameter(HitTrigger, AnimatorControllerParameterType.Trigger);
            hasDeathTrigger = HasAnimatorParameter(DeathTrigger, AnimatorControllerParameterType.Trigger);
        }

        currentHp = maxHp;
    }

    public void TakeDamage(int damage) => TryTakeDamage(new DamageInfo(damage));

    public bool TryTakeDamage(DamageInfo damage)
    {
        if (isDead || damage.Amount <= 0) return false;

        currentHp = Mathf.Max(currentHp - damage.Amount, 0);
        HealthChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
        {
            Die();
            return true;
        }

        SetAnimatorTrigger(HitTrigger, hasHitTrigger);
        return true;
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        PlayerPermanentProgress.AddScrap(scrapReward);

        SetAnimatorTrigger(DeathTrigger, hasDeathTrigger);
        Died?.Invoke();
        onDied.Invoke();
    }

    private void SetAnimatorTrigger(int triggerHash, bool hasTrigger)
    {
        if (animator == null || !hasTrigger)
        {
            return;
        }

        animator.SetTrigger(triggerHash);
    }

    private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }

    [ContextMenu("Debug Take 10 Damage")]
    private void DebugTakeDamage()
    {
        TakeDamage(10);
    }
}

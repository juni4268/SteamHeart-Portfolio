using System;
using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeSettings", menuName = "Project K/Upgrade Settings")]
public sealed class UpgradeSettings : ScriptableObject
{
    private const string ResourcePath = "Settings/UpgradeSettings";
    private static UpgradeSettings shared;

    [Header("Shared Materials")]
    [SerializeField, Min(0)] private int materialScrapCost = 5;
    [UnityEngine.Serialization.FormerlySerializedAs("healthPiecesPerUpgrade")]
    [SerializeField, Min(1)] private int materialsPerUpgrade = 3;

    [Header("Attack Upgrade")]
    [Tooltip("공격력 강화 1회 구매 시 증가하는 영구 공격력입니다.")]
    [SerializeField, Min(1)] private int attackPowerIncrease = 1;
    [SerializeField, Min(1)] private int maxAttackPowerBonus = 10;

    [Header("Health Upgrade")]
    [Tooltip("최대 체력 강화 1회 구매 시 증가하는 영구 최대 체력입니다.")]
    [SerializeField, Min(1)] private int maxHealthIncrease = 1;
    [SerializeField, Min(1)] private int maxHealthBonus = 10;

    [Header("Hit Invincibility Upgrade")]
    [SerializeField, Min(0.01f)] private float hitInvincibilityIncrease = 0.1f;
    [SerializeField, Min(0.01f)] private float maxHitInvincibilityBonus = 1f;

    public int MaterialScrapCost => Mathf.Max(0, materialScrapCost);
    public int MaterialsPerUpgrade => Mathf.Max(1, materialsPerUpgrade);
    public int AttackPowerIncrease => Mathf.Max(1, attackPowerIncrease);
    public int MaxAttackPowerBonus => Mathf.Max(1, maxAttackPowerBonus);
    public int MaxHealthIncrease => Mathf.Max(1, maxHealthIncrease);
    public int MaxHealthBonus => Mathf.Max(1, maxHealthBonus);
    public float HitInvincibilityIncrease => Mathf.Max(0.01f, hitInvincibilityIncrease);
    public float MaxHitInvincibilityBonus => Mathf.Max(0.01f, maxHitInvincibilityBonus);

    public static UpgradeSettings Shared
    {
        get
        {
            if (shared == null)
                shared = Resources.Load<UpgradeSettings>(ResourcePath);
            if (shared == null)
                throw new InvalidOperationException("Assign the shared upgrade settings at Assets/Resources/Settings/UpgradeSettings.asset.");
            return shared;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => shared = null;
}

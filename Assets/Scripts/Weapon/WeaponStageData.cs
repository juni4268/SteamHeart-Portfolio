using System;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class WeaponStageData
{
    [SerializeField] private string stageName;
    [SerializeField] private AnimatorOverrideController animatorOverrideController;
    [SerializeField] private WeaponAttackData[] comboAttacks = Array.Empty<WeaponAttackData>();
    [SerializeField] private WeaponAttackData airAttack = new WeaponAttackData();

    [Header("Skill Damage")]
    [FormerlySerializedAs("skillAMinDamage")]
    [SerializeField, Min(0)] private int groundSlamMinDamage;
    [FormerlySerializedAs("skillAMaxDamage")]
    [SerializeField, Min(0)] private int groundSlamMaxDamage;
    [FormerlySerializedAs("skillSDamage")]
    [SerializeField, Min(0)] private int counterDamage;

    public string StageName => stageName;
    public AnimatorOverrideController AnimatorOverrideController => animatorOverrideController;
    public WeaponAttackData[] ComboAttacks => comboAttacks;
    public WeaponAttackData AirAttack => airAttack;
    public int GroundSlamMinDamage => groundSlamMinDamage;
    public int GroundSlamMaxDamage => groundSlamMaxDamage;
    public int CounterDamage => counterDamage;

    internal void Validate()
    {
        // Random.Range uses an exclusive upper bound for integer damage rolls.
        groundSlamMinDamage = Mathf.Clamp(groundSlamMinDamage, 0, int.MaxValue - 1);
        groundSlamMaxDamage = Mathf.Clamp(groundSlamMaxDamage, groundSlamMinDamage, int.MaxValue - 1);
        counterDamage = Mathf.Max(0, counterDamage);
        airAttack?.Validate();
        if (comboAttacks == null) return;

        foreach (WeaponAttackData attack in comboAttacks)
        {
            attack?.Validate();
        }
    }
}

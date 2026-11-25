using System;
using UnityEngine;

public sealed class PlayerGroundAttack
{
    private static readonly int GroundAttack1Trigger = Animator.StringToHash("GroundAttack1");
    private static readonly int GroundAttack2Trigger = Animator.StringToHash("GroundAttack2");

    private readonly Player player;
    private readonly PlayerWeaponController weaponController;
    private readonly PlayerAttackDamage damage;
    private readonly Action<Component> onHit;

    private int attackNumber;
    private bool isSecondAttackQueued;
    private bool isComboInputOpen;
    private WeaponAttackData currentAttack;
    private WeaponAttackData secondAttack;

    public bool IsExecuting => attackNumber != 0;
    public int CurrentAttackNumber => attackNumber;
    public bool BlocksMovement => IsExecuting;

    public PlayerGroundAttack(Player player, PlayerWeaponController weaponController,
        PlayerAttackDamage damage, Action<Component> onHit)
    {
        this.player = player;
        this.weaponController = weaponController;
        this.damage = damage;
        this.onHit = onHit;
    }

    public bool TryHandleComboInput()
    {
        if (attackNumber != 1 || !isComboInputOpen) return false;
        isSecondAttackQueued = true;
        return true;
    }

    public bool TryStart()
    {
        WeaponAttackData firstAttack = GetAttackData(0);
        if (firstAttack == null || firstAttack.Damage <= 0) return false;

        return TryStartCombo(firstAttack);
    }

    private bool TryStartCombo(WeaponAttackData firstAttack)
    {
        Animator animator = player.anim;
        if (animator == null || !animator.isActiveAndEnabled)
            return false;

        WeaponAttackData nextAttack = GetAttackData(1);
        if (nextAttack == null || nextAttack.Damage <= 0) return false;

        attackNumber = 1;
        isSecondAttackQueued = false;
        isComboInputOpen = false;
        currentAttack = firstAttack;
        secondAttack = nextAttack;
        damage.BeginAttack();
        player.StopMovementForCombat();
        ResetAnimationTriggers();
        animator.SetTrigger(GroundAttack1Trigger);
        return true;
    }

    public void ApplyDamage()
    {
        if (!IsExecuting || currentAttack == null) return;
        damage.Apply(currentAttack, onHit);
    }

    public void StartComboInput()
    {
        if (attackNumber != 1) return;
        isComboInputOpen = true;
    }

    public void EndComboInput()
    {
        if (attackNumber != 1 || !isComboInputOpen) return;

        isComboInputOpen = false;
        if (!isSecondAttackQueued) return;

        isSecondAttackQueued = false;
        attackNumber = 2;
        currentAttack = secondAttack;
        secondAttack = null;
        damage.BeginAttack();
        player.anim.ResetTrigger(GroundAttack2Trigger);
        player.anim.SetTrigger(GroundAttack2Trigger);
    }

    // Attack 1 exiting during a combo must not finish attack 2.
    public void Finish(int expectedAttackNumber)
    {
        if (attackNumber != expectedAttackNumber) return;

        ResetAnimationTriggers();
        ClearCombo();
        damage.EndAttack();
    }

    public void Cancel()
    {
        ResetAnimationTriggers();
        ClearCombo();
        damage.EndAttack();
    }

    public void Cancel(int expectedAttackNumber)
    {
        if (attackNumber == expectedAttackNumber) Cancel();
    }

    private void ResetAnimationTriggers()
    {
        player?.anim?.ResetTrigger(GroundAttack1Trigger);
        player?.anim?.ResetTrigger(GroundAttack2Trigger);
    }

    private void ClearCombo()
    {
        attackNumber = 0;
        isSecondAttackQueued = false;
        isComboInputOpen = false;
        currentAttack = null;
        secondAttack = null;
    }

    private WeaponAttackData GetAttackData(int index)
    {
        if (weaponController == null || weaponController.CurrentWeapon == null)
            return null;

        WeaponAttackData[] attacks = weaponController.CurrentWeaponStage?.ComboAttacks;
        return attacks != null && index >= 0 && index < attacks.Length
            ? attacks[index]
            : null;
    }
}

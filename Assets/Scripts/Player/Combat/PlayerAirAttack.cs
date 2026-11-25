using UnityEngine;

public sealed class PlayerAirAttack
{
    private static readonly int AirAttackTrigger = Animator.StringToHash("AirAttack");
    private static readonly int JumpTrigger = Animator.StringToHash("Jump");

    private readonly Player player;
    private readonly PlayerMovement movement;
    private readonly PlayerAttackDamage damage;
    private readonly System.Action<Component> onHit;
    private WeaponAttackData attackData;
    private bool hasUsedAirAttack;

    public bool IsExecuting => attackData != null;
    public bool CanAttack => !hasUsedAirAttack && !IsExecuting;

    public PlayerAirAttack(Player player, PlayerMovement movement,
        PlayerAttackDamage damage, System.Action<Component> onHit)
    {
        this.player = player;
        this.movement = movement;
        this.damage = damage;
        this.onHit = onHit;
    }

    public void ResetUsage() => hasUsedAirAttack = false;

    public bool TryStart(WeaponAttackData data)
    {
        if (!CanAttack || data == null || data.Damage <= 0 || player.anim == null) return false;

        hasUsedAirAttack = true;
        attackData = data;
        damage.BeginAttack();
        movement?.ClearJumpRequest();
        player.anim.ResetTrigger(JumpTrigger);
        player.anim.ResetTrigger(AirAttackTrigger);
        player.anim.SetTrigger(AirAttackTrigger);
        return true;
    }

    public void ApplyDamage()
    {
        if (!IsExecuting) return;
        damage.Apply(attackData, onHit);
    }

    public void Finish()
    {
        if (!IsExecuting) return;

        attackData = null;
        damage.EndAttack();
    }

    public void Cancel()
    {
        attackData = null;
        damage.EndAttack();
        player?.anim?.ResetTrigger(AirAttackTrigger);
    }
}

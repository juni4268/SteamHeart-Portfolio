using UnityEngine;

public enum PlayerAction { None, Jump, Dash, Attack, GroundSlam, Counter }

// Control rules and a single input buffer; dash takes priority over counter, then other inputs.
public sealed class PlayerActionController
{
    private readonly Player player;
    private readonly PlayerMovement movement;
    private readonly PlayerAttack attack;
    private readonly PlayerGroundSlam groundSlam;
    private readonly PlayerCounter counter;
    private PlayerAction bufferedAction;
    private float bufferedUntil;
    private bool bufferedRejectionNotified;

    public PlayerActionController(Player player, PlayerMovement movement, PlayerAttack attack,
        PlayerGroundSlam groundSlam, PlayerCounter counter)
    {
        this.player = player;
        this.movement = movement;
        this.attack = attack;
        this.groundSlam = groundSlam;
        this.counter = counter;
    }

    public bool IsCombatExecuting => (attack != null && attack.IsExecuting) ||
        (groundSlam != null && groundSlam.IsExecuting) || (counter != null && counter.IsExecuting);

    public bool CanAnimate => player.anim != null && player.anim.isActiveAndEnabled;

    private bool CanBufferAction => player.isActiveAndEnabled &&
        player.input != null && player.input.Player.enabled &&
        player.health != null && player.health.isActiveAndEnabled && !player.health.IsDead &&
        (Time.timeScale > 0f || HitStopManager.IsActive) &&
        (movement == null || (!movement.IsKnockbackLocked && !movement.IsBeingCarried));

    private bool CanControl => CanBufferAction && !HitStopManager.IsActive &&
        (player.dash == null || !player.dash.IsDashing);

    private bool CanControlMovement => CanBufferAction && !HitStopManager.IsActive &&
        (player.dash == null || !player.dash.IsDashing || player.dash.IsLanding);

    public bool CanStartAction => CanControl && !IsCombatExecuting;
    private bool CanStartDefensiveAction => CanControl &&
        (attack == null || !attack.IsExecuting || attack.CanDashCancel) &&
        (groundSlam == null || !groundSlam.IsExecuting) && (counter == null || !counter.IsExecuting);
    public bool CanStartDash => CanStartDefensiveAction;
    public bool CanStartCounter => CanStartDefensiveAction;
    public bool CanMove => CanControlMovement && (attack == null || !attack.BlocksMovement) &&
        (groundSlam == null || !groundSlam.IsExecuting) && (counter == null || !counter.IsExecuting);
    public bool CanTurn => CanMove && (attack == null || !attack.IsAirAttacking);

    public bool CanExecuteCombatAction(Behaviour action) => action != null && action.isActiveAndEnabled &&
        player != null && player.isActiveAndEnabled && player.health != null && !player.health.IsDead;

    public void RequestAction(PlayerAction action)
    {
        if (!CanBufferAction || player.Data == null)
        {
            bufferedAction = PlayerAction.None;
            bufferedRejectionNotified = false;
            RejectSkill(action);
            return;
        }

        if (Time.time <= bufferedUntil && GetInputPriority(bufferedAction) > GetInputPriority(action))
        {
            ConsumeBufferedAction();
            RejectSkill(action);
            return;
        }

        bufferedAction = action;
        bufferedRejectionNotified = false;
        // Scaled time preserves the input's remaining lifetime throughout hit stop.
        bufferedUntil = Time.time + (action == PlayerAction.Jump
            ? player.Data.JumpBufferDuration : player.Data.ActionBufferDuration);
        ConsumeBufferedAction();
        float cooldown = action switch
        {
            PlayerAction.GroundSlam => groundSlam != null ? groundSlam.CooldownRemaining : 0f,
            PlayerAction.Counter => counter != null ? counter.CooldownRemaining : 0f,
            _ => 0f
        };
        // Only reject immediately when this cooldown cannot finish within the input buffer.
        if (bufferedAction == action && cooldown > bufferedUntil - Time.time)
        {
            bufferedRejectionNotified = true;
            RejectSkill(action);
        }
    }

    private static int GetInputPriority(PlayerAction action) => action switch
    {
        PlayerAction.Dash => 2,
        PlayerAction.Counter => 1,
        _ => 0
    };

    public void ClearBufferedAction(PlayerAction action)
    {
        if (bufferedAction != action) return;
        bufferedAction = PlayerAction.None;
        bufferedRejectionNotified = false;
    }

    private void RejectSkill(PlayerAction action)
    {
        if (action == PlayerAction.GroundSlam || action == PlayerAction.Counter)
            player.ReportRejectedAction(action);
    }

    public void ConsumeBufferedAction()
    {
        if (bufferedAction == PlayerAction.None) return;
        bool canBuffer = CanBufferAction;
        if (!canBuffer || Time.time > bufferedUntil ||
            ((bufferedAction == PlayerAction.Jump || bufferedAction == PlayerAction.Dash) &&
                (movement == null || !movement.isActiveAndEnabled || movement.IsInGravityField)))
        {
            PlayerAction expiredAction = bufferedAction;
            bool rejected = canBuffer && Time.time > bufferedUntil && !bufferedRejectionNotified;
            bufferedAction = PlayerAction.None;
            bufferedRejectionNotified = false;
            if (rejected) RejectSkill(expiredAction);
            return;
        }
        // Combo input may be queued during hit stop without advancing the animation.
        if (HitStopManager.IsActive && bufferedAction != PlayerAction.Attack) return;

        bool consumed;
        switch (bufferedAction)
        {
            case PlayerAction.Jump:
                consumed = movement.TryJump();
                break;
            case PlayerAction.Dash:
                consumed = player.dash != null && player.dash.TryDash();
                break;
            case PlayerAction.Attack:
                consumed = attack != null && attack.TryAttack();
                break;
            case PlayerAction.GroundSlam:
                consumed = groundSlam != null && groundSlam.TryUse();
                break;
            case PlayerAction.Counter:
                consumed = counter != null && counter.TryUse();
                break;
            default:
                consumed = true;
                break;
        }
        if (consumed)
        {
            bufferedAction = PlayerAction.None;
            bufferedRejectionNotified = false;
        }
    }

    public void CancelCombatActions()
    {
        bufferedAction = PlayerAction.None;
        bufferedRejectionNotified = false;
        attack?.Cancel();
        groundSlam?.Cancel();
        counter?.Cancel();
    }
}

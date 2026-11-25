using UnityEngine;

// Animation Events are received by the GameObject that owns the Animator.
[RequireComponent(typeof(Animator))]
public sealed class PlayerAttackAnimationEvents : MonoBehaviour
{
    private PlayerAttack attack;

    private void Awake() => attack = GetComponentInParent<PlayerAttack>();

    public void ApplyAirAttackDamage() => attack?.ApplyAirAttackDamage();
    // First event on combo attack 2, before damage and effect events at time zero.
    public void FaceGroundComboDirection() => GetComponentInParent<PlayerMovement>()?.FaceMovementInput();
    public void ApplyGroundAttackDamage() => attack?.ApplyGroundAttackDamage();
    public void StartGroundComboInput() => attack?.StartGroundComboInput();
    public void EndGroundComboInput() => attack?.EndGroundComboInput();
    public void OpenDashCancelWindow() => attack?.OpenDashCancelWindow();
}

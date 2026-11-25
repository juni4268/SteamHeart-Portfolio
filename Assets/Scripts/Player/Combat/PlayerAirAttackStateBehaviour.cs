using UnityEngine;

public sealed class PlayerAirAttackStateBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerAttack attack = animator.GetComponentInParent<PlayerAttack>();
        attack?.CloseDashCancelWindow();
        if (attack != null && attack.IsAirAttacking)
            animator.GetComponentInParent<PlayerAudio>()?.PlayAirSwing();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<PlayerAudio>()?.StopAirSwing();
        PlayerAttack attack = animator.GetComponentInParent<PlayerAttack>();
        attack?.CloseDashCancelWindow();
        if (!stateInfo.loop && stateInfo.normalizedTime >= 1f) attack?.FinishAirAttack();
        else attack?.CancelAirAttack();
    }
}

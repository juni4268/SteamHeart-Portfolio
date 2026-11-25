using UnityEngine;

public sealed class PlayerGroundAttackStateBehaviour : StateMachineBehaviour
{
    [SerializeField, Range(1, 2)] private int attackNumber = 1;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerAttack attack = animator.GetComponentInParent<PlayerAttack>();
        attack?.CloseDashCancelWindow();
        if (attack != null && attack.IsExecuting)
            animator.GetComponentInParent<PlayerAudio>()?.PlayGroundSwing(attackNumber);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<PlayerAudio>()?.StopGroundSwing(attackNumber);
        PlayerAttack attack = animator.GetComponentInParent<PlayerAttack>();
        attack?.CloseDashCancelWindow();
        if (!stateInfo.loop && stateInfo.normalizedTime >= 1f) attack?.FinishGroundAttack(attackNumber);
        else attack?.CancelGroundAttack(attackNumber);
    }
}

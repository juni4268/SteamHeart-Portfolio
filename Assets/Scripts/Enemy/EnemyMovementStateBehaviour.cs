using UnityEngine;

public sealed class EnemyMovementStateBehaviour : StateMachineBehaviour
{
    private EnemyFrameMovement movement;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        movement = animator.GetComponentInParent<EnemyFrameMovement>();
        movement?.NotifyMovementEntered(this, stateInfo.loop ? stateInfo.normalizedTime : 0f);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        movement?.NotifyMovementUpdated(this, stateInfo.loop ? stateInfo.normalizedTime : 0f);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        movement?.NotifyMovementExited(this);
    }
}

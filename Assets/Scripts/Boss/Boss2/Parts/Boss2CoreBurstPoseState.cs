using UnityEngine;

public sealed class Boss2CoreBurstPoseState : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo info, int layer) =>
        animator.GetComponent<Boss2CoreBurstPose>()?.NotifyEntered();
    public override void OnStateExit(Animator animator, AnimatorStateInfo info, int layer) =>
        animator.GetComponent<Boss2CoreBurstPose>()?.NotifyExited();
}

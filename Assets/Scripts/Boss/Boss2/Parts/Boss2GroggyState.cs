using UnityEngine;

public sealed class Boss2GroggyState : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo info, int layer) =>
        animator.GetComponent<Boss2GroggyAnimation>()?.NotifyEntered();

    public override void OnStateExit(Animator animator, AnimatorStateInfo info, int layer) =>
        animator.GetComponent<Boss2GroggyAnimation>()?.NotifyExited();
}

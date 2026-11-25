using UnityEngine;

public sealed class Boss2DeathState : Boss2MotionState
{
    protected override void OnMotionEntered(Animator animator, AnimatorStateInfo info) =>
        animator.GetComponent<Boss2DeathAnimation>()?.Entered();
    protected override void OnMotionCompleted(Animator animator) =>
        animator.GetComponent<Boss2DeathAnimation>()?.ReachedEnd();
    protected override void OnMotionExited(Animator animator) =>
        animator.GetComponent<Boss2DeathAnimation>()?.Exited();
}

using UnityEngine;

public sealed class Boss2CoreBurstVisualState : Boss2MotionState
{
    protected override void OnMotionEntered(Animator animator, AnimatorStateInfo stateInfo) =>
        animator.GetComponent<Boss2CoreBurstVisual>()?.Entered();

    protected override void OnMotionCompleted(Animator animator) =>
        animator.GetComponent<Boss2CoreBurstVisual>()?.ReachedEnd();

    protected override void OnMotionExited(Animator animator) =>
        animator.GetComponent<Boss2CoreBurstVisual>()?.Exited();
}

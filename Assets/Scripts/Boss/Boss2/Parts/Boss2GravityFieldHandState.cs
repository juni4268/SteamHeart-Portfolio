using UnityEngine;

public sealed class Boss2GravityFieldHandState : Boss2MotionState
{
    protected override void OnMotionCompleted(Animator animator) =>
        animator.GetComponent<Boss2GravityFieldHandAnimation>()?.ReachedEnd();

    protected override void OnMotionExited(Animator animator) =>
        animator.GetComponent<Boss2GravityFieldHandAnimation>()?.Exited();
}

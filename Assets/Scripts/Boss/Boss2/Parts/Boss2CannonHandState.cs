using UnityEngine;

public enum Boss2CannonHandStage { Prepare, Fire, Return }

/// <summary>Reports actual entry, completion and exit without binding gameplay to state names.</summary>
public sealed class Boss2CannonHandState : Boss2MotionState
{
    [SerializeField] private Boss2CannonHandStage stage;

    protected override void OnMotionEntered(Animator animator, AnimatorStateInfo stateInfo) =>
        animator.GetComponent<Boss2CannonHandAnimation>()?.Entered(stage);

    protected override void OnMotionCompleted(Animator animator) =>
        animator.GetComponent<Boss2CannonHandAnimation>()?.ReachedEnd(stage);

    protected override void OnMotionExited(Animator animator) =>
        animator.GetComponent<Boss2CannonHandAnimation>()?.Exited(stage);
}

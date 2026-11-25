using UnityEngine;

public enum Boss2RingBurstStage { Prepare, Loop, Attack, Charge, ReturnCharge, ReturnPrepare }

public sealed class Boss2RingBurstState : Boss2MotionState
{
    [SerializeField] private Boss2RingBurstStage stage;

    protected override void OnMotionEntered(Animator animator, AnimatorStateInfo stateInfo) =>
        animator.GetComponent<Boss2RingBurstAnimation>()?.Entered(stage);

    protected override void OnMotionCompleted(Animator animator) =>
        animator.GetComponent<Boss2RingBurstAnimation>()?.ReachedEnd(stage);

    protected override void OnMotionExited(Animator animator) =>
        animator.GetComponent<Boss2RingBurstAnimation>()?.Exited(stage);
}

using UnityEngine;

public enum Boss2CannonBeamStage { Aim, Fire, Fade }

/// <summary>Reports actual beam animation state progress without depending on state names.</summary>
public sealed class Boss2CannonBeamState : Boss2MotionState
{
    [SerializeField] private Boss2CannonBeamStage stage;

    protected override void OnMotionEntered(Animator animator, AnimatorStateInfo stateInfo) =>
        animator.GetComponent<Boss2CannonBeamVisual>()?.Entered(stage, stateInfo);

    protected override void OnMotionCompleted(Animator animator) =>
        animator.GetComponent<Boss2CannonBeamVisual>()?.ReachedEnd(stage);

    protected override void OnMotionExited(Animator animator) =>
        animator.GetComponent<Boss2CannonBeamVisual>()?.Exited(stage);
}

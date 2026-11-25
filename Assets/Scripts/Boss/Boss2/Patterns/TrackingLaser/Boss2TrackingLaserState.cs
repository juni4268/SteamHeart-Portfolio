using UnityEngine;

public enum Boss2TrackingLaserStage { Fire, Fade }

/// <summary>Reports actual Animator entry, normal completion and exit by semantic role.</summary>
public sealed class Boss2TrackingLaserState : StateMachineBehaviour
{
    [SerializeField] private Boss2TrackingLaserStage stage;
    private bool reachedEnd;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        reachedEnd = false;
        animator.GetComponent<Boss2TrackingLaserVisual>()?.Entered(stage);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (stage != Boss2TrackingLaserStage.Fade || reachedEnd || stateInfo.loop ||
            stateInfo.normalizedTime < 1f || animator.IsInTransition(layerIndex)) return;
        reachedEnd = true;
        animator.GetComponent<Boss2TrackingLaserVisual>()?.ReachedEnd(stage);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
        animator.GetComponent<Boss2TrackingLaserVisual>()?.Exited(stage);
}

using UnityEngine;

/// <summary>Reports entry, normal completion and interruption independently of Animator state names.</summary>
public sealed class Boss2TrackingLaserHandState : StateMachineBehaviour
{
    [SerializeField] private bool reverse;
    private bool reachedEnd;
    private float enteredNormalizedTime;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        reachedEnd = false;
        enteredNormalizedTime = stateInfo.normalizedTime;
        animator.GetComponent<Boss2TrackingLaserHandAnimation>()?.Entered(reverse);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (reachedEnd || stateInfo.loop || animator.IsInTransition(layerIndex)) return;
        // Animator state time advances during reverse playback as well; the motion itself runs backwards.
        if (reverse ? stateInfo.normalizedTime - enteredNormalizedTime < 1f : stateInfo.normalizedTime < 1f) return;
        reachedEnd = true;
        animator.GetComponent<Boss2TrackingLaserHandAnimation>()?.ReachedEnd(reverse);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponent<Boss2TrackingLaserHandAnimation>()?.Exited(reverse);
    }
}

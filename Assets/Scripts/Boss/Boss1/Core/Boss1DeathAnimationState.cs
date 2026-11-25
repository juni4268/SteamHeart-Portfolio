using UnityEngine;

/// <summary>Applies death visuals on entry and reports only normal animation completion.</summary>
public sealed class Boss1DeathAnimationState : StateMachineBehaviour
{
    private bool completed;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        completed = false;
        animator.GetComponentInParent<Boss1BodyAnimationController>()?.NotifyDeathEntered();
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (completed || stateInfo.loop || stateInfo.normalizedTime < 1f || animator.IsInTransition(layerIndex)) return;

        completed = true;
        animator.GetComponentInParent<Boss1BodyAnimationController>()?.NotifyDeathCompleted();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<Boss1BodyAnimationController>()?.NotifyDeathExited();
    }
}

using UnityEngine;

/// <summary>Updates head tracking and hidden visuals on actual Groggy state entry and exit.</summary>
public sealed class Boss1GroggyAnimationState : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<Boss1BodyAnimationController>()?.NotifyGroggyEntered();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<Boss1BodyAnimationController>()?.NotifyGroggyExited();
    }
}

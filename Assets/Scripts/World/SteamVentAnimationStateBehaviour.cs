using UnityEngine;

public sealed class SteamVentAnimationStateBehaviour : StateMachineBehaviour
{
    [SerializeField] private SteamVentPhase phase;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => animator.GetComponentInParent<SteamVentTrap>()?.NotifyAnimationPhaseEntered(animator, phase);

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (phase == SteamVentPhase.Waiting)
            animator.GetComponentInParent<SteamVentTrap>()?.SynchronizeWaitingAnimation(animator);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (phase == SteamVentPhase.Attacking)
            animator.GetComponentInParent<SteamVentTrap>()?.NotifyAttackAnimationExited(animator);
    }
}

using UnityEngine;

public sealed class EnemyBChargeStateBehaviour : StateMachineBehaviour
{
    [SerializeField] private EnemyBChargePhase phase;

    private EnemyBChargeController charge;

    public EnemyBChargePhase Phase => phase;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        charge = animator.GetComponentInParent<EnemyBChargeController>();
        charge?.NotifyPhaseEntered(phase, stateInfo.normalizedTime);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        charge?.NotifyPhaseUpdated(phase, stateInfo.normalizedTime);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        charge?.NotifyPhaseExited(phase);
    }
}

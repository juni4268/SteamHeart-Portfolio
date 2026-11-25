using UnityEngine;

public sealed class Boss2GravityFieldZoneState : StateMachineBehaviour
{
    public enum Phase { Attached, Falling, Landing, Active }
    [SerializeField] private Phase phase;
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponent<Boss2GravityFieldZone>()?.Entered(phase);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
        animator.GetComponent<Boss2GravityFieldZone>()?.Exited(phase);
}

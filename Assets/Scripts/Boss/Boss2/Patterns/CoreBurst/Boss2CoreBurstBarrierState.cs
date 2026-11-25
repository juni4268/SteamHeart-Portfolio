using UnityEngine;

public sealed class Boss2CoreBurstBarrierState : Boss2MotionState
{
    public enum Role { On, Break, Take, Standby, End }
    [SerializeField] private Role role;

    protected override void OnMotionEntered(Animator animator, AnimatorStateInfo stateInfo) =>
        animator.GetComponent<Boss2CoreBurstBarrier>()?.Entered(role);

    protected override void OnMotionCompleted(Animator animator) =>
        animator.GetComponent<Boss2CoreBurstBarrier>()?.ReachedEnd(role);

    protected override void OnMotionExited(Animator animator) =>
        animator.GetComponent<Boss2CoreBurstBarrier>()?.Exited(role);
}

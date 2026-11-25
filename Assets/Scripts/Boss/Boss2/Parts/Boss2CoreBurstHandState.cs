using UnityEngine;

public sealed class Boss2CoreBurstHandState : StateMachineBehaviour
{
    [SerializeField] private bool reverse;
    private bool reachedEnd;
    private float enteredTime;
    public override void OnStateEnter(Animator animator, AnimatorStateInfo info, int layer)
    {
        reachedEnd = false;
        enteredTime = info.normalizedTime;
    }
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo info, int layer)
    {
        if (reachedEnd || info.loop || animator.IsInTransition(layer) ||
            (reverse ? info.normalizedTime - enteredTime < 1f : info.normalizedTime < 1f)) return;
        reachedEnd = true;
        animator.GetComponent<Boss2CoreBurstHandAnimation>()?.ReachedEnd(reverse);
    }
    public override void OnStateExit(Animator animator, AnimatorStateInfo info, int layer) =>
        animator.GetComponent<Boss2CoreBurstHandAnimation>()?.Exited(reverse);
}

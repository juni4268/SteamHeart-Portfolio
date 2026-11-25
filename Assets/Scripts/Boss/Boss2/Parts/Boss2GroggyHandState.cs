using UnityEngine;

public sealed class Boss2GroggyHandState : StateMachineBehaviour
{
    [SerializeField] private bool reverse;
    [SerializeField] private string completionParameter = "GroggyRecoveryComplete";
    private float enteredTime;
    private bool reachedEnd;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo info, int layer)
    {
        enteredTime = info.normalizedTime;
        reachedEnd = false;
        animator.ResetTrigger(completionParameter);
        if (!reverse) animator.GetComponent<Boss2GroggyAnimation>()?.NotifyEntered();
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo info, int layer)
    {
        if (!reverse || reachedEnd || info.loop || animator.IsInTransition(layer) ||
            info.normalizedTime - enteredTime < 1f) return;
        reachedEnd = true;
        animator.SetTrigger(completionParameter);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo info, int layer)
    {
        var animation = animator.GetComponent<Boss2GroggyAnimation>();
        if (animation == null) return;
        if (reverse && reachedEnd) animation.NotifyExited();
        else if (reverse || !animation.EndRequested) animation.Cancel();
    }
}

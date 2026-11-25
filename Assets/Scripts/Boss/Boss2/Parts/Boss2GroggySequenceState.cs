using UnityEngine;

public enum Boss2GroggySequencePhase { Enter, Loop, Recover }

public sealed class Boss2GroggySequenceState : Boss2MotionState
{
    [SerializeField] private Boss2GroggySequencePhase phase;
    [SerializeField] private string completionParameter = "GroggyMotionComplete";
    [SerializeField] private string activeParameter = "GroggySequenceActive";
    private bool completed;

    protected override void OnMotionEntered(Animator animator, AnimatorStateInfo stateInfo)
    {
        completed = false;
        animator.ResetTrigger(completionParameter);
        if (phase == Boss2GroggySequencePhase.Enter)
        {
            animator.SetBool(activeParameter, true);
            animator.GetComponent<Boss2GroggyAnimation>()?.NotifyEntered();
        }
    }

    protected override void OnMotionCompleted(Animator animator)
    {
        completed = true;
        animator.SetTrigger(completionParameter);
    }

    protected override void OnMotionExited(Animator animator)
    {
        var animation = animator.GetComponent<Boss2GroggyAnimation>();
        if (animation == null) return;
        if (phase == Boss2GroggySequencePhase.Recover && completed)
        {
            animator.SetBool(activeParameter, false);
            animation.NotifyExited();
        }
        else if (phase == Boss2GroggySequencePhase.Loop ? !animation.EndRequested : !completed)
        {
            animator.SetBool(activeParameter, false);
            animation.Cancel();
        }
    }
}

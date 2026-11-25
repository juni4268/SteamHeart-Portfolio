using UnityEngine;

public abstract class Boss2MotionState : StateMachineBehaviour
{
    private bool reachedEnd;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        reachedEnd = false;
        OnMotionEntered(animator, stateInfo);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (reachedEnd || stateInfo.loop || stateInfo.normalizedTime < 1f || animator.IsInTransition(layerIndex)) return;
        reachedEnd = true;
        OnMotionCompleted(animator);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
        OnMotionExited(animator);

    protected virtual void OnMotionEntered(Animator animator, AnimatorStateInfo stateInfo) { }
    protected abstract void OnMotionCompleted(Animator animator);
    protected abstract void OnMotionExited(Animator animator);
}

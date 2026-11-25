using UnityEngine;

public enum Boss2BlackHoleStage { Prepare = 0, Active = 1, Return = 2, Windup = 3, Recovery = 4 }

public sealed class Boss2BlackHoleState : StateMachineBehaviour
{
    [SerializeField] private Boss2BlackHoleStage stage;
    private bool reachedEnd;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        reachedEnd = false;
        animator.GetComponent<Boss2BlackHoleAnimation>()?.Entered(stage);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var animation = animator.GetComponent<Boss2BlackHoleAnimation>();
        animation?.Progress(stage, stateInfo.normalizedTime);
        if (reachedEnd || stateInfo.loop || stateInfo.normalizedTime < 1f || animator.IsInTransition(layerIndex)) return;
        reachedEnd = true;
        animation?.ReachedEnd(stage);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
        animator.GetComponent<Boss2BlackHoleAnimation>()?.Exited(stage);
}

using UnityEngine;

public sealed class PlayerGroundSlamStateBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerGroundSlam skill = animator.GetComponentInParent<PlayerGroundSlam>();
        if (skill != null && skill.IsExecuting) animator.GetComponentInParent<PlayerAudio>()?.PlayGroundSlam();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerGroundSlam skill = animator.GetComponentInParent<PlayerGroundSlam>();
        if (!stateInfo.loop && stateInfo.normalizedTime >= 1f) skill?.FinishGroundSlam();
        else skill?.Cancel();
    }
}

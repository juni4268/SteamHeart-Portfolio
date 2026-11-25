using UnityEngine;

public sealed class PlayerDashStateBehaviour : StateMachineBehaviour
{
    [SerializeField] private bool isLanding;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerDash dash = animator.GetComponentInParent<PlayerDash>();
        if (isLanding)
        {
            dash?.BeginDashLanding();
            return;
        }

        var clips = animator.GetCurrentAnimatorClipInfo(layerIndex);
        float length = clips.Length > 0 ? clips[0].clip.length : stateInfo.length;
        float speed = Mathf.Max(Mathf.Abs(stateInfo.speed * stateInfo.speedMultiplier * animator.speed), 0.01f);
        dash?.BeginDashMovement(length / speed);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerDash dash = animator.GetComponentInParent<PlayerDash>();
        if (!isLanding) dash?.NotifyDashMovementExited();
        // The authored landing State exits after one cycle even though its Clip loops.
        else if (stateInfo.normalizedTime >= 1f) dash?.FinishDash();
        else dash?.CancelDash();
    }
}

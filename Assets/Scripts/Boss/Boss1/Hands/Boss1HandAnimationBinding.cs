using UnityEngine;

public sealed class Boss1HandAnimationBinding : StateMachineBehaviour
{
    [SerializeField] private Boss1HandAnimationRole role;
    [SerializeField] private string triggerName;

    public Boss1HandAnimationRole Role => role;
    public string TriggerName => triggerName;
    public int TriggerHash => Animator.StringToHash(triggerName);
    public bool IsAssigned => role != Boss1HandAnimationRole.None && !string.IsNullOrWhiteSpace(triggerName);

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<Boss1HandAnimationController>()?.NotifyStateEntered(this);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        Boss1HandAnimationController controller = animator.GetComponentInParent<Boss1HandAnimationController>();
        controller?.NotifyStateProgress(this, stateInfo.normalizedTime);
        if (!stateInfo.loop && stateInfo.normalizedTime >= 1f)
            controller?.NotifyStateCompleted(this);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<Boss1HandAnimationController>()?
            .NotifyStateExited(this, !stateInfo.loop && stateInfo.normalizedTime >= 1f);
    }
}

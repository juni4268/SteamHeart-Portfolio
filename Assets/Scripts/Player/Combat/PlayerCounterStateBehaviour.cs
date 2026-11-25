using UnityEngine;

public enum PlayerCounterAnimationRole { Preparation, Counter }

public sealed class PlayerCounterStateBehaviour : StateMachineBehaviour
{
    [SerializeField] private PlayerCounterAnimationRole role;
    public PlayerCounterAnimationRole Role => role;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<PlayerCounter>()?.NotifyAnimationEntered(role);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.GetComponentInParent<PlayerCounter>()?
            .NotifyAnimationExited(role, !stateInfo.loop && stateInfo.normalizedTime >= 1f);
    }
}

using UnityEngine;

public sealed class PlayerDeathStateBehaviour : StateMachineBehaviour
{
    private PlayerHealth playerHealth;
    private bool hasCompletedDeath;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        playerHealth = animator.GetComponentInParent<PlayerHealth>();
        hasCompletedDeath = false;
        animator.GetComponentInParent<PlayerAudio>()?.PlayDeath();
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (hasCompletedDeath || stateInfo.normalizedTime < 1f) return;

        hasCompletedDeath = true;
        playerHealth?.CompleteDeath();
    }
}

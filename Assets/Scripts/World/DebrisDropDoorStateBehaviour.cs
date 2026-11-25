using UnityEngine;

// Attach only to the fully open and fully closed poses, after the motion finishes.
public sealed class DebrisDropDoorStateBehaviour : StateMachineBehaviour
{
    [SerializeField] private bool doorsOpen;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => ReportPose(animator);

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => ReportPose(animator);

    private void ReportPose(Animator animator)
    {
        // Repeating the report also handles re-enabling a trap already in this pose.
        animator.GetComponentInParent<DebrisDropTrap>()?.NotifyDoorPoseReached(animator, doorsOpen);
    }
}

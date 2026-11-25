using UnityEngine;

// Animation Events are received by the GameObject that owns the Animator.
[RequireComponent(typeof(Animator))]
public sealed class PlayerGroundSlamAnimationEvents : MonoBehaviour
{
    private PlayerGroundSlam skill;

    private void Awake() => skill = GetComponentInParent<PlayerGroundSlam>();

    public void ApplyGroundSlamDamage() => skill?.ApplyGroundSlamDamage();
}

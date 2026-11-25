using UnityEngine;

// Animation Events control invincibility; disabling the Animator object cancels the dash.
[RequireComponent(typeof(Animator))]
public sealed class PlayerDashAnimationEvents : MonoBehaviour
{
    private PlayerDash dash;

    private void Awake() => dash = GetComponentInParent<PlayerDash>();

    public void BeginDashInvincibility() => dash?.BeginDashInvincibility();
    public void EndDashInvincibility() => dash?.EndDashInvincibility();

    private void OnDisable() => dash?.CancelDash();
}

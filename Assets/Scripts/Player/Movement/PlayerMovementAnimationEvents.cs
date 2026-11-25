using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class PlayerMovementAnimationEvents : MonoBehaviour
{
    private PlayerAudio playerAudio;

    private void Awake() => playerAudio = GetComponentInParent<PlayerAudio>();

    public void PlayRunFootstep(float playbackOffset) => playerAudio?.PlayRunFootstep(playbackOffset);
    public void PlayWalkFootstep(float playbackOffset) => playerAudio?.PlayWalkFootstep(playbackOffset);
}

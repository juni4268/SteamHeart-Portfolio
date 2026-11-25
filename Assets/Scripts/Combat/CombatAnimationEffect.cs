using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class CombatAnimationEffect : MonoBehaviour
{
    // Animation Event on the final frame of the effect's own clip.
    public void FinishEffect() => Destroy(gameObject);
}

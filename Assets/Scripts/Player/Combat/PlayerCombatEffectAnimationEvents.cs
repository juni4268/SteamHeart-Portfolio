using UnityEngine;

// Add SpawnCombatEffect to an attack clip and assign its effect prefab in the event.
[RequireComponent(typeof(Animator))]
public sealed class PlayerCombatEffectAnimationEvents : MonoBehaviour
{
    public void SpawnCombatEffect(GameObject effectPrefab)
    {
        if (!isActiveAndEnabled || effectPrefab == null) return;

        // Copy the spawn pose, including facing and scale, then leave the player hierarchy.
        Transform effect = Instantiate(effectPrefab, transform, false).transform;
        effect.SetParent(null, true);
    }

    public void SpawnFollowingCombatEffect(GameObject effectPrefab)
    {
        if (!isActiveAndEnabled || effectPrefab == null) return;

        // Keep the effect under the Animator so its independent animation follows the player.
        Instantiate(effectPrefab, transform, false);
    }
}

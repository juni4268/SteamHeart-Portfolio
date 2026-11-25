using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerPlatformDrop
{
    private readonly Collider2D playerCollider;
    private readonly List<Collider2D> ignoredPlatforms = new List<Collider2D>();

    public PlayerPlatformDrop(Collider2D playerCollider) => this.playerCollider = playerCollider;
    public bool IsIgnored(Collider2D platform) => ignoredPlatforms.Contains(platform);

    public bool TryBegin(Collider2D supportingCollider, LayerMask platformLayer)
    {
        if (playerCollider == null || supportingCollider == null || IsIgnored(supportingCollider) ||
            (platformLayer.value & (1 << supportingCollider.gameObject.layer)) == 0) return false;

        // Drop only through the supporting one-way surface, never through solid scrap.
        PlatformEffector2D effector = supportingCollider.GetComponent<PlatformEffector2D>();
        if (!supportingCollider.usedByEffector || effector == null || !effector.enabled || !effector.useOneWay)
            return false;
        ignoredPlatforms.Add(supportingCollider);
        Physics2D.IgnoreCollision(playerCollider, supportingCollider, true);
        return true;
    }

    public void RestoreClearedCollisions(float checkDistance)
    {
        float clearance = Mathf.Max(Physics2D.defaultContactOffset, checkDistance + PlayerGroundSensor.ProbeHeight);
        for (int i = ignoredPlatforms.Count - 1; i >= 0; i--)
        {
            Collider2D platform = ignoredPlatforms[i];
            if (playerCollider == null || platform == null || !platform.enabled || !platform.gameObject.activeInHierarchy)
            {
                RestoreCollision(i);
                continue;
            }

            // Use actual shapes: Tilemap bounds can include platforms below the player.
            ColliderDistance2D separation = playerCollider.Distance(platform);
            // A composite outline can flip its separation normal while the player is still inside.
            if (separation.isValid && !separation.isOverlapped && separation.distance > clearance)
                RestoreCollision(i);
        }
    }

    public void RestoreAllCollisions()
    {
        for (int i = ignoredPlatforms.Count - 1; i >= 0; i--) RestoreCollision(i);
    }

    private void RestoreCollision(int index)
    {
        Collider2D platform = ignoredPlatforms[index];
        if (playerCollider != null && platform != null)
            Physics2D.IgnoreCollision(playerCollider, platform, false);
        ignoredPlatforms.RemoveAt(index);
    }
}

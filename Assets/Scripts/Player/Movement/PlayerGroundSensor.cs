using UnityEngine;

public sealed class PlayerGroundSensor
{
    public const float ProbeHeight = 0.08f;
    private readonly Collider2D playerCollider;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[8];

    public PlayerGroundSensor(Collider2D playerCollider) => this.playerCollider = playerCollider;
    public Collider2D SupportingCollider { get; private set; }
    public Vector2 SupportingNormal { get; private set; } = Vector2.up;

    public void Clear()
    {
        SupportingCollider = null;
        SupportingNormal = Vector2.up;
    }

    public bool Probe(float checkDistance, float widthMultiplier, float minNormalY,
        LayerMask groundLayer, PlayerPlatformDrop platformDrop)
    {
        Clear();
        if (playerCollider == null) return false;

        Bounds bounds = playerCollider.bounds;
        Vector2 size = new Vector2(bounds.size.x * widthMultiplier, ProbeHeight);
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + ProbeHeight);
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayer);
        filter.useTriggers = false;
        int count = Physics2D.BoxCast(origin, size, 0f, Vector2.down, filter, hits,
            ProbeHeight + checkDistance);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || platformDrop.IsIgnored(hit.collider) || hit.normal.y < minNormalY)
                continue;
            SupportingCollider = hit.collider;
            // An overlapping box cast reports the cast direction's inverse, not the slope normal.
            ColliderDistance2D separation = hit.collider.Distance(playerCollider);
            SupportingNormal = separation.isValid && separation.normal.y >= minNormalY
                ? separation.normal
                : hit.normal;
            return true;
        }
        return false;
    }
}

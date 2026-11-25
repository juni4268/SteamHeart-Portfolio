using UnityEngine;

public sealed class EnemySensor
{
    private readonly Transform origin;

    public EnemySensor(Transform origin) => this.origin = origin;

    public bool IsTargetVisible(Transform target, float range, float height, LayerMask wallLayer)
    {
        if (target == null) return false;
        Vector2 distance = target.position - origin.position;
        return Mathf.Abs(distance.x) <= range && Mathf.Abs(distance.y) <= height
            && !IsWallBetween(target, wallLayer);
    }

    public int DirectionTo(Transform target, float deadZone)
    {
        if (target == null) return 0;
        float distance = target.position.x - origin.position.x;
        return Mathf.Abs(distance) <= deadZone ? 0 : (distance > 0f ? 1 : -1);
    }

    public bool IsWallBetween(Transform target, LayerMask wallLayer)
    {
        if (target == null || wallLayer.value == 0) return false;
        Vector2 direction = target.position - origin.position;
        return direction.sqrMagnitude > Mathf.Epsilon
            && Physics2D.Raycast(origin.position, direction.normalized, direction.magnitude, wallLayer).collider != null;
    }

    public bool IsWallAhead(Transform checkPoint, int direction, float distance, LayerMask wallLayer)
    {
        return checkPoint != null && Physics2D.Raycast(checkPoint.position,
            Vector2.right * direction, distance, wallLayer).collider != null;
    }

    public bool IsLedgeAhead(Transform checkPoint, float distance, LayerMask groundLayer)
    {
        return checkPoint != null && Physics2D.Raycast(checkPoint.position,
            Vector2.down, distance, groundLayer).collider == null;
    }
}

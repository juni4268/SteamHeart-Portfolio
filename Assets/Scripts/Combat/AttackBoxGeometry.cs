using UnityEngine;

public readonly struct AttackBoxGeometry
{
    public Vector2 Center { get; }
    public Vector2 Size { get; }
    public float Angle { get; }

    public AttackBoxGeometry(Transform point, Vector2 localOffset, Vector2 localSize)
    {
        Center = point.TransformPoint(localOffset);
        Size = Vector2.Scale(localSize, AbsoluteScale(point));
        Angle = point.eulerAngles.z;
    }

    public static Vector2 ToLocalSize(Transform point, Vector2 worldSize)
    {
        Vector2 scale = AbsoluteScale(point);
        return new Vector2(worldSize.x / Mathf.Max(scale.x, 0.0001f),
            worldSize.y / Mathf.Max(scale.y, 0.0001f));
    }

    private static Vector2 AbsoluteScale(Transform point) =>
        new Vector2(Mathf.Abs(point.lossyScale.x), Mathf.Abs(point.lossyScale.y));
}

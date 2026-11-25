using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss2BasicAttackArea : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(10f, 3.8f);
    [SerializeField, Min(0f)] private float minimumTravelDistance = 1.5f;
    [SerializeField, Min(0f)] private float minimumPlayerDistance = 2f;
    [Tooltip("Radius around each hand's destination; overlapping is allowed if no separated point is found.")]
    [SerializeField, Min(0f)] private float handSeparationRadius = 0.5f;

    public bool TryGetPosition(Vector3 origin, Vector2 playerPosition, out Vector3 destination)
        => TryGetPosition(origin, playerPosition, null, out destination);

    public bool TryGetPosition(Vector3 origin, Vector2 playerPosition,
        IEnumerable<Vector3> otherDestinations, out Vector3 destination)
        => TryGetPosition(origin, playerPosition, otherDestinations, null, out destination);

    public bool TryGetPosition(Vector3 origin, Vector2 playerPosition,
        IEnumerable<Vector3> otherDestinations, bool? leftSide, out Vector3 destination)
    {
        Vector3 fallback = origin;
        bool hasFallback = false;
        for (int i = 0; i < 32; i++)
        {
            Vector3 point = transform.TransformPoint(new Vector3(
                Random.Range(-size.x * 0.5f, size.x * 0.5f),
                Random.Range(-size.y * 0.5f, size.y * 0.5f), 0f));
            point.z = origin.z;
            if (leftSide.HasValue && (leftSide.Value ? point.x > transform.position.x : point.x < transform.position.x)) continue;
            if (Vector2.Distance(point, origin) < minimumTravelDistance ||
                Vector2.Distance(point, playerPosition) < minimumPlayerDistance) continue;
            if (!hasFallback) { fallback = point; hasFallback = true; }
            bool overlaps = false;
            if (otherDestinations != null)
            {
                float minimumDistance = Mathf.Max(0f, handSeparationRadius) * 2f;
                foreach (Vector3 other in otherDestinations)
                    if (Vector2.Distance(point, other) < minimumDistance) { overlaps = true; break; }
            }
            if (overlaps) continue;
            destination = point;
            return true;
        }
        destination = fallback;
        return hasFallback;
    }

    private void OnValidate()
    {
        size.x = Mathf.Max(0f, size.x);
        size.y = Mathf.Max(0f, size.y);
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0f));
        Gizmos.matrix = previous;
    }
}

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class DebrisCluster : MonoBehaviour
{
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    private BoxCollider2D damageArea;
    private SpriteRenderer[] visualRenderers;
    private float fallSpeed;

    public float Top => GetTop(damageArea.bounds);

    private float GetTop(Bounds bounds)
    {
        float top = bounds.max.y;
        foreach (SpriteRenderer renderer in visualRenderers)
        {
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null)
                top = Mathf.Max(top, renderer.bounds.max.y);
        }
        return top;
    }

    private void Awake()
    {
        damageArea = GetComponent<BoxCollider2D>();
        damageArea.isTrigger = true;
        visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public void BeginFalling(float speed)
    {
        fallSpeed = Mathf.Max(0f, speed);
        damageArea.enabled = true;
    }

    // The trap advances all clusters and then resolves their combined target contacts.
    public bool Advance(float deltaTime, float endY, LayerMask targetLayers,
        Dictionary<IDamageable, Vector2> contacts)
    {
        if (!isActiveAndEnabled || !damageArea.enabled) return false;

        Bounds bounds = damageArea.bounds;
        float top = GetTop(bounds);
        if (top <= endY) return false;

        float remainingDistance = top - endY;
        float distance = Mathf.Min(fallSpeed * Mathf.Max(0f, deltaTime), remainingDistance);

        var targetFilter = new ContactFilter2D { useTriggers = true };
        targetFilter.SetLayerMask(targetLayers);
        // Sweep the entire fall step so fast clusters cannot skip damageable targets.
        Physics2D.BoxCast(bounds.center, bounds.size, 0f, Vector2.down,
            targetFilter, hits, distance);
        foreach (RaycastHit2D hit in hits)
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead && !contacts.ContainsKey(target))
                contacts.Add(target, bounds.center);
        }

        transform.position += Vector3.down * distance;
        return distance < remainingDistance;
    }

    private void OnDisable()
    {
        if (damageArea != null) damageArea.enabled = false;
    }
}

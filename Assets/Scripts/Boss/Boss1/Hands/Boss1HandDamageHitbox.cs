using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class Boss1HandDamageHitbox : MonoBehaviour
{
    [SerializeField] private Collider2D attackCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private PlayerHealth target;
    private Collider2D targetCollider;
    private Action<PlayerHealth> onPlayerHit;
    private bool attackActive;
    private Boss1HandController hand;
    private readonly List<RaycastHit2D> movementHits = new List<RaycastHit2D>();
    private readonly List<Vector2> shapePoints = new List<Vector2>();
    private Sprite syncedSprite;
    private Matrix4x4 syncedSpriteToCollider;
    private Vector2 syncedOffset;
    private bool syncedFlipX;
    private bool syncedFlipY;
    private bool hasSyncedShape;

    public Collider2D AttackCollider
    {
        get
        {
            SynchronizeCollider();
            return attackCollider;
        }
    }

    private void Reset()
    {
        ResolveReferences();
        SynchronizeCollider();
    }

    private void Awake()
    {
        ResolveReferences();

        if (attackCollider != null)
        {
            attackCollider.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        if (hand != null) hand.BeforeMove -= CheckMovementTo;
        EndAttack();
    }

    private void OnEnable()
    {
        ResolveReferences();
        hasSyncedShape = false;
        SynchronizeCollider();
        hand = GetComponentInParent<Boss1HandController>();
        if (hand != null) hand.BeforeMove += CheckMovementTo;
    }

    private void LateUpdate()
    {
        SynchronizeCollider();
        CheckCurrentOverlap();
    }

    public void BeginAttack(PlayerHealth playerHealth, Action<PlayerHealth> hitCallback)
    {
        target = playerHealth;
        targetCollider = playerHealth != null ? playerHealth.GetComponent<Collider2D>() : null;
        onPlayerHit = hitCallback;
        attackActive = target != null && onPlayerHit != null;

        CheckCurrentOverlap();
    }

    public void EndAttack()
    {
        attackActive = false;
        target = null;
        targetCollider = null;
        onPlayerHit = null;
    }

    public void CheckCurrentOverlap()
    {
        if (!attackActive || attackCollider == null || targetCollider == null)
        {
            return;
        }

        SynchronizeCollider();
        Physics2D.SyncTransforms();
        if (!attackCollider.enabled || !targetCollider.enabled) return;
        ColliderDistance2D distance = attackCollider.Distance(targetCollider);
        if (distance.isValid && distance.isOverlapped)
        {
            NotifyPlayerHit(targetCollider);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        NotifyPlayerHit(other);
    }

    private void CheckMovementTo(Vector2 nextHandPosition)
    {
        if (!attackActive || attackCollider == null || targetCollider == null) return;
        if (!attackCollider.enabled || !targetCollider.enabled || hand == null) return;
        SynchronizeCollider();
        Physics2D.SyncTransforms();
        Vector2 movement = nextHandPosition - (Vector2)hand.transform.position;
        if (movement.sqrMagnitude <= Mathf.Epsilon) return;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(1 << targetCollider.gameObject.layer);
        filter.useTriggers = true;
        attackCollider.Cast(movement.normalized, filter, movementHits, movement.magnitude);
        foreach (RaycastHit2D hit in movementHits) NotifyPlayerHit(hit.collider);
        CheckCurrentOverlap();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        NotifyPlayerHit(other);
    }

    private void NotifyPlayerHit(Collider2D other)
    {
        if (!attackActive || other == null)
        {
            return;
        }

        PlayerHealth hitPlayer = other.GetComponentInParent<PlayerHealth>();
        if (hitPlayer == target)
        {
            onPlayerHit?.Invoke(hitPlayer);
        }
    }

    private void SynchronizeCollider()
    {
        PolygonCollider2D polygonCollider = attackCollider as PolygonCollider2D;
        if (polygonCollider == null || spriteRenderer == null) return;

        Sprite sprite = spriteRenderer.sprite;
        Matrix4x4 spriteToCollider = polygonCollider.transform.worldToLocalMatrix
            * spriteRenderer.transform.localToWorldMatrix;
        if (hasSyncedShape && syncedSprite == sprite
            && syncedFlipX == spriteRenderer.flipX && syncedFlipY == spriteRenderer.flipY
            && syncedSpriteToCollider == spriteToCollider && syncedOffset == polygonCollider.offset)
            return;

        int pathCount = sprite != null ? sprite.GetPhysicsShapeCount() : 0;
        polygonCollider.pathCount = pathCount;
        for (int path = 0; path < pathCount; path++)
        {
            sprite.GetPhysicsShape(path, shapePoints);
            for (int pointIndex = 0; pointIndex < shapePoints.Count; pointIndex++)
            {
                Vector2 point = shapePoints[pointIndex];
                if (spriteRenderer.flipX) point.x = -point.x;
                if (spriteRenderer.flipY) point.y = -point.y;
                // Physics Shape points use the Sprite pivot; include the child's transform and flips.
                shapePoints[pointIndex] = (Vector2)spriteToCollider.MultiplyPoint3x4(point)
                    - polygonCollider.offset;
            }
            polygonCollider.SetPath(path, shapePoints);
        }

        syncedSprite = sprite;
        syncedSpriteToCollider = spriteToCollider;
        syncedOffset = polygonCollider.offset;
        syncedFlipX = spriteRenderer.flipX;
        syncedFlipY = spriteRenderer.flipY;
        hasSyncedShape = true;
    }

    private void ResolveReferences()
    {
        if (attackCollider == null)
        {
            attackCollider = GetComponent<PolygonCollider2D>();
        }
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }
}

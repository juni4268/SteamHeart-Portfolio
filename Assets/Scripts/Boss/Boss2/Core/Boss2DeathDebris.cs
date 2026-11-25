using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss2DeathDebris : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] bodyParts = Array.Empty<SpriteRenderer>();
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private Vector2 horizontalSpeedRange = new Vector2(1.5f, 3.5f);
    [SerializeField] private Vector2 upwardSpeedRange = new Vector2(2f, 4f);
    [SerializeField] private Vector2 angularSpeedRange = new Vector2(-180f, 180f);
    [SerializeField, Min(0f)] private float gravityScale = 1f;

    public void Launch(SpriteRenderer[] hands)
    {
        if (bodyParts.Length != 4 || hands.Length != 4)
            throw new InvalidOperationException("Assign one head, three body parts and four hand visuals.");
        for (int i = 0; i < bodyParts.Length; i++) LaunchPart(bodyParts[i], i);
        for (int i = 0; i < hands.Length; i++)
        {
            SpriteRenderer source = hands[i];
            var go = new GameObject("Hand Debris " + (i + 1));
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.color = source.color;
            renderer.flipX = source.flipX;
            renderer.flipY = source.flipY;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            LaunchPart(renderer, i + bodyParts.Length);
        }
    }

    private void LaunchPart(SpriteRenderer renderer, int index)
    {
        if (renderer == null || renderer.sprite == null)
            throw new InvalidOperationException("Assign a sprite to every death fragment.");
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector2 vertex in renderer.sprite.vertices)
        {
            min = Vector2.Min(min, vertex);
            max = Vector2.Max(max, vertex);
        }
        var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
        Vector2 offset = (min + max) * 0.5f;
        if (renderer.flipX) offset.x = -offset.x;
        if (renderer.flipY) offset.y = -offset.y;
        collider.offset = offset;
        collider.size = max - min;
        collider.excludeLayers = ~obstacleLayers.value;
        collider.includeLayers = obstacleLayers;
        var body = renderer.gameObject.AddComponent<Rigidbody2D>();
        body.gravityScale = gravityScale;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        float direction = index % 2 == 0 ? -1f : 1f;
        body.linearVelocity = new Vector2(direction * UnityEngine.Random.Range(horizontalSpeedRange.x, horizontalSpeedRange.y),
            UnityEngine.Random.Range(upwardSpeedRange.x, upwardSpeedRange.y));
        body.angularVelocity = UnityEngine.Random.Range(angularSpeedRange.x, angularSpeedRange.y);
    }
}

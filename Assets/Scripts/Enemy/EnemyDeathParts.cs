using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDeathParts : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private SpriteRenderer[] parts = Array.Empty<SpriteRenderer>();
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Launch")]
    [SerializeField] private Vector2 horizontalSpeedRange = new Vector2(1.2f, 2.2f);
    [SerializeField] private Vector2 upwardSpeedRange = new Vector2(2.5f, 4f);
    [SerializeField] private Vector2 angularSpeedRange = new Vector2(180f, 480f);
    [SerializeField, Min(0f)] private float impactDirectionBias = 0.3f;

    [Header("Cleanup")]
    [SerializeField, Min(0.1f)] private float lifetime = 0.95f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.3f;

    private Color[] initialColors;
    private float elapsed;
    private bool launched;

    public void Launch(SpriteRenderer source, int impactDirection)
    {
        Vector3 sourceScale = source.transform.lossyScale;
        Vector3 scale = new Vector3(Mathf.Abs(sourceScale.x), Mathf.Abs(sourceScale.y), 1f);
        transform.localScale = Vector3.Scale(transform.localScale, scale);
        float motionScale = (scale.x + scale.y) * 0.5f;
        bool mirrorX = source.flipX ^ (sourceScale.x < 0f);
        bool mirrorY = source.flipY ^ (sourceScale.y < 0f);
        initialColors = new Color[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            SpriteRenderer part = parts[i];
            float pixelScale = part.sprite.pixelsPerUnit / source.sprite.pixelsPerUnit;
            part.transform.localScale *= pixelScale;
            Vector3 position = part.transform.localPosition;
            if (mirrorX) position.x = -position.x;
            if (mirrorY) position.y = -position.y;
            part.transform.localPosition = position;
            part.flipX ^= mirrorX;
            part.flipY ^= mirrorY;
            // Keep the prefab material; the source may use a flash material it will destroy.
            part.sortingLayerID = source.sortingLayerID;
            part.sortingOrder = source.sortingOrder + 1;
            initialColors[i] = part.color;

            Collider2D collider = part.GetComponent<Collider2D>();
            Vector2 offset = collider.offset;
            if (mirrorX) offset.x = -offset.x;
            if (mirrorY) offset.y = -offset.y;
            collider.offset = offset;
            collider.excludeLayers = ~obstacleLayers.value;
            collider.includeLayers = obstacleLayers;
            Rigidbody2D body = part.GetComponent<Rigidbody2D>();
            body.gravityScale *= motionScale;
            body.rotation += UnityEngine.Random.Range(-25f, 25f);
            float side = i % 2 == 0 ? -1f : 1f;
            float horizontalSpeed = side * UnityEngine.Random.Range(horizontalSpeedRange.x, horizontalSpeedRange.y)
                + Mathf.Sign(impactDirection) * impactDirectionBias;
            body.linearVelocity = new Vector2(horizontalSpeed,
                UnityEngine.Random.Range(upwardSpeedRange.x, upwardSpeedRange.y)) * motionScale;
            body.angularVelocity = side * UnityEngine.Random.Range(angularSpeedRange.x, angularSpeedRange.y);
        }

        launched = true;
    }

    private void Update()
    {
        if (!launched) return;

        elapsed += Time.deltaTime;
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float fadeStart = Mathf.Max(0f, lifetime - fadeDuration);
        float alpha = 1f - Mathf.InverseLerp(fadeStart, lifetime, elapsed);
        for (int i = 0; i < parts.Length; i++)
        {
            Color color = initialColors[i];
            color.a *= alpha;
            parts[i].color = color;
        }
    }
}

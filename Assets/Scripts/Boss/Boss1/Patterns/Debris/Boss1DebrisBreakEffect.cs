using UnityEngine;

public sealed class Boss1DebrisBreakEffect : MonoBehaviour
{
    private Fragment[] fragments;
    private float duration;
    private float elapsed;

    public static void Play(SpriteRenderer source, Transform parent, float duration = 0.55f)
    {
        if (source == null || source.sprite == null) return;
        var root = new GameObject("Boss1 Debris Break");
        root.transform.SetParent(parent, false);
        root.transform.position = source.bounds.center;
        var effect = root.AddComponent<Boss1DebrisBreakEffect>();
        effect.duration = Mathf.Max(0.01f, duration);
        effect.fragments = new Fragment[10];
        for (int i = 0; i < effect.fragments.Length; i++)
        {
            // Deterministic scatter must not consume gameplay's Random sequence.
            float angle = i * 2.399963f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var piece = new GameObject("Debris Fragment");
            piece.layer = source.gameObject.layer;
            piece.transform.SetParent(root.transform, false);
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerName = "BossEffect";
            renderer.sortingOrder = source.sortingOrder + 2;
            renderer.flipX = source.flipX;
            renderer.flipY = source.flipY;
            renderer.color = source.color;
            float size = Mathf.Max(source.bounds.size.x, source.bounds.size.y) * (0.08f + i % 3 * 0.025f);
            Vector3 parentScale = root.transform.lossyScale;
            Vector2 spriteSize = source.sprite.bounds.size;
            piece.transform.localScale = new Vector3(size / (spriteSize.x * Mathf.Abs(parentScale.x)),
                size / (spriteSize.y * Mathf.Abs(parentScale.y)), 1f);
            piece.transform.position += root.transform.position - renderer.bounds.center;
            effect.fragments[i] = new Fragment
            {
                renderer = renderer,
                velocity = direction * (2.4f + i % 4 * 0.5f) + Vector2.up,
                spin = i % 2 == 0 ? 240f : -210f,
                color = source.color
            };
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        foreach (Fragment fragment in fragments)
        {
            fragment.velocity += Vector2.down * (9f * Time.deltaTime);
            fragment.renderer.transform.position += (Vector3)(fragment.velocity * Time.deltaTime);
            fragment.renderer.transform.Rotate(0f, 0f, fragment.spin * Time.deltaTime);
            Color color = fragment.color;
            color.a *= (1f - progress) * (1f - progress);
            fragment.renderer.color = color;
        }
        if (progress >= 1f) Destroy(gameObject);
    }

    private sealed class Fragment
    {
        public SpriteRenderer renderer;
        public Vector2 velocity;
        public float spin;
        public Color color;
    }
}

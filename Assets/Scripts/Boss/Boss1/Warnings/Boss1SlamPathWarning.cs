using UnityEngine;

[RequireComponent(typeof(Boss1WarningVisual))]
public sealed class Boss1SlamPathWarning : MonoBehaviour, System.IDisposable
{
    private Boss1WarningVisual visual;
    private Collider2D hitbox;
    private Transform hand;
    private float targetX;
    private float floorY;
    private bool locked;
    private bool disposed;

    public SpriteRenderer Renderer => visual.Renderer;

    public static Boss1SlamPathWarning Create(Transform owner, SpriteRenderer style)
    {
        var root = new GameObject("Slam Path Warning");
        root.layer = style.gameObject.layer;
        root.transform.SetParent(owner, false);
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = style.sprite;
        renderer.sharedMaterial = style.sharedMaterial;
        renderer.sortingLayerID = style.sortingLayerID;
        renderer.sortingOrder = style.sortingOrder;
        var warning = root.AddComponent<Boss1SlamPathWarning>();
        warning.visual.CopyStyleFrom(style.GetComponent<Boss1WarningVisual>());
        return warning;
    }

    private void Awake()
    {
        visual = GetComponent<Boss1WarningVisual>();
        visual.Style = Boss1WarningVisual.WarningStyle.Falling;
        visual.Hide();
    }

    public void Show(float targetX, Collider2D hitbox, Transform hand, float floorY, float duration)
    {
        this.targetX = targetX;
        this.hitbox = hitbox;
        this.hand = hand;
        this.floorY = floorY;
        locked = false;
        UpdateArea();
        visual.Show(duration);
    }

    public void UpdateTarget(float x)
    {
        if (locked || disposed) return;
        targetX = x;
        UpdateArea();
    }

    public void Lock()
    {
        if (disposed) return;
        UpdateArea();
        locked = true;
        visual.Show(0f);
    }

    private void LateUpdate()
    {
        if (!locked && Renderer.enabled) UpdateArea();
    }

    private void UpdateArea()
    {
        if (hitbox == null || hand == null) return;
        Physics2D.SyncTransforms();
        Bounds bounds = hitbox.bounds;
        float top = Mathf.Max(floorY + 0.01f, bounds.max.y);
        Vector3 center = new Vector3(targetX + bounds.center.x - hand.position.x,
            (floorY + top) * 0.5f, hand.position.z);
        Vector2 size = new Vector2(bounds.size.x, top - floorY);
        Vector3 parentScale = transform.parent.lossyScale;
        Vector2 spriteSize = Renderer.sprite.bounds.size;
        transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(size.x / (spriteSize.x * Mathf.Abs(parentScale.x)),
            size.y / (spriteSize.y * Mathf.Abs(parentScale.y)), 1f);
        transform.position = center;
        transform.position += center - Renderer.bounds.center;
    }

    public void Hide()
    {
        visual.Hide();
        hitbox = null;
        hand = null;
    }

    private void OnDisable() => Hide();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
}

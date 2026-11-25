using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Boss1WarningVisual))]
public abstract class Boss1WarningAreaBase : MonoBehaviour
{
    [SerializeField] private SpriteRenderer warningRenderer;
    [SerializeField] private Boss1WarningVisual warningVisual;
    [SerializeField] private string sortingLayerName = "WarningArea";
    [SerializeField] private int sortingOrder = 50;

    private Sprite runtimeSprite;

    protected virtual void Reset() => ResolveReferences();

    protected virtual void Awake()
    {
        ResolveReferences();
        EnsureSprite();
        Hide();
    }

    protected virtual void OnDestroy()
    {
        if (runtimeSprite != null) Destroy(runtimeSprite);
    }

    public virtual void Hide()
    {
        if (warningVisual != null) warningVisual.Hide();
        else if (warningRenderer != null) warningRenderer.enabled = false;
    }

    protected void ShowArea(Vector3 center, Vector2 size, float duration,
        Boss1WarningVisual.WarningStyle style = Boss1WarningVisual.WarningStyle.Horizontal)
    {
        EnsureSprite();
        transform.position = center;
        transform.rotation = Quaternion.identity;

        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        Vector2 spriteSize = warningRenderer.sprite.bounds.size;
        transform.localScale = new Vector3(
            DivideByScale(Mathf.Max(0.01f, size.x), parentScale.x * spriteSize.x),
            DivideByScale(Mathf.Max(0.01f, size.y), parentScale.y * spriteSize.y),
            1f
        );
        warningVisual.Style = style;
        warningVisual.Show(duration);
    }

    private void ResolveReferences()
    {
        if (warningRenderer == null) warningRenderer = GetComponent<SpriteRenderer>();
        if (warningVisual == null) warningVisual = GetComponent<Boss1WarningVisual>();
        if (warningRenderer != null)
        {
            warningRenderer.sortingLayerName = sortingLayerName;
            warningRenderer.sortingOrder = sortingOrder;
        }
    }

    private void EnsureSprite()
    {
        ResolveReferences();
        if (warningRenderer == null || warningRenderer.sprite != null) return;

        runtimeSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            1f
        );
        runtimeSprite.name = $"{GetType().Name} Runtime Sprite";
        warningRenderer.sprite = runtimeSprite;
    }

    private static float DivideByScale(float value, float scale)
    {
        return Mathf.Approximately(scale, 0f) ? value : value / Mathf.Abs(scale);
    }
}

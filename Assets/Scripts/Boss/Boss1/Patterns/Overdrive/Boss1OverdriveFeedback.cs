using UnityEngine;

// Visuals belong to one Overdrive execution; they never own damage or control locks.
public sealed class Boss1OverdriveFeedback : MonoBehaviour, System.IDisposable
{
    private const float TargetHeight = 0.15f;
    private const float TargetFlashDuration = 0.1f;
    private Sprite whiteSprite;
    private Material glowMaterial;
    private SpriteRenderer targetRenderer;
    private Boss1WarningVisual targetVisual;
    private Boss1SlamPathWarning pathWarning;
    private float targetFlashRemaining;
    private PlayerStunEffect stun;
    private PlayerStunEffect stunPrefab;
    private float breakDuration;
    private int sortingLayer;
    private int sortingOrder;
    private bool disposed;

    public bool IsTargetFlashing => targetFlashRemaining > 0f;

    public static Boss1OverdriveFeedback Create(Transform owner, SpriteRenderer warningStyle,
        Boss1OverdriveConfig config)
    {
        var root = new GameObject("Boss1 Overdrive Feedback");
        root.transform.SetParent(owner, false);
        var feedback = root.AddComponent<Boss1OverdriveFeedback>();
        feedback.Initialize(warningStyle, config);
        return feedback;
    }

    private void Initialize(SpriteRenderer warningStyle, Boss1OverdriveConfig config)
    {
        sortingLayer = warningStyle.sortingLayerID;
        sortingOrder = warningStyle.sortingOrder;
        stunPrefab = config.StunEffectPrefab;
        breakDuration = config.DebrisBreakDuration;
        whiteSprite = Sprite.Create(Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f), Texture2D.whiteTexture.width);
        whiteSprite.name = "Overdrive Feedback Sprite";
        glowMaterial = new Material(Resources.Load<Shader>("Shaders/Boss1OverdriveCharge"));
        glowMaterial.name = "Overdrive Feedback Runtime Material";
        glowMaterial.SetFloat("_Intensity", 1.6f);
        targetRenderer = CreateRenderer("Overdrive Slam Target", transform, whiteSprite, glowMaterial);
        targetVisual = targetRenderer.gameObject.AddComponent<Boss1WarningVisual>();
        targetVisual.Style = Boss1WarningVisual.WarningStyle.Ground;
        targetVisual.Hide();
        pathWarning = Boss1SlamPathWarning.Create(transform, targetRenderer);
    }

    public void FlashTarget(Vector2 target, Collider2D hitbox, Transform hand, float floorY)
    {
        if (disposed || !isActiveAndEnabled) return;
        UpdateTarget(target, hitbox, hand, floorY);
        targetVisual.Show(0f);
        pathWarning.Show(target.x, hitbox, hand, floorY, 0f);
        pathWarning.Lock();
        targetFlashRemaining = TargetFlashDuration;
    }

    private void UpdateTarget(Vector2 target, Collider2D hitbox, Transform hand, float floorY)
    {
        if (disposed || !isActiveAndEnabled) return;
        Bounds bounds = hitbox.bounds;
        targetRenderer.transform.position = new Vector3(
            target.x + bounds.center.x - hand.position.x, floorY + TargetHeight * 0.5f, hand.position.z);
        SetWorldSize(targetRenderer, new Vector2(bounds.size.x, TargetHeight));
    }

    public void HideTarget()
    {
        targetFlashRemaining = 0f;
        targetVisual?.Hide();
        if (pathWarning != null) pathWarning.Hide();
    }

    public void ShowStun(Collider2D collider, PlayerMovement movement, int version)
    {
        if (disposed || !isActiveAndEnabled || stunPrefab == null) return;
        stun?.Dispose();
        stun = Instantiate(stunPrefab, transform);
        stun.Bind(collider, movement, version);
    }

    public void PlayDebrisBreak(SpriteRenderer source)
    {
        if (disposed || !isActiveAndEnabled) return;
        Boss1DebrisBreakEffect.Play(source, transform, breakDuration);
    }

    private void Update()
    {
        if (targetFlashRemaining > 0f)
        {
            targetFlashRemaining -= Time.deltaTime;
            if (targetFlashRemaining <= 0f) HideTarget();
        }

    }

    private SpriteRenderer CreateRenderer(string name, Transform parent, Sprite sprite, Material material)
    {
        var root = new GameObject(name);
        root.layer = gameObject.layer;
        root.transform.SetParent(parent, false);
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = sortingLayer;
        renderer.sortingOrder = sortingOrder + 2;
        return renderer;
    }

    private static void SetWorldSize(SpriteRenderer renderer, Vector2 size)
    {
        Vector3 parentScale = renderer.transform.parent.lossyScale;
        Vector2 spriteSize = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(size.x / (spriteSize.x * Mathf.Abs(parentScale.x)),
            size.y / (spriteSize.y * Mathf.Abs(parentScale.y)), 1f);
    }

    private void OnDisable()
    {
        HideTarget();
        stun?.Dispose();
        stun = null;
        foreach (Boss1DebrisBreakEffect effect in GetComponentsInChildren<Boss1DebrisBreakEffect>(true))
            DestroyFeedbackObject(effect.gameObject);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        gameObject.SetActive(false);
        DestroyFeedbackObject(gameObject);
    }

    private void OnDestroy()
    {
        if (whiteSprite != null) DestroyFeedbackObject(whiteSprite);
        if (glowMaterial != null) DestroyFeedbackObject(glowMaterial);
    }

    private static void DestroyFeedbackObject(Object value)
    {
        if (Application.isPlaying) Object.Destroy(value);
        else Object.DestroyImmediate(value);
    }

}

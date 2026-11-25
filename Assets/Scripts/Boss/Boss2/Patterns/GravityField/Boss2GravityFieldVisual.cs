using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class Boss2GravityFieldVisual : MonoBehaviour
{
    private static readonly int AreaSizeId = Shader.PropertyToID("_AreaSize");
    private static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int ParticleColorId = Shader.PropertyToID("_BaseColor");

    [Tooltip("The visual and particles must be direct children of this collider's Transform.")]
    [SerializeField] private BoxCollider2D area;
    [SerializeField] private ParticleSystem particles;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;

    private SpriteRenderer fieldRenderer;
    private ParticleSystemRenderer particleRenderer;
    private MaterialPropertyBlock fieldProperties;
    private MaterialPropertyBlock particleProperties;
    private Sprite runtimeSprite;
    private Vector2 fittedSize;
    private Vector2 fittedOffset;
    private float duration;

    private void Awake()
    {
        fieldRenderer = GetComponent<SpriteRenderer>();
        if (area == null || particles == null)
        {
            Debug.LogError("Gravity Field visual requires its area and particles.", this);
            enabled = false;
            fieldRenderer.enabled = false;
            return;
        }

        particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        fieldProperties = new MaterialPropertyBlock();
        particleProperties = new MaterialPropertyBlock();
        Texture2D white = Texture2D.whiteTexture;
        runtimeSprite = Sprite.Create(white, new Rect(0f, 0f, white.width, white.height),
            new Vector2(0.5f, 0.5f), white.width);
        runtimeSprite.name = "Gravity Field Runtime Sprite";
        fieldRenderer.sprite = runtimeSprite;
        FitToArea();
        Hide();
    }

    public void Show(float activeDuration)
    {
        if (!isActiveAndEnabled) return;
        duration = Mathf.Max(0f, activeDuration);
        FitToArea();
        fieldRenderer.enabled = true;
        SetRemaining(duration);
        particles.Play(true);
    }

    public void SetRemaining(float remaining)
    {
        if (!isActiveAndEnabled) return;
        if (area.size != fittedSize || area.offset != fittedOffset) FitToArea();

        float elapsed = Mathf.Max(0f, duration - remaining);
        float fadeIn = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
        float fadeOut = fadeOutDuration > 0f ? Mathf.Clamp01(remaining / fadeOutDuration) : 1f;
        float opacity = Mathf.Min(fadeIn, fadeOut);
        Vector3 scale = area.transform.lossyScale;
        fieldProperties.SetVector(AreaSizeId, new Vector4(
            area.size.x * Mathf.Abs(scale.x), area.size.y * Mathf.Abs(scale.y), 0f, 0f));
        fieldProperties.SetFloat(EffectTimeId, elapsed);
        fieldProperties.SetFloat(OpacityId, opacity);
        fieldRenderer.SetPropertyBlock(fieldProperties);

        particleProperties.SetColor(ParticleColorId, new Color(1f, 1f, 1f, opacity));
        particleRenderer.SetPropertyBlock(particleProperties);
    }

    private void FitToArea()
    {
        fittedSize = area.size;
        fittedOffset = area.offset;
        transform.localPosition = area.offset;
        transform.localRotation = Quaternion.identity;
        transform.localScale = new Vector3(area.size.x, area.size.y, 1f);

        // Emit below the top edge and expire particles before they cross the bottom edge.
        var main = particles.main;
        float margin = Mathf.Max(main.startSize.constantMax * 0.5f,
            fieldRenderer.sharedMaterial.GetFloat("_EdgeSoftness"));
        float travelHeight = Mathf.Max(0.01f, area.size.y - margin * 2f);
        float downwardSpeed = Mathf.Abs(particles.velocityOverLifetime.y.constant);
        float lifetime = travelHeight / Mathf.Max(0.01f, downwardSpeed);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime * 0.9f);
        particles.transform.localPosition = new Vector3(area.offset.x,
            area.offset.y + area.size.y * 0.5f - margin, 0f);
        var shape = particles.shape;
        shape.scale = new Vector3(Mathf.Max(0f, area.size.x - margin * 2f), 0f, 0f);
    }

    public void Hide()
    {
        if (fieldRenderer != null) fieldRenderer.enabled = false;
        if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable() => Hide();

    private void OnDestroy()
    {
        if (runtimeSprite != null) Destroy(runtimeSprite);
    }
}

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class Boss1WarningVisual : MonoBehaviour
{
    public enum WarningStyle
    {
        Horizontal,
        Falling,
        Ground
    }

    [Header("Colors")]
    [SerializeField] private Color baseColor = new Color(1f, 0.03f, 0.01f, 0.1f);
    [SerializeField] private Color flashColor = new Color(1f, 0.8f, 0.25f, 0.72f);
    [SerializeField, Range(0f, 0.5f)] private float maximumFillAlpha = 0.1f;
    [SerializeField, Range(0f, 1f)] private float finalFlashStart = 0.82f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float initialPulseFrequency = 1.5f;
    [SerializeField, Min(0f)] private float finalPulseFrequency = 8f;
    [SerializeField, Min(0.005f)] private float frameThickness = 0.06f;

    private readonly SpriteRenderer[] frameRenderers = new SpriteRenderer[4];
    private MaterialPropertyBlock properties;
    private SpriteRenderer warningRenderer;
    private Material warningMaterial;
    private Material originalMaterial;
    private float duration;
    private float elapsed;
    private float pulsePhase;
    private float flowPhase;
    private bool isPlaying;

    public WarningStyle Style { get; set; }

    // Copy authored appearance without sharing timing, renderers or runtime materials.
    public void CopyStyleFrom(Boss1WarningVisual source)
    {
        if (source == null) return;

        baseColor = source.baseColor;
        flashColor = source.flashColor;
        maximumFillAlpha = source.maximumFillAlpha;
        finalFlashStart = source.finalFlashStart;
        initialPulseFrequency = source.initialPulseFrequency;
        finalPulseFrequency = source.finalPulseFrequency;
        frameThickness = source.frameThickness;
    }

    public SpriteRenderer Renderer
    {
        get
        {
            ResolveRenderer();
            return warningRenderer;
        }
    }

    private void Reset()
    {
        ResolveRenderer();
    }

    private void Awake()
    {
        ResolveRenderer();
        Hide();
    }

    private void Update()
    {
        if (!isPlaying) return;

        elapsed += Time.deltaTime;
        float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
        float frequency = Mathf.Lerp(initialPulseFrequency, finalPulseFrequency, progress);
        pulsePhase += Time.deltaTime * frequency * Mathf.PI * 2f;
        float flowSpeed = Style == WarningStyle.Falling
            ? Mathf.Lerp(0.6f, 1.3f, progress)
            : Mathf.Lerp(0.9f, 2.6f, progress);
        flowPhase = Mathf.Repeat(flowPhase + Time.deltaTime * flowSpeed, 2f);
        ApplyFrame(progress, 0.5f + Mathf.Sin(pulsePhase) * 0.5f);
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < frameRenderers.Length; i++)
        {
            if (frameRenderers[i] != null)
            {
                DestroyVisualObject(frameRenderers[i].gameObject);
            }
        }

        if (warningMaterial != null)
        {
            if (warningRenderer != null)
            {
                warningRenderer.sharedMaterial = originalMaterial;
                warningRenderer.SetPropertyBlock(null);
            }
            DestroyVisualObject(warningMaterial);
        }
    }

    public void Show(float warningDuration)
    {
        ResolveRenderer();
        if (warningRenderer == null || warningRenderer.sprite == null) return;

        duration = Mathf.Max(0f, warningDuration);
        elapsed = 0f;
        pulsePhase = 0f;
        flowPhase = 0f;
        isPlaying = true;

        EnsureFrame();
        warningRenderer.enabled = true;
        SetFrameEnabled(true);
        ApplyFrame(0f, 0.5f);
    }

    public void Hide()
    {
        isPlaying = false;
        elapsed = 0f;
        pulsePhase = 0f;
        flowPhase = 0f;

        if (warningRenderer != null)
        {
            warningRenderer.enabled = false;
            warningRenderer.color = baseColor;
        }

        SetFrameEnabled(false);
    }

    private void ApplyFrame(float progress, float pulse)
    {
        float flash = Mathf.InverseLerp(finalFlashStart, 1f, progress);
        float alphaPulse = Mathf.Lerp(0.48f, 1f, pulse);
        Color fillColor = Color.Lerp(baseColor, flashColor, flash);
        float baseAlpha = Mathf.Min(baseColor.a, maximumFillAlpha);
        fillColor.a = Mathf.Lerp(baseAlpha, flashColor.a, flash) * alphaPulse;
        warningRenderer.color = fillColor;

        // The warning boundary stays fixed; only the interior pattern flows.
        PositionFrame(1f);

        Color frameColor = fillColor;
        frameColor.a = Mathf.Lerp(0.6f, 0.9f, flash) * alphaPulse;
        for (int i = 0; i < frameRenderers.Length; i++)
        {
            if (frameRenderers[i] != null)
            {
                frameRenderers[i].color = frameColor;
            }
        }

        Bounds spriteBounds = warningRenderer.sprite.bounds;
        Vector3 worldSize = warningRenderer.bounds.size;
        properties.SetVector("_SpriteRect", new Vector4(spriteBounds.min.x,
            spriteBounds.min.y, spriteBounds.size.x, spriteBounds.size.y));
        properties.SetVector("_AreaSize", new Vector4(worldSize.x, worldSize.y, 0f, 0f));
        properties.SetFloat("_PatternStyle", (float)Style);
        properties.SetFloat("_FlowPhase", flowPhase);
        properties.SetColor("_PatternColor", Color.Lerp(new Color(1f, 0.3f, 0.04f), flashColor, flash));
        properties.SetColor("_HotColor", Color.Lerp(new Color(1f, 0.72f, 0.16f), flashColor, flash));
        properties.SetFloat("_PatternOpacity", Mathf.Lerp(0.36f, 0.8f, flash) * alphaPulse);
        warningMaterial.SetFloat("_Intensity", 1f + flash * pulse * 0.55f);
        warningRenderer.SetPropertyBlock(properties);
    }

    private void EnsureFrame()
    {
        if (properties == null) properties = new MaterialPropertyBlock();
        if (warningMaterial == null)
        {
            originalMaterial = warningRenderer.sharedMaterial;
            warningMaterial = new Material(Resources.Load<Shader>("Shaders/Boss1Warning"));
            warningMaterial.name = "Boss1 Warning Runtime Material";
            warningMaterial.hideFlags = HideFlags.DontSave;
        }
        warningRenderer.sharedMaterial = warningMaterial;

        for (int i = 0; i < frameRenderers.Length; i++)
        {
            if (frameRenderers[i] == null)
            {
                GameObject frameObject = new GameObject($"Warning Frame {i + 1}");
                frameObject.hideFlags = HideFlags.DontSave;
                frameObject.transform.SetParent(transform, false);
                frameRenderers[i] = frameObject.AddComponent<SpriteRenderer>();
            }

            SpriteRenderer frameRenderer = frameRenderers[i];
            frameRenderer.sprite = warningRenderer.sprite;
            frameRenderer.sharedMaterial = warningMaterial;
            frameRenderer.sortingLayerID = warningRenderer.sortingLayerID;
            frameRenderer.sortingOrder = warningRenderer.sortingOrder + 1;
        }
    }

    private void PositionFrame(float frameScale)
    {
        if (warningRenderer == null || warningRenderer.sprite == null) return;

        Vector2 spriteSize = warningRenderer.sprite.bounds.size;
        float scaleX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float scaleY = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        float verticalThickness = Mathf.Min(frameThickness / scaleX, spriteSize.x * 0.08f);
        float horizontalThickness = Mathf.Min(frameThickness / scaleY, spriteSize.y * 0.24f);
        float halfWidth = spriteSize.x * 0.5f * frameScale;
        float halfHeight = spriteSize.y * 0.5f * frameScale;

        SetFrameTransform(frameRenderers[0], new Vector2(-halfWidth, 0f),
            new Vector2(verticalThickness / spriteSize.x, frameScale));
        SetFrameTransform(frameRenderers[1], new Vector2(halfWidth, 0f),
            new Vector2(verticalThickness / spriteSize.x, frameScale));
        SetFrameTransform(frameRenderers[2], new Vector2(0f, halfHeight),
            new Vector2(frameScale, horizontalThickness / spriteSize.y));
        SetFrameTransform(frameRenderers[3], new Vector2(0f, -halfHeight),
            new Vector2(frameScale, horizontalThickness / spriteSize.y));
    }

    private static void SetFrameTransform(SpriteRenderer frameRenderer, Vector2 position, Vector2 scale)
    {
        if (frameRenderer == null) return;

        Transform frameTransform = frameRenderer.transform;
        frameTransform.localPosition = new Vector3(position.x, position.y, -0.001f);
        frameTransform.localRotation = Quaternion.identity;
        frameTransform.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    private void SetFrameEnabled(bool value)
    {
        for (int i = 0; i < frameRenderers.Length; i++)
        {
            if (frameRenderers[i] != null)
            {
                frameRenderers[i].enabled = value;
            }
        }
    }

    private void ResolveRenderer()
    {
        if (warningRenderer == null)
        {
            warningRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private static void DestroyVisualObject(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}

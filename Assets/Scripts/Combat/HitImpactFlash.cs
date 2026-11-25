using UnityEngine;

[DisallowMultipleComponent]
public sealed class HitImpactFlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] targetRenderers;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float flashStrength = 0.65f;
    [SerializeField, Min(0f)] private float holdDuration = 0.1f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

    private static readonly int ColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int AmountId = Shader.PropertyToID("_FlashAmount");
    private Material[] originals;
    private Material[] flashMaterials;
    private MaterialPropertyBlock[] savedBlocks;
    private MaterialPropertyBlock block;
    private bool playing;
    private float startTime;
    private float activeStrength;
    private float activeHoldDuration;
    private float activeFadeDuration;

    public bool IsPlaying => playing;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (targetRenderers == null) targetRenderers = System.Array.Empty<SpriteRenderer>();
        int count = targetRenderers.Length;
        originals = new Material[count];
        flashMaterials = new Material[count];
        savedBlocks = new MaterialPropertyBlock[count];
    }

    public void Play() => Play(flashStrength, holdDuration, fadeDuration);

    public void Play(float strength, float hold, float fade)
    {
        if (!isActiveAndEnabled) return;
        activeStrength = Mathf.Clamp01(strength);
        activeHoldDuration = Mathf.Max(0f, hold);
        activeFadeDuration = Mathf.Max(0f, fade);
        if (playing)
        {
            startTime = Time.unscaledTime;
            SetAmount(activeStrength);
            return;
        }
        Shader shader = Resources.Load<Shader>("Shaders/SpriteLitHitImpactFlash");
        if (shader == null) return;
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer renderer = targetRenderers[i];
            if (renderer == null) continue;
            originals[i] = renderer.sharedMaterial;
            savedBlocks[i] ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(savedBlocks[i]);
            if (flashMaterials[i] == null) flashMaterials[i] = new Material(shader);
            if (originals[i] != null) flashMaterials[i].CopyPropertiesFromMaterial(originals[i]);
            renderer.sharedMaterial = flashMaterials[i];
        }
        startTime = Time.unscaledTime;
        playing = true;
        SetAmount(activeStrength);
    }

    private void Update()
    {
        if (!playing) return;
        float elapsed = Time.unscaledTime - startTime;
        if (elapsed >= activeHoldDuration + activeFadeDuration)
        {
            Restore();
            return;
        }
        float remaining = activeFadeDuration > 0f
            ? 1f - Mathf.Clamp01((elapsed - activeHoldDuration) / activeFadeDuration) : 1f;
        SetAmount(activeStrength * remaining * remaining);
    }

    private void SetAmount(float amount)
    {
        foreach (SpriteRenderer renderer in targetRenderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(block);
            block.SetColor(ColorId, flashColor);
            block.SetFloat(AmountId, amount);
            renderer.SetPropertyBlock(block);
        }
    }

    private void Restore()
    {
        if (!playing) return;
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer renderer = targetRenderers[i];
            if (renderer == null) continue;
            renderer.sharedMaterial = originals[i];
            renderer.SetPropertyBlock(savedBlocks[i]);
        }
        playing = false;
    }

    private void OnDisable() => Restore();

    private void OnDestroy()
    {
        Restore();
        if (flashMaterials == null) return;
        foreach (Material material in flashMaterials)
            if (material != null) Destroy(material);
    }
}

using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Unscaled screen impact and a world-space shockwave, independent of the charge Animator.</summary>
[DisallowMultipleComponent]
public sealed class Boss2CoreBurstImpact : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField] private Volume impactVolume;
    [SerializeField] private MeshRenderer shockwave;
    [SerializeField, Min(0.01f)] private float monochromeHold = 0.38f;
    [SerializeField, Min(0.01f)] private float colorReturnDuration = 0.55f;

    [Header("Shockwave")]
    [Tooltip("Minimum world-space radius. Expands farther when needed to cover the camera view.")]
    [SerializeField, Min(1f)] private float minimumRadius = 16f;
    [SerializeField, Min(0f)] private float expansionDelay = 0.08f;
    [SerializeField, Min(0.01f)] private float expansionDuration = 0.5f;
    [SerializeField, Min(0f)] private float blastHold = 0.3f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.8f;
    [SerializeField, Min(0.01f)] private float ringWidth = 0.36f;
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float flashRadius = 1.6f;

    [Header("Screen Flash")]
    [Tooltip("Earliest flash time. The full-view flash always waits for the blast to finish expanding.")]
    [SerializeField, Min(0f)] private float screenFlashDelay = 0.58f;
    [SerializeField, Min(0.01f)] private float screenFlashDuration = 0.18f;
    [SerializeField, Range(0f, 1f)] private float screenFlashStrength = 0.94f;

    [Header("Fragments")]
    [SerializeField, Min(0.01f)] private float fragmentDuration = 0.48f;
    [SerializeField, Range(0f, 1f)] private float fragmentStrength = 0.85f;

    [Header("Impact")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.07f;
    [SerializeField, Min(0f)] private float shakeDuration = 1.05f;
    [SerializeField, Min(0f)] private float shakeStrength = 0.24f;

    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int WidthId = Shader.PropertyToID("_RingWidth");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");
    private static readonly int FlashRadiusId = Shader.PropertyToID("_FlashRadius");
    private static readonly int WhiteoutId = Shader.PropertyToID("_Whiteout");
    private static readonly int FillId = Shader.PropertyToID("_FillOpacity");
    private static readonly int ExpansionProgressId = Shader.PropertyToID("_ExpansionProgress");
    private static readonly int FragmentProgressId = Shader.PropertyToID("_FragmentProgress");
    private static readonly int FragmentOpacityId = Shader.PropertyToID("_FragmentOpacity");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private MaterialPropertyBlock properties;
    private float startTime;
    private float radius;
    private bool playing;
    private Boss2Audio audioOwner;
    private Boss2Audio.Playback audioPlayback;

    public void BindAudio(Boss2Audio audio) => audioOwner = audio;

    private float ScreenFlashStart => Mathf.Max(screenFlashDelay, expansionDelay + expansionDuration);

    public void Play(Camera viewCamera)
    {
        if (impactVolume == null || impactVolume.sharedProfile == null || shockwave == null ||
            shockwave.sharedMaterial == null || viewCamera == null)
            throw new InvalidOperationException("Assign the Core Burst impact Volume, shockwave material and camera.");

        audioPlayback?.FadeOut();
        audioPlayback = audioOwner != null ? audioOwner.Play(Boss2Audio.Sound.CoreAttack) : null;
        radius = minimumRadius;
        float depth = Vector3.Dot(transform.position - viewCamera.transform.position, viewCamera.transform.forward);
        for (int y = 0; y <= 1; y++)
            for (int x = 0; x <= 1; x++)
                radius = Mathf.Max(radius, Vector2.Distance(transform.position,
                    viewCamera.ViewportToWorldPoint(new Vector3(x, y, depth))) * 1.2f + ringWidth);
        shockwave.transform.localScale = Vector3.one * (radius * 2f);
        properties ??= new MaterialPropertyBlock();
        startTime = Time.unscaledTime;
        playing = true;
        shockwave.enabled = true;
        UpdatePresentation(0f);
        HitCameraShake.Play(this, shakeDuration, shakeStrength, new Vector2(1f, 0.6f), 28f, 0.04f);
        HitStopManager.Request(this, hitStopDuration);
    }

    private void Update()
    {
        if (!playing) return;
        float elapsed = Time.unscaledTime - startTime;
        float blastDuration = expansionDelay + expansionDuration + blastHold + fadeOutDuration;
        float screenDuration = Mathf.Max(monochromeHold + colorReturnDuration, flashDuration, shakeDuration,
            ScreenFlashStart + screenFlashDuration, expansionDelay + fragmentDuration);
        if (elapsed >= Mathf.Max(blastDuration, screenDuration))
        {
            Cancel();
            return;
        }
        UpdatePresentation(elapsed);
    }

    private void UpdatePresentation(float elapsed)
    {
        float progress = Mathf.Clamp01((elapsed - expansionDelay) / expansionDuration);
        // Constant travel keeps the advancing silhouette visible before the full-view white peak.
        float expansion = progress;
        float fadeStart = expansionDelay + expansionDuration + blastHold;
        float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - fadeStart) / fadeOutDuration));
        float fragmentProgress = Mathf.Clamp01((elapsed - expansionDelay) / fragmentDuration);
        float fragmentOpacity = elapsed < expansionDelay ? 0f :
            fragmentStrength * (1f - Mathf.SmoothStep(0.1f, 1f, fragmentProgress));
        float flashProgress = (elapsed - ScreenFlashStart) / screenFlashDuration;
        float screenFlash = flashProgress < 0f || flashProgress > 1f ? 0f :
            Mathf.Clamp01(flashProgress / 0.15f) *
            (1f - Mathf.SmoothStep(0.15f, 1f, flashProgress)) * screenFlashStrength;
        impactVolume.weight = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((elapsed - monochromeHold) / colorReturnDuration));
        properties.SetFloat(RadiusId, Mathf.Lerp(0.35f / radius, 1f, expansion));
        properties.SetFloat(WidthId, ringWidth / radius * Mathf.Lerp(1.8f, 0.7f, progress));
        properties.SetFloat(OpacityId, opacity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.08f)));
        properties.SetFloat(FlashId, Mathf.Pow(1f - Mathf.Clamp01(elapsed / flashDuration), 2f));
        properties.SetFloat(FlashRadiusId, flashRadius / radius * Mathf.Lerp(0.5f, 1.5f,
            Mathf.Clamp01(elapsed / flashDuration)));
        properties.SetFloat(WhiteoutId, screenFlash);
        properties.SetFloat(FillId, elapsed < expansionDelay ? 0f : opacity);
        properties.SetFloat(ExpansionProgressId, progress);
        properties.SetFloat(FragmentProgressId, fragmentProgress);
        properties.SetFloat(FragmentOpacityId, fragmentOpacity);
        properties.SetFloat(PhaseId, elapsed);
        shockwave.SetPropertyBlock(properties);
    }

    public void Cancel()
    {
        StopPresentation();
        Destroy(gameObject);
    }

    private void OnDisable() => StopPresentation();

    private void StopPresentation()
    {
        audioPlayback?.FadeOut();
        playing = false;
        if (impactVolume != null) impactVolume.weight = 0f;
        if (shockwave != null) shockwave.enabled = false;
        HitCameraShake.Cancel(this);
        HitStopManager.Cancel(this);
    }
}

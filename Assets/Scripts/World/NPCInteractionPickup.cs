using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NPCInteractionPickup : MonoBehaviour
{
    private const float StartScale = 0.85f;
    private const float OvershootScale = 1.08f;
    private const float ScalePeakTime = 0.6f;

    private Transform target;
    private Vector3 riseStart;
    private Vector3 riseEnd;
    private Vector3 originalScale;
    private float riseDuration;
    private float pauseDuration;
    private float flightDuration;
    private float arcHeight;
    private float shrinkDuration;
    private float elapsed;
    private ParticleSystem acquisitionEffectPrefab;
    private SpriteInteractionOutline[] outlines;
    private Material outlineMaterial;

    public event Action Collected;

    public void Initialize(Transform player, float height, float duration,
        float pause, float flightDuration, float arcHeight, float shrinkDuration,
        ParticleSystem acquisitionEffectPrefab, float hueShift)
    {
        target = player;
        riseStart = transform.position;
        riseEnd = riseStart + Vector3.up * Mathf.Max(0f, height);
        originalScale = transform.localScale;
        riseDuration = Mathf.Max(0f, duration);
        pauseDuration = Mathf.Max(0f, pause);
        this.flightDuration = Mathf.Max(0f, flightDuration);
        this.arcHeight = Mathf.Max(0f, arcHeight);
        this.shrinkDuration = Mathf.Clamp(shrinkDuration, 0f, this.flightDuration);
        elapsed = 0f;
        this.acquisitionEffectPrefab = acquisitionEffectPrefab;

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        outlines = new SpriteInteractionOutline[sprites.Length];
        if (sprites.Length > 0)
        {
            outlineMaterial = new Material(Resources.Load<Shader>("Shaders/SpriteInteractionOutline"));
            outlineMaterial.SetFloat("_HueShift", hueShift);
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            sprites[i].sharedMaterial = outlineMaterial;
            if (!sprites[i].TryGetComponent(out SpriteInteractionOutline outline))
            {
                outline = sprites[i].gameObject.AddComponent<SpriteInteractionOutline>();
            }

            outlines[i] = outline;
            outline.SetFocused(true);
        }

        if (riseDuration > 0f)
        {
            transform.localScale = originalScale * StartScale;
        }
        else
        {
            transform.position = riseEnd;
        }
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed < riseDuration)
        {
            float progress = elapsed / riseDuration;
            float easedProgress = 1f - (1f - progress) * (1f - progress);
            transform.position = Vector3.Lerp(riseStart, riseEnd, easedProgress);
            float scale = progress < ScalePeakTime
                ? Mathf.SmoothStep(StartScale, OvershootScale, progress / ScalePeakTime)
                : Mathf.SmoothStep(OvershootScale, 1f, (progress - ScalePeakTime) / (1f - ScalePeakTime));
            transform.localScale = originalScale * scale;
            return;
        }

        transform.localScale = originalScale;
        float flightElapsed = elapsed - riseDuration - pauseDuration;
        if (flightElapsed < 0f)
        {
            transform.position = riseEnd;
            return;
        }

        float flightProgress = flightDuration > 0f ? Mathf.Clamp01(flightElapsed / flightDuration) : 1f;
        float travelProgress = flightProgress * flightProgress;
        Vector3 destination = target.position;
        destination.z = transform.position.z;
        float arcOffset = 4f * arcHeight * travelProgress * (1f - travelProgress);
        transform.position = Vector3.Lerp(riseEnd, destination, travelProgress) + Vector3.up * arcOffset;

        if (shrinkDuration > 0f)
        {
            float shrinkProgress = Mathf.InverseLerp(flightDuration - shrinkDuration, flightDuration, flightElapsed);
            transform.localScale = originalScale * Mathf.SmoothStep(1f, 0f, shrinkProgress);
        }

        if (flightProgress >= 1f)
        {
            if (acquisitionEffectPrefab != null)
            {
                Instantiate(acquisitionEffectPrefab, destination, Quaternion.identity, target);
            }

            enabled = false;
            Collected?.Invoke();
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        if (outlines == null) return;

        foreach (SpriteInteractionOutline outline in outlines)
        {
            if (outline != null) outline.SetFocused(true);
        }
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }
}

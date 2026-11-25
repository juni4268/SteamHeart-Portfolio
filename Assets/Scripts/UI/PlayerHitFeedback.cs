using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PlayerHitFeedback : MonoBehaviour
{
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    [SerializeField] private PlayerHealth playerHealth;

    [Header("Screen Flash")]
    [SerializeField] private CanvasGroup damageOverlay;
    [SerializeField, Range(0f, 1f)] private float overlayPeakAlpha = 0.8f;
    [SerializeField, Min(0f)] private float overlayFadeInDuration = 0.08f;
    [SerializeField, Min(0f)] private float overlayFadeOutDuration = 0.45f;

    [Header("Character Flash")]
    [SerializeField] private Color characterFlashColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float characterFlashAmount = 0.65f;
    [SerializeField, Min(0f)] private float characterFlashHoldDuration = 0.1f;
    [SerializeField, Min(0f)] private float characterFlashFadeDuration = 0.25f;

    [Header("Hit Impact")]
    [SerializeField] private CombatAnimationEffect hitEffectPrefab;
    [SerializeField, Min(0f)] private float hitEffectScale = 0.55f;

    [Header("Hit Stop")]
    [SerializeField, Min(0f)] private float hitStopDuration = 0.05f;

    [Header("Camera Shake")]
    [SerializeField, Min(0f)] private float cameraShakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float cameraShakeStrength = 0.06f;

    private SpriteRenderer[] flashRenderers;
    private MaterialPropertyBlock propertyBlock;
    private Sequence overlaySequence;
    private Sequence characterFlashSequence;
    private float currentFlashAmount;
    private Collider2D playerCollider;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (damageOverlay == null)
        {
            damageOverlay = GetComponent<CanvasGroup>();
        }

        if (damageOverlay != null)
        {
            damageOverlay.alpha = 0f;
            damageOverlay.interactable = false;
            damageOverlay.blocksRaycasts = false;
        }

        if (playerHealth == null)
        {
            Debug.LogError($"{nameof(PlayerHitFeedback)} could not find a {nameof(PlayerHealth)}.", this);
            enabled = false;
            return;
        }

        propertyBlock = new MaterialPropertyBlock();
        playerCollider = playerHealth.GetComponent<Collider2D>();
        CacheFlashRenderers();
        SetFlashAmount(0f);
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged += Play;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.Damaged -= Play;
        }

        StopFeedback();
    }

    private void Play(int damage)
    {
        if (damage <= 0) return;

        PlayScreenFlash();
        PlayCharacterFlash();
        PlayHitImpact();
        HitStopManager.Request(this, hitStopDuration);
        HitCameraShake.Play(this, cameraShakeDuration, cameraShakeStrength, new Vector2(1f, 0.5f));
    }

    private void PlayHitImpact()
    {
        if (hitEffectPrefab == null || hitEffectScale <= 0f) return;
        Vector3 position = playerCollider != null ? playerCollider.bounds.center : playerHealth.transform.position;
        CombatAnimationEffect effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        effect.transform.localScale *= hitEffectScale;
    }

    private void PlayScreenFlash()
    {
        if (damageOverlay == null) return;

        overlaySequence?.Kill();
        damageOverlay.alpha = 0f;

        overlaySequence = DOTween.Sequence()
            .Append(damageOverlay
                .DOFade(overlayPeakAlpha, overlayFadeInDuration)
                .SetEase(Ease.OutQuad))
            .Append(damageOverlay
                .DOFade(0f, overlayFadeOutDuration)
                .SetEase(Ease.OutQuad))
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnKill(() => overlaySequence = null);
    }

    private void PlayCharacterFlash()
    {
        if (flashRenderers == null || flashRenderers.Length == 0) return;

        characterFlashSequence?.Kill();

        SetFlashAmount(characterFlashAmount);

        characterFlashSequence = DOTween.Sequence()
            .AppendInterval(characterFlashHoldDuration)
            .Append(DOTween.To(
                () => currentFlashAmount,
                SetFlashAmount,
                0f,
                characterFlashFadeDuration)
                .SetEase(Ease.OutQuad))
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnKill(() => characterFlashSequence = null);
    }

    private void CacheFlashRenderers()
    {
        SpriteRenderer[] playerRenderers =
            playerHealth.GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> compatibleRenderers = new();

        foreach (SpriteRenderer playerRenderer in playerRenderers)
        {
            Material material = playerRenderer.sharedMaterial;
            if (material != null &&
                material.HasProperty(FlashColorId) &&
                material.HasProperty(FlashAmountId))
            {
                compatibleRenderers.Add(playerRenderer);
            }
        }

        flashRenderers = compatibleRenderers.ToArray();
        if (flashRenderers.Length == 0)
        {
            Debug.LogWarning(
                $"{nameof(PlayerHitFeedback)} could not find a compatible player renderer.",
                this);
        }
    }

    private void SetFlashAmount(float amount)
    {
        currentFlashAmount = amount;
        if (flashRenderers == null || propertyBlock == null) return;

        foreach (SpriteRenderer flashRenderer in flashRenderers)
        {
            if (flashRenderer == null) continue;

            flashRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(FlashColorId, characterFlashColor);
            propertyBlock.SetFloat(FlashAmountId, currentFlashAmount);
            flashRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void StopFeedback()
    {
        HitStopManager.Cancel(this);
        HitCameraShake.Cancel(this);

        overlaySequence?.Kill();
        overlaySequence = null;

        characterFlashSequence?.Kill();
        characterFlashSequence = null;

        if (damageOverlay != null)
        {
            damageOverlay.alpha = 0f;
        }

        SetFlashAmount(0f);
    }
}

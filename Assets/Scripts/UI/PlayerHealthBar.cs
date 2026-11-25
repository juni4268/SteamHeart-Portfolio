using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBar : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image segmentTemplate;
    [SerializeField] private Color filledColor = new Color(0.85f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color emptyColor = new Color(0.2f, 0.03f, 0.03f, 1f);
    [SerializeField, Min(0f)] private float transitionDuration = 0.18f;
    [SerializeField, Min(0f)] private float damagePunchScale = 0.2f;
    [SerializeField] private Vector2 segmentSize = new Vector2(12f, 12f);
    [SerializeField, Min(0f)] private float spacing = 2f;
    [SerializeField, Min(0f)] private float padding = 2f;

    [Header("Low Health")]
    [SerializeField, Range(0.01f, 1f)] private float lowHealthRatio = 0.3f;
    [SerializeField] private Color lowHealthColor = new Color(1f, 0.25f, 0.18f);
    [SerializeField, Min(0.1f)] private float lowHealthPulseDuration = 1.2f;

    private readonly List<Image> segments = new List<Image>();
    private int displayedHP;
    private bool hasDisplayedHealth;
    private bool hasStarted;
    private bool isSubscribed;
    private bool isLowHealth;
    private float lowHealthElapsed;

    private void Awake()
    {
        if (segmentTemplate == null)
        {
            Debug.LogError($"{nameof(PlayerHealthBar)} requires a segment template.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (hasStarted)
        {
            BindPlayerHealth();
        }
    }

    private void Start()
    {
        hasStarted = true;
        BindPlayerHealth();
    }

    private void OnDisable()
    {
        Unsubscribe();

        KillSegmentTweens();
        RestoreFilledColors();
        isLowHealth = false;
        lowHealthElapsed = 0f;
        hasDisplayedHealth = false;
    }

    private void LateUpdate()
    {
        if (!hasDisplayedHealth || playerHealth == null) return;

        int threshold = Mathf.Max(1, Mathf.FloorToInt(playerHealth.MaxHP * lowHealthRatio));
        bool shouldPulse = displayedHP > 0 && displayedHP <= threshold && !playerHealth.IsDead;
        if (!shouldPulse)
        {
            if (isLowHealth) RestoreFilledColors();
            isLowHealth = false;
            lowHealthElapsed = 0f;
            return;
        }

        isLowHealth = true;
        lowHealthElapsed += Time.deltaTime;
        float pulse = 0.5f - 0.5f * Mathf.Cos(lowHealthElapsed * Mathf.PI * 2f / lowHealthPulseDuration);
        Color color = Color.Lerp(filledColor, lowHealthColor, Mathf.Lerp(0.35f, 1f, pulse));
        for (int i = 0; i < displayedHP && i < segments.Count; i++)
        {
            // Let damage and recovery transitions finish before tinting a segment.
            if (!DOTween.IsTweening(segments[i])) segments[i].color = color;
        }
    }

    private void RestoreFilledColors()
    {
        for (int i = 0; i < displayedHP && i < segments.Count; i++)
        {
            Image segment = segments[i];
            if (segment != null && !DOTween.IsTweening(segment)) segment.color = filledColor;
        }
    }

    private void OnValidate()
    {
        segmentSize.x = Mathf.Max(0f, segmentSize.x);
        segmentSize.y = Mathf.Max(0f, segmentSize.y);
    }

    private void BindPlayerHealth()
    {
        if (segmentTemplate == null) return;

        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            Debug.LogError($"{nameof(PlayerHealthBar)} could not find a {nameof(PlayerHealth)}.", this);
            enabled = false;
            return;
        }

        if (!isSubscribed)
        {
            playerHealth.HealthChanged += UpdateHealthBar;
            isSubscribed = true;
        }

        SetHealthBarImmediate(playerHealth.CurrentHP, playerHealth.MaxHP);
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || playerHealth == null) return;

        playerHealth.HealthChanged -= UpdateHealthBar;
        isSubscribed = false;
    }

    private void UpdateHealthBar(int currentHP, int maxHP)
    {
        EnsureSegmentCount(maxHP);

        int filledSegments = Mathf.Clamp(currentHP, 0, maxHP);
        if (!hasDisplayedHealth)
        {
            SetHealthBarImmediate(filledSegments, maxHP);
            return;
        }

        int previousFilledSegments = Mathf.Clamp(displayedHP, 0, maxHP);

        for (int i = 0; i < maxHP; i++)
        {
            Image segment = segments[i];
            segment.DOKill(true);
            segment.rectTransform.DOKill(true);
            segment.color = i < previousFilledSegments ? filledColor : emptyColor;
        }

        if (filledSegments < previousFilledSegments)
        {
            for (int i = filledSegments; i < previousFilledSegments; i++)
            {
                AnimateDamagedSegment(segments[i]);
            }
        }
        else if (filledSegments > previousFilledSegments)
        {
            for (int i = previousFilledSegments; i < filledSegments; i++)
            {
                AnimateRecoveredSegment(segments[i]);
            }
        }

        displayedHP = filledSegments;
    }

    private void EnsureSegmentCount(int maxHP)
    {
        if (segments.Count == 0)
        {
            segments.Add(segmentTemplate);
        }

        while (segments.Count < maxHP)
        {
            Image segment = Instantiate(segmentTemplate, segmentTemplate.transform.parent);
            segment.name = $"Segment {segments.Count + 1}";
            segments.Add(segment);
        }

        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].gameObject.SetActive(i < maxHP);
        }

        ApplySegmentSizes(maxHP);

        if (TryGetComponent(out HorizontalLayoutGroup layoutGroup))
        {
            ConfigureLayoutGroup(layoutGroup);
        }
        else
        {
            LayoutSegments(maxHP);
        }
    }

    private void ApplySegmentSizes(int segmentCount)
    {
        for (int i = 0; i < segmentCount; i++)
        {
            RectTransform segmentRect = segments[i].rectTransform;
            segmentRect.sizeDelta = segmentSize;
        }
    }

    private void ConfigureLayoutGroup(HorizontalLayoutGroup layoutGroup)
    {
        int roundedPadding = Mathf.RoundToInt(padding);
        layoutGroup.padding = new RectOffset(
            roundedPadding,
            roundedPadding,
            roundedPadding,
            roundedPadding);
        layoutGroup.spacing = spacing;
        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
    }

    private void LayoutSegments(int segmentCount)
    {
        if (segmentCount <= 0) return;

        RectTransform container = transform as RectTransform;
        if (container == null) return;

        for (int i = 0; i < segmentCount; i++)
        {
            RectTransform segmentRect = segments[i].rectTransform;
            segmentRect.anchorMin = new Vector2(0f, 0.5f);
            segmentRect.anchorMax = new Vector2(0f, 0.5f);
            segmentRect.pivot = new Vector2(0f, 0.5f);
            segmentRect.sizeDelta = segmentSize;
            segmentRect.anchoredPosition =
                new Vector2(padding + i * (segmentSize.x + spacing), 0f);
        }
    }

    private void SetHealthBarImmediate(int currentHP, int maxHP)
    {
        EnsureSegmentCount(maxHP);

        int filledSegments = Mathf.Clamp(currentHP, 0, maxHP);
        for (int i = 0; i < maxHP; i++)
        {
            Image segment = segments[i];
            segment.DOKill(true);
            segment.rectTransform.DOKill(true);
            segment.color = i < filledSegments ? filledColor : emptyColor;
        }

        displayedHP = filledSegments;
        hasDisplayedHealth = true;
    }

    private void AnimateDamagedSegment(Image segment)
    {
        if (transitionDuration <= 0f)
        {
            segment.color = emptyColor;
            return;
        }

        segment
            .DOColor(emptyColor, transitionDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);

        if (damagePunchScale > 0f)
        {
            segment.rectTransform
                .DOPunchScale(Vector3.one * damagePunchScale, transitionDuration, 5, 0.5f)
                .SetLink(gameObject);
        }
    }

    private void AnimateRecoveredSegment(Image segment)
    {
        if (transitionDuration <= 0f)
        {
            segment.color = filledColor;
            return;
        }

        segment
            .DOColor(filledColor, transitionDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
    }

    private void KillSegmentTweens()
    {
        foreach (Image segment in segments)
        {
            if (segment == null) continue;

            segment.DOKill(true);
            segment.rectTransform.DOKill(true);
        }
    }
}

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BossHealthBar : MonoBehaviour
{
    private const float NameFrameEndInset = 4f;

    [SerializeField] private BossHealth bossHealth;
    [SerializeField] private GameObject content;
    [SerializeField] private Text nameLabel;
    [SerializeField] private RectTransform nameFrame;
    [SerializeField] private Text epithetLabel;
    [SerializeField] private Image fill;
    [SerializeField] private Image damageFill;

    [Header("Entrance")]
    [Tooltip("전투 시작 시 보스명과 프레임이 나타나는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float entranceFadeDuration = 0.25f;
    [Tooltip("페이드인 후 체력이 왼쪽부터 차오르는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float entranceFillDuration = 0.65f;

    [Header("Damage Feedback")]
    [SerializeField, Min(0f)] private float damageDelay = 0.04f;
    [SerializeField, Min(0f)] private float damageDrainDuration = 0.24f;

    [Header("Boss 2 Phase Feedback")]
    [SerializeField] private BossHealthBarSweep phaseSweep;
    [SerializeField, Min(0f)] private float phaseSweepDuration = 0.3f;

    private bool hasStarted;
    private bool isSubscribed;
    private bool hasAppeared;
    private float displayedRatio;
    private Color fillColor;
    private Boss1Controller controller;
    private Boss2Controller boss2Controller;
    private CanvasGroup contentGroup;
    private Tween entranceTween;
    private Tween damageTween;
    private Tween phaseTween;

    private void Awake()
    {
        if (content == null || nameLabel == null || nameFrame == null || epithetLabel == null
            || fill == null || damageFill == null)
        {
            Debug.LogError("BossHealthBar needs its content, name frame, name and epithet labels, health fill, and damage fill references.", this);
            enabled = false;
            return;
        }

        fillColor = fill.color;
        if (phaseSweep != null) phaseSweep.enabled = false;
        contentGroup = content.GetComponent<CanvasGroup>();
        if (contentGroup == null) contentGroup = content.AddComponent<CanvasGroup>();
        content.SetActive(false);
    }

    private void Start()
    {
        hasStarted = true;
        BindBoss();
    }

    private void OnEnable()
    {
        GameSettings.Changed += ApplyVisibilitySettings;
        if (hasStarted) BindBoss();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= ApplyVisibilitySettings;
        if (isSubscribed && bossHealth != null)
            bossHealth.HealthChanged -= UpdateHealthBar;
        if (isSubscribed && controller != null)
            controller.PatternStarted -= HandlePatternStarted;
        if (isSubscribed && boss2Controller != null)
        {
            boss2Controller.StateChanged -= HandleBoss2StateChanged;
            boss2Controller.PhaseChanged -= HandleBoss2PhaseChanged;
        }
        isSubscribed = false;
        StopFeedback();
        if (content != null) content.SetActive(false);
    }

    private void BindBoss()
    {
        if (bossHealth == null) bossHealth = FindFirstObjectByType<BossHealth>();
        if (bossHealth == null)
        {
            content.SetActive(false);
            return;
        }

        if (!isSubscribed)
        {
            bossHealth.HealthChanged += UpdateHealthBar;
            controller = bossHealth.GetComponent<Boss1Controller>();
            if (controller != null) controller.PatternStarted += HandlePatternStarted;
            boss2Controller = bossHealth.GetComponent<Boss2Controller>();
            if (boss2Controller != null)
            {
                boss2Controller.StateChanged += HandleBoss2StateChanged;
                boss2Controller.PhaseChanged += HandleBoss2PhaseChanged;
            }
            isSubscribed = true;
        }

        nameLabel.text = bossHealth.DisplayName;
        UpdateNameFrameWidth();
        epithetLabel.text = bossHealth.Epithet;
        epithetLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(bossHealth.Epithet));
        SetHealthImmediate(bossHealth.CurrentHp, bossHealth.MaxHp);
        bool combatStarted = boss2Controller != null
            ? boss2Controller.CurrentState == Boss2CombatState.Fighting
            : controller == null || controller.IsExecutingPattern;
        if (!hasAppeared && combatStarted) PlayEntrance();
    }

    private void UpdateNameFrameWidth()
    {
        TextGenerationSettings settings = nameLabel.GetGenerationSettings(Vector2.zero);
        TextGenerator generator = nameLabel.cachedTextGeneratorForLayout;
        float spaceWidth = (generator.GetPreferredWidth("M M", settings)
            - generator.GetPreferredWidth("MM", settings)) / nameLabel.pixelsPerUnit;
        float sidePadding = NameFrameEndInset + spaceWidth;
        float frameWidth = Mathf.Ceil(nameLabel.preferredWidth + sidePadding * 2f);
        nameFrame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, frameWidth);

        RectTransform labelRect = nameLabel.rectTransform;
        labelRect.anchorMin = nameFrame.anchorMin;
        labelRect.anchorMax = nameFrame.anchorMax;
        labelRect.anchoredPosition = nameFrame.anchoredPosition;
        labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, frameWidth);
    }

    private void HandlePatternStarted(Boss1PatternBase pattern)
    {
        if (!hasAppeared) PlayEntrance();
    }

    private void HandleBoss2StateChanged(Boss2CombatState previous, Boss2CombatState current)
    {
        if (!hasAppeared && current == Boss2CombatState.Fighting) PlayEntrance();
    }

    private void HandleBoss2PhaseChanged(Boss2Phase phase)
    {
        StopPhaseFeedback();
        if (phase != Boss2Phase.Two || phaseSweep == null || !hasAppeared
            || !content.activeInHierarchy || phaseSweepDuration <= 0f) return;

        phaseSweep.Progress = 0f;
        phaseSweep.enabled = true;
        phaseTween = DOTween.To(() => phaseSweep.Progress, value => phaseSweep.Progress = value,
                1f, phaseSweepDuration)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                phaseTween = null;
                if (phaseSweep != null) phaseSweep.enabled = false;
            });
    }

    private void ApplyVisibilitySettings()
    {
        if (bossHealth == null) return;
        bool shouldShow = GameSettings.ShowBossHealthBar && hasAppeared && bossHealth.CurrentHp > 0 && !bossHealth.IsDead;
        if (content.activeSelf != shouldShow)
            SetHealthImmediate(bossHealth.CurrentHp, bossHealth.MaxHp);
    }

    private void PlayEntrance()
    {
        if (bossHealth == null || bossHealth.IsDead) return;

        StopFeedback();
        hasAppeared = true;
        if (!GameSettings.ShowBossHealthBar)
        {
            content.SetActive(false);
            return;
        }
        contentGroup.alpha = 0f;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        damageFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        content.SetActive(true);

        Vector2 target = new Vector2(displayedRatio, 1f);
        Sequence sequence = DOTween.Sequence();
        entranceTween = sequence;
        sequence.SetLink(gameObject);
        sequence.Append(contentGroup.DOFade(1f, entranceFadeDuration).SetEase(Ease.OutQuad));
        sequence.Append(fill.rectTransform.DOAnchorMax(target, entranceFillDuration).SetEase(Ease.OutCubic));
        sequence.Join(damageFill.rectTransform.DOAnchorMax(target, entranceFillDuration).SetEase(Ease.OutCubic));
        sequence.OnComplete(() => entranceTween = null);
    }

    private void UpdateHealthBar(int currentHp, int maxHp)
    {
        if (entranceTween != null)
        {
            StopEntrance();
            Vector2 previous = new Vector2(displayedRatio, 1f);
            fill.rectTransform.anchorMax = previous;
            damageFill.rectTransform.anchorMax = previous;
        }

        float ratio = Mathf.Clamp01((float)currentHp / Mathf.Max(1, maxHp));
        bool tookDamage = ratio < displayedRatio;
        bool isVisible = GameSettings.ShowBossHealthBar && currentHp > 0 && !bossHealth.IsDead;
        if (!hasAppeared || !tookDamage || !isVisible)
        {
            SetHealthImmediate(currentHp, maxHp);
            return;
        }

        damageTween?.Kill();
        fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        displayedRatio = ratio;

        damageTween = damageFill.rectTransform
            .DOAnchorMax(new Vector2(ratio, 1f), damageDrainDuration)
            .SetDelay(damageDelay)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);

    }

    private void SetHealthImmediate(int currentHp, int maxHp)
    {
        StopFeedback();
        displayedRatio = Mathf.Clamp01((float)currentHp / Mathf.Max(1, maxHp));
        Vector2 anchorMax = new Vector2(displayedRatio, 1f);
        fill.rectTransform.anchorMax = anchorMax;
        damageFill.rectTransform.anchorMax = anchorMax;
        content.SetActive(GameSettings.ShowBossHealthBar && hasAppeared && currentHp > 0 && !bossHealth.IsDead);
    }

    private void StopEntrance()
    {
        entranceTween?.Kill();
        entranceTween = null;
        if (contentGroup != null) contentGroup.alpha = 1f;
    }

    private void StopFeedback()
    {
        StopEntrance();
        damageTween?.Kill();
        damageTween = null;
        StopPhaseFeedback();
        if (fill != null) fill.color = fillColor;
    }

    private void StopPhaseFeedback()
    {
        phaseTween?.Kill();
        phaseTween = null;
        if (phaseSweep != null) phaseSweep.enabled = false;
    }
}

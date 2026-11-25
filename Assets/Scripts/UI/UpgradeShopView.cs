using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum UpgradeKind { Attack, Health, HitInvincibility }

[DisallowMultipleComponent]
public sealed class UpgradeShopView : MonoBehaviour
{
    [Header("Upgrade Effects")]
    [SerializeField] private Text attackHeading;
    [SerializeField] private Text healthHeading;
    [SerializeField] private Text attackValueLabel;
    [SerializeField] private Text healthBonusLabel;
    [SerializeField] private Text hitInvincibilityHeading;
    [SerializeField] private Text hitInvincibilityValueLabel;

    [Header("Purchase Feedback")]
    [SerializeField] private Color purchaseHighlight = new Color(1f, 0.78f, 0.4f);
    [SerializeField, Min(0f)] private float highlightDuration = 0.3f;

    [Header("Purchase Buttons")]
    [SerializeField] private Text attackPurchaseLabel;
    [SerializeField] private Text attackStatusLabel;
    [SerializeField] private Text healthPurchaseLabel;
    [SerializeField] private Text healthStatusLabel;
    [SerializeField] private Text hitInvincibilityPurchaseLabel;
    [SerializeField] private Text hitInvincibilityStatusLabel;
    [SerializeField] private Text materialPurchaseLabel;
    [SerializeField] private Text materialStatusLabel;

    [Header("Selected Upgrade")]
    [SerializeField] private Text selectedHeading;
    [SerializeField] private Text selectedComparison;
    [SerializeField] private Text selectedLimit;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Text confirmLabel;
    [SerializeField] private Graphic[] selectionMarks;
    [SerializeField] private Text[] axisLabels;
    private UpgradeKind selectedUpgrade;

    public UpgradeKind SelectedUpgrade => selectedUpgrade;
    public bool SelectedUpgradeAtLimit => selectedUpgrade == UpgradeKind.Attack ? PlayerPermanentProgress.IsAttackMaxed
        : selectedUpgrade == UpgradeKind.Health ? PlayerPermanentProgress.IsHealthMaxed : PlayerPermanentProgress.IsHitInvincibilityMaxed;

    [Header("Shared Materials")]
    [UnityEngine.Serialization.FormerlySerializedAs("healthPieceCounter")]
    [UnityEngine.Serialization.FormerlySerializedAs("materialCounter")]
    [SerializeField] private Text materialPriceLabel;
    [UnityEngine.Serialization.FormerlySerializedAs("healthPieceSlots")]
    [SerializeField] private Image[] materialSlots;
    [SerializeField] private Graphic[] materialFrames;
    [SerializeField] private Button materialPurchaseButton;
    [SerializeField] private UpgradeTriangleFrame triangle;
    [SerializeField, Min(0f)] private float bounceHeight = 1.5f;
    [SerializeField, Min(0.1f)] private float bounceDuration = 0.55f;
    [SerializeField] private Color collectedPieceColor = new Color(1f, 0.6f, 0.28f);
    [SerializeField] private Color emptyPieceColor = new Color(0.3f, 0.2f, 0.14f);
    [SerializeField] private Color shortageTextColor = new Color(0.92f, 0.62f, 0.48f);

    private Tween attackFeedback;
    private Tween healthFeedback;
    private Tween hitInvincibilityFeedback;
    private Tween materialFeedback;
    private RectTransform materialFeedbackTarget;
    private Tween materialBounce;
    private int bouncingSlot = -1;
    private Vector2 bounceRestPosition;
    private Color comparisonRestColor;
    private bool colorsCached;
    private bool triangleInitialized;

    public bool HasRequiredReferences => attackHeading != null && healthHeading != null
        && attackValueLabel != null && healthBonusLabel != null && hitInvincibilityHeading != null
        && hitInvincibilityValueLabel != null && attackPurchaseLabel != null && attackStatusLabel != null
        && healthPurchaseLabel != null && healthStatusLabel != null && hitInvincibilityPurchaseLabel != null
        && hitInvincibilityStatusLabel != null && materialPurchaseLabel != null && materialStatusLabel != null
        && materialPriceLabel != null && materialSlots != null && materialSlots.Length > 0
        && System.Array.TrueForAll(materialSlots, slot => slot != null)
        && materialFrames != null && materialFrames.Length == materialSlots.Length
        && System.Array.TrueForAll(materialFrames, frame => frame != null)
        && materialPurchaseButton != null && triangle != null && selectedHeading != null
        && selectedComparison != null && selectedLimit != null && confirmButton != null && confirmLabel != null
        && selectionMarks != null && selectionMarks.Length == 3
        && System.Array.TrueForAll(selectionMarks, mark => mark != null)
        && axisLabels != null && axisLabels.Length == 3
        && System.Array.TrueForAll(axisLabels, label => label != null);

    public void SelectUpgrade(UpgradeKind kind)
    {
        selectedUpgrade = kind;
        Refresh();
    }

    private void OnEnable()
    {
        if (HasRequiredReferences) Refresh();
    }

    private void Update()
    {
        int next = materialPurchaseButton != null && materialPurchaseButton.isActiveAndEnabled
            && materialPurchaseButton.IsInteractable() ? PlayerPermanentProgress.UpgradeMaterials : -1;
        SetBouncingSlot(next);
        if (materialPriceLabel != null && materialPurchaseLabel != null
            && PlayerPermanentProgress.Scrap >= UpgradeSettings.Shared.MaterialScrapCost)
            materialPriceLabel.color = materialPurchaseLabel.color;
    }

    public void Refresh()
    {
        if (!HasRequiredReferences) return;
        UpgradeSettings settings = UpgradeSettings.Shared;
        int required = settings.MaterialsPerUpgrade;
        int collected = PlayerPermanentProgress.UpgradeMaterials;
        attackHeading.text = $"1회 +{settings.AttackPowerIncrease} · 최대 +{settings.MaxAttackPowerBonus}";
        healthHeading.text = $"1회 +{settings.MaxHealthIncrease} · 최대 +{settings.MaxHealthBonus}";
        hitInvincibilityHeading.text = $"1회 +{settings.HitInvincibilityIncrease:0.##}초 · 최대 +{settings.MaxHitInvincibilityBonus:0.##}초";
        attackValueLabel.gameObject.SetActive(false);
        healthBonusLabel.gameObject.SetActive(false);
        hitInvincibilityValueLabel.gameObject.SetActive(false);
        materialPriceLabel.text = settings.MaterialScrapCost.ToString();
        materialPriceLabel.color = PlayerPermanentProgress.Scrap < settings.MaterialScrapCost
            ? shortageTextColor : new Color(0.98f, 0.9f, 0.7f);
        for (int i = 0; i < materialSlots.Length; i++)
        {
            materialSlots[i].color = i < collected
                ? collectedPieceColor : emptyPieceColor;
            materialFrames[i].color = i < collected ? new Color(1f, 0.65f, 0.2f, 0.5f)
                : new Color(0.7f, 0.45f, 0.2f, 0.08f);
        }
        materialPurchaseLabel.text = "재료 구매";
        int next = Mathf.Clamp(collected, 0, materialSlots.Length - 1);
        materialPurchaseButton.gameObject.SetActive(true);
        materialPurchaseButton.interactable = PlayerPermanentProgress.CanPurchaseMaterial;
        if (collected < required && materialPurchaseButton.interactable)
            materialFrames[next].color = new Color(1f, 0.8f, 0.4f, 0.35f);
        materialStatusLabel.color = shortageTextColor;
        materialStatusLabel.text = collected < required && PlayerPermanentProgress.Scrap < settings.MaterialScrapCost
            ? $"{settings.MaterialScrapCost - PlayerPermanentProgress.Scrap} 고철 부족" : string.Empty;
        attackPurchaseLabel.text = "공격력";
        healthPurchaseLabel.text = "체력";
        hitInvincibilityPurchaseLabel.text = "무적";
        RefreshSelectedUpgrade(settings);
        triangle.SetProgress(new Vector3(
            (float)PlayerPermanentProgress.AttackPowerBonus / settings.MaxAttackPowerBonus,
            (float)PlayerPermanentProgress.BonusMaxHP / settings.MaxHealthBonus,
            PlayerPermanentProgress.HitInvincibilityBonus / settings.MaxHitInvincibilityBonus), triangleInitialized);
        triangleInitialized = true;
    }

    private void RefreshSelectedUpgrade(UpgradeSettings settings)
    {
        attackFeedback?.Kill();
        healthFeedback?.Kill();
        hitInvincibilityFeedback?.Kill();
        if (colorsCached) selectedComparison.color = comparisonRestColor;
        string title;
        string current;
        string next;
        string maximum;
        if (selectedUpgrade == UpgradeKind.Attack)
        {
            title = "공격력";
            int value = PlayerPermanentProgress.AttackPowerBonus;
            current = $"+{value}";
            next = $"+{value + Mathf.Min(settings.AttackPowerIncrease, Mathf.Max(0, settings.MaxAttackPowerBonus - value))}";
            maximum = $"+{settings.MaxAttackPowerBonus}";
        }
        else if (selectedUpgrade == UpgradeKind.Health)
        {
            title = "체력";
            int value = PlayerPermanentProgress.BonusMaxHP;
            current = $"+{value}";
            next = $"+{value + Mathf.Min(settings.MaxHealthIncrease, Mathf.Max(0, settings.MaxHealthBonus - value))}";
            maximum = $"+{settings.MaxHealthBonus}";
        }
        else
        {
            title = "피격 무적시간";
            float value = PlayerPermanentProgress.HitInvincibilityBonus;
            current = $"+{value:0.##}초";
            next = $"+{Mathf.Min(value + settings.HitInvincibilityIncrease, settings.MaxHitInvincibilityBonus):0.##}초";
            maximum = $"+{settings.MaxHitInvincibilityBonus:0.##}초";
        }
        selectedHeading.text = title;
        selectedComparison.text = SelectedUpgradeAtLimit ? current : $"{current} → <color=#FFC36B>{next}</color>";
        selectedLimit.text = $"최대 {maximum}";
        confirmLabel.text = SelectedUpgradeAtLimit ? "강화 완료" : $"{(selectedUpgrade == UpgradeKind.HitInvincibility ? "무적시간" : title)} 강화";
        confirmButton.interactable = PlayerPermanentProgress.HasUpgradeMaterials && !SelectedUpgradeAtLimit;
        for (int i = 0; i < selectionMarks.Length; i++)
        {
            selectionMarks[i].color = i == (int)selectedUpgrade ? new Color(1f, 0.72f, 0.32f, 0.16f) : Color.clear;
            axisLabels[i].color = i == (int)selectedUpgrade
                ? new Color(1f, 0.85f, 0.48f) : new Color(0.88f, 0.8f, 0.65f);
        }
        triangle.SetSelectedAxis((int)selectedUpgrade);
    }

    private void CacheColors()
    {
        if (colorsCached) return;
        comparisonRestColor = selectedComparison.color;
        colorsCached = true;
    }

    private void Highlight(Text label, Color restColor, ref Tween tween)
    {
        tween?.Kill();
        label.color = purchaseHighlight;
        tween = label.DOColor(restColor, highlightDuration).SetUpdate(true);
    }

    public void PlayAttackPurchaseFeedback()
    {
        CacheColors();
        Highlight(selectedComparison, comparisonRestColor, ref attackFeedback);
    }

    public void PlayHealthPurchaseFeedback()
    {
        CacheColors();
        Highlight(selectedComparison, comparisonRestColor, ref healthFeedback);
    }

    public void PlayHitInvincibilityPurchaseFeedback()
    {
        CacheColors();
        Highlight(selectedComparison, comparisonRestColor, ref hitInvincibilityFeedback);
    }

    public void PlayMaterialPurchaseFeedback()
    {
        materialFeedback?.Kill();
        if (materialFeedbackTarget != null) materialFeedbackTarget.localScale = Vector3.one;
        materialFeedbackTarget = materialSlots[Mathf.Clamp(PlayerPermanentProgress.UpgradeMaterials - 1, 0, materialSlots.Length - 1)].rectTransform;
        materialFeedback = materialFeedbackTarget.DOPunchScale(Vector3.one * 0.2f, 0.25f, 1, 0.3f).SetUpdate(true);
    }

    private void SetBouncingSlot(int index)
    {
        if (materialSlots == null) return;
        if (index >= materialSlots.Length) index = -1;
        if (index == bouncingSlot) return;
        materialBounce?.Kill();
        materialBounce = null;
        if (bouncingSlot >= 0) materialSlots[bouncingSlot].rectTransform.anchoredPosition = bounceRestPosition;
        bouncingSlot = index;
        if (index < 0) return;
        RectTransform icon = materialSlots[index].rectTransform;
        bounceRestPosition = icon.anchoredPosition;
        materialBounce = icon.DOAnchorPosY(bounceRestPosition.y + bounceHeight, bounceDuration)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private void OnDisable()
    {
        triangleInitialized = false;
        attackFeedback?.Kill();
        healthFeedback?.Kill();
        hitInvincibilityFeedback?.Kill();
        materialFeedback?.Kill();
        materialFeedback = null;
        if (materialFeedbackTarget != null) materialFeedbackTarget.localScale = Vector3.one;
        SetBouncingSlot(-1);
        attackFeedback = healthFeedback = hitInvincibilityFeedback = materialFeedback = null;
        if (!colorsCached) return;
        selectedComparison.color = comparisonRestColor;
    }
}

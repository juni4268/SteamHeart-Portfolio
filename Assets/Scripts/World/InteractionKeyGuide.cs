using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class InteractionKeyGuide : MonoBehaviour
{
    [Header("Show / Hide")]
    [SerializeField, Min(0f)] private float showDuration = 0.15f;
    [SerializeField, Min(0f)] private float hideDuration = 0.15f;
    [Tooltip("Canvas의 로컬 픽셀 기준 등장·퇴장 이동 거리입니다.")]
    [SerializeField, Min(0f)] private float entranceOffset = 4f;
    [SerializeField] private CanvasGroup presentation;
    [SerializeField] private RectTransform visualRoot;

    [Header("Keycap")]
    [SerializeField] private InteractionInputType inputType;
    [SerializeField] private Text keyLabel;
    [SerializeField] private RectTransform keycap;
    [SerializeField] private Image keycapImage;
    [SerializeField] private RectTransform actionLabel;
    [SerializeField] private RectTransform actionPlate;

    private Sequence transitionTween;
    private Vector2 restPosition;
    private bool targetVisible;
    private bool isInitialized;
    private Vector3 keycapScale;
    private Color keycapColor;
    private float keyPressAmount;
    private readonly Vector3[] labelCorners = new Vector3[4];
    private string inputAction;
    private Vector2 HiddenPosition => restPosition + Vector2.down * (entranceOffset * visualRoot.localScale.y);

    private void Awake()
    {
        if (presentation == null || visualRoot == null || keycap == null || keycapImage == null || keyLabel == null)
        {
            Debug.LogError("InteractionKeyGuide needs its presentation, keycap, and key label references.", this);
            enabled = false;
            return;
        }
        Initialize();
    }

    private void OnDisable()
    {
        PlayerKeyBindings.Changed -= RefreshInputKey;
        LastInputDevice.Changed -= RefreshInputKey;
        transitionTween?.Kill();
        transitionTween = null;
        targetVisible = false;

        if (isInitialized)
        {
            visualRoot.anchoredPosition = restPosition;
            presentation.alpha = 1f;
        }
        if (isInitialized && keycap != null) keycap.localScale = keycapScale;
        if (isInitialized && keycapImage != null) keycapImage.color = keycapColor;
        keyPressAmount = 0f;
    }

    private void OnEnable()
    {
        PlayerKeyBindings.Changed += RefreshInputKey;
        LastInputDevice.Changed += RefreshInputKey;
        RefreshInputKey();
    }

    public void SetInputType(InteractionInputType type)
    {
        inputType = type;
        if (isInitialized) RefreshInputKey();
    }

    private void RefreshInputKey()
    {
        inputAction = inputType == InteractionInputType.PortalUse ? "PortalUse" : "Interact";
        keyLabel.text = PlayerKeyBindings.DisplayName(PlayerKeyBindings.CurrentBindingId(inputAction));
        float width = Mathf.Clamp(keyLabel.preferredWidth + 8f, 22f, 84f);
        keycap.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        keyLabel.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 8f);
    }

    private void LateUpdate()
    {
        if (actionLabel != null && actionPlate != null)
        {
            actionLabel.GetWorldCorners(labelCorners);
            var parent = (RectTransform)actionPlate.parent;
            Vector3 leftBottom = parent.InverseTransformPoint(labelCorners[0]);
            Vector3 rightTop = parent.InverseTransformPoint(labelCorners[2]);
            actionPlate.anchoredPosition = (leftBottom + rightTop) * 0.5f;
            actionPlate.sizeDelta = new Vector2(rightTop.x - leftBottom.x + 14f, 21f);
        }
        if (keycap == null || keycapImage == null) return;
        bool pressed = PlayerKeyBindings.IsCurrentActionPressed(inputAction);
        keyPressAmount = Mathf.MoveTowards(keyPressAmount, pressed ? 1f : 0f, Time.unscaledDeltaTime / 0.06f);
        keycap.localScale = new Vector3(keycapScale.x, keycapScale.y * (1f - 0.12f * keyPressAmount), keycapScale.z);
        keycapImage.color = Color.Lerp(keycapColor, new Color(0.82f, 0.69f, 0.48f), keyPressAmount);
    }

    public void Show()
    {
        if (!enabled) return;
        Initialize();
        if (!GameSettings.ShowInteractionKeys)
        {
            transitionTween?.Kill();
            SetHidden();
            return;
        }
        if (targetVisible && gameObject.activeSelf) return;
        bool wasInactive = !gameObject.activeSelf;
        bool wasTransitioning = transitionTween != null && transitionTween.IsActive();
        targetVisible = true;
        if (wasInactive)
        {
            gameObject.SetActive(true);
        }

        transitionTween?.Kill();

        if (wasInactive || !wasTransitioning)
        {
            presentation.alpha = 0f;
            visualRoot.anchoredPosition = HiddenPosition;
        }

        if (showDuration <= 0f)
        {
            visualRoot.anchoredPosition = restPosition;
            presentation.alpha = 1f;
            return;
        }

        transitionTween = DOTween.Sequence().SetUpdate(true);
        transitionTween.Append(presentation.DOFade(1f, showDuration).SetEase(Ease.OutQuad));
        transitionTween.Join(visualRoot.DOAnchorPos(restPosition, showDuration).SetEase(Ease.OutQuad));
        transitionTween.OnComplete(() => transitionTween = null);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf || !enabled)
        {
            return;
        }

        Initialize();
        if (!targetVisible && transitionTween != null) return;
        targetVisible = false;
        transitionTween?.Kill();

        if (!gameObject.activeInHierarchy || hideDuration <= 0f)
        {
            SetHidden();
            return;
        }

        transitionTween = DOTween.Sequence().SetUpdate(true);
        transitionTween.Append(presentation.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
        transitionTween.Join(visualRoot.DOAnchorPos(HiddenPosition, hideDuration).SetEase(Ease.InQuad));
        transitionTween.OnComplete(SetHidden);
    }

    private void SetHidden()
    {
        transitionTween = null;
        targetVisible = false;
        visualRoot.anchoredPosition = restPosition;
        gameObject.SetActive(false);
    }

    private void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        restPosition = visualRoot.anchoredPosition;
        if (keycap != null) keycapScale = keycap.localScale;
        if (keycapImage != null) keycapColor = keycapImage.color;
        isInitialized = true;
    }

}

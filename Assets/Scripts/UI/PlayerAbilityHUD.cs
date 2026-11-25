using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class PlayerAbilityHUD : MonoBehaviour
{
    [Serializable]
    private sealed class Slot
    {
        public Image frame;
        public Image icon;
        public Image cooldownFill;
        public Text remainingLabel;
        public Text keyLabel;
        public RectTransform keyPlate;

        [NonSerialized] private int displayedSeconds = -1;
        [NonSerialized] private bool readyPending;
        [NonSerialized] private float readyRemaining;
        [NonSerialized] private float rejectedUntil = float.NegativeInfinity;
        [NonSerialized] private float nextRejectionTime;
        [NonSerialized] private bool feedbackInitialized;
        [NonSerialized] private Vector3 iconRestScale;
        [NonSerialized] private Vector3 keyRestScale;
        [NonSerialized] private Vector3 remainingRestScale;
        [NonSerialized] private Color keyRestColor;
        [NonSerialized] private Color remainingRestColor;
        [NonSerialized] private Image keyBackground;
        [NonSerialized] private Color keyBackgroundRestColor;

        public bool IsConfigured => frame != null && icon != null && cooldownFill != null
            && remainingLabel != null && keyLabel != null && keyPlate != null;

        public void InitializeFeedback()
        {
            if (feedbackInitialized) return;
            iconRestScale = icon.rectTransform.localScale;
            keyRestScale = keyPlate.localScale;
            remainingRestScale = remainingLabel.rectTransform.localScale;
            keyRestColor = keyLabel.color;
            remainingRestColor = remainingLabel.color;
            keyBackground = keyPlate.GetComponent<Image>();
            if (keyBackground != null) keyBackgroundRestColor = keyBackground.color;
            feedbackInitialized = true;
        }

        public bool TryReject(PlayerAbilityHUD view, float now)
        {
            if (now < nextRejectionTime) return false;
            rejectedUntil = now + view.rejectedDuration;
            nextRejectionTime = now + view.rejectedInterval;
            return true;
        }

        public void Draw(float remaining, float duration, bool executing, bool available,
            PlayerAbilityHUD view, float deltaTime)
        {
            bool coolingDown = remaining > 0f;
            bool usable = !coolingDown && !executing && available;
            if (coolingDown)
            {
                readyPending = true;
                readyRemaining = 0f;
            }
            else if (readyPending && usable)
            {
                readyPending = false;
                readyRemaining = view.readyDuration;
            }

            if (!usable) readyRemaining = 0f;

            icon.color = coolingDown ? new Color(0.42f, 0.38f, 0.32f, 0.65f)
                : !available && !executing ? new Color(0.65f, 0.6f, 0.5f, 0.85f) : Color.white;

            Color ringColor = executing ? view.activeRingColor
                : usable ? view.readyRingColor : view.inactiveRingColor;
            frame.color = ringColor;
            if (readyRemaining > 0f && view.readyDuration > 0f)
            {
                float progress = 1f - readyRemaining / view.readyDuration;
                frame.color = Color.Lerp(ringColor, view.readyColor, Mathf.Sin(progress * Mathf.PI));
                readyRemaining = Mathf.Max(0f, readyRemaining - deltaTime);
            }

            cooldownFill.enabled = coolingDown;
            cooldownFill.color = view.readyRingColor;
            cooldownFill.fillAmount = coolingDown && duration > 0f
                ? 1f - Mathf.Clamp01(remaining / duration)
                : 0f;

            int seconds = coolingDown ? Mathf.CeilToInt(remaining) : 0;
            if (displayedSeconds != seconds)
            {
                displayedSeconds = seconds;
                remainingLabel.text = seconds > 0 ? seconds.ToString() : string.Empty;
            }

            float flash = view.rejectedDuration > 0f
                ? Mathf.Clamp01((rejectedUntil - Time.unscaledTime) / view.rejectedDuration) : 0f;
            float scale = 1f;
            if (flash > 0f)
            {
                float progress = 1f - flash;
                scale = progress < 0.35f ? Mathf.Lerp(0.9f, 1.08f, progress / 0.35f)
                    : Mathf.Lerp(1.08f, 1f, (progress - 0.35f) / 0.65f);
                frame.color = Color.Lerp(frame.color, view.rejectedColor, flash);
                icon.color = Color.Lerp(icon.color, view.rejectedColor, flash * 0.8f);
            }
            icon.rectTransform.localScale = iconRestScale * scale;
            keyPlate.localScale = keyRestScale * scale;
            keyLabel.color = Color.Lerp(keyRestColor, view.rejectedColor, flash);
            if (keyBackground != null)
                keyBackground.color = Color.Lerp(keyBackgroundRestColor, view.rejectedColor, flash);
            remainingLabel.color = Color.Lerp(remainingRestColor, Color.white, flash);
            remainingLabel.rectTransform.localScale = remainingRestScale * (1f + flash * 0.18f);
        }

        public void ResetFeedback(Color ringColor)
        {
            readyPending = false;
            readyRemaining = 0f;
            rejectedUntil = float.NegativeInfinity;
            nextRejectionTime = 0f;
            displayedSeconds = -1;
            if (frame != null) frame.color = ringColor;
            if (icon != null) icon.color = Color.white;
            if (cooldownFill != null)
            {
                cooldownFill.enabled = false;
                cooldownFill.fillAmount = 0f;
            }
            if (remainingLabel != null) remainingLabel.text = string.Empty;
            if (!feedbackInitialized) return;
            icon.rectTransform.localScale = iconRestScale;
            keyPlate.localScale = keyRestScale;
            keyLabel.color = keyRestColor;
            remainingLabel.color = remainingRestColor;
            remainingLabel.rectTransform.localScale = remainingRestScale;
            if (keyBackground != null) keyBackground.color = keyBackgroundRestColor;
        }
    }

    [SerializeField] private Player player;
    [FormerlySerializedAs("skillAView")]
    [SerializeField] private Slot groundSlamView = new Slot();
    [FormerlySerializedAs("skillSView")]
    [SerializeField] private Slot counterView = new Slot();

    [Header("Dial Colors")]
    [SerializeField] private Color readyRingColor = new Color(0.78f, 0.58f, 0.32f);
    [SerializeField] private Color inactiveRingColor = new Color(0.28f, 0.23f, 0.17f);
    [SerializeField] private Color activeRingColor = new Color(0.96f, 0.72f, 0.36f);

    [Header("Ready Feedback")]
    [SerializeField] private Color readyColor = new Color(1f, 0.91f, 0.65f);
    [SerializeField, Min(0f)] private float readyDuration = 0.45f;

    [Header("Rejected Input Feedback")]
    [SerializeField] private Color rejectedColor = new Color(0.9f, 0.26f, 0.16f);
    [SerializeField, Min(0f)] private float rejectedDuration = 0.2f;
    [SerializeField, Min(0f)] private float rejectedInterval = 0.3f;

    private PlayerGroundSlam groundSlam;
    private PlayerCounter counter;
    private PlayerMovement movement;
    private bool subscribedToPlayer;
    private float nextRejectedSoundTime;

    public bool HasRequiredReferences => groundSlamView.IsConfigured && counterView.IsConfigured;

    private void OnEnable()
    {
        if (!HasRequiredReferences) return;
        groundSlamView.InitializeFeedback();
        counterView.InitializeFeedback();
        PlayerKeyBindings.Changed += RefreshKeyLabels;
        LastInputDevice.Changed += RefreshKeyLabels;
        RefreshKeyLabels();
        SubscribeToPlayer();
    }

    private void SubscribeToPlayer()
    {
        if (player == null || subscribedToPlayer) return;
        player.ActionRejected += OnActionRejected;
        subscribedToPlayer = true;
    }

    private void OnActionRejected(PlayerAction action)
    {
        if (player == null || !player.isActiveAndEnabled || player.input == null ||
            !player.input.Player.enabled || player.health.IsDead || GameplayPause.IsActive) return;
        Slot slot = action == PlayerAction.GroundSlam ? groundSlamView
            : action == PlayerAction.Counter ? counterView : null;
        float now = Time.unscaledTime;
        if (slot == null || !slot.TryReject(this, now)) return;
        if (now < nextRejectedSoundTime) return;
        nextRejectedSoundTime = now + rejectedInterval;
        UIAudio.PlayBack();
    }

    private void RefreshKeyLabels()
    {
        RefreshKeyLabel(groundSlamView, "GroundSlam");
        RefreshKeyLabel(counterView, "Counter");
    }

    private static void RefreshKeyLabel(Slot slot, string action)
    {
        slot.keyLabel.text = PlayerKeyBindings.DisplayName(PlayerKeyBindings.CurrentBindingId(action));
        float width = Mathf.Clamp(slot.keyLabel.preferredWidth + 2f, 7f, 28f);
        slot.keyLabel.rectTransform.sizeDelta = new Vector2(width, slot.keyLabel.rectTransform.sizeDelta.y);
        slot.keyPlate.sizeDelta = new Vector2(width, slot.keyPlate.sizeDelta.y);
    }

    private void Start()
    {
        if (!HasRequiredReferences)
        {
            Debug.LogError("PlayerAbilityHUD needs its two skill slot references.", this);
            enabled = false;
            return;
        }

        if (player == null) player = FindFirstObjectByType<Player>();
        if (player == null)
        {
            gameObject.SetActive(false);
            return;
        }

        groundSlam = player.GetComponent<PlayerGroundSlam>();
        counter = player.GetComponent<PlayerCounter>();
        movement = player.GetComponent<PlayerMovement>();
        if (groundSlam == null || counter == null || movement == null)
        {
            Debug.LogError("PlayerAbilityHUD requires both skills and movement on the bound Player.", this);
            enabled = false;
            return;
        }
        SubscribeToPlayer();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        bool canAct = player.CanStartAction && player.CanAnimate;
        groundSlamView.Draw(groundSlam.CooldownRemaining, groundSlam.CooldownDuration, groundSlam.IsExecuting,
            canAct && groundSlam.isActiveAndEnabled,
            this, Time.deltaTime);
        counterView.Draw(counter.CooldownRemaining, counter.CooldownDuration, counter.IsExecuting,
            player.CanStartCounter && player.CanAnimate && movement.IsGrounded && counter.isActiveAndEnabled,
            this, Time.deltaTime);
    }

    private void OnDisable()
    {
        PlayerKeyBindings.Changed -= RefreshKeyLabels;
        LastInputDevice.Changed -= RefreshKeyLabels;
        if (subscribedToPlayer && player != null) player.ActionRejected -= OnActionRejected;
        subscribedToPlayer = false;
        nextRejectedSoundTime = 0f;
        groundSlamView.ResetFeedback(readyRingColor);
        counterView.ResetFeedback(readyRingColor);
    }
}

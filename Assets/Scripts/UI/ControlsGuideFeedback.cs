using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ControlsGuideFeedback : MonoBehaviour
{
    private const float KeycapSpacing = 8f;
    private const float EscapeFeedbackDuration = 0.1f;
    private static readonly Color DuplicateKeycapTint = new Color(1f, 0.64f, 0.60f);
    private static readonly Color DuplicatePressedTint = new Color(1f, 0.46f, 0.42f);
    private static readonly Color DuplicateLegendTint = new Color(0.41f, 0.08f, 0.07f);
    private static readonly Color DuplicateLabelTint = new Color(1f, 0.48f, 0.44f);

    [Serializable]
    private sealed class Keycap
    {
        public Key key;
        public Image image;
        public string bindingId;
        public Button button;
        public Text legend;
        public Text actionLabel;
        public RectTransform separator;

        [NonSerialized] public RectTransform rect;
        [NonSerialized] public Vector2 restPosition;
        [NonSerialized] public Color restLabelColor;
        [NonSerialized] public Color restLegendColor;
        [NonSerialized] public Color restKeycapColor;
        [NonSerialized] public float pressAmount;
        [NonSerialized] public float pressedUntil;
        [NonSerialized] public bool gamepad;
        [NonSerialized] public bool duplicate;
    }

    [SerializeField] private Keycap[] keycaps = Array.Empty<Keycap>();
    [SerializeField] private Keycap[] gamepadKeycaps = Array.Empty<Keycap>();
    [SerializeField] private Color highlightTint = new Color(1f, 0.82f, 0.28f);
    [SerializeField] private Text bindingHint;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backButton;
    [SerializeField] private MenuConfirmationDialog resetConfirmation;
    [SerializeField] private ControlsGuideTabs deviceTabs;
    [SerializeField] private Text gamepadBindingHint;

    private const string DefaultHint = "키변경 : 키캡 선택 → Enter/클릭 → 새 입력 · Esc 뒤로";
    private Keycap[] allKeycaps;
    private ButtonControl capturedGamepadButton;
    private Text CurrentHint => deviceTabs != null && deviceTabs.ShowingGamepad ? gamepadBindingHint : bindingHint;
    private Keycap waitingKeycap;
    private int waitingFrame;
    private EventSystem waitingEventSystem;
    private bool previousNavigationEvents;
    private bool navigationRestorePending;
    private Button selectionAfterCapture;
    private Canvas canvas;
    public bool IsRebinding => waitingKeycap != null;
    public bool IsConfirming => resetConfirmation.IsOpen;
    public Selectable FirstSelection => deviceTabs != null ? deviceTabs.FirstSelection : keycaps[0].button;

    private void Awake()
    {
        allKeycaps = new Keycap[keycaps.Length + gamepadKeycaps.Length];
        Array.Copy(keycaps, allKeycaps, keycaps.Length);
        Array.Copy(gamepadKeycaps, 0, allKeycaps, keycaps.Length, gamepadKeycaps.Length);
        foreach (Keycap cap in gamepadKeycaps) cap.gamepad = true;
        foreach (Keycap cap in allKeycaps)
            if (cap.button != null) cap.button.onClick.AddListener(() => BeginRebind(cap));
        resetButton.onClick.AddListener(() =>
        {
            bool gamepad = deviceTabs != null && deviceTabs.ShowingGamepad;
            resetConfirmation.Show(gamepad ? "패드 설정 초기화" : "키 설정 초기화",
                gamepad ? "패드 버튼을 기본값으로 되돌릴까요?" : "키 설정을 기본값으로 되돌릴까요?",
                string.Empty, "복원", "취소", resetButton,
                accepted => { if (accepted) PlayerKeyBindings.ResetToDefaults(gamepad); });
        });
    }

    private KeyControl deferredEscape;
    private InputAction deferredAction;
    private Action deferredBack;
    private float earliestBackTime;
    private bool escapeReleased;

    private void OnEnable()
    {
        canvas = GetComponentInParent<Canvas>();
        PlayerKeyBindings.Changed += RefreshBindings;
        LastInputDevice.Changed += RefreshBindings;
        foreach (Keycap cap in allKeycaps)
        {
            cap.rect = cap.image.rectTransform;
            cap.restPosition = cap.rect.anchoredPosition;
            cap.restLabelColor = cap.actionLabel.color;
            cap.restLegendColor = cap.legend.color;
            cap.restKeycapColor = cap.image.color;
            cap.pressAmount = cap.pressedUntil = 0f;
        }
        RefreshBindings();
    }

    private void OnDisable()
    {
        resetConfirmation.TryCancel();
        StopRebind();
        RestoreNavigation();
        PlayerKeyBindings.Changed -= RefreshBindings;
        LastInputDevice.Changed -= RefreshBindings;
        ClearDeferredBack();
        foreach (Keycap cap in allKeycaps)
        {
            if (cap.rect == null) continue;
            cap.pressAmount = cap.pressedUntil = 0f;
            cap.rect.anchoredPosition = cap.restPosition;
            cap.image.color = cap.restKeycapColor;
            cap.legend.color = cap.restLegendColor;
            cap.actionLabel.color = cap.restLabelColor;
        }
    }

    public bool DeferKeyboardBack(InputAction.CallbackContext context, Action back)
    {
        if (resetConfirmation.IsOpen) return false;
        if (!isActiveAndEnabled || context.control is not KeyControl key || key.keyCode != Key.Escape)
            return false;

        if (TryCancelRebind()) return true;

        ClearDeferredBack();
        deferredEscape = key;
        deferredAction = context.action;
        deferredAction.canceled += OnEscapeReleased;
        deferredBack = back;
        earliestBackTime = Time.unscaledTime + EscapeFeedbackDuration;
        return true;
    }

    public bool TryCancelRebind()
    {
        if (!IsRebinding) return false;
        StopRebind();
        SetDefaultHints();
        UIAudio.PlayBack();
        return true;
    }

    public bool TryCloseConfirmation() => resetConfirmation.TryCancel();

    public bool ConsumesGamepadBack(InputAction.CallbackContext context) => context.control?.device is Gamepad
        && (waitingKeycap?.gamepad == true || capturedGamepadButton?.isPressed == true);

    private void SetDefaultHints()
    {
        bindingHint.text = DefaultHint;
        if (gamepadBindingHint == null) return;
        string submit = PlayerKeyBindings.GamepadDisplayName(PlayerKeyBindings.GamepadSubmitPath);
        string cancel = PlayerKeyBindings.GamepadDisplayName(PlayerKeyBindings.GamepadCancelPath);
        string left = PlayerKeyBindings.GamepadDisplayName("<Gamepad>/leftShoulder");
        string right = PlayerKeyBindings.GamepadDisplayName("<Gamepad>/rightShoulder");
        gamepadBindingHint.text = $"키변경 : 버튼 선택 → {submit}/클릭 → 새 입력\n{left}/{right} 탭 전환 · {cancel} 뒤로";
    }

    private void BeginRebind(Keycap cap)
    {
        if (IsRebinding || !cap.button.IsInteractable()) return;
        RestoreNavigation();
        ClearDeferredBack();
        waitingKeycap = cap;
        waitingFrame = Time.frameCount;
        // Capture disables the button immediately; finish its brief press feedback in this owner.
        cap.pressAmount = 1f;
        cap.pressedUntil = Time.unscaledTime + 0.12f;
        DrawPress(cap);
        waitingEventSystem = EventSystem.current;
        if (waitingEventSystem != null)
        {
            previousNavigationEvents = waitingEventSystem.sendNavigationEvents;
            waitingEventSystem.sendNavigationEvents = false;
        }
        foreach (Keycap item in allKeycaps)
            if (item.button != null) item.button.interactable = false;
        resetButton.interactable = backButton.interactable = false;
        cap.image.GetComponent<Outline>().enabled = true;
        string cancel = cap.gamepad ? $"{PlayerKeyBindings.GamepadDisplayName("<Gamepad>/start")}/Esc" : "Esc";
        CurrentHint.text = $"‘{cap.actionLabel.text}’ 새 입력 대기 · {cancel} 취소";
        cap.legend.text = "?";
    }

    private void StopRebind()
    {
        if (!IsRebinding) return;
        Keycap cap = waitingKeycap;
        waitingKeycap = null;
        navigationRestorePending = true;
        RefreshBindings();
        selectionAfterCapture = cap.button;
    }

    private void RestoreNavigation()
    {
        foreach (Keycap cap in allKeycaps)
            if (cap.button != null) cap.button.interactable = true;
        resetButton.interactable = backButton.interactable = true;
        if (selectionAfterCapture != null && selectionAfterCapture.IsActive()) selectionAfterCapture.Select();
        selectionAfterCapture = null;
        if (waitingEventSystem != null) waitingEventSystem.sendNavigationEvents = previousNavigationEvents;
        waitingEventSystem = null;
        navigationRestorePending = false;
        capturedGamepadButton = null;
    }

    // Restore after input dispatch and mouse release so the capture click cannot reopen the editor.
    private void LateUpdate()
    {
        if (navigationRestorePending && Mouse.current?.leftButton.isPressed != true
            && capturedGamepadButton?.isPressed != true) RestoreNavigation();
    }

    private void RefreshBindings()
    {
        var groups = new Dictionary<Transform, List<Keycap>>();
        foreach (Keycap cap in allKeycaps)
        {
            cap.duplicate = PlayerKeyBindings.HasDuplicateBinding(cap.bindingId);
            if (!string.IsNullOrEmpty(cap.bindingId))
            {
                if (cap != waitingKeycap) cap.legend.text = PlayerKeyBindings.DisplayName(cap.bindingId);
            }
            if (!groups.TryGetValue(cap.image.transform.parent, out var group))
                groups.Add(cap.image.transform.parent, group = new List<Keycap>());
            group.Add(cap);
        }
        foreach (var group in groups)
        {
            float maximum = (((RectTransform)group.Key).rect.width - (group.Value.Count - 1) * KeycapSpacing) / group.Value.Count;
            foreach (Keycap cap in group.Value)
            {
                float width = Mathf.Clamp(cap.legend.preferredWidth + 8f, cap.rect.rect.height, maximum);
                cap.rect.sizeDelta = new Vector2(width, cap.rect.sizeDelta.y);
                cap.legend.rectTransform.sizeDelta = new Vector2(width - 8f, cap.legend.rectTransform.sizeDelta.y);
            }
            float x = -((RectTransform)group.Key).rect.width * 0.5f;
            foreach (Keycap cap in group.Value)
            {
                cap.restPosition = new Vector2(x + cap.rect.rect.width * 0.5f, cap.restPosition.y);
                DrawPress(cap);
                x += cap.rect.rect.width;
                if (cap.separator != null)
                    cap.separator.anchoredPosition = new Vector2(x + KeycapSpacing * 0.5f, cap.separator.anchoredPosition.y);
                x += KeycapSpacing;
            }
        }
        if (!IsRebinding) SetDefaultHints();
    }

    private void DrawPress(Keycap cap)
    {
        float pixelScale = canvas != null ? canvas.scaleFactor : 1f;
        cap.rect.anchoredPosition = cap.restPosition + Vector2.down * (Mathf.Round(cap.pressAmount * 5f) / pixelScale);
        Color rest = cap.duplicate ? DuplicateKeycapTint : cap.restKeycapColor;
        Color pressed = cap.duplicate ? DuplicatePressedTint : highlightTint;
        cap.image.color = Color.Lerp(rest, pressed, cap.pressAmount);
        cap.legend.color = cap.duplicate ? DuplicateLegendTint : cap.restLegendColor;
        bool selected = cap == waitingKeycap || cap.button != null &&
            EventSystem.current?.currentSelectedGameObject == cap.button.gameObject;
        cap.actionLabel.color = cap.duplicate ? DuplicateLabelTint : selected ? highlightTint : cap.restLabelColor;
    }

    private void OnEscapeReleased(InputAction.CallbackContext context)
    {
        if (context.control == deferredEscape) escapeReleased = true;
    }

    private void ClearDeferredBack()
    {
        if (deferredAction != null) deferredAction.canceled -= OnEscapeReleased;
        deferredAction = null;
        deferredEscape = null;
        deferredBack = null;
        escapeReleased = false;
    }

    private void Update()
    {
        if (IsRebinding && Time.frameCount > waitingFrame) CaptureInput();
        foreach (Keycap cap in allKeycaps)
        {
            cap.pressAmount = Mathf.MoveTowards(cap.pressAmount, Time.unscaledTime < cap.pressedUntil ? 1f : 0f,
                Time.unscaledDeltaTime / 0.06f);
            DrawPress(cap);
        }

        if (deferredEscape == null || !escapeReleased || Time.unscaledTime < earliestBackTime) return;
        Action back = deferredBack;
        ClearDeferredBack();
        back?.Invoke();
    }

    private void CaptureInput()
    {
        if (waitingKeycap.gamepad)
        {
            Gamepad pad = LastInputDevice.ActiveGamepad;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || pad?.startButton.wasPressedThisFrame == true)
            { TryCancelRebind(); return; }
            if (pad == null) return;
            foreach (GamepadButton padButton in Enum.GetValues(typeof(GamepadButton)))
            {
                if (PlayerKeyBindings.GamepadPath(padButton) == null || !pad[padButton].wasPressedThisFrame) continue;
                bool accepted = PlayerKeyBindings.TryRebind(waitingKeycap.bindingId, padButton, out _);
                if (accepted) capturedGamepadButton = pad[padButton];
                HandleCapture(accepted);
                return;
            }
            return;
        }
        if (Keyboard.current != null)
            foreach (KeyControl key in Keyboard.current.allKeys)
            {
                if (key.synthetic || !key.wasPressedThisFrame) continue;
                if (key.keyCode == Key.Escape) { TryCancelRebind(); return; }
                bool accepted = PlayerKeyBindings.TryRebind(waitingKeycap.bindingId, key.keyCode, out _);
                HandleCapture(accepted);
                return;
            }
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        MouseButton? button = mouse.leftButton.wasPressedThisFrame ? MouseButton.Left
            : mouse.rightButton.wasPressedThisFrame ? MouseButton.Right
            : mouse.middleButton.wasPressedThisFrame ? MouseButton.Middle
            : mouse.backButton.wasPressedThisFrame ? MouseButton.Back
            : mouse.forwardButton.wasPressedThisFrame ? MouseButton.Forward : null;
        if (button.HasValue)
        {
            bool accepted = PlayerKeyBindings.TryRebind(waitingKeycap.bindingId, button.Value, out _);
            HandleCapture(accepted);
        }
    }

    private void HandleCapture(bool accepted)
    {
        if (accepted)
        {
            StopRebind();
            UIAudio.PlayClick();
            return;
        }
        CurrentHint.text = "이 입력은 사용할 수 없습니다 · 다른 입력을 누르세요";
    }
}

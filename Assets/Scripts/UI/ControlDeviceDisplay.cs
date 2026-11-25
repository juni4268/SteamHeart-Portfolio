using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ControlDeviceDisplay : MonoBehaviour
{
    [Serializable]
    private sealed class ControlVisual
    {
        public string controlPath;
        public string alternatePath;
        public string bindingId;
        public Image image;
        public Text legend;
        public Text actionLabel;
        public bool fixedControl;
        [NonSerialized] public ButtonControl control;
        [NonSerialized] public ButtonControl alternate;
        [NonSerialized] public Color labelColor;
        [NonSerialized] public Color restColor;
        [NonSerialized] public Vector3 restScale;
        [NonSerialized] public bool bindingSelector;
        [NonSerialized] public bool assigned;
        [NonSerialized] public float amount;
        [NonSerialized] public float pressedUntil;
        [NonSerialized] public Vector2 restPosition;
    }

    [SerializeField] private ControlVisual[] visuals = Array.Empty<ControlVisual>();
    [SerializeField] private Color unusedColor = new Color(0.91f, 0.85f, 0.73f);
    [SerializeField] private Color assignedColor = new Color(0.96f, 0.76f, 0.44f);
    [SerializeField] private Color pressedColor = new Color(0.99f, 0.86f, 0.62f);
    [SerializeField] private Color actionHighlightColor = new Color(1f, .82f, .28f);
    [SerializeField] private RectTransform leftStick;
    [SerializeField] private RectTransform rightStick;
    [SerializeField, Min(0f)] private float stickTravel = 2.5f;
    private Vector2 leftStickRest;
    private Vector2 rightStickRest;
    private Canvas canvas;

    private void OnEnable()
    {
        canvas = GetComponentInParent<Canvas>();
        foreach (ControlVisual visual in visuals)
        {
            visual.amount = visual.pressedUntil = 0f;
            visual.restPosition = visual.image.rectTransform.anchoredPosition;
            visual.restScale = visual.image.rectTransform.localScale;
            visual.restColor = visual.image.color;
            visual.bindingSelector = visual.image.GetComponent<Button>() != null;
            if (!visual.bindingSelector && visual.legend == null
                && visual.controlPath.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase))
                visual.legend = visual.image.GetComponentInChildren<Text>(true);
            if (visual.actionLabel != null) visual.labelColor = visual.actionLabel.color;
        }
        if (leftStick != null) leftStickRest = leftStick.anchoredPosition;
        if (rightStick != null) rightStickRest = rightStick.anchoredPosition;
        InputSystem.onDeviceChange += RefreshDevices;
        PlayerKeyBindings.Changed += RefreshBindings;
        LastInputDevice.Changed += RefreshBindings;
        RefreshBindings();
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= RefreshDevices;
        PlayerKeyBindings.Changed -= RefreshBindings;
        LastInputDevice.Changed -= RefreshBindings;
        foreach (ControlVisual visual in visuals)
        {
            if (visual.image == null) continue;
            visual.amount = 0f;
            Draw(visual);
        }
        if (leftStick != null) leftStick.anchoredPosition = leftStickRest;
        if (rightStick != null) rightStick.anchoredPosition = rightStickRest;
    }

    private void RefreshDevices(InputDevice device, InputDeviceChange change)
    {
        if (device is Keyboard || device is Mouse || device is Gamepad) RefreshBindings();
    }

    private void RefreshBindings()
    {
        foreach (ControlVisual visual in visuals)
        {
            string path = string.IsNullOrEmpty(visual.bindingId) ? visual.controlPath : PlayerKeyBindings.BindingPath(visual.bindingId);
            visual.control = PlayerKeyBindings.ControlForPath(path);
            visual.alternate = string.IsNullOrEmpty(visual.alternatePath) || path != visual.controlPath ? null
                : PlayerKeyBindings.ControlForPath(visual.alternatePath);
            if (!visual.bindingSelector && visual.legend != null)
            {
                if (!string.IsNullOrEmpty(visual.bindingId))
                    visual.legend.text = PlayerKeyBindings.DisplayName(visual.bindingId);
                else if (path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase))
                    visual.legend.text = PlayerKeyBindings.GamepadDisplayName(path);
            }
            visual.assigned = visual.fixedControl || PlayerKeyBindings.IsAssigned(visual.controlPath);
            Draw(visual);
        }
    }

    private void Update()
    {
        foreach (ControlVisual visual in visuals)
        {
            if (visual.bindingSelector) continue;
            if (visual.control?.wasPressedThisFrame == true || visual.alternate?.wasPressedThisFrame == true)
                visual.pressedUntil = Time.unscaledTime + 0.08f;
            bool pressed = visual.control?.isPressed == true || visual.alternate?.isPressed == true
                || Time.unscaledTime < visual.pressedUntil;
            float amount = Mathf.MoveTowards(visual.amount, pressed ? 1f : 0f, Time.unscaledDeltaTime / 0.05f);
            if (Mathf.Approximately(amount, visual.amount)) continue;
            visual.amount = amount;
            Draw(visual);
        }
        Gamepad pad = LastInputDevice.ActiveGamepad;
        if (leftStick != null) leftStick.anchoredPosition = leftStickRest + (pad?.leftStick.ReadValue() ?? Vector2.zero) * stickTravel;
        if (rightStick != null) rightStick.anchoredPosition = rightStickRest + (pad?.rightStick.ReadValue() ?? Vector2.zero) * stickTravel;
    }

    private void Draw(ControlVisual visual)
    {
        if (visual.bindingSelector) return;
        bool gamepad = visual.controlPath.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase);
        bool fixedKeycap = gamepad && visual.actionLabel != null && string.IsNullOrEmpty(visual.bindingId);
        Color normal = fixedKeycap ? visual.restColor : visual.assigned ? assignedColor : unusedColor;
        visual.image.color = Color.Lerp(normal, pressedColor, visual.amount);
        float pixels = visual.control is KeyControl || gamepad ? Mathf.Round(visual.amount * 2f) : 0f;
        float pixelScale = canvas != null ? canvas.scaleFactor : 1f;
        visual.image.rectTransform.anchoredPosition = visual.restPosition + Vector2.down * (pixels / pixelScale);
        if (gamepad) visual.image.rectTransform.localScale = visual.restScale * (1f - visual.amount * .06f);
        if (visual.actionLabel != null)
            visual.actionLabel.color = Color.Lerp(visual.labelColor, actionHighlightColor, visual.amount);
    }
}

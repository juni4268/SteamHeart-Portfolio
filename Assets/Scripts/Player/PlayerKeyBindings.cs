using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

public static class PlayerKeyBindings
{
    public const string GamepadSubmitPath = "<Gamepad>/{Submit}";
    public const string GamepadCancelPath = "<Gamepad>/{Cancel}";
    private const string PreferencePrefix = "ProjectK.Key.";
    // Key codes stay positive; mouse buttons use -1..-5 and gamepad buttons use -100 minus their enum value.
    private static readonly string[] MouseControls = { "leftButton", "rightButton", "middleButton", "backButton", "forwardButton" };
    private static PlayerInputSet controls;
    public static event Action Changed;

    private static PlayerInputSet Controls
    {
        get
        {
            if (controls != null) return controls;
            controls = new PlayerInputSet();
            foreach (InputAction action in controls)
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (!IsEditable(action, binding)) continue;
                    int value = PlayerPrefs.GetInt(PreferencePrefix + binding.id);
                    string path = PathForSavedInput(value);
                    if (path != null && IsGamepadBinding(binding) == (value <= -100)) action.ApplyBindingOverride(i, path);
                }
            SynchronizeDownBinding();
            SynchronizeGamepadDirections();
            return controls;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        controls?.Dispose();
        controls = null;
        Changed = null;
    }

    private static bool IsValidKey(Key key) => key != Key.None && key != Key.Escape
        && Enum.IsDefined(typeof(Key), key);

    public static string PathFor(Key key)
    {
        if (Keyboard.current != null) return "<Keyboard>/" + Keyboard.current[key].name;
        string name = key.ToString();
        if (name.StartsWith("Digit", StringComparison.Ordinal)) name = name.Substring(5);
        return "<Keyboard>/" + name;
    }

    private static string PathForSavedInput(int value)
    {
        if (value < 0 && value >= -MouseControls.Length) return "<Mouse>/" + MouseControls[-value - 1];
        if (value <= -100 && value >= -200) return GamepadPath((GamepadButton)(-100 - value));
        return IsValidKey((Key)value) ? PathFor((Key)value) : null;
    }

    public static string GamepadPath(GamepadButton button) => button switch {
        GamepadButton.South => "<Gamepad>/buttonSouth", GamepadButton.East => "<Gamepad>/buttonEast",
        GamepadButton.West => "<Gamepad>/buttonWest", GamepadButton.North => "<Gamepad>/buttonNorth",
        GamepadButton.LeftShoulder => "<Gamepad>/leftShoulder", GamepadButton.RightShoulder => "<Gamepad>/rightShoulder",
        GamepadButton.LeftTrigger => "<Gamepad>/leftTrigger", GamepadButton.RightTrigger => "<Gamepad>/rightTrigger",
        GamepadButton.LeftStick => "<Gamepad>/leftStickPress", GamepadButton.RightStick => "<Gamepad>/rightStickPress",
        GamepadButton.Select => "<Gamepad>/select", GamepadButton.DpadUp => "<Gamepad>/dpad/up",
        GamepadButton.DpadDown => "<Gamepad>/dpad/down", _ => null
    };

    private static bool IsGamepadBinding(InputBinding binding) => binding.path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase);

    private static bool IsEditable(InputAction action, InputBinding binding) => !binding.isComposite
        && (!IsGamepadBinding(binding) || action != controls.Player.Movement && !binding.path.StartsWith("<Gamepad>/leftStick/", StringComparison.OrdinalIgnoreCase))
        && !(action == controls.Player.Movement && binding.name == "down");

    private static bool FindBinding(string id, out InputAction action, out int index)
    {
        foreach (InputAction candidate in Controls)
            for (int i = 0; i < candidate.bindings.Count; i++)
                if (string.Equals(candidate.bindings[i].id.ToString(), id, StringComparison.OrdinalIgnoreCase))
                {
                    action = candidate;
                    index = i;
                    return true;
                }
        action = null;
        index = -1;
        return false;
    }

    public static string ActionBindingId(string actionName) =>
        Controls.asset.FindAction(actionName, true).bindings[0].id.ToString();

    public static string GamepadBindingId(string actionName)
    {
        InputAction action = Controls.asset.FindAction(actionName, true);
        foreach (InputBinding binding in action.bindings)
            if (IsGamepadBinding(binding) && IsEditable(action, binding)) return binding.id.ToString();
        return string.Empty;
    }

    public static string CurrentBindingId(string actionName) => LastInputDevice.IsGamepad
        ? GamepadBindingId(actionName) : ActionBindingId(actionName);

    public static bool IsCurrentActionPressed(string actionName)
    {
        InputAction action = Controls.asset.FindAction(actionName, true);
        foreach (InputBinding binding in action.bindings)
            if (!binding.isComposite && IsGamepadBinding(binding) == LastInputDevice.IsGamepad
                && ControlForPath(binding.effectivePath)?.isPressed == true) return true;
        return false;
    }

    public static string BindingPath(string bindingId) => FindBinding(bindingId, out InputAction action, out int index)
        ? action.bindings[index].effectivePath : string.Empty;

    public static Key GetKey(string bindingId)
    {
        return (GetControl(bindingId) as KeyControl)?.keyCode ?? Key.None;
    }

    public static ButtonControl ControlForPath(string path)
    {
        InputDevice device = path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase)
            ? Mouse.current : path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase)
                ? LastInputDevice.ActiveGamepad : Keyboard.current;
        return device != null ? InputControlPath.TryFindControl(device, path) as ButtonControl : null;
    }

    public static ButtonControl GetControl(string bindingId) =>
        FindBinding(bindingId, out InputAction action, out int index)
            ? ControlForPath(action.bindings[index].effectivePath) : null;

    public static bool IsAssigned(string path)
    {
        foreach (InputAction action in Controls)
            foreach (InputBinding binding in action.bindings)
                if (IsEditable(action, binding) && string.Equals(path, binding.effectivePath, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static string DisplayName(string bindingId)
    {
        if (!FindBinding(bindingId, out InputAction action, out int index)) return string.Empty;
        string path = action.bindings[index].effectivePath;
        if (IsGamepadBinding(action.bindings[index])) return GamepadDisplayName(path);
        for (int i = 0; i < MouseControls.Length; i++)
            if (string.Equals(path, "<Mouse>/" + MouseControls[i], StringComparison.OrdinalIgnoreCase)) return "M" + (i + 1);
        if (ControlForPath(path) is KeyControl key) return DisplayName(key.keyCode);
        return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    public static string GamepadDisplayName(string path)
    {
        ButtonControl control = ControlForPath(path);
        return path switch {
            "<Gamepad>/dpad/up" => "↑", "<Gamepad>/dpad/down" => "↓",
            "<Gamepad>/dpad/left" => "←", "<Gamepad>/dpad/right" => "→",
            "<Gamepad>/leftStickPress" => "L3", "<Gamepad>/rightStickPress" => "R3",
            "<Gamepad>/start" => control?.displayName == "Plus" ? "+" : control?.displayName ?? "Start",
            "<Gamepad>/select" => control?.displayName == "Minus" ? "-" : control?.displayName ?? "Select",
            _ => (control?.shortDisplayName ?? control?.displayName) switch {
                "Cross" => "×", "Circle" => "○", "Square" => "□", "Triangle" => "△",
                _ => control?.shortDisplayName ?? (path switch {
                    GamepadSubmitPath or "<Gamepad>/buttonSouth" => "A",
                    GamepadCancelPath or "<Gamepad>/buttonEast" => "B",
                    "<Gamepad>/buttonWest" => "X", "<Gamepad>/buttonNorth" => "Y",
                    "<Gamepad>/leftShoulder" => "LB", "<Gamepad>/rightShoulder" => "RB",
                    "<Gamepad>/leftTrigger" => "LT", "<Gamepad>/rightTrigger" => "RT",
                    _ => control?.displayName ?? string.Empty
                })
            }
        };
    }

    public static string DisplayName(Key key)
    {
        switch (key)
        {
            case Key.LeftArrow: return "←";
            case Key.RightArrow: return "→";
            case Key.UpArrow: return "↑";
            case Key.DownArrow: return "↓";
            case Key.Escape: return "Esc";
            case Key.Backspace: return "Bksp";
            case Key.PrintScreen: return "PrtSc";
            case Key.LeftShift: return "L Shift";
            case Key.RightShift: return "R Shift";
            case Key.LeftCtrl: return "L Ctrl";
            case Key.RightCtrl: return "R Ctrl";
            case Key.LeftAlt: return "L Alt";
            case Key.RightAlt: return "R Alt";
            case Key.LeftMeta: return "L Meta";
            case Key.RightMeta: return "R Meta";
            case Key.ContextMenu: return "Menu";
            case Key.ScrollLock: return "ScrLk";
            case Key.NumpadEnter: return "Num↵";
            case Key.NumpadDivide: return "Num/";
            case Key.NumpadMultiply: return "Num*";
            case Key.NumpadPlus: return "Num+";
            case Key.NumpadMinus: return "Num-";
            case Key.NumpadPeriod: return "Num.";
        }
        KeyControl control = Keyboard.current != null && key != Key.None ? Keyboard.current[key] : null;
        return control?.shortDisplayName ?? control?.displayName ?? key.ToString();
    }

    public static bool TryRebind(string bindingId, Key key, out string conflictingBinding)
    {
        return TryRebindSavedInput(bindingId, (int)key, out conflictingBinding);
    }

    public static bool TryRebind(string bindingId, MouseButton button, out string conflictingBinding)
    {
        int value = button switch {
            MouseButton.Left => -1, MouseButton.Right => -2, MouseButton.Middle => -3,
            MouseButton.Back => -4, MouseButton.Forward => -5, _ => 0
        };
        return TryRebindSavedInput(bindingId, value, out conflictingBinding);
    }

    public static bool TryRebind(string bindingId, GamepadButton button, out string conflictingBinding) =>
        TryRebindSavedInput(bindingId, -100 - (int)button, out conflictingBinding);

    private static bool TryRebindSavedInput(string bindingId, int value, out string conflictingBinding)
    {
        conflictingBinding = null;
        string path = PathForSavedInput(value);
        if (path == null || !FindBinding(bindingId, out InputAction action, out int index)
            || !IsEditable(action, action.bindings[index]) || IsGamepadBinding(action.bindings[index]) != (value <= -100)) return false;
        conflictingBinding = FindDuplicate(action.bindings[index].id, path);
        action.ApplyBindingOverride(index, path);
        SynchronizeDownBinding();
        SynchronizeGamepadDirections();
        PlayerPrefs.SetInt(PreferencePrefix + bindingId, value);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    public static bool HasDuplicateBinding(string bindingId)
    {
        if (!FindBinding(bindingId, out InputAction action, out int index)) return false;
        InputBinding binding = action.bindings[index];
        return IsEditable(action, binding) && FindDuplicate(binding.id, binding.effectivePath) != null;
    }

    private static string FindDuplicate(Guid bindingId, string path)
    {
        foreach (InputAction action in Controls)
            foreach (InputBinding binding in action.bindings)
                if (binding.id != bindingId && IsEditable(action, binding)
                    && string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase))
                    return binding.id.ToString();
        return null;
    }

    // Movement's down component and dropping through a platform share one user-facing key.
    private static void SynchronizeDownBinding()
    {
        InputAction movement = controls.Player.Movement;
        for (int i = 0; i < movement.bindings.Count; i++)
            if (movement.bindings[i].isPartOfComposite && movement.bindings[i].name == "down")
            {
                string path = controls.Player.Down.bindings[0].overridePath;
                if (path == null) movement.RemoveBindingOverride(i);
                else movement.ApplyBindingOverride(i, path);
            }
    }

    // The stick and D-pad share the default direction; a reassigned button replaces both.
    private static void SynchronizeGamepadDirections()
    {
        foreach (InputAction action in new[] { controls.Player.Down, controls.Player.PortalUse })
        {
            string path = null;
            foreach (InputBinding binding in action.bindings)
                if (IsGamepadBinding(binding) && IsEditable(action, binding)) { path = binding.overridePath; break; }
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].path.StartsWith("<Gamepad>/leftStick/", StringComparison.OrdinalIgnoreCase))
                {
                    if (path == null) action.RemoveBindingOverride(i);
                    else action.ApplyBindingOverride(i, path);
                }
        }
    }

    public static void ApplyTo(PlayerInputSet target)
    {
        bool enabled = target.Player.enabled;
        target.Disable();
        target.LoadBindingOverridesFromJson(Controls.SaveBindingOverridesAsJson());
        if (enabled) target.Player.Enable();
    }

    public static void ResetToDefaults(bool gamepad = false)
    {
        foreach (InputAction action in Controls)
            for (int i = 0; i < action.bindings.Count; i++)
                if (IsEditable(action, action.bindings[i]) && IsGamepadBinding(action.bindings[i]) == gamepad)
                {
                    PlayerPrefs.DeleteKey(PreferencePrefix + action.bindings[i].id);
                    action.RemoveBindingOverride(i);
                }
        SynchronizeDownBinding();
        SynchronizeGamepadDirections();
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}

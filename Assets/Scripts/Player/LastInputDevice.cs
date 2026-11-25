using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public static class LastInputDevice
{
    public static bool IsGamepad { get; private set; }
    public static Gamepad ActiveGamepad => lastGamepad ?? Gamepad.current;
    public static event Action Changed;

    private static IDisposable buttonListener;
    private static InputAction mouseMotion;
    private static InputAction gamepadMotion;
    private static Gamepad lastGamepad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        buttonListener?.Dispose();
        buttonListener = null;
        mouseMotion?.Dispose();
        mouseMotion = null;
        gamepadMotion?.Dispose();
        gamepadMotion = null;
        InputSystem.onDeviceChange -= OnDeviceChange;
        lastGamepad = null;
        IsGamepad = false;
        Changed = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Listen()
    {
        buttonListener = InputSystem.onAnyButtonPress.Call(control => SetDevice(control.device));
        mouseMotion = new InputAction("DetectMouseMotion", InputActionType.PassThrough);
        mouseMotion.AddBinding("<Mouse>/delta");
        mouseMotion.AddBinding("<Mouse>/scroll");
        mouseMotion.performed += context =>
        {
            var mouse = context.control.device as Mouse;
            float minimum = context.control == mouse.delta ? 4f : 0f;
            if (context.ReadValue<Vector2>().sqrMagnitude > minimum) SetDevice(mouse);
        };
        gamepadMotion = new InputAction("DetectGamepadMotion", InputActionType.PassThrough);
        gamepadMotion.AddBinding("<Gamepad>/leftStick");
        gamepadMotion.AddBinding("<Gamepad>/rightStick");
        gamepadMotion.performed += context =>
        {
            if (context.ReadValue<Vector2>().sqrMagnitude >= .35f * .35f) SetDevice(context.control.device);
        };
        InputSystem.onDeviceChange += OnDeviceChange;
        mouseMotion.Enable();
        gamepadMotion.Enable();
    }

    private static void SetDevice(InputDevice device)
    {
        bool changedGamepad = false;
        if (device is Gamepad pad)
        {
            changedGamepad = lastGamepad != pad;
            lastGamepad = pad;
        }
        else if (device is not Keyboard && device is not Mouse) return;
        bool gamepad = device is Gamepad;
        if (IsGamepad == gamepad && !changedGamepad) return;
        IsGamepad = gamepad;
        Changed?.Invoke();
    }

    private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device != lastGamepad || (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected)) return;
        lastGamepad = null;
        IsGamepad = false;
        Changed?.Invoke();
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ControlsGuideTabs : MonoBehaviour
{
    [SerializeField] private GameObject[] keyboardContent;
    [SerializeField] private GameObject gamepadPage;
    [SerializeField] private Button keyboardTab;
    [SerializeField] private Button gamepadTab;
    [SerializeField] private Selectable keyboardFirstSelection;
    [SerializeField] private Selectable gamepadFirstSelection;
    [SerializeField] private Selectable gamepadLastSelection;
    [SerializeField] private Selectable gamepadDownSelection;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Image keyboardUnderline;
    [SerializeField] private Image gamepadUnderline;
    [SerializeField] private ControlsGuideFeedback feedback;

    private InputAction keyboardAction;
    private InputAction gamepadAction;
    private Navigation keyboardBackNavigation;
    private Navigation keyboardResetNavigation;
    public bool ShowingGamepad { get; private set; }
    public Selectable FirstSelection => (isActiveAndEnabled ? ShowingGamepad : LastInputDevice.IsGamepad)
        ? gamepadFirstSelection : keyboardFirstSelection;

    private void Awake()
    {
        keyboardBackNavigation = backButton.navigation;
        keyboardResetNavigation = resetButton.navigation;
        keyboardTab.onClick.AddListener(ShowKeyboard);
        gamepadTab.onClick.AddListener(ShowGamepad);
        keyboardAction = new InputAction("KeyboardControlsTab", InputActionType.Button, "<Gamepad>/leftShoulder");
        gamepadAction = new InputAction("GamepadControlsTab", InputActionType.Button, "<Gamepad>/rightShoulder");
        keyboardAction.performed += _ => ShowKeyboard();
        gamepadAction.performed += _ => ShowGamepad();
    }

    private void OnEnable()
    {
        ShowingGamepad = LastInputDevice.IsGamepad;
        ApplyPage();
        keyboardAction.Enable();
        gamepadAction.Enable();
    }

    private void OnDisable()
    {
        keyboardAction.Disable();
        gamepadAction.Disable();
    }

    private void OnDestroy()
    {
        keyboardAction?.Dispose();
        gamepadAction?.Dispose();
    }

    public void ShowKeyboard() => SelectPage(false);
    public void ShowGamepad() => SelectPage(true);

    private void SelectPage(bool gamepad)
    {
        if (feedback.IsRebinding || feedback.IsConfirming) return;
        if (ShowingGamepad != gamepad) UIAudio.PlayMove();
        ShowingGamepad = gamepad;
        ApplyPage();
        FirstSelection.Select();
    }

    private void ApplyPage()
    {
        foreach (GameObject item in keyboardContent) item.SetActive(!ShowingGamepad);
        gamepadPage.SetActive(ShowingGamepad);
        keyboardUnderline.enabled = !ShowingGamepad;
        gamepadUnderline.enabled = ShowingGamepad;
        var navigation = keyboardBackNavigation;
        if (ShowingGamepad)
        {
            navigation.selectOnUp = gamepadLastSelection;
            navigation.selectOnLeft = resetButton;
        }
        backButton.navigation = navigation;
        navigation = keyboardResetNavigation;
        if (ShowingGamepad) navigation.selectOnUp = gamepadDownSelection;
        resetButton.navigation = navigation;
        foreach (Button tab in new[] { keyboardTab, gamepadTab })
        {
            navigation = tab.navigation;
            navigation.selectOnDown = FirstSelection;
            tab.navigation = navigation;
        }
    }
}

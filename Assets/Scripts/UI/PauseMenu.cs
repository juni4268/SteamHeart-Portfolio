using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenu : MonoBehaviour
{
    [Header("Pages")]
    [SerializeField] private GameObject overlay;
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject settingsPage;
    [SerializeField] private GameObject controlsPage;
    [SerializeField] private GameObject quitPage;
    [SerializeField] private Text title;
    [SerializeField] private Canvas overlayCanvas;

    [Header("Menu")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button controlsBackButton;
    [SerializeField] private Button cancelQuitButton;
    [SerializeField] private Button confirmQuitButton;

    [Header("Settings")]
    [SerializeField] private GameSettingsPanel settingsPanel;

    private InputAction pauseAction;
    private InputAction cancelAction;
    private GameplayPause.Handle pause;
    private GameObject previousSelection;
    private Player player;
    private bool blockedAtInputUpdate;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private MenuPanelTransition presentation;
    private bool closing;

    public bool IsOpen { get; private set; }

    private bool HasRequiredReferences => overlay != null && mainPage != null && settingsPage != null
        && controlsPage != null && controlsButton != null && controlsBackButton != null
        && quitPage != null && title != null && overlayCanvas != null && resumeButton != null && settingsButton != null
        && quitButton != null && cancelQuitButton != null && confirmQuitButton != null
        && settingsPanel != null && overlay.GetComponent<MenuPanelTransition>() != null;

    private void Awake()
    {
        if (!HasRequiredReferences)
        {
            Debug.LogError("PauseMenu needs its page, button, and settings references.", this);
            enabled = false;
            return;
        }

        pauseAction = new InputAction("PauseMenu", InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.performed += OnPause;
        cancelAction = new InputAction("BackFromPauseMenu", InputActionType.Button);
        cancelAction.AddBinding(PlayerKeyBindings.GamepadCancelPath);
        cancelAction.performed += OnCancel;
        overlayCanvas.sortingLayerID = overlayCanvas.rootCanvas.sortingLayerID;
        presentation = overlay.GetComponent<MenuPanelTransition>();
        overlay.SetActive(false);
    }

    private void OnEnable()
    {
        if (!HasRequiredReferences) return;
        InputSystem.onBeforeUpdate += CapturePauseAvailability;
        pauseAction.Enable();
        resumeButton.onClick.AddListener(Close);
        settingsButton.onClick.AddListener(ShowSettings);
        controlsButton.onClick.AddListener(ShowControls);
        quitButton.onClick.AddListener(ShowQuitConfirmation);
        controlsBackButton.onClick.AddListener(Back);
        cancelQuitButton.onClick.AddListener(Back);
        confirmQuitButton.onClick.AddListener(QuitGame);
        settingsPanel.BackRequested += Back;
    }

    private void OnDisable()
    {
        if (IsOpen) GameSettings.Save();
        FinishClose();
        InputSystem.onBeforeUpdate -= CapturePauseAvailability;
        pauseAction?.Disable();
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Close);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(ShowSettings);
        if (controlsButton != null) controlsButton.onClick.RemoveListener(ShowControls);
        if (quitButton != null) quitButton.onClick.RemoveListener(ShowQuitConfirmation);
        if (controlsBackButton != null) controlsBackButton.onClick.RemoveListener(Back);
        if (cancelQuitButton != null) cancelQuitButton.onClick.RemoveListener(Back);
        if (confirmQuitButton != null) confirmQuitButton.onClick.RemoveListener(QuitGame);
        if (settingsPanel != null) settingsPanel.BackRequested -= Back;
    }

    private void OnDestroy()
    {
        pauseAction?.Dispose();
        cancelAction?.Dispose();
    }

    private void Update()
    {
        if (IsOpen && (player == null || !player.isActiveAndEnabled || player.health.IsDead
            || SceneTransitionOverlay.IsTransitioning || !overlay.activeSelf))
            FinishClose();
    }

    private void CapturePauseAvailability()
    {
        // Escape closing a shop must not also open this menu during the same input update.
        blockedAtInputUpdate = GameplayPause.IsActive && !HitStopManager.IsActive;
    }

    private void OnPause(InputAction.CallbackContext context)
    {
        if (closing) return;
        if (IsOpen && controlsPage.activeInHierarchy
            && controlsPage.TryGetComponent(out ControlsGuideFeedback feedback)
            && feedback.DeferKeyboardBack(context, Back)) return;
        if (IsOpen) Back();
        else if (!blockedAtInputUpdate) Open();
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (controlsPage.activeInHierarchy && controlsPage.TryGetComponent(out ControlsGuideFeedback feedback)
            && feedback.ConsumesGamepadBack(context)) return;
        Back();
    }

    public void Open()
    {
        if (IsOpen || !isActiveAndEnabled || !HasRequiredReferences || SceneTransitionOverlay.IsTransitioning
            || VideoCutsceneOverlay.IsPlaying || (GameplayPause.IsActive && !HitStopManager.IsActive)) return;
        player = FindFirstObjectByType<Player>();
        if (player == null || !player.isActiveAndEnabled || player.health == null || player.health.IsDead) return;

        HitStopManager.PrepareForPause();
        if (!GameplayPause.TryAcquire(this, out pause)) return;
        pause.PauseAllPlayers();
        pause.PauseAudio();
        IsOpen = true;
        closing = false;
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (EventSystem.current != null)
            previousSelection = EventSystem.current.currentSelectedGameObject;
        overlay.SetActive(true);
        cancelAction.Enable();
        ShowMain(resumeButton);
    }

    public void Close()
    {
        if (!IsOpen || closing) return;
        UIAudio.PlayBack();
        GameSettings.Save();
        closing = true;
        cancelAction.Disable();
        presentation.Hide(FinishClose);
    }

    private void FinishClose()
    {
        if (!IsOpen) return;
        presentation.ResetImmediate();
        IsOpen = false;
        closing = false;
        cancelAction.Disable();
        if (overlay != null) overlay.SetActive(false);
        pause?.Dispose();
        pause = null;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null
                && previousSelection.activeInHierarchy ? previousSelection : null);
        previousSelection = null;
        player = null;
    }

    public void Back()
    {
        if (!IsOpen || closing || presentation.IsTransitioning) return;
        if (controlsPage.activeInHierarchy && controlsPage.GetComponent<ControlsGuideFeedback>().TryCloseConfirmation()) return;
        if (settingsPage.activeInHierarchy && settingsPanel.TryCloseConfirmation()) return;
        if (controlsPage.activeInHierarchy && controlsPage.GetComponent<ControlsGuideFeedback>().TryCancelRebind()) return;
        UIAudio.PlayBack();
        if (settingsPage.activeSelf)
        {
            if (settingsPanel.TryCloseDropdown()) return;
            GameSettings.Save();
            ShowMain(settingsButton);
        }
        else if (controlsPage.activeSelf) ShowMain(controlsButton);
        else if (quitPage.activeSelf) ShowMain(quitButton);
        else Close();
    }

    public void ShowSettings()
    {
        if (!IsOpen || closing || presentation.IsTransitioning) return;
        ShowPage(settingsPage, "설정", settingsPanel.FirstSelection);
    }

    public void ShowControls()
    {
        if (IsOpen && !closing && !presentation.IsTransitioning)
            ShowPage(controlsPage, "조작 안내", controlsPage.GetComponent<ControlsGuideFeedback>().FirstSelection);
    }

    public void ShowQuitConfirmation()
    {
        if (IsOpen && !closing && !presentation.IsTransitioning) ShowPage(quitPage, "게임 종료", cancelQuitButton);
    }

    private void ShowMain(Selectable selection) => ShowPage(mainPage, "일시정지", selection);

    private void ShowPage(GameObject page, string heading, Selectable selection)
    {
        mainPage.SetActive(page == mainPage);
        settingsPage.SetActive(page == settingsPage);
        controlsPage.SetActive(page == controlsPage);
        quitPage.SetActive(page == quitPage);
        title.text = heading;
        title.gameObject.SetActive(page == mainPage || page == quitPage);
        presentation.Show();
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selection.gameObject);
        }
    }

    private void QuitGame()
    {
        if (!IsOpen || closing || presentation.IsTransitioning || !quitPage.activeSelf) return;
        PlayerProgressPersistence.RetryPendingSave(immediately: true);
        FinishClose();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

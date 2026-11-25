using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public sealed class TitleMenu : MonoBehaviour
{
    [Header("Game Start")]
    [SerializeField] private SceneReference startScene = new SceneReference();
    [SerializeField] private VideoClip prologueVideo;
    [SerializeField] private SceneReference prologueScene = new SceneReference();
    [SerializeField, Min(0.01f)] private float prologueDuration = 9.5f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.35f;

    [Header("Pages")]
    [SerializeField] private CanvasGroup menuGroup;
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject modal;
    [SerializeField] private GameObject settingsPage;
    [SerializeField] private GameObject controlsPage;
    [SerializeField] private GameObject quitPage;
    [SerializeField] private Text modalTitle;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button controlsBackButton;
    [SerializeField] private Button cancelQuitButton;
    [SerializeField] private Button confirmQuitButton;

    [Header("Settings")]
    [SerializeField] private GameSettingsPanel settingsPanel;

    private InputAction cancelAction;
    private bool isLoading;
    private bool isSwitching;
    private MenuPanelTransition mainPresentation;
    private MenuPanelTransition modalPresentation;

    private bool HasRequiredReferences => menuGroup != null && mainPage != null && modal != null
        && controlsPage != null && controlsButton != null && controlsBackButton != null
        && settingsPage != null && quitPage != null && modalTitle != null && startButton != null
        && settingsButton != null && quitButton != null && cancelQuitButton != null
        && confirmQuitButton != null && settingsPanel != null
        && mainPage.GetComponent<MenuPanelTransition>() != null && modal.GetComponent<MenuPanelTransition>() != null;

    private void Awake()
    {
        if (!HasRequiredReferences)
        {
            Debug.LogError("TitleMenu needs its page, button, and settings references.", this);
            enabled = false;
            return;
        }

        cancelAction = new InputAction("BackFromTitleMenu", InputActionType.Button);
        cancelAction.AddBinding("<Keyboard>/escape");
        cancelAction.AddBinding(PlayerKeyBindings.GamepadCancelPath);
        cancelAction.performed += OnCancel;
        mainPresentation = mainPage.GetComponent<MenuPanelTransition>();
        modalPresentation = modal.GetComponent<MenuPanelTransition>();
        mainPage.SetActive(true);
        modal.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnEnable()
    {
        if (!HasRequiredReferences) return;
        cancelAction.Enable();
        startButton.onClick.AddListener(StartGame);
        settingsButton.onClick.AddListener(ShowSettings);
        controlsButton.onClick.AddListener(ShowControls);
        quitButton.onClick.AddListener(ShowQuitConfirmation);
        controlsBackButton.onClick.AddListener(Back);
        cancelQuitButton.onClick.AddListener(Back);
        confirmQuitButton.onClick.AddListener(QuitGame);
        settingsPanel.BackRequested += Back;
    }

    private void Start()
    {
        mainPresentation.Show();
        Select(startButton);
    }

    private void OnDisable()
    {
        GameSettings.Save();
        cancelAction?.Disable();
        mainPresentation?.ResetImmediate();
        modalPresentation?.ResetImmediate();
        isSwitching = false;
        if (startButton != null) startButton.onClick.RemoveListener(StartGame);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(ShowSettings);
        if (controlsButton != null) controlsButton.onClick.RemoveListener(ShowControls);
        if (quitButton != null) quitButton.onClick.RemoveListener(ShowQuitConfirmation);
        if (controlsBackButton != null) controlsBackButton.onClick.RemoveListener(Back);
        if (cancelQuitButton != null) cancelQuitButton.onClick.RemoveListener(Back);
        if (confirmQuitButton != null) confirmQuitButton.onClick.RemoveListener(QuitGame);
        if (settingsPanel != null) settingsPanel.BackRequested -= Back;
    }

    private void OnDestroy() => cancelAction?.Dispose();

    private void OnValidate()
    {
#if UNITY_EDITOR
        startScene?.Validate();
        prologueScene?.Validate();
#endif
    }

    public void StartGame()
    {
        if (isLoading || isSwitching || mainPresentation.IsTransitioning || !isActiveAndEnabled || SceneTransitionOverlay.IsTransitioning) return;
        if (startScene == null || !startScene.IsAssigned || !Application.CanStreamedLevelBeLoaded(startScene.Path))
        {
            Debug.LogError("Assign a playable start scene to TitleMenu and add it to Build Settings.", this);
            return;
        }

        if (prologueVideo == null || prologueScene == null || !prologueScene.IsAssigned
            || !Application.CanStreamedLevelBeLoaded(prologueScene.Path))
        {
            Debug.LogError("Assign the Prologue video and its playable cutscene scene to TitleMenu.", this);
            return;
        }
        GameSettings.Save();
        PlayerProgressPersistence.RetryPendingSave(immediately: true);
        PlayerRuntimeState.ResetRunProgress();
        isLoading = VideoCutsceneOverlay.TryPlay(prologueVideo, completed =>
        {
            bool playIntro = completed && !LowerTownIntro.ShouldAutoSkip;
            if (playIntro) LowerTownIntro.RequestPlayback();
            LoadOpeningScene(playIntro ? prologueScene.Path : startScene.Path);
        }, 0.15f, fadeOutDuration, playbackLimit: prologueDuration, pauseAudio: false,
            onSkipped: () => LoadOpeningScene(startScene.Path), cutsceneId: CutsceneId.Prologue);
        menuGroup.interactable = !isLoading;
    }

    private void LoadOpeningScene(string path)
    {
        if (SceneTransitionOverlay.TryLoadScene(path, 0f, fadeInDuration, Color.black, pauseAudio: false)) return;
        LowerTownIntro.CancelPlaybackRequest();
        isLoading = false;
        menuGroup.interactable = true;
    }

    public void ShowSettings()
    {
        if (isLoading || isSwitching || mainPresentation.IsTransitioning || SceneTransitionOverlay.IsTransitioning) return;
        ShowPage(settingsPage, "설정", settingsPanel.FirstSelection);
    }

    public void ShowControls()
    {
        if (!isLoading && !isSwitching && !mainPresentation.IsTransitioning && !SceneTransitionOverlay.IsTransitioning)
            ShowPage(controlsPage, "조작 안내", controlsPage.GetComponent<ControlsGuideFeedback>().FirstSelection);
    }

    public void ShowQuitConfirmation()
    {
        if (!isLoading && !isSwitching && !mainPresentation.IsTransitioning && !SceneTransitionOverlay.IsTransitioning)
            ShowPage(quitPage, "게임 종료", cancelQuitButton);
    }

    public void Back()
    {
        if (isLoading || isSwitching || mainPresentation.IsTransitioning || SceneTransitionOverlay.IsTransitioning) return;
        if (controlsPage.activeInHierarchy && controlsPage.GetComponent<ControlsGuideFeedback>().TryCloseConfirmation()) return;
        if (settingsPage.activeInHierarchy && settingsPanel.TryCloseConfirmation()) return;
        if (controlsPage.activeInHierarchy && controlsPage.GetComponent<ControlsGuideFeedback>().TryCancelRebind()) return;
        UIAudio.PlayBack();
        if (settingsPage.activeInHierarchy)
        {
            if (settingsPanel.TryCloseDropdown()) return;
            GameSettings.Save();
            ShowMain(settingsButton);
        }
        else if (controlsPage.activeInHierarchy) ShowMain(controlsButton);
        else if (quitPage.activeInHierarchy) ShowMain(quitButton);
        else ShowQuitConfirmation();
    }

    private void ShowMain(Selectable selection)
    {
        isSwitching = true;
        modalPresentation.Hide(() =>
        {
            modal.SetActive(false);
            mainPage.SetActive(true);
            mainPresentation.Show(() => isSwitching = false);
            Select(selection);
        });
    }

    private void ShowPage(GameObject page, string heading, Selectable selection)
    {
        isSwitching = true;
        if (modal.activeSelf)
        {
            ActivatePage(page, heading, selection);
            return;
        }
        mainPresentation.Hide(() => ActivatePage(page, heading, selection));
    }

    private void ActivatePage(GameObject page, string heading, Selectable selection)
    {
        mainPage.SetActive(false);
        settingsPage.SetActive(page == settingsPage);
        controlsPage.SetActive(page == controlsPage);
        quitPage.SetActive(page == quitPage);
        modalTitle.text = heading;
        modalTitle.gameObject.SetActive(page == quitPage);
        modal.SetActive(true);
        modalPresentation.Show(() => isSwitching = false);
        Select(selection);
    }

    private void Select(Selectable selection)
    {
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selection.gameObject);
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (controlsPage.activeInHierarchy && controlsPage.TryGetComponent(out ControlsGuideFeedback feedback)
            && (feedback.ConsumesGamepadBack(context) || feedback.DeferKeyboardBack(context, Back))) return;
        Back();
    }

    private void QuitGame()
    {
        if (isLoading || isSwitching || !quitPage.activeInHierarchy) return;
        GameSettings.Save();
        PlayerProgressPersistence.RetryPendingSave(immediately: true);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

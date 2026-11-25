using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Shared by the title and pause menus; all controls are editable Prefab objects.
public sealed class GameSettingsPanel : MonoBehaviour
{
    [SerializeField] private RectTransform[] controlColumns;
    [SerializeField] private Button[] resetButtons;
    [SerializeField] private Slider[] volumeSliders;
    [SerializeField] private Text[] volumeLabels;
    [SerializeField] private Dropdown displayMode;
    [SerializeField] private Dropdown resolution;
    [SerializeField] private Toggle vSync;
    [SerializeField] private Toggle interactionKeys;
    [SerializeField] private Toggle damageNumbers;
    [SerializeField] private Toggle enemyHealthBars;
    [SerializeField] private Toggle bossHealthBar;
    [SerializeField] private Toggle autoSkipWatchedCutscenes;
    [SerializeField] private Button backButton;
    [SerializeField] private MenuConfirmationDialog displayConfirmation;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private InputAction switchPaneAction;
    private Text navigationHint;
    private float displayConfirmationDeadline;
    public Selectable FirstSelection => volumeSliders[0];
    public event Action BackRequested;

    private void Awake()
    {
        navigationHint = transform.Find("NavigationHint")?.GetComponent<Text>();
        switchPaneAction = new InputAction("SwitchSettingsPane", InputActionType.Button, "<Keyboard>/tab");
        switchPaneAction.AddBinding("<Gamepad>/rightShoulder");
        switchPaneAction.performed += _ => SwitchFocusPane();
        for (int i = 0; i < resetButtons.Length; i++)
        {
            int index = i;
            resetButtons[i].onClick.AddListener(() =>
            {
                if ((SettingsCategory)index == SettingsCategory.Display)
                    PreviewDisplay(Screen.currentResolution.width, Screen.currentResolution.height, true, resetButtons[index], true);
                else GameSettings.ResetCategory((SettingsCategory)index);
            });
        }
        for (int i = 0; i < volumeSliders.Length; i++)
        {
            int index = i;
            volumeSliders[i].onValueChanged.AddListener(value =>
            {
                if (index == 0) GameSettings.SetMasterVolume(value);
                else GameSettings.SetChannelVolume((GameAudioChannel)(index - 1), value);
            });
        }
        displayMode.onValueChanged.AddListener(value => PreviewDisplay(GameSettings.ResolutionWidth,
            GameSettings.ResolutionHeight, value == 0, displayMode));
        resolution.onValueChanged.AddListener(value => PreviewDisplay(resolutions[value].x,
            resolutions[value].y, GameSettings.Fullscreen, resolution));
        vSync.onValueChanged.AddListener(GameSettings.SetVSync);
        interactionKeys.onValueChanged.AddListener(GameSettings.SetInteractionKeys);
        damageNumbers.onValueChanged.AddListener(GameSettings.SetDamageNumbers);
        enemyHealthBars.onValueChanged.AddListener(GameSettings.SetEnemyHealthBars);
        bossHealthBar.onValueChanged.AddListener(GameSettings.SetBossHealthBar);
        autoSkipWatchedCutscenes.onValueChanged.AddListener(GameSettings.SetAutoSkipWatchedCutscenes);
        backButton.onClick.AddListener(() => { if (!TryCloseConfirmation()) BackRequested?.Invoke(); });
    }

    private void OnEnable()
    {
        switchPaneAction.Enable();
        LastInputDevice.Changed += RefreshNavigationHint;
        RefreshNavigationHint();
        GameSettings.Changed += Refresh;
        RefreshResolutions();
        ConfigureNavigation();
        Refresh();
    }

    private void OnDisable()
    {
        displayConfirmation.TryCancel();
        switchPaneAction.Disable();
        LastInputDevice.Changed -= RefreshNavigationHint;
        GameSettings.Changed -= Refresh;
        GameSettings.Save();
    }

    private void OnDestroy() => switchPaneAction?.Dispose();

    private void RefreshNavigationHint()
    {
        if (navigationHint == null) return;
        string shoulder = PlayerKeyBindings.GamepadDisplayName("<Gamepad>/rightShoulder");
        navigationHint.text = LastInputDevice.IsGamepad ? $"{shoulder} 분류 이동" : "Tab 분류 이동";
    }

    private void SwitchFocusPane()
    {
        if (displayConfirmation.IsOpen) return;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;
        UIAudio.PlayMove();

        GameObject selected = eventSystem.currentSelectedGameObject;
        TryCloseDropdown();
        for (int i = 0; i < controlColumns.Length; i++)
        {
            if (selected == null || !selected.transform.IsChildOf(controlColumns[i])) continue;
            FirstControl(controlColumns[(i + 1) % controlColumns.Length]).Select();
            return;
        }
        FirstSelection.Select();
    }

    private static Selectable FirstControl(RectTransform column) =>
        column.GetComponentsInChildren<Selectable>().First(control => control.IsActive() && control.IsInteractable());

    private void ConfigureNavigation()
    {
        for (int column = 0; column < controlColumns.Length; column++)
        {
            // Dropdown templates contain Toggles that are not part of the board navigation.
            var controls = controlColumns[column].GetComponentsInChildren<Selectable>()
                .Where(control => control.IsActive() && control.IsInteractable()).ToArray();
            for (int row = 0; row < controls.Length; row++)
                SetNavigation(controls[row], row > 0 ? controls[row - 1] : null,
                    row + 1 < controls.Length ? controls[row + 1] : backButton,
                    column > 0 ? FirstControl(controlColumns[column - 1]) : null,
                    column + 1 < controlColumns.Length ? FirstControl(controlColumns[column + 1]) : null);
        }
        SetNavigation(backButton, resetButtons[resetButtons.Length - 1], null, resetButtons[0]);
    }

    private static void SetNavigation(Selectable control, Selectable up, Selectable down,
        Selectable left = null, Selectable right = null)
    {
        // uGUI sliders change value only when that direction has no navigation target.
        if (control is Slider)
        {
            left = null;
            right = null;
        }
        control.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
    }

    private void RefreshResolutions()
    {
        resolutions.Clear();
        resolutions.AddRange(Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct());
        var current = new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight);
        if (!resolutions.Contains(current)) resolutions.Add(current);
        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        resolution.ClearOptions();
        resolution.AddOptions(resolutions.Select(r => $"{r.x} × {r.y}").ToList());
    }

    private void Refresh()
    {
        float[] volumes = { GameSettings.MasterVolume, GameSettings.MusicVolume,
            GameSettings.EffectsVolume, GameSettings.UIVolume };
        for (int i = 0; i < volumes.Length; i++)
        {
            volumeSliders[i].SetValueWithoutNotify(volumes[i]);
            volumeLabels[i].text = Percent(volumes[i]);
        }
        displayMode.SetValueWithoutNotify(GameSettings.Fullscreen ? 0 : 1);
        var current = new Vector2Int(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight);
        if (!resolutions.Contains(current)) RefreshResolutions();
        resolution.SetValueWithoutNotify(Mathf.Max(0, resolutions.IndexOf(current)));
        vSync.SetIsOnWithoutNotify(GameSettings.VSync);
        interactionKeys.SetIsOnWithoutNotify(GameSettings.ShowInteractionKeys);
        damageNumbers.SetIsOnWithoutNotify(GameSettings.ShowDamageNumbers);
        enemyHealthBars.SetIsOnWithoutNotify(GameSettings.ShowEnemyHealthBars);
        bossHealthBar.SetIsOnWithoutNotify(GameSettings.ShowBossHealthBar);
        autoSkipWatchedCutscenes.SetIsOnWithoutNotify(GameSettings.AutoSkipWatchedCutscenes);
    }

    private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

    private void PreviewDisplay(int width, int height, bool fullscreen, Selectable selection, bool resetVSync = false)
    {
        if (displayConfirmation.IsOpen) return;
        if (width == GameSettings.ResolutionWidth && height == GameSettings.ResolutionHeight
            && fullscreen == GameSettings.Fullscreen)
        {
            if (resetVSync) GameSettings.SetVSync(true);
            return;
        }

        var previousResolution = new Vector2Int(Screen.width, Screen.height);
        FullScreenMode previousMode = Screen.fullScreenMode;
        Screen.SetResolution(width, height, fullscreen);
        // Keep unconfirmed display changes out of PlayerPrefs, even if another system saves.
        displayConfirmationDeadline = Time.unscaledTime + 15f;
        displayConfirmation.Show("화면 설정 확인", "변경한 화면 설정을 유지할까요?",
            "15초 후 이전 설정으로 돌아갑니다.", "유지", "되돌리기", selection, accepted =>
            {
                displayConfirmationDeadline = 0f;
                if (accepted)
                {
                    GameSettings.SetFullscreen(fullscreen);
                    GameSettings.SetResolution(width, height);
                    if (resetVSync) GameSettings.SetVSync(true);
                    GameSettings.Save();
                }
                else Screen.SetResolution(previousResolution.x, previousResolution.y, previousMode);
                Refresh();
            });
    }

    private void Update()
    {
        if (!displayConfirmation.IsOpen) return;
        int seconds = Mathf.Clamp(Mathf.CeilToInt(displayConfirmationDeadline - Time.unscaledTime), 0, 15);
        if (seconds <= 0) displayConfirmation.TryCancel();
        else displayConfirmation.SetDetail($"{seconds}초 후 이전 설정으로 돌아갑니다.");
    }

    public bool TryCloseConfirmation() => displayConfirmation.TryCancel();

    public bool TryCloseDropdown()
    {
        foreach (Dropdown dropdown in new[] { displayMode, resolution })
        {
            // uGUI adds an active child Canvas for the open option list.
            if (dropdown.GetComponentInChildren<Canvas>() != null)
            {
                dropdown.Hide();
                UIAudio.PlayBack();
                return true;
            }
        }
        return false;
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UpgradeShop : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private InteractableNPC merchant;
    [SerializeField] private InteractionKeyGuide interactionKeyGuide;
    [SerializeField] private Player player;

    [Header("UI")]
    [SerializeField] private GameObject panel;
    [UnityEngine.Serialization.FormerlySerializedAs("coinsLabel")]
    [SerializeField] private Text scrapLabel;
    [SerializeField] private Text attackLabel;
    [SerializeField] private Text healthLabel;
    [SerializeField] private Button attackButton;
    [SerializeField] private Button healthButton;
    [SerializeField] private Button materialButton;
    [SerializeField] private Button hitInvincibilityButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button closeButton;

    private InputAction cancelAction;
    private InputAction confirmAction;
    private GameplayPause.Handle pause;
    private bool restoreGuide;
    private GameObject previousSelection;
    private UpgradeShopView view;
    private MenuPanelTransition presentation;
    private bool closing;
    private WeaponSkinShop skinShop;
    private Text controlsHint;
    public PlayerWeaponController WeaponController => player != null ? player.GetComponent<PlayerWeaponController>() : null;

    public bool IsOpen { get; private set; }
    public bool HasRequiredReferences => merchant != null && player != null
        && panel != null && scrapLabel != null && attackLabel != null && healthLabel != null
        && attackButton != null && healthButton != null && materialButton != null
        && hitInvincibilityButton != null && confirmButton != null && closeButton != null
        && panel.TryGetComponent(out UpgradeShopView panelView) && panelView.HasRequiredReferences
        && panel.GetComponent<MenuPanelTransition>() != null;

    private void Awake()
    {
        if (!HasRequiredReferences)
        {
            Debug.LogError($"{nameof(UpgradeShop)} needs its Interaction and UI references. Use Create Shop for Selected NPC to connect a Scene instance.", this);
            enabled = false;
            return;
        }
        view = panel.GetComponent<UpgradeShopView>();
        presentation = panel.GetComponent<MenuPanelTransition>();
        skinShop = panel.GetComponent<WeaponSkinShop>();
        controlsHint = panel.transform.Find("Card/ControlsHint")?.GetComponent<Text>();
        cancelAction = new InputAction("CloseShop", InputActionType.Button);
        cancelAction.AddBinding("<Keyboard>/escape");
        cancelAction.AddBinding(PlayerKeyBindings.GamepadCancelPath);
        cancelAction.performed += OnCancel;
        confirmAction = new InputAction("ConfirmShop", InputActionType.Button, "<Keyboard>/z");
        confirmAction.performed += OnConfirm;
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (!HasRequiredReferences) { enabled = false; return; }
        merchant.OnInteract.AddListener(Open);
        attackButton.onClick.AddListener(SelectAttack);
        healthButton.onClick.AddListener(SelectHealth);
        materialButton.onClick.AddListener(PurchaseMaterial);
        hitInvincibilityButton.onClick.AddListener(SelectHitInvincibility);
        confirmButton.onClick.AddListener(PurchaseSelected);
        closeButton.onClick.AddListener(Close);
        PlayerPermanentProgress.ScrapChanged += OnScrapChanged;
        PlayerPermanentProgress.UpgradesChanged += Refresh;
        LastInputDevice.Changed += RefreshControlsHint;
        RefreshControlsHint();
    }

    private void OnDisable()
    {
        FinishClose();
        if (merchant != null) merchant.OnInteract.RemoveListener(Open);
        if (attackButton != null) attackButton.onClick.RemoveListener(SelectAttack);
        if (healthButton != null) healthButton.onClick.RemoveListener(SelectHealth);
        if (materialButton != null) materialButton.onClick.RemoveListener(PurchaseMaterial);
        if (hitInvincibilityButton != null) hitInvincibilityButton.onClick.RemoveListener(SelectHitInvincibility);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(PurchaseSelected);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        PlayerPermanentProgress.ScrapChanged -= OnScrapChanged;
        PlayerPermanentProgress.UpgradesChanged -= Refresh;
        LastInputDevice.Changed -= RefreshControlsHint;
    }

    private void OnDestroy()
    {
        cancelAction?.Dispose();
        confirmAction?.Dispose();
    }

    private void Update()
    {
        if (IsOpen && (player == null || !player.isActiveAndEnabled || player.health.IsDead ||
            merchant == null || !merchant.isActiveAndEnabled || panel == null || !panel.activeSelf))
            FinishClose();
    }

    public void Open()
    {
        // A screen may take over a hit stop, but must not steal another screen's pause.
        if (IsOpen || !isActiveAndEnabled || player == null || !player.isActiveAndEnabled ||
            player.health.IsDead || (Time.timeScale <= 0f && !HitStopManager.IsActive)) return;

        HitStopManager.PrepareForPause();
        if (!GameplayPause.TryAcquire(this, out pause)) return;
        IsOpen = true;
        closing = false;
        pause.PausePlayer(player);
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        restoreGuide = interactionKeyGuide != null && interactionKeyGuide.gameObject.activeSelf;
        if (interactionKeyGuide != null) interactionKeyGuide.gameObject.SetActive(false);
        panel.SetActive(true);
        skinShop?.Open(this);
        presentation.Show();
        Refresh();
        cancelAction.Enable();
        confirmAction.Enable();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(materialButton.interactable
                ? materialButton.gameObject : closeButton.gameObject);
        }
    }

    public void Close()
    {
        if (!IsOpen || closing) return;
        UIAudio.PlayBack();
        closing = true;
        cancelAction.Disable();
        confirmAction.Disable();
        presentation.Hide(FinishClose);
    }

    private void FinishClose()
    {
        if (!IsOpen) return;
        presentation.ResetImmediate();
        IsOpen = false;
        closing = false;
        cancelAction.Disable();
        confirmAction.Disable();
        if (panel != null) panel.SetActive(false);
        pause?.Dispose();
        pause = null;
        if (restoreGuide && interactionKeyGuide != null && merchant != null && merchant.isActiveAndEnabled)
            interactionKeyGuide.Show();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    public bool CanPurchase => IsOpen && !closing && !presentation.IsTransitioning;

    private void SelectAttack() => SelectUpgrade(UpgradeKind.Attack);
    private void SelectHealth() => SelectUpgrade(UpgradeKind.Health);
    private void SelectHitInvincibility() => SelectUpgrade(UpgradeKind.HitInvincibility);

    private void SelectUpgrade(UpgradeKind kind)
    {
        if (!CanPurchase) return;
        view.SelectUpgrade(kind);
        RefreshNavigation();
    }

    public void PurchaseSelected()
    {
        if (view.SelectedUpgrade == UpgradeKind.Attack) PurchaseAttack();
        else if (view.SelectedUpgrade == UpgradeKind.Health) PurchaseHealth();
        else PurchaseHitInvincibility();
    }

    public void PurchaseMaterial()
    {
        if (CanPurchase && PlayerPermanentProgress.TryPurchaseMaterial()) view.PlayMaterialPurchaseFeedback();
    }

    public void PurchaseAttack()
    {
        if (CanPurchase && PlayerPermanentProgress.TryPurchaseAttackUpgrade()) view.PlayAttackPurchaseFeedback();
    }

    public void PurchaseHealth()
    {
        if (CanPurchase && PlayerPermanentProgress.TryPurchaseHealthUpgrade()) view.PlayHealthPurchaseFeedback();
    }

    public void PurchaseHitInvincibility()
    {
        if (CanPurchase && PlayerPermanentProgress.TryPurchaseHitInvincibilityUpgrade()) view.PlayHitInvincibilityPurchaseFeedback();
    }

    private void OnCancel(InputAction.CallbackContext context) => Close();
    private void RefreshControlsHint()
    {
        if (controlsHint == null) return;
        string submit = PlayerKeyBindings.GamepadDisplayName(PlayerKeyBindings.GamepadSubmitPath);
        string cancel = PlayerKeyBindings.GamepadDisplayName(PlayerKeyBindings.GamepadCancelPath);
        controlsHint.text = LastInputDevice.IsGamepad
            ? $"스틱 / 방향키 이동 · {submit} 선택 · {cancel} 닫기" : "방향키 이동 · Z / Enter 선택 · Esc 닫기";
    }
    private void OnConfirm(InputAction.CallbackContext context)
    {
        if (!CanPurchase || EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(panel.transform)) return;
        ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }
    private void OnScrapChanged(int scrap) => Refresh();

    private void Refresh()
    {
        if (!IsOpen) return;
        scrapLabel.text = $"보유 고철  <color=#FFC36B>{PlayerPermanentProgress.Scrap:N0}</color>";
        view.Refresh();
        materialButton.interactable = PlayerPermanentProgress.CanPurchaseMaterial;
        attackButton.interactable = healthButton.interactable = hitInvincibilityButton.interactable = true;

        RefreshNavigation();
    }

    public void RefreshNavigation()
    {
        var activeButtons = new System.Collections.Generic.List<Button>();
        skinShop?.AddNavigationButtons(activeButtons);
        if (skinShop == null || !skinShop.ShowingSkins)
            activeButtons.AddRange(new[] { attackButton, healthButton, hitInvincibilityButton, materialButton, confirmButton });
        activeButtons.Add(closeButton);
        Button[] buttons = activeButtons.ToArray();
        Canvas.ForceUpdateCanvases();
        for (int i = 0; i < buttons.Length; i++)
        {
            Button previous = null;
            Button next = null;
            for (int j = 1; j < buttons.Length; j++)
            {
                Button candidate = buttons[(i + j) % buttons.Length];
                if (next == null && candidate.gameObject.activeInHierarchy && candidate.interactable) next = candidate;
                candidate = buttons[(i - j + buttons.Length) % buttons.Length];
                if (previous == null && candidate.gameObject.activeInHierarchy && candidate.interactable) previous = candidate;
            }
            buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = previous, selectOnRight = next,
                selectOnUp = FindVerticalButton(buttons[i], buttons, 1f),
                selectOnDown = FindVerticalButton(buttons[i], buttons, -1f) };
        }
        if (EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        foreach (Button button in buttons)
        {
            if (selected == button.gameObject && button.isActiveAndEnabled && button.IsInteractable()) return;
        }
        Button fallback = materialButton.isActiveAndEnabled && materialButton.IsInteractable()
            ? materialButton : confirmButton.isActiveAndEnabled && confirmButton.IsInteractable()
            ? confirmButton : closeButton;
        EventSystem.current.SetSelectedGameObject(fallback.gameObject);
    }

    private Button FindVerticalButton(Button current, Button[] buttons, float direction)
    {
        var currentRect = (RectTransform)current.transform;
        Vector3 origin = panel.transform.InverseTransformPoint(currentRect.TransformPoint(currentRect.rect.center));
        Button best = null;
        float bestScore = 0f;
        foreach (Button candidate in buttons)
        {
            if (candidate == current || !candidate.gameObject.activeInHierarchy || !candidate.interactable) continue;
            var rect = (RectTransform)candidate.transform;
            Vector3 position = panel.transform.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
            Vector2 offset = position - origin;
            float verticalDistance = offset.y * direction;
            if (verticalDistance <= 0.01f) continue;
            float score = verticalDistance / offset.sqrMagnitude;
            if (score <= bestScore) continue;
            best = candidate;
            bestScore = score;
        }
        return best;
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Selectable))]
public sealed class UIAudioFeedback : MonoBehaviour, IPointerEnterHandler, ISelectHandler,
    IPointerClickHandler, ISubmitHandler
{
    private Selectable selectable;

    private void OnEnable()
    {
        selectable = GetComponent<Selectable>();
        if (selectable is Button button) button.onClick.AddListener(Clicked);
        else if (selectable is Toggle toggle) toggle.onValueChanged.AddListener(Toggled);
        else if (selectable is Slider slider) slider.onValueChanged.AddListener(Slid);
        else if (selectable is Dropdown dropdown) dropdown.onValueChanged.AddListener(OptionChanged);
    }

    private void OnDisable()
    {
        if (selectable is Button button) button.onClick.RemoveListener(Clicked);
        else if (selectable is Toggle toggle) toggle.onValueChanged.RemoveListener(Toggled);
        else if (selectable is Slider slider) slider.onValueChanged.RemoveListener(Slid);
        else if (selectable is Dropdown dropdown) dropdown.onValueChanged.RemoveListener(OptionChanged);
    }

    private bool CanPlay => selectable != null && selectable.IsActive() && selectable.IsInteractable();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CanPlay) UIAudio.PlayMove();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!CanPlay) return;
        // Automatic initial focus and focus restored after closing a panel remain silent.
        var module = EventSystem.current != null
            ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
        bool navigating = module != null && module.move != null && module.move.action != null
            && module.move.action.ReadValue<Vector2>().sqrMagnitude > 0.01f;
        if (eventData is PointerEventData || eventData is AxisEventData || navigating) UIAudio.PlayMove();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (CanPlay && selectable is Dropdown && eventData.button == PointerEventData.InputButton.Left)
            UIAudio.PlayClick();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (CanPlay && selectable is Dropdown) UIAudio.PlayClick();
    }

    private void Clicked() => UIAudio.PlayClick();
    private void Toggled(bool value) => UIAudio.PlayClick();
    private void Slid(float value) => UIAudio.PlayMove();
    private void OptionChanged(int value) => UIAudio.PlayClick();
}

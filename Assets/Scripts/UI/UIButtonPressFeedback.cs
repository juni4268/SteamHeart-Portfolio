using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerExitHandler, ISubmitHandler, IDeselectHandler
{
    [SerializeField, Min(0f)] private float depth = 1.5f;
    [SerializeField, Min(0.01f)] private float moveDuration = 0.04f;
    [SerializeField, Min(0.01f)] private float minimumHold = 0.08f;

    private Button button;
    private RectTransform rect;
    private Vector2 restPosition;
    private bool pointerHeld;
    private float pressedUntil;
    private float amount;

    private void OnEnable()
    {
        button = GetComponent<Button>();
        rect = (RectTransform)transform;
        restPosition = rect.anchoredPosition;
        pointerHeld = false;
        pressedUntil = 0f;
        amount = 0f;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !button.IsInteractable()) return;
        pointerHeld = true;
        pressedUntil = Time.unscaledTime + minimumHold;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) pointerHeld = false;
    }

    public void OnPointerExit(PointerEventData eventData) => pointerHeld = false;
    public void OnDeselect(BaseEventData eventData) => pointerHeld = false;

    public void OnSubmit(BaseEventData eventData)
    {
        if (button.IsInteractable()) pressedUntil = Time.unscaledTime + minimumHold;
    }

    private void LateUpdate()
    {
        bool pressed = button.IsInteractable() && (pointerHeld || Time.unscaledTime < pressedUntil);
        amount = Mathf.MoveTowards(amount, pressed ? 1f : 0f, Time.unscaledDeltaTime / moveDuration);
        rect.anchoredPosition = restPosition + Vector2.down * (depth * amount);
    }

    private void OnDisable()
    {
        if (rect != null) rect.anchoredPosition = restPosition;
    }
}

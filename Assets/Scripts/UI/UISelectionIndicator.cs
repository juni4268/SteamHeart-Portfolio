using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The outline belongs to the selectable's target graphic and is visible only while selected.
[DisallowMultipleComponent]
[RequireComponent(typeof(Selectable))]
public sealed class UISelectionIndicator : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Sprite selectedButtonSprite;
    private Outline outline;
    private Image targetImage;
    private Image marker;
    private Text label;
    private Color normalLabelColor;
    private Selectable selectable;

    private void OnEnable()
    {
        selectable = GetComponent<Selectable>();
        outline = selectable.targetGraphic != null ? selectable.targetGraphic.GetComponent<Outline>() : null;
        targetImage = selectable is Button ? selectable.targetGraphic as Image : null;
        marker = transform.Find("FocusMarker")?.GetComponent<Image>();
        label = targetImage != null ? transform.Find("Label")?.GetComponent<Text>() : null;
        if (label != null) normalLabelColor = label.color;
        SetVisible(EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject);
    }

    private void OnDisable() => SetVisible(false);
    public void OnSelect(BaseEventData eventData) => SetVisible(true);
    public void OnDeselect(BaseEventData eventData) => SetVisible(false);

    private void SetVisible(bool visible)
    {
        // Panel transitions temporarily block input while the initial selection is already visible.
        visible = visible && selectable != null && selectable.interactable;
        if (outline != null) outline.enabled = visible;
        if (marker != null) marker.enabled = visible;
        if (targetImage != null && selectedButtonSprite != null)
        {
            targetImage.overrideSprite = visible ? selectedButtonSprite : null;
            if (label != null) label.color = visible ? new Color(0.2f, 0.1f, 0.055f) : normalLabelColor;
        }
    }
}

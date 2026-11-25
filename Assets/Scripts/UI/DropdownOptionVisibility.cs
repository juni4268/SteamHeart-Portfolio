using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Dropdown positions its generated options after selecting the current value.
// Scroll once after that layout, and again whenever keyboard focus changes rows.
[DisallowMultipleComponent]
public sealed class DropdownOptionVisibility : MonoBehaviour, ISelectHandler
{
    private bool pending;

    public void OnSelect(BaseEventData eventData) => pending = true;
    private void OnDisable() => pending = false;

    private void LateUpdate()
    {
        if (!pending) return;
        pending = false;
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != gameObject) return;
        ScrollRect scroll = GetComponentInParent<ScrollRect>();
        if (scroll == null || scroll.viewport == null || scroll.content == null) return;

        Canvas.ForceUpdateCanvases();
        Bounds item = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, transform);
        Rect viewport = scroll.viewport.rect;
        float offset = item.min.y < viewport.yMin ? viewport.yMin - item.min.y :
            item.max.y > viewport.yMax ? viewport.yMax - item.max.y : 0f;
        if (Mathf.Abs(offset) < 0.01f) return;
        scroll.StopMovement();
        scroll.content.anchoredPosition += new Vector2(0, offset);
    }
}

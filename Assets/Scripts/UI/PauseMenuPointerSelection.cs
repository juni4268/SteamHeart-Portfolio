using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuPointerSelection : MonoBehaviour, IPointerEnterHandler
{
    private Selectable selectable;

    private void OnEnable() => selectable = GetComponent<Selectable>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable == null || !selectable.IsActive() || !selectable.IsInteractable()
            || EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(gameObject, eventData);
    }
}

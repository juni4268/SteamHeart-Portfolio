using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MenuConfirmationDialog : MonoBehaviour
{
    [SerializeField] private CanvasGroup backgroundControls;
    [SerializeField] private Text heading;
    [SerializeField] private Text message;
    [SerializeField] private Text detail;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action<bool> completed;
    private Selectable returnSelection;
    private bool previousInteractable;
    public bool IsOpen => completed != null;

    private void Awake()
    {
        confirmButton.onClick.AddListener(() => Finish(true));
        cancelButton.onClick.AddListener(() => Finish(false));
    }

    public void Show(string title, string question, string description, string confirm, string cancel,
        Selectable selection, Action<bool> onCompleted)
    {
        if (IsOpen) return;
        heading.text = title;
        message.text = question;
        detail.text = description;
        confirmButton.GetComponentInChildren<Text>().text = confirm;
        cancelButton.GetComponentInChildren<Text>().text = cancel;
        returnSelection = selection;
        previousInteractable = backgroundControls.interactable;
        backgroundControls.interactable = false;
        completed = onCompleted;
        gameObject.SetActive(true);
        cancelButton.Select();
    }

    public void SetDetail(string value) => detail.text = value;

    public bool TryCancel()
    {
        if (!IsOpen) return false;
        Finish(false);
        UIAudio.PlayBack();
        return true;
    }

    private void Finish(bool accepted)
    {
        if (!IsOpen) return;
        Action<bool> callback = completed;
        completed = null;
        backgroundControls.interactable = previousInteractable;
        gameObject.SetActive(false);
        if (returnSelection != null && returnSelection.IsActive()) returnSelection.Select();
        returnSelection = null;
        callback(accepted);
    }

    private void LateUpdate()
    {
        // Dropdown.Hide can restore its own selection after opening this dialog.
        if (!IsOpen || EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(transform)) cancelButton.Select();
    }

    private void OnDisable() => Finish(false);
}

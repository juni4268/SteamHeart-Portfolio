using System;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuPanelTransition : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform content;
    [SerializeField, Min(0f)] private float duration = 0.15f;
    [SerializeField, Min(0f)] private float travel = 4f;

    private Vector2 restPosition;
    private bool initialized;
    private Sequence transition;

    public bool IsTransitioning => transition != null && transition.IsActive();

    private void Initialize()
    {
        if (initialized) return;
        restPosition = content.anchoredPosition;
        initialized = true;
    }

    public void Show(Action completed = null)
    {
        Initialize();
        transition?.Kill();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = true;
        content.anchoredPosition = restPosition + Vector2.down * travel;
        transition = DOTween.Sequence().SetUpdate(true);
        transition.Append(group.DOFade(1f, duration).SetEase(Ease.OutQuad));
        transition.Join(content.DOAnchorPos(restPosition, duration).SetEase(Ease.OutQuad));
        transition.OnComplete(() =>
        {
            transition = null;
            group.interactable = true;
            completed?.Invoke();
        });
    }

    public void Hide(Action completed)
    {
        Initialize();
        transition?.Kill();
        group.interactable = false;
        // Keep the fading screen between the pointer and the game until its owner releases pause.
        group.blocksRaycasts = true;
        transition = DOTween.Sequence().SetUpdate(true);
        transition.Append(group.DOFade(0f, duration).SetEase(Ease.InQuad));
        transition.Join(content.DOAnchorPos(restPosition + Vector2.down * travel, duration).SetEase(Ease.InQuad));
        transition.OnComplete(() =>
        {
            transition = null;
            completed?.Invoke();
        });
    }

    public void ResetImmediate()
    {
        transition?.Kill();
        transition = null;
        if (!initialized) return;
        content.anchoredPosition = restPosition;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private void OnDisable() => ResetImmediate();
}

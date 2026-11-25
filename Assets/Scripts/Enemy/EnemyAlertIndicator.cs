using DG.Tweening;
using UnityEngine;

public class EnemyAlertIndicator : MonoBehaviour
{
    [Header("Alert")]
    [SerializeField] private GameObject alertObject;
    [SerializeField, Min(0f)] private float showDuration = 0.3f;
    [SerializeField, Min(1f)] private float popScale = 1.65f;
    [SerializeField, Min(0f)] private float visibleDuration = 0.5f;
    [SerializeField, Min(0f)] private float hideDuration = 0.2f;

    private Sequence alertSequence;
    private Transform alertTransform;
    private SpriteRenderer alertRenderer;
    private Vector3 originalLocalScale;
    private Color originalColor;

    private void Awake()
    {
        if (alertObject != null)
        {
            alertTransform = alertObject.transform;
            alertRenderer = alertObject.GetComponent<SpriteRenderer>();
            originalLocalScale = alertTransform.localScale;

            if (alertRenderer != null)
            {
                originalColor = alertRenderer.color;
            }
        }

        HideAlertObject();
    }

    private void OnDisable()
    {
        alertSequence?.Kill();
        alertSequence = null;
        HideAlertObject();
    }

    public void Show()
    {
        if (alertObject == null || alertTransform == null) return;

        alertSequence?.Kill();
        alertObject.SetActive(true);
        alertTransform.localScale = Vector3.zero;

        if (alertRenderer != null)
        {
            alertRenderer.color = WithAlpha(originalColor, 0f);
        }

        alertSequence = DOTween.Sequence();

        float expandDuration = showDuration * 0.65f;
        float settleDuration = showDuration - expandDuration;

        alertSequence
            .Append(alertTransform
                .DOScale(originalLocalScale * popScale, expandDuration)
                .SetEase(Ease.OutCubic))
            .Append(alertTransform
                .DOScale(originalLocalScale, settleDuration)
                .SetEase(Ease.InOutSine));

        if (alertRenderer != null)
        {
            alertSequence.Insert(0f,
                alertRenderer.DOFade(originalColor.a, showDuration * 0.5f));
        }

        alertSequence.AppendInterval(visibleDuration);

        alertSequence.Append(alertTransform
            .DOScale(Vector3.zero, hideDuration)
            .SetEase(Ease.InBack));

        if (alertRenderer != null)
        {
            alertSequence.Join(alertRenderer.DOFade(0f, hideDuration));
        }

        alertSequence
            .OnComplete(HideAlertObject)
            .OnKill(() => alertSequence = null)
            .SetLink(gameObject);
    }

    private void HideAlertObject()
    {
        if (alertObject != null)
        {
            if (alertTransform != null)
            {
                alertTransform.localScale = originalLocalScale;
            }

            if (alertRenderer != null)
            {
                alertRenderer.color = originalColor;
            }

            alertObject.SetActive(false);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}

using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnemyHealthBarView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image fill;
    [SerializeField] private Image damageFill;
    [SerializeField] private Image frame;
    [SerializeField] private CanvasGroup visibility;

    [Header("Damage Feedback")]
    [Tooltip("피격 후 줄어든 체력 구간을 잠깐 유지하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float damageDelay = 0.12f;
    [Tooltip("유지 시간이 지난 후 피해 잔상이 실제 체력까지 줄어드는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float damageDrainDuration = 0.25f;
    [Tooltip("피격 순간 테두리에 표시할 색상입니다.")]
    [SerializeField] private Color damageFlashColor = new Color(0.95f, 0.79f, 0.5f, 1f);
    [Tooltip("밝아진 테두리가 원래 색상으로 돌아오는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float damageFlashDuration = 0.08f;

    private bool hasHealth;
    private Color frameColor;
    private float damageStartRatio;
    private float feedbackElapsed;

    public RectTransform RectTransform => (RectTransform)transform;
    public bool HasRequiredReferences => fill != null && damageFill != null && frame != null && visibility != null;

    public void SetHealth(int previous, int current, int maximum)
    {
        if (!hasHealth)
        {
            frameColor = frame.color;
            damageFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)previous / Mathf.Max(1, maximum)), 1f);
            hasHealth = true;
        }

        damageStartRatio = damageFill.rectTransform.anchorMax.x;
        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)current / Mathf.Max(1, maximum)), 1f);
        feedbackElapsed = 0f;
        AdvanceFeedback(0f);
    }

    public void AdvanceFeedback(float deltaTime)
    {
        feedbackElapsed += deltaTime;
        float drainProgress = damageDrainDuration > 0f
            ? Mathf.Clamp01((feedbackElapsed - damageDelay) / damageDrainDuration)
            : feedbackElapsed >= damageDelay ? 1f : 0f;
        float ratio = Mathf.Lerp(damageStartRatio, fill.rectTransform.anchorMax.x, drainProgress);
        damageFill.rectTransform.anchorMax = new Vector2(ratio, 1f);

        float flashProgress = damageFlashDuration > 0f
            ? Mathf.Clamp01(feedbackElapsed / damageFlashDuration) : 1f;
        frame.color = Color.Lerp(damageFlashColor, frameColor, flashProgress);
    }

    public void SetOpacity(float alpha) => visibility.alpha = alpha;
}

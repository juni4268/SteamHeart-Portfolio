using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class EnemyHealthBarDisplay : MonoBehaviour
{
    private sealed class Bar
    {
        public EnemyHealthBar Target;
        public EnemyHealthBarView View;
        public float Elapsed;
    }

    private static EnemyHealthBarDisplay activeDisplay;

    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private EnemyHealthBarView barTemplate;

    [Header("Visibility")]
    [Tooltip("마지막 피격 후 체력바를 유지하는 시간(초)입니다. 일시정지 중에는 진행하지 않습니다.")]
    [SerializeField, Min(0f)] private float visibleDuration = 3f;
    [Tooltip("유지 시간이 지난 후 서서히 숨기는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;

    private readonly List<Bar> bars = new List<Bar>();
    private readonly Dictionary<EnemyHealthBar, Bar> barsByTarget = new Dictionary<EnemyHealthBar, Bar>();
    private RectTransform canvasRect;

    public static void Show(EnemyHealthBar target, int previous, int current, int maximum)
    {
        if (!Application.isPlaying || !GameSettings.ShowEnemyHealthBars || target == null || !target.CanDisplay) return;
        if (activeDisplay == null)
        {
            EnemyHealthBarDisplay prefab = Resources.Load<EnemyHealthBarDisplay>("UI/EnemyHealthBarDisplay");
            if (prefab == null || prefab.canvas == null || prefab.barTemplate == null
                || !prefab.barTemplate.HasRequiredReferences)
            {
                Debug.LogError("EnemyHealthBarDisplay prefab or its UI references are missing.");
                return;
            }
            activeDisplay = Instantiate(prefab);
        }
        activeDisplay.ShowBar(target, previous, current, maximum);
    }

    public static void Hide(EnemyHealthBar target)
    {
        if (activeDisplay == null || !activeDisplay.barsByTarget.TryGetValue(target, out Bar bar)) return;
        activeDisplay.barsByTarget.Remove(target);
        activeDisplay.bars.Remove(bar);
        bar.View.gameObject.SetActive(false);
        Destroy(bar.View.gameObject);
    }

    private void Awake()
    {
        canvasRect = (RectTransform)canvas.transform;
        barTemplate.gameObject.SetActive(false);
    }

    private void OnEnable() => GameSettings.Changed += ApplySettings;
    private void OnDisable() => GameSettings.Changed -= ApplySettings;

    private void ApplySettings()
    {
        if (!GameSettings.ShowEnemyHealthBars)
            for (int i = bars.Count - 1; i >= 0; i--) RemoveBar(i);
    }

    private void ShowBar(EnemyHealthBar target, int previous, int current, int maximum)
    {
        if (!barsByTarget.TryGetValue(target, out Bar bar))
        {
            bar = new Bar { Target = target, View = Instantiate(barTemplate, canvas.transform, false) };
            bar.View.name = "EnemyHealthBar";
            bars.Add(bar);
            barsByTarget.Add(target, bar);
        }
        bar.Elapsed = 0f;
        bar.View.SetHealth(previous, current, maximum);
        bar.View.SetOpacity(1f);
        UpdatePosition(bar, Camera.main);
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        for (int i = bars.Count - 1; i >= 0; i--)
        {
            Bar bar = bars[i];
            if (bar.Target == null || !bar.Target.CanDisplay)
            {
                RemoveBar(i);
                continue;
            }

            bar.Elapsed += Time.deltaTime;
            if (bar.Elapsed >= visibleDuration + fadeOutDuration)
            {
                RemoveBar(i);
                continue;
            }
            float alpha = fadeOutDuration > 0f && bar.Elapsed > visibleDuration
                ? 1f - (bar.Elapsed - visibleDuration) / fadeOutDuration : 1f;
            bar.View.AdvanceFeedback(Time.deltaTime);
            bar.View.SetOpacity(Mathf.Clamp01(alpha));
            UpdatePosition(bar, camera);
        }
    }

    private void UpdatePosition(Bar bar, Camera camera)
    {
        if (camera == null)
        {
            bar.View.gameObject.SetActive(false);
            return;
        }
        Vector3 point = camera.WorldToScreenPoint(bar.Target.Anchor.position);
        bool onScreen = point.z > 0f && camera.pixelRect.Contains(point);
        bar.View.gameObject.SetActive(onScreen);
        if (!onScreen) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, point, null, out Vector2 position);
        bar.View.RectTransform.anchoredPosition = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
    }

    private void RemoveBar(int index)
    {
        Bar bar = bars[index];
        barsByTarget.Remove(bar.Target);
        bars.RemoveAt(index);
        bar.View.gameObject.SetActive(false);
        Destroy(bar.View.gameObject);
    }

    private void OnDestroy()
    {
        if (activeDisplay == this) activeDisplay = null;
    }
}

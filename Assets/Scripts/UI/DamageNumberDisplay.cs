using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DamageNumberDisplay : MonoBehaviour
{
    private sealed class Number
    {
        public Text Label;
        public int TargetId;
        public int Amount;
        public Vector3 WorldPosition;
        public float Age;
        public float Remaining;
        public Vector2 Offset;
        public Vector2 Size;
        public bool HasSpawnPosition;
    }

    private static DamageNumberDisplay activeDisplay;

    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Text numberTemplate;

    [Header("Motion")]
    [Tooltip("데미지 숫자가 표시되는 시간(초)입니다. 일시정지 중에는 진행하지 않습니다.")]
    [SerializeField, Min(0.01f)] private float lifetime = 0.65f;
    [Tooltip("480 x 270 기준 화면에서 숫자가 위로 이동하는 거리입니다.")]
    [SerializeField, Min(0f)] private float riseDistance = 14f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.2f;
    [Tooltip("생성 직후의 크기 배율입니다. 이후 Template의 원래 크기로 줄어듭니다.")]
    [SerializeField, Min(1f)] private float spawnScale = 1.35f;
    [Tooltip("생성 크기에서 원래 크기로 줄어드는 시간입니다. 축소 후 상승을 시작합니다.")]
    [SerializeField, Min(0f)] private float shrinkDuration = 0.1f;
    [Tooltip("숫자를 처음 표시한 뒤 이 시간 안에 같은 적에게 명중하면 합산합니다. 0이면 합산하지 않습니다.")]
    [SerializeField, Min(0f)] private float mergeWindow = 0.12f;
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 8f);
    [Tooltip("생성 시 한 번 적용할 X축 Random Offset 범위입니다. X는 최솟값, Y는 최댓값이며 기준 해상도의 UI 좌표입니다.")]
    [SerializeField] private Vector2 spawnXOffsetRange = new Vector2(-4f, 4f);
    [Tooltip("생성 시 한 번 적용할 Y축 Random Offset 범위입니다. X는 최솟값, Y는 최댓값이며 기준 해상도의 UI 좌표입니다.")]
    [SerializeField] private Vector2 spawnYOffsetRange = new Vector2(-4f, 6f);

    [Header("Capacity")]
    [Tooltip("표시 개수가 가득 차면 가장 오래된 숫자를 재사용합니다.")]
    [SerializeField, Min(1)] private int maxVisibleNumbers = 32;

    private readonly List<Number> numbers = new List<Number>();
    private readonly Dictionary<int, Number> recentNumbers = new Dictionary<int, Number>();
    private readonly Stack<Number> pool = new Stack<Number>();
    private RectTransform canvasRect;
    private Vector2 outlinePadding;
    private Vector3 templateScale;

    public static void Show(Component target, int amount, Vector3 worldPosition)
    {
        if (!Application.isPlaying || !GameSettings.ShowDamageNumbers || amount <= 0 || (!(target is EnemyHealth) && !(target is BossHealth))) return;
        Camera camera = Camera.main;
        if (camera == null) return;
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f || !camera.pixelRect.Contains(screenPoint)) return;

        if (activeDisplay == null)
        {
            DamageNumberDisplay prefab = Resources.Load<DamageNumberDisplay>("UI/DamageNumberDisplay");
            if (prefab == null || prefab.canvas == null || prefab.numberTemplate == null)
            {
                Debug.LogError("DamageNumberDisplay prefab or its UI references are missing.");
                return;
            }
            activeDisplay = Instantiate(prefab);
        }
        if (activeDisplay.isActiveAndEnabled)
            activeDisplay.AddNumber(target.GetInstanceID(), amount, worldPosition, camera);
    }

    private void Awake()
    {
        canvasRect = (RectTransform)canvas.transform;
        templateScale = numberTemplate.rectTransform.localScale;
        numberTemplate.gameObject.SetActive(false);
        Outline outline = numberTemplate.GetComponent<Outline>();
        if (outline != null && outline.enabled)
            outlinePadding = new Vector2(Mathf.Abs(outline.effectDistance.x), Mathf.Abs(outline.effectDistance.y));
        Canvas.ForceUpdateCanvases();
    }

    private void AddNumber(int targetId, int amount, Vector3 position, Camera camera)
    {
        if (mergeWindow > 0f && recentNumbers.TryGetValue(targetId, out Number recent)
            && recent.Age <= mergeWindow && recent.Remaining > 0f)
        {
            recent.Amount = (int)System.Math.Min(int.MaxValue, (long)recent.Amount + amount);
            recent.Remaining = lifetime;
            UpdateText(recent);
        }
        else
        {
            while (numbers.Count >= maxVisibleNumbers) ReleaseNumber(0);
            Number number = pool.Count > 0 ? pool.Pop()
                : new Number { Label = Instantiate(numberTemplate, canvas.transform, false) };
            number.TargetId = targetId;
            number.Amount = amount;
            number.WorldPosition = position;
            number.Age = 0f;
            number.Remaining = lifetime;
            number.Offset = Vector2.zero;
            number.HasSpawnPosition = false;
            number.Label.name = "DamageNumber";
            UpdateText(number);
            numbers.Add(number);
            recentNumbers[targetId] = number;
            number.Label.rectTransform.SetAsLastSibling();
            number.Label.gameObject.SetActive(true);
        }
        LayoutNumbers(camera);
    }

    private void UpdateText(Number number)
    {
        Text label = number.Label;
        label.text = number.Amount.ToString();
        number.Size = Vector2.Scale(new Vector2(label.preferredWidth, label.preferredHeight) + outlinePadding * 2f,
            new Vector2(Mathf.Abs(templateScale.x), Mathf.Abs(templateScale.y)));
    }

    private void LateUpdate() => Tick(Time.deltaTime, Camera.main);

    private void Tick(float deltaTime, Camera camera)
    {
        for (int i = numbers.Count - 1; i >= 0; i--)
        {
            Number number = numbers[i];
            number.Age += deltaTime;
            number.Remaining -= deltaTime;
            if (number.Remaining <= 0f) ReleaseNumber(i);
        }
        LayoutNumbers(camera);
    }

    private void LayoutNumbers(Camera camera)
    {
        if (camera == null || !TryGetViewport(camera, out Rect viewport))
        {
            foreach (Number number in numbers)
                number.Label.enabled = false;
            return;
        }

        for (int i = 0; i < numbers.Count; i++)
        {
            Number number = numbers[i];
            Vector3 screenPoint = camera.WorldToScreenPoint(number.WorldPosition);
            if (screenPoint.z <= 0f || !camera.pixelRect.Contains(screenPoint)
                || !TryGetCanvasPoint(screenPoint, out Vector2 origin))
            {
                number.Label.enabled = false;
                continue;
            }

            float shrinkProgress = shrinkDuration > 0f ? Mathf.Clamp01(number.Age / shrinkDuration) : 1f;
            float shrinkRemaining = 1f - shrinkProgress;
            float scale = Mathf.Lerp(1f, spawnScale, shrinkRemaining * shrinkRemaining * shrinkRemaining);
            number.Label.rectTransform.localScale = templateScale * scale;

            float progress = Mathf.Clamp01((number.Age - shrinkDuration) / Mathf.Max(0.01f, lifetime - shrinkDuration));
            float rise = riseDistance * (1f - (1f - progress) * (1f - progress));
            Vector2 basePoint = origin + screenOffset + Vector2.up * rise;
            // Reserve the largest size so shrinking does not slide the number along screen edges.
            Vector2 half = number.Size * (spawnScale * 0.5f);
            Rect limits = Rect.MinMaxRect(viewport.xMin + half.x, viewport.yMin + half.y,
                viewport.xMax - half.x, viewport.yMax - half.y - (riseDistance - rise));
            if (limits.width < 0f || limits.height < 0f)
            {
                number.Label.enabled = false;
                continue;
            }

            Vector2 point = number.HasSpawnPosition ? ClampPoint(basePoint + number.Offset, limits)
                : FindSpawnPoint(basePoint, limits, i);
            number.HasSpawnPosition = true;
            number.Offset = point - basePoint;
            number.Label.enabled = true;
            // Canvas-local coordinates are relative to its pivot, not the label's anchors.
            number.Label.rectTransform.localPosition = new Vector3(Mathf.Round(point.x), Mathf.Round(point.y), 0f);
            Color color = numberTemplate.color;
            if (fadeDuration > 0f) color.a *= Mathf.Clamp01(number.Remaining / fadeDuration);
            number.Label.color = color;
        }
    }

    private Vector2 FindSpawnPoint(Vector2 basePoint, Rect limits, int previousCount)
    {
        // Move the random range inside the viewport before sampling, rather than clamping every hit to one edge.
        float width = Mathf.Min(spawnXOffsetRange.y - spawnXOffsetRange.x, limits.width);
        float minX = Mathf.Clamp(basePoint.x + spawnXOffsetRange.x, limits.xMin, limits.xMax - width);
        float maxX = minX + width;
        float height = Mathf.Min(spawnYOffsetRange.y - spawnYOffsetRange.x, limits.height);
        float minY = Mathf.Clamp(basePoint.y + spawnYOffsetRange.x, limits.yMin, limits.yMax - height);
        float maxY = minY + height;
        Vector2 point = new Vector2(Random.Range(minX, maxX), minY);
        int minPixel = Mathf.CeilToInt(minY);
        int maxPixel = Mathf.FloorToInt(maxY);
        if (minPixel > maxPixel)
        {
            point.y = Random.Range(minY, maxY);
            return point;
        }

        int firstY = Random.Range(minPixel, maxPixel + 1);
        int count = maxPixel - minPixel + 1;
        int attempts = Mathf.Min(count, previousCount + 1);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            point.y = minPixel + (firstY - minPixel + attempt) % count;
            bool used = false;
            for (int i = 0; i < previousCount; i++)
            {
                Number other = numbers[i];
                if (other.Age > 0f || !other.Label.enabled) continue;
                Vector3 position = other.Label.rectTransform.localPosition;
                if (position.x == Mathf.Round(point.x) && position.y == point.y)
                {
                    used = true;
                    break;
                }
            }
            if (!used) return point;
        }

        point.y = firstY;
        return point;
    }

    private bool TryGetCanvasPoint(Vector2 screenPoint, out Vector2 point)
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out point);
    }

    private bool TryGetViewport(Camera camera, out Rect viewport)
    {
        viewport = default;
        if (!TryGetCanvasPoint(camera.pixelRect.min, out Vector2 min)
            || !TryGetCanvasPoint(camera.pixelRect.max, out Vector2 max)) return false;

        Rect rect = canvasRect.rect;
        // Leave room for rounding to whole UI pixels at the screen edges.
        viewport = Rect.MinMaxRect(Mathf.Max(min.x, rect.xMin) + 1f, Mathf.Max(min.y, rect.yMin) + 1f,
            Mathf.Min(max.x, rect.xMax) - 1f, Mathf.Min(max.y, rect.yMax) - 1f);
        return viewport.width > 0f && viewport.height > 0f;
    }

    private static Vector2 ClampPoint(Vector2 point, Rect limits) => new Vector2(
        Mathf.Clamp(point.x, limits.xMin, limits.xMax), Mathf.Clamp(point.y, limits.yMin, limits.yMax));

    private void ReleaseNumber(int index)
    {
        Number number = numbers[index];
        if (recentNumbers.TryGetValue(number.TargetId, out Number recent) && recent == number)
            recentNumbers.Remove(number.TargetId);
        numbers.RemoveAt(index);
        number.Label.gameObject.SetActive(false);
        pool.Push(number);
    }

    private void OnEnable() => GameSettings.Changed += ApplySettings;

    private void ApplySettings()
    {
        if (!GameSettings.ShowDamageNumbers)
            for (int i = numbers.Count - 1; i >= 0; i--) ReleaseNumber(i);
    }

    private void OnDisable()
    {
        GameSettings.Changed -= ApplySettings;
        for (int i = numbers.Count - 1; i >= 0; i--) ReleaseNumber(i);
    }

    private void OnValidate()
    {
        lifetime = Mathf.Max(0.01f, lifetime);
        riseDistance = Mathf.Max(0f, riseDistance);
        fadeDuration = Mathf.Clamp(fadeDuration, 0f, lifetime);
        spawnScale = Mathf.Max(1f, spawnScale);
        shrinkDuration = Mathf.Clamp(shrinkDuration, 0f, lifetime - fadeDuration);
        mergeWindow = Mathf.Clamp(mergeWindow, 0f, lifetime);
        if (spawnXOffsetRange.x > spawnXOffsetRange.y)
            spawnXOffsetRange = new Vector2(spawnXOffsetRange.y, spawnXOffsetRange.x);
        if (spawnYOffsetRange.x > spawnYOffsetRange.y)
            spawnYOffsetRange = new Vector2(spawnYOffsetRange.y, spawnYOffsetRange.x);
        maxVisibleNumbers = Mathf.Max(1, maxVisibleNumbers);
    }

    private void OnDestroy()
    {
        if (activeDisplay == this) activeDisplay = null;
    }
}

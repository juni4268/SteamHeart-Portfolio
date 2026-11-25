using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Category("DamageNumberDisplay")]
public sealed class DamageNumberDisplayTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private Scene previewScene;
    private DamageNumberDisplay display;
    private Canvas canvas;
    private Text template;
    private Camera camera;
    private Random.State randomState;

    [SetUp]
    public void SetUp()
    {
        randomState = Random.state;
        previewScene = EditorSceneManager.NewPreviewScene();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/UI/DamageNumberDisplay.prefab");
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene);
        display = root.GetComponent<DamageNumberDisplay>();
        canvas = root.GetComponent<Canvas>();
        template = root.GetComponentInChildren<Text>(true);
        // Keep test timing independent of the user's Inspector tuning.
        SetField("lifetime", 0.65f);
        SetField("riseDistance", 14f);
        SetField("fadeDuration", 0.2f);
        SetField("spawnScale", 1.35f);
        SetField("shrinkDuration", 0.1f);
        SetField("mergeWindow", 0.12f);
        SetField("screenOffset", new Vector2(0f, 8f));
        SetField("spawnXOffsetRange", Vector2.zero);
        SetField("spawnYOffsetRange", new Vector2(-4f, 6f));
        SetField("maxVisibleNumbers", 32);

        GameObject cameraObject = new GameObject("Damage number test camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
        camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 3f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.pixelRect = new Rect(0f, 0f, Screen.width, Screen.height);
        Invoke("Awake");
        // Preview scenes do not render Overlay canvases, so apply their calculated UI scale explicitly.
        canvas.transform.localScale = Vector3.one * canvas.scaleFactor;
    }

    [TearDown]
    public void TearDown()
    {
        if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
        Random.state = randomState;
    }

    [Test]
    public void NewNumbersUseOffsetsAndRenderInFront()
    {
        SetField("spawnXOffsetRange", new Vector2(4f, 4f));
        SetField("spawnYOffsetRange", new Vector2(6f, 6f));
        Add(1, 10);
        SetField("spawnXOffsetRange", new Vector2(-4f, -4f));
        SetField("spawnYOffsetRange", new Vector2(-4f, -4f));
        Add(2, 20);
        Text[] labels = VisibleLabels();
        Assert.That(labels.Length, Is.EqualTo(2));
        Assert.That(labels[0].rectTransform.localPosition.x - labels[1].rectTransform.localPosition.x,
            Is.EqualTo(8f));
        Assert.That(labels[0].rectTransform.localPosition.y - labels[1].rectTransform.localPosition.y,
            Is.EqualTo(10f));
        Assert.That(Bounds(labels[0]).Overlaps(Bounds(labels[1])), Is.True);
        Assert.That(labels[1].text, Is.EqualTo("20"));
        Assert.That(labels[1].transform.GetSiblingIndex(), Is.EqualTo(canvas.transform.childCount - 1));
    }

    [TestCase(0.001f)]
    [TestCase(0.999f)]
    public void SimultaneousHitsAtScreenEdgesUseDifferentHeights(float y)
    {
        SetField("spawnScale", 2f);
        SetField("shrinkDuration", 0.45f);
        SetField("spawnYOffsetRange", new Vector2(-10f, 10f));
        Random.InitState(617);
        for (int i = 0; i < 6; i++) Add(i, 10, new Vector2(0.5f, y));

        Text[] labels = VisibleLabels();
        Assert.That(labels.Length, Is.EqualTo(6));
        Assert.That(labels.Select(label => label.rectTransform.localPosition.y).Distinct().Count(), Is.EqualTo(6));
        Assert.That(labels.Select(label => label.rectTransform.localPosition.x).Distinct().Count(), Is.EqualTo(1));
        AssertInsideViewport(labels);

        Vector3[] positions = labels.Select(label => label.rectTransform.localPosition).ToArray();
        Invoke("Tick", 0.05f, camera);
        Add(5, 1, new Vector2(0.75f, 0.5f));
        Assert.That(VisibleLabels().Select(label => label.rectTransform.localPosition), Is.EqualTo(positions));
    }

    [Test]
    public void NewNumbersShrinkBeforeRisingAndFading()
    {
        Add(1, 10);
        Text label = VisibleLabels().Single();
        Vector3 position = label.rectTransform.localPosition;
        Vector3 baseScale = template.rectTransform.localScale;
        Assert.That(Vector3.Distance(label.rectTransform.localScale, baseScale * 1.35f), Is.LessThan(0.001f));
        Assert.That(label.color.a, Is.EqualTo(template.color.a));

        Invoke("Tick", 0.05f, camera);
        Assert.That(label.rectTransform.localScale.x, Is.InRange(baseScale.x, baseScale.x * 1.35f));
        Assert.That(label.rectTransform.localPosition, Is.EqualTo(position));
        Invoke("Tick", 0.05f, camera);
        Assert.That(Vector3.Distance(label.rectTransform.localScale, baseScale), Is.LessThan(0.001f));
        Assert.That(label.rectTransform.localPosition, Is.EqualTo(position));

        Invoke("Tick", 0.35f, camera);
        Assert.That(label.rectTransform.localPosition.y, Is.GreaterThan(position.y));
        Invoke("Tick", 0.1f, camera);
        Assert.That(label.color.a, Is.GreaterThan(0f).And.LessThan(template.color.a));
        Invoke("Tick", 0.11f, camera);
        Assert.That(VisibleLabels(), Is.Empty);
    }

    [Test]
    public void MergingKeepsPositionAndDoesNotRestartTheSpawnAnimation()
    {
        SetField("spawnXOffsetRange", new Vector2(-4f, 4f));
        Add(1, 9);
        Invoke("Tick", 0.11f, camera);
        Text label = VisibleLabels().Single();
        Vector3 position = label.rectTransform.localPosition;
        Vector3 scale = label.rectTransform.localScale;

        Add(1, 11, new Vector2(0.75f, 0.7f));

        Assert.That(VisibleLabels().Single(), Is.SameAs(label));
        Assert.That(label.text, Is.EqualTo("20"));
        Assert.That(label.rectTransform.localPosition, Is.EqualTo(position));
        Assert.That(label.rectTransform.localScale, Is.EqualTo(scale));
        Invoke("Tick", 0.1f, camera);
        Assert.That(label.rectTransform.localPosition.y, Is.GreaterThanOrEqualTo(position.y));
    }

    [Test]
    public void MergeWindowEndsAfterTheFirstHitAndZeroDisablesMerging()
    {
        Add(1, 1);
        Invoke("Tick", 0.08f, camera);
        Add(1, 1);
        Invoke("Tick", 0.08f, camera);
        Add(1, 1);
        Assert.That(VisibleLabels().Select(label => label.text), Is.EquivalentTo(new[] { "2", "1" }));

        Invoke("OnDisable");
        SetField("mergeWindow", 0f);
        Add(2, 1);
        Add(2, 1);
        Assert.That(VisibleLabels().Length, Is.EqualTo(2));
    }

    [Test]
    public void WiderMergedNumbersStayInsideTheScreen()
    {
        Add(1, 1, new Vector2(0.99f, 0.9f));
        Add(2, 10, new Vector2(0.99f, 0.9f));
        Add(1, 999999, new Vector2(0.99f, 0.9f));
        Assert.That(VisibleLabels().Length, Is.EqualTo(2));
        AssertInsideViewport(VisibleLabels());
    }

    [TestCase(0.001f, 0.001f)]
    [TestCase(0.001f, 0.999f)]
    [TestCase(0.999f, 0.001f)]
    [TestCase(0.999f, 0.999f)]
    public void WholeNumbersRemainVisibleAtScreenCorners(float x, float y)
    {
        SetField("spawnXOffsetRange", new Vector2(-4f, 4f));
        for (int i = 0; i < 4; i++) Add(i, 123456, new Vector2(x, y));
        Assert.That(VisibleLabels().Length, Is.EqualTo(4));
        AssertInsideViewport(VisibleLabels());
        Vector3[] positions = VisibleLabels().Select(label => label.rectTransform.localPosition).ToArray();
        Invoke("Tick", 0.05f, camera);
        Assert.That(VisibleLabels().Select(label => label.rectTransform.localPosition), Is.EqualTo(positions));
        Invoke("Tick", 0.5f, camera);
        AssertInsideViewport(VisibleLabels());
    }

    [TestCase(0f)]
    [TestCase(0.5f)]
    public void CanvasPivotDoesNotShiftTheImpactPosition(float pivot)
    {
        SetField("spawnYOffsetRange", Vector2.zero);
        ((RectTransform)canvas.transform).pivot = new Vector2(pivot, pivot);
        Canvas.ForceUpdateCanvases();
        canvas.transform.position = new Vector3(Screen.width * pivot, Screen.height * pivot, 0f);
        Add(1, 10);

        Vector2 actual = RectTransformUtility.WorldToScreenPoint(null,
            VisibleLabels().Single().rectTransform.position);
        Vector2 expected = camera.pixelRect.center + Vector2.up * (8f * canvas.scaleFactor);
        Assert.That(Vector2.Distance(actual, expected), Is.LessThanOrEqualTo(canvas.scaleFactor));
    }

    [Test]
    public void CapacityAndExpiryReuseLabelsWithoutAllocatingMoreObjects()
    {
        SetField("maxVisibleNumbers", 3);
        for (int i = 0; i < 3; i++) Add(i, i + 1);
        Text oldest = VisibleLabels()[0];
        Invoke("Tick", 0.2f, camera);
        Add(3, 4);
        Assert.That(VisibleLabels().Select(label => label.text), Is.EquivalentTo(new[] { "2", "3", "4" }));
        Assert.That(VisibleLabels().Last(), Is.SameAs(oldest));
        Assert.That(oldest.text, Is.EqualTo("4"));
        Assert.That(oldest.transform.GetSiblingIndex(), Is.EqualTo(canvas.transform.childCount - 1));
        Assert.That(Vector3.Distance(oldest.rectTransform.localScale,
            template.rectTransform.localScale * 1.35f), Is.LessThan(0.001f));
        Assert.That(AllLabels().Length, Is.EqualTo(3));

        Invoke("Tick", 1f, camera);
        Assert.That(VisibleLabels(), Is.Empty);
        Add(10, 50);
        Assert.That(VisibleLabels().Single().text, Is.EqualTo("50"));
        Assert.That(VisibleLabels().Single().transform.GetSiblingIndex(), Is.EqualTo(canvas.transform.childCount - 1));
        Assert.That(AllLabels().Length, Is.EqualTo(3));
    }

    [Test]
    public void ZeroDeltaFreezesMotionAndFadeAndDisableClearsNumbers()
    {
        Add(1, 10);
        Invoke("Tick", 0.5f, camera);
        Text label = VisibleLabels().Single();
        Vector3 position = label.rectTransform.localPosition;
        Color color = label.color;
        Invoke("Tick", 0f, camera);
        Assert.That(label.rectTransform.localPosition, Is.EqualTo(position));
        Assert.That(label.color, Is.EqualTo(color));

        Invoke("OnDisable");
        Assert.That(VisibleLabels(), Is.Empty);
        Add(1, 7);
        Assert.That(VisibleLabels().Single().text, Is.EqualTo("7"));
        Assert.That(AllLabels().Length, Is.EqualTo(1));
    }

    private void Add(int target, int amount, Vector2? viewportPoint = null)
    {
        Vector2 point = viewportPoint ?? new Vector2(0.5f, 0.5f);
        Vector3 world = camera.ViewportToWorldPoint(new Vector3(point.x, point.y, 10f));
        Invoke("AddNumber", target, amount, world, camera);
    }

    private Text[] AllLabels() => display.GetComponentsInChildren<Text>(true)
        .Where(label => label != template).ToArray();

    private Text[] VisibleLabels() => AllLabels()
        .Where(label => label.gameObject.activeSelf && label.enabled).ToArray();

    private static Rect Bounds(Text label)
    {
        Vector2 half = new Vector2(label.preferredWidth, label.preferredHeight) * 0.5f;
        Outline outline = label.GetComponent<Outline>();
        if (outline != null && outline.enabled)
            half += new Vector2(Mathf.Abs(outline.effectDistance.x), Mathf.Abs(outline.effectDistance.y));
        Vector3 scale = label.rectTransform.localScale;
        half = Vector2.Scale(half, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
        return new Rect((Vector2)label.rectTransform.localPosition - half, half * 2f);
    }

    private void AssertInsideViewport(Text[] labels)
    {
        foreach (Text label in labels)
        {
            Rect rect = Bounds(label);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null,
                canvas.transform.TransformPoint(rect.min));
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null,
                canvas.transform.TransformPoint(rect.max));
            Assert.That(min.x, Is.GreaterThanOrEqualTo(camera.pixelRect.xMin));
            Assert.That(min.y, Is.GreaterThanOrEqualTo(camera.pixelRect.yMin));
            Assert.That(max.x, Is.LessThanOrEqualTo(camera.pixelRect.xMax));
            Assert.That(max.y, Is.LessThanOrEqualTo(camera.pixelRect.yMax));
        }
    }

    private void Invoke(string method, params object[] args) =>
        typeof(DamageNumberDisplay).GetMethod(method, PrivateInstance).Invoke(display, args);

    private void SetField(string field, object value) =>
        typeof(DamageNumberDisplay).GetField(field, PrivateInstance).SetValue(display, value);
}

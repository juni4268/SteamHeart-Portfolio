using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2TrackingLaserVisualTests
{
    private Boss2TrackingLaserVisual visual;
    private Animator animator;

    [SetUp]
    public void SetUp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss2/Boss2TrackingLaser.prefab");
        Assert.That(prefab, Is.Not.Null);
        visual = Object.Instantiate(prefab).GetComponent<Boss2TrackingLaserVisual>();
        animator = visual.GetComponent<Animator>();
        animator.Rebind();
        animator.Update(0f);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(visual.gameObject);

    [Test]
    public void Boss1WarningMarksDamageBoundsAndHidesBeforeActualFireEntry()
    {
        var serialized = new SerializedObject(visual);
        var warningArea = (Boss1DebrisWarningArea)serialized.FindProperty("warningArea").objectReferenceValue;
        var warning = warningArea.GetComponent<Boss1WarningVisual>();
        var renderers = new[]
        {
            (SpriteRenderer)serialized.FindProperty("beamRenderer").objectReferenceValue,
            (SpriteRenderer)serialized.FindProperty("impactRenderer").objectReferenceValue
        };
        var colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            colors[i] = new Color(0.8f, 0.7f, 0.6f, 0.8f - i * 0.1f);
            renderers[i].color = colors[i];
        }
        visual.BeginWarning(0.6f);
        visual.SetGroundPosition(new Vector3(3f, -5f));
        foreach (var renderer in renderers) Assert.That(renderer.enabled, Is.False);
        Assert.That(warning.Renderer.enabled, Is.True);
        Assert.That(warning.Renderer.bounds.center.x, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(warning.Style, Is.EqualTo(Boss1WarningVisual.WarningStyle.Falling));
        Assert.That(Vector3.Distance(warning.Renderer.bounds.center, visual.DamageBounds.center), Is.LessThan(0.0001f));
        Assert.That(Vector2.Distance(warning.Renderer.bounds.size, visual.DamageBounds.size), Is.LessThan(0.0001f));
        Assert.That(visual.FireEntered, Is.False);
        for (int i = 0; i < 240; i++) animator.Update(1f / 60f);
        Assert.That(visual.FireEntered, Is.False);
        Assert.That(visual.Interrupted, Is.False);
        Assert.That(animator.GetCurrentAnimatorClipInfo(0), Is.Empty,
            "The attack motion must not play during the procedural warning.");
        foreach (var renderer in renderers) Assert.That(renderer.enabled, Is.False);
        visual.BeginFire();
        foreach (var renderer in warningArea.GetComponentsInChildren<SpriteRenderer>()) Assert.That(renderer.enabled, Is.False);
        Assert.That(visual.FireEntered, Is.False, "A request is not actual Animator entry.");
        StepUntil(() => visual.FireEntered);
        Assert.That(visual.FireEntered, Is.True);
        for (int i = 0; i < renderers.Length; i++)
        {
            Assert.That(renderers[i].enabled, Is.True);
            Assert.That(renderers[i].color, Is.EqualTo(colors[i]));
        }
    }

    [Test]
    public void GroundPositioningPreservesAuthoredSizesAndDamageBounds()
    {
        var serialized = new SerializedObject(visual);
        var topMarker = (Transform)serialized.FindProperty("beamTop").objectReferenceValue;
        var bottomMarker = (Transform)serialized.FindProperty("beamBottom").objectReferenceValue;
        var groundAnchor = (Transform)serialized.FindProperty("groundAnchor").objectReferenceValue;
        var beamRenderer = (SpriteRenderer)serialized.FindProperty("beamRenderer").objectReferenceValue;
        var impactRenderer = (SpriteRenderer)serialized.FindProperty("impactRenderer").objectReferenceValue;
        var beamScale = new Vector3(2f, 0.75f, 1f);
        var impactScale = new Vector3(0.5f, 0.8f, 1f);
        beamRenderer.transform.localScale = beamScale;
        impactRenderer.transform.localScale = impactScale;
        Vector3 rootScale = visual.transform.localScale;
        float height = Mathf.Abs(topMarker.position.y - bottomMarker.position.y);
        Vector3 beamOffset = bottomMarker.position - groundAnchor.position;
        Vector3 impactOffset = impactRenderer.transform.position - groundAnchor.position;
        visual.BeginWarning(0.6f);
        foreach (Vector3 ground in new[] { new Vector3(3f, -5f), new Vector3(-2f, 1f) })
        {
            visual.SetGroundPosition(ground);
            Assert.That(visual.transform.localScale, Is.EqualTo(rootScale));
            Assert.That(beamRenderer.transform.localScale, Is.EqualTo(beamScale));
            Assert.That(impactRenderer.transform.localScale, Is.EqualTo(impactScale));
            Assert.That(groundAnchor.position.y, Is.EqualTo(ground.y).Within(0.0001f));
            Assert.That(visual.DamageBounds.center.x, Is.EqualTo(ground.x).Within(0.0001f));
            Assert.That(Vector3.Distance(bottomMarker.position - groundAnchor.position, beamOffset), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(impactRenderer.transform.position - groundAnchor.position, impactOffset), Is.LessThan(0.0001f));
            Assert.That(visual.DamageBounds.size.y, Is.EqualTo(height).Within(0.0001f));
            Assert.That(visual.DamageBounds.size.x, Is.EqualTo(serialized.FindProperty("referenceWidth").floatValue *
                Mathf.Abs(beamRenderer.transform.lossyScale.x)).Within(0.0001f));
        }
    }

    [Test]
    public void FiringLoopsUntilFadeIsRequestedAndThenCompletesNormally()
    {
        visual.BeginWarning(0.6f);
        visual.SetGroundPosition(new Vector3(0f, -5f));
        visual.BeginFire();
        StepUntil(() => visual.FireEntered);
        for (int i = 0; i < 240; i++) animator.Update(1f / 60f);
        Assert.That(visual.Completed, Is.False);
        Assert.That(visual.Interrupted, Is.False);
        visual.BeginFade();
        StepUntil(() => visual.Completed);
        Assert.That(visual.Completed, Is.True);
        Assert.That(visual.Interrupted, Is.False);
        Assert.That(visual.Active, Is.False);
        foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>()) Assert.That(renderer.enabled, Is.False);
    }

    [Test]
    public void CancelDoesNotReportNormalCompletion()
    {
        visual.BeginWarning(0.6f);
        visual.SetGroundPosition(new Vector3(0f, -5f));
        visual.Cancel();
        for (int i = 0; i < 10; i++) animator.Update(1f / 60f);
        Assert.That(visual.Interrupted, Is.True);
        Assert.That(visual.Completed, Is.False);
        foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>()) Assert.That(renderer.enabled, Is.False);
    }

    private void StepUntil(System.Func<bool> condition)
    {
        for (int i = 0; i < 180 && !condition(); i++) animator.Update(1f / 60f);
    }
}

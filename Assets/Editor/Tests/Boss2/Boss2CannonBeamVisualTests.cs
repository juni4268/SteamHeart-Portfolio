using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2CannonBeamVisualTests
{
    private GameObject root;
    private Boss2CannonBeamVisual beam;
    private Animator animator;

    [SetUp]
    public void SetUp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Boss/Boss2/Boss2CannonBeamVisual.prefab");
        Assert.That(prefab, Is.Not.Null);
        root = Object.Instantiate(prefab);
        beam = root.GetComponent<Boss2CannonBeamVisual>();
        animator = root.GetComponent<Animator>();
        Assert.That(beam, Is.Not.Null);
        Assert.That(animator, Is.Not.Null);
        animator.Rebind();
        animator.Update(0f);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [TestCase(0.15f)]
    [TestCase(0.45f)]
    [TestCase(0.9f)]
    public void AnimatedBeamUsesConfiguredDamageDurationAndCompletesFade(float duration)
    {
        beam.BeginAim();
        beam.SetGeometry(new Vector3(2f, 3f, 0f), Vector2.left, 6f, 0.24f);
        StepUntil(() => beam.AimCompleted);
        Assert.That(beam.AimCompleted, Is.True);
        beam.BeginFire(duration);
        StepUntil(() => beam.FireEntered);
        Assert.That(beam.FireEntered, Is.True);
        int steps = 0;
        while (!beam.FireCompleted && steps < 180)
        {
            animator.Update(1f / 60f);
            steps++;
        }
        Assert.That(beam.FireCompleted, Is.True);
        Assert.That(steps / 60f, Is.EqualTo(duration).Within(0.07f));
        beam.BeginFade();
        StepUntil(() => beam.Completed);
        Assert.That(beam.Completed, Is.True);
        Assert.That(beam.Interrupted, Is.False);
        Assert.That(root.GetComponent<SpriteRenderer>().enabled, Is.False,
            "The completed beam must remain hidden while the hand returns.");
    }

    [Test]
    public void GeometryUpdatesDuringFadeCannotShowTheCompletedBeamAgain()
    {
        beam.BeginAim();
        StepUntil(() => beam.AimCompleted);
        beam.BeginFire(0.15f);
        StepUntil(() => beam.FireCompleted);
        beam.BeginFade();
        for (int i = 0; i < 180 && !beam.Completed; i++)
        {
            animator.Update(1f / 60f);
            beam.SetGeometry(Vector3.zero, Vector2.right, 6f, 0.24f);
        }
        Assert.That(beam.Completed, Is.True);
        Assert.That(beam.Interrupted, Is.False);
        Assert.That(root.GetComponent<SpriteRenderer>().enabled, Is.False);
    }

    [Test]
    public void StretchKeepsTheAuthoredStartAndEndAtTheGameplayEndpoints()
    {
        beam.BeginAim();
        Vector3 origin = new Vector3(3f, -2f, 0f);
        Vector2 direction = new Vector2(-3f, 4f).normalized;
        beam.SetGeometry(origin, direction, 7f, 0.24f);
        var so = new SerializedObject(beam);
        var start = (Transform)so.FindProperty("startMarker").objectReferenceValue;
        var end = (Transform)so.FindProperty("endMarker").objectReferenceValue;
        Assert.That(Vector3.Distance(start.position, origin), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(end.position, origin + (Vector3)(direction * 7f)), Is.LessThan(0.001f));
        Bounds spriteBounds = root.GetComponent<SpriteRenderer>().sprite.bounds;
        Vector3 visibleStart = root.transform.TransformPoint(new Vector3(spriteBounds.min.x, start.localPosition.y, 0f));
        Vector3 visibleEnd = root.transform.TransformPoint(new Vector3(spriteBounds.max.x, end.localPosition.y, 0f));
        Assert.That(Vector3.Distance(visibleStart, origin), Is.LessThan(0.001f));
        Assert.That(Vector3.Distance(visibleEnd, origin + (Vector3)(direction * 7f)), Is.LessThan(0.001f));
    }

    [Test]
    public void CannonDistanceUsesAttackRangeThroughWalls()
    {
        var boss = new GameObject("Cannon wall test");
        var wall = new GameObject("Wall");
        try
        {
            var cannon = boss.AddComponent<Boss2CannonBeamPattern>();
            wall.layer = 31;
            wall.transform.position = new Vector3(1002f, 1000f, 0f);
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.2f, 0.1f);
            typeof(Boss2BasicPatternBase).GetField("attackRange", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(cannon, 18f);
            Physics2D.SyncTransforms();
            var method = typeof(Boss2CannonBeamPattern).GetMethod("GetAttackDistance", BindingFlags.Instance | BindingFlags.NonPublic);
            float distance = (float)method.Invoke(cannon, new object[] { new Vector3(1000f, 1000f, 0f), Vector2.right });
            Assert.That(distance, Is.EqualTo(18f));
        }
        finally { Object.DestroyImmediate(wall); Object.DestroyImmediate(boss); }
    }

    private void StepUntil(System.Func<bool> complete)
    {
        for (int i = 0; i < 180 && !complete(); i++) animator.Update(1f / 60f);
    }
}

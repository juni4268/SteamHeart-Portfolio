using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2BlackHoleTests
{
    private GameObject root;
    private Boss2Controller boss;
    private Boss2BlackHolePattern pattern;

    private static void Set(object target, System.Type type, string field, object value) =>
        type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Black Hole test");
        boss = root.AddComponent<Boss2Controller>();
        var health = root.GetComponent<BossHealth>();
        Set(health, typeof(BossHealth), "currentHp", health.MaxHp);
        Set(boss, typeof(Boss2Controller), "health", health);
        string[] fields = { "upperLeftHand", "upperRightHand", "lowerLeftHand", "lowerRightHand", "ring" };
        for (int i = 0; i < fields.Length; i++)
        {
            var child = new GameObject(fields[i]); child.transform.SetParent(root.transform);
            var part = child.AddComponent<Boss2PartController>();
            Set(part, typeof(Boss2PartController), "partId", (Boss2PartId)i);
            Set(boss, typeof(Boss2Controller), fields[i], part);
        }
        pattern = root.AddComponent<Boss2BlackHolePattern>();
        boss.BeginCombat();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    private void Destination(float x)
    {
        var point = new GameObject("Destination").transform;
        point.SetParent(root.transform); point.position = new Vector3(x, 0f);
        Set(pattern, typeof(Boss2BlackHolePattern), "destinations", new[] { point });
    }

    [TestCase(-4f, Boss2PartId.UpperLeftHand)]
    [TestCase(4f, Boss2PartId.UpperRightHand)]
    public void DestinationSideSelectsTheMatchingHandAndNeverSubstitutesTheOther(float x, Boss2PartId id)
    {
        Destination(x);
        Assert.That(pattern.RequiredParts, Is.EqualTo(Boss2Parts.Ring | Boss2PartMask.FromId(id)));
        Assert.That(boss.PartUsage.TryReserve(Boss2PartMask.FromId(id), Boss2PatternKind.Main, Boss2Parts.None, out var busy), Is.True);
        Assert.That(pattern.RequiredParts, Is.EqualTo(Boss2Parts.None));
        boss.PartUsage.Release(busy);
        Assert.That(pattern.RequiredParts, Is.EqualTo(Boss2Parts.Ring | Boss2PartMask.FromId(id)));
    }

    [Test]
    public void MainRequestWaitsForTheMatchingHandsBasicAttack()
    {
        Destination(-4f);
        boss.PartUsage.TryReserve(Boss2Parts.UpperLeftHand, Boss2PatternKind.Basic, Boss2Parts.None, out var basic);
        boss.PartUsage.TryActivate(basic);
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True);
        Assert.That(runner.HeldParts, Is.EqualTo(Boss2Parts.UpperLeftHand | Boss2Parts.Ring));
        Assert.That(boss.PartUsage.GetOwner(Boss2PartId.UpperLeftHand), Is.SameAs(basic));
        Assert.That(boss.PartUsage.GetReservation(Boss2PartId.UpperRightHand), Is.Null);
        runner.Cancel(); boss.PartUsage.Release(basic);
    }

    [Test]
    public void PullForceRespectsRangeDirectionAndItsMaximumAddedSpeed()
    {
        var field = root.AddComponent<Boss2BlackHoleField>();
        Set(field, typeof(Boss2BlackHoleField), "origin", root.transform);
        var method = typeof(Boss2BlackHoleField).GetMethod("CalculateForce", BindingFlags.NonPublic | BindingFlags.Instance);
        Vector2 Force(Vector2 position, Vector2 velocity) => (Vector2)method.Invoke(field, new object[] {position,velocity,1f,0.02f});
        Assert.That(Force(new Vector2(10f, 0f), Vector2.zero), Is.EqualTo(Vector2.zero));
        Assert.That(Force(Vector2.zero, Vector2.zero), Is.EqualTo(Vector2.zero));
        Vector2 pull = Force(new Vector2(2f, 0f), Vector2.zero);
        Assert.That(pull.x, Is.LessThan(0f)); Assert.That(pull.y, Is.EqualTo(0f));
        Vector2 limited = Force(new Vector2(2f, 0f), new Vector2(-4.9f, 0f));
        Assert.That(limited.x, Is.EqualTo(-5f).Within(0.001f));
        Assert.That(Force(new Vector2(2f, 0f), new Vector2(-10f, 0f)), Is.EqualTo(Vector2.zero));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void AnimatorReportsNormalAndForcedReturnThroughActualStates(bool ring, bool force)
    {
        var visual = new GameObject("Black Hole visual"); visual.transform.SetParent(root.transform);
        visual.AddComponent<SpriteRenderer>();
        var animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            ring ? "Assets/Animations/Boss/Boss2/Boss2RingIdle.controller" : "Assets/Animations/Boss/Boss2/Boss2UpperHandIdle.controller");
        var animation = visual.AddComponent<Boss2BlackHoleAnimation>();
        animator.Rebind(); animator.Update(0f);
        animation.Begin();
        if (!force)
        {
            for (int i = 0; i < 180 && !animation.Prepared; i++) animator.Update(1f / 60f);
            Assert.That(animation.Prepared, Is.True);
            if (ring)
            {
                animation.BeginActive();
                for (int i = 0; i < 30 && !animation.ActiveEntered; i++) animator.Update(1f / 60f);
                Assert.That(animation.ActiveEntered, Is.True);
            }
        }
        animation.BeginReturn(force);
        if (ring && !force)
        {
            for (int i = 0; i < 180 && !animation.RecoveryCompleted; i++) animator.Update(1f / 60f);
            Assert.That(animation.RecoveryCompleted, Is.True);
            Assert.That(animation.Completed, Is.False);
            animation.BeginReturnTravel();
        }
        for (int i = 0; i < 180 && !animation.Completed; i++) animator.Update(1f / 60f);
        Assert.That(animation.Completed, Is.True);
        Assert.That(animation.Active, Is.False);
        Assert.That(animation.Interrupted, Is.False);
    }

    [Test]
    public void RingLoopsOnlyTheSustainFramesUntilReturnIsRequested()
    {
        var visual = new GameObject("Sustain animation test"); visual.transform.SetParent(root.transform);
        var renderer = visual.AddComponent<SpriteRenderer>();
        var animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2RingIdle.controller");
        var animation = visual.AddComponent<Boss2BlackHoleAnimation>();
        animator.Rebind(); animator.Update(0f); animation.Begin();
        for (int i = 0; i < 180 && !animation.Prepared; i++) animator.Update(1f / 60f);
        animation.BeginActive();
        for (int i = 0; i < 180 && !animation.ActiveEntered; i++) animator.Update(1f / 60f);
        Assert.That(animation.ActiveEntered, Is.True);
        var allowed = new HashSet<Sprite>(AssetDatabase.LoadAllAssetsAtPath(
            "Assets/Graphics/Characters/Boss2/Boss2BlackholeRing.png").OfType<Sprite>().OrderBy(s => s.rect.x).Skip(13).Take(25));
        var seen = new HashSet<Sprite>();
        for (int i = 0; i < 300; i++)
        {
            animator.Update(1f / 60f);
            Assert.That(allowed.Contains(renderer.sprite), Is.True, "Windup/recovery frames must not repeat during sustain.");
            seen.Add(renderer.sprite);
        }
        Assert.That(seen.Count, Is.EqualTo(25));
        Assert.That(animation.Completed, Is.False);
        animation.BeginReturn();
        for (int i = 0; i < 180 && !animation.RecoveryCompleted; i++) animator.Update(1f / 60f);
        Assert.That(animation.RecoveryCompleted, Is.True);
        Assert.That(animation.Completed, Is.False);
        animation.BeginReturnTravel();
        for (int i = 0; i < 180 && !animation.Completed; i++) animator.Update(1f / 60f);
        Assert.That(animation.Completed, Is.True);
    }

    [Test]
    public void RingWaitsForTheWindupBeforeReportingActiveEntry()
    {
        var visual = new GameObject("Windup animation test"); visual.transform.SetParent(root.transform);
        visual.AddComponent<SpriteRenderer>();
        var animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2RingIdle.controller");
        var animation = visual.AddComponent<Boss2BlackHoleAnimation>();
        animator.Rebind(); animator.Update(0f); animation.Begin();
        for (int i = 0; i < 180 && !animation.Prepared; i++) animator.Update(1f / 60f);
        animation.BeginActive();
        for (int i = 0; i < 12; i++) animator.Update(1f / 60f);
        Assert.That(animation.ActiveEntered, Is.False);
        for (int i = 0; i < 180 && !animation.ActiveEntered; i++) animator.Update(1f / 60f);
        Assert.That(animation.ActiveEntered, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RingCanReturnWhenStoppedDuringWindupOrRecovery(bool recovering)
    {
        var visual = new GameObject("Interrupted ring test"); visual.transform.SetParent(root.transform);
        visual.AddComponent<SpriteRenderer>();
        var animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2RingIdle.controller");
        var animation = visual.AddComponent<Boss2BlackHoleAnimation>();
        animator.Rebind(); animator.Update(0f); animation.Begin();
        for (int i = 0; i < 180 && !animation.Prepared; i++) animator.Update(1f / 60f);
        animation.BeginActive();
        if (recovering)
        {
            for (int i = 0; i < 180 && !animation.ActiveEntered; i++) animator.Update(1f / 60f);
            animation.BeginReturn();
        }
        animator.Update(0.1f);
        animation.BeginReturn(true);
        for (int i = 0; i < 180 && !animation.Completed; i++)
        {
            animator.Update(1f / 60f);
            if (animation.RecoveryCompleted) animation.BeginReturnTravel();
        }
        Assert.That(animation.Completed, Is.True);
        Assert.That(animation.Interrupted, Is.False);
    }
}

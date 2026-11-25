using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2CannonHandAnimationTests
{
    private GameObject root;
    private Animator animator;
    private SpriteRenderer renderer;
    private Boss2CannonHandAnimation hand;
    private Boss2CannonBeamVisual spawnedBeam;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Cannon hand animation test");
        renderer = root.AddComponent<SpriteRenderer>();
        animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2LowerHandIdle.controller");
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        hand = root.AddComponent<Boss2CannonHandAnimation>();
        animator.Rebind();
        animator.Update(0f);
    }

    [TearDown]
    public void TearDown()
    {
        if (spawnedBeam != null) Object.DestroyImmediate(spawnedBeam.gameObject);
        Object.DestroyImmediate(root);
    }

    [Test]
    public void CannonPreparationFireAndReturnReportActualAnimationCompletion()
    {
        hand.Begin();
        StepUntil(() => hand.Prepared);
        Assert.That(hand.Prepared, Is.True);
        Assert.That(hand.Interrupted, Is.False);
        Sprite prepared = renderer.sprite;
        Assert.That(prepared, Is.Not.Null);

        hand.BeginFire();
        StepUntil(() => hand.FireEntered);
        Assert.That(hand.FireEntered, Is.True);
        bool changedDuringFire = false;
        for (int i = 0; i < 180 && !hand.FireCompleted; i++)
        {
            animator.Update(1f / 60f);
            if (renderer.sprite != prepared) changedDuringFire = true;
        }
        Assert.That(hand.FireCompleted, Is.True);
        Assert.That(changedDuringFire, Is.True, "The firing reaction frames were not shown.");

        hand.BeginReturn();
        StepUntil(() => hand.Completed);
        Assert.That(hand.Completed, Is.True);
        Assert.That(hand.Interrupted, Is.False);
        Assert.That(hand.Active, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CannonCanRepeatShortFireMotionsBeforeReturning(bool upperHand)
    {
        if (upperHand)
        {
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Boss/Boss2/Boss2UpperHandIdle.controller");
            animator.Rebind();
            animator.Update(0f);
        }
        hand.Begin();
        StepUntil(() => hand.Prepared);
        hand.BeginFire();
        StepUntil(() => hand.FireCompleted);
        for (int shot = 0; shot < 3; shot++)
        {
            hand.RepeatFire();
            Assert.That(hand.FireEntered, Is.False);
            StepUntil(() => hand.FireEntered && hand.FireCompleted);
            Assert.That(hand.FireEntered && hand.FireCompleted, Is.True);
            Assert.That(hand.Interrupted, Is.False);
        }
        hand.BeginReturn();
        StepUntil(() => hand.Completed);
        Assert.That(hand.Completed, Is.True);
        Assert.That(hand.Interrupted, Is.False);
    }

    [Test]
    public void PreparedHandCanReturnWithoutFiringWhenTargetIsLost()
    {
        hand.Begin();
        StepUntil(() => hand.Prepared);
        Assert.That(hand.Prepared, Is.True);
        hand.BeginReturn();
        StepUntil(() => hand.Completed);
        Assert.That(hand.Completed, Is.True);
        Assert.That(hand.FireEntered, Is.False);
        Assert.That(hand.Interrupted, Is.False);
    }

    [Test]
    public void CancelledHandDoesNotReportNormalCompletion()
    {
        hand.Begin();
        StepUntil(() => hand.Prepared);
        Assert.That(hand.Prepared, Is.True);
        hand.BeginFire();
        StepUntil(() => hand.FireEntered);
        hand.Cancel();
        for (int i = 0; i < 10; i++) animator.Update(1f / 60f);
        Assert.That(hand.Interrupted, Is.True);
        Assert.That(hand.Completed, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeathCanReturnFromUnfinishedPreparationOrFiring(bool duringFire)
    {
        hand.Begin();
        if (duringFire)
        {
            StepUntil(() => hand.Prepared);
            hand.BeginFire();
            StepUntil(() => hand.FireEntered);
        }
        // Also covers a death before the prepare state has actually entered.
        hand.BeginDeathReturn();
        StepUntil(() => hand.Completed);
        Assert.That(hand.Completed, Is.True);
        Assert.That(hand.Interrupted, Is.False);
        Assert.That(hand.Active, Is.False);
    }

    [Test]
    public void UpperHandFlipsOnlyDuringCannonAndRestoresItsScale()
    {
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2UpperHandIdle.controller");
        SetField(hand, typeof(Boss2CannonHandAnimation), "flipVerticallyDuringCannon", true);
        animator.Rebind();
        animator.Update(0f);
        Vector3 original = root.transform.localScale;
        hand.Begin();
        StepUntil(() => hand.Prepared);
        Assert.That(hand.Prepared, Is.True);
        Assert.That(root.transform.localScale.y, Is.EqualTo(-original.y));
        hand.BeginFire();
        StepUntil(() => hand.FireCompleted);
        Assert.That(hand.FireCompleted, Is.True);
        hand.BeginReturn();
        StepUntil(() => hand.Completed);
        Assert.That(hand.Completed, Is.True);
        Assert.That(root.transform.localScale, Is.EqualTo(original));
        hand.Begin();
        StepUntil(() => hand.Prepared);
        hand.Cancel();
        Assert.That(root.transform.localScale, Is.EqualTo(original));
    }

    [Test]
    public void CannonPatternWaitsForPreparationFireAndReturnPresentation()
    {
        var part = root.AddComponent<Boss2PartController>();
        var bossObject = new GameObject("Cannon pattern test");
        bossObject.transform.SetParent(root.transform);
        var pattern = bossObject.AddComponent<Boss2CannonBeamPattern>();
        var aim = root.AddComponent<Boss2PartAim>();
        var pivot = new GameObject("Aim pivot").transform;
        pivot.SetParent(root.transform, false);
        var mirror = new GameObject("Visual group").transform;
        mirror.SetParent(pivot, false);
        var firePoint = new GameObject("Fire point").transform;
        firePoint.SetParent(mirror, false);
        SetField(aim, typeof(Boss2PartAim), "aimPivot", pivot);
        SetField(aim, typeof(Boss2PartAim), "visualGroup", mirror);
        SetField(aim, typeof(Boss2PartAim), "firePoint", firePoint);
        SetField(part, typeof(Boss2PartController), "aim", aim);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss2/Boss2CannonBeamVisual.prefab");
        Assert.That(prefab, Is.Not.Null);
        SetField(pattern, typeof(Boss2CannonBeamPattern), "beamPrefab", prefab.GetComponent<Boss2CannonBeamVisual>());

        var beginPreparation = typeof(Boss2CannonBeamPattern).GetMethod("BeginAttackPreparation", BindingFlags.Instance | BindingFlags.NonPublic);
        beginPreparation.Invoke(pattern, new object[] { part });
        Assert.That(hand.Active, Is.True);
        RunPresentation(pattern, "WaitForAttackPreparation", part);
        Assert.That(hand.Prepared, Is.True);
        var beforeBeams = Object.FindObjectsByType<Boss2CannonBeamVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var beginAim = typeof(Boss2CannonBeamPattern).GetMethod("BeginAimPresentation", BindingFlags.Instance | BindingFlags.NonPublic);
        beginAim.Invoke(pattern, new object[] { part, Vector3.zero, Vector2.right, 6f });
        foreach (var candidate in Object.FindObjectsByType<Boss2CannonBeamVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (System.Array.IndexOf(beforeBeams, candidate) < 0) spawnedBeam = candidate;
        Assert.That(spawnedBeam, Is.Not.Null);
        RunPresentation(pattern, "StartAttackPresentation", part);
        Assert.That(hand.FireEntered, Is.True);
        RunPresentation(pattern, "WaitForAttackPresentation", part);
        Assert.That(hand.FireCompleted, Is.True);
        RunPresentation(pattern, "ReturnAttackPresentation", part);
        Assert.That(hand.Completed, Is.True);
        Assert.That(hand.Interrupted, Is.False);
    }

    [Test]
    public void CannonPreparationStartsInTheSameTickAsMovement()
    {
        var part = root.AddComponent<Boss2PartController>();
        SetField(part, typeof(Boss2PartController), "partId", Boss2PartId.LowerLeftHand);
        var aim = root.AddComponent<Boss2PartAim>();
        var pivot = new GameObject("Aim pivot").transform;
        pivot.SetParent(root.transform, false);
        var mirror = new GameObject("Visual group").transform;
        mirror.SetParent(pivot, false);
        var firePoint = new GameObject("Fire point").transform;
        firePoint.SetParent(mirror, false);
        SetField(aim, typeof(Boss2PartAim), "aimPivot", pivot);
        SetField(aim, typeof(Boss2PartAim), "visualGroup", mirror);
        SetField(aim, typeof(Boss2PartAim), "firePoint", firePoint);
        SetField(part, typeof(Boss2PartController), "aim", aim);

        var bossObject = new GameObject("Cannon movement test");
        bossObject.transform.SetParent(root.transform);
        var boss = bossObject.AddComponent<Boss2Controller>();
        var health = bossObject.GetComponent<BossHealth>();
        SetField(health, typeof(BossHealth), "currentHp", health.MaxHp);
        SetField(boss, typeof(Boss2Controller), "health", health);
        SetField(boss, typeof(Boss2Controller), "lowerLeftHand", part);
        var pattern = bossObject.AddComponent<Boss2CannonBeamPattern>();

        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        Assert.That(playerPrefab, Is.Not.Null);
        var player = Object.Instantiate(playerPrefab);
        player.transform.SetParent(root.transform);
        player.transform.position = new Vector3(20f, 0f, 0f);
        var target = player.GetComponent<PlayerHealth>();
        Assert.That(target, Is.Not.Null);
        var areaObject = new GameObject("Attack area");
        areaObject.transform.SetParent(root.transform);
        areaObject.transform.position = new Vector3(4f, 0f, 0f);
        var area = areaObject.AddComponent<Boss2BasicAttackArea>();
        SetField(area, typeof(Boss2BasicAttackArea), "size", Vector2.zero);
        var beamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss2/Boss2CannonBeamVisual.prefab");
        Assert.That(beamPrefab, Is.Not.Null);
        SetField(pattern, typeof(Boss2BasicPatternBase), "movementArea", area);
        SetField(pattern, typeof(Boss2BasicPatternBase), "target", target);
        SetField(pattern, typeof(Boss2CannonBeamPattern), "beamPrefab", beamPrefab.GetComponent<Boss2CannonBeamVisual>());

        Assert.That(boss.BeginCombat(), Is.True);
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True);
        typeof(Boss2PatternRunner).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runner, null);
        Assert.That(runner.State, Is.EqualTo(Boss2ExecutionState.Running), runner.Error?.ToString());
        Assert.That(part.IsMoving, Is.True);
        Assert.That(hand.Active, Is.True);

        Sprite before = renderer.sprite;
        for (int i = 0; i < 15; i++) animator.Update(1f / 60f);
        Assert.That(part.IsMoving, Is.True);
        Assert.That(renderer.sprite, Is.Not.EqualTo(before), "The hand did not animate while moving.");
        runner.Cancel();
    }

    private void RunPresentation(Boss2CannonBeamPattern pattern, string method, Boss2PartController part)
    {
        var info = typeof(Boss2CannonBeamPattern).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null);
        var routine = (IEnumerator)info.Invoke(pattern, new object[] { part });
        for (int i = 0; i < 180; i++)
        {
            if (!routine.MoveNext()) return;
            animator.Update(1f / 60f);
            if (spawnedBeam != null) spawnedBeam.GetComponent<Animator>().Update(1f / 60f);
        }
        Assert.Fail($"{method} did not finish.");
    }

    private static void SetField(object target, System.Type type, string field, object value) =>
        type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private void StepUntil(System.Func<bool> complete)
    {
        for (int i = 0; i < 180 && !complete(); i++) animator.Update(1f / 60f);
    }
}

using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2BasicAttackTests
{
    private GameObject root;
    private Boss2Controller boss;

    private static void Set(object target, System.Type type, string field, object value) =>
        type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Boss2 basic attack test");
        boss = root.AddComponent<Boss2Controller>();
        var health = root.GetComponent<BossHealth>();
        Set(health, typeof(BossHealth), "currentHp", health.MaxHp);
        Set(boss, typeof(Boss2Controller), "health", health);
        string[] fields = { "upperLeftHand", "upperRightHand", "lowerLeftHand", "lowerRightHand", "ring" };
        for (int i = 0; i < fields.Length; i++)
        {
            var child = new GameObject(fields[i]);
            child.transform.SetParent(root.transform);
            var part = child.AddComponent<Boss2PartController>();
            Set(part, typeof(Boss2PartController), "partId", (Boss2PartId)i);
            Set(boss, typeof(Boss2Controller), fields[i], part);
        }
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void ReturnSpeedCurveControlsSpeedAndPreservesArrival()
    {
        var part = boss.GetPart(Boss2PartId.LowerLeftHand);
        Set(part, typeof(Boss2PartController), "idleAnchor", root.transform);
        part.ReturnToIdle(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        var field = typeof(Boss2PartController).GetField("returnProgressCurve", BindingFlags.Instance | BindingFlags.NonPublic);
        var progress = (AnimationCurve)field.GetValue(part);
        Assert.That(progress.Evaluate(0f), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(progress.Evaluate(0.5f), Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(progress.Evaluate(1f), Is.EqualTo(1f).Within(0.0001f));

        part.ReturnToIdle(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        progress = (AnimationCurve)field.GetValue(part);
        Assert.That(progress.Evaluate(0.5f), Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(progress.Evaluate(1f), Is.EqualTo(1f).Within(0.0001f));
        part.ReturnToIdle(1f);
        Assert.That(field.GetValue(part), Is.Null, "A regular return must not reuse the cannon speed curve.");
    }

    [Test]
    public void ZeroReturnSpeedCurveIsRejectedBeforeMovementStarts()
    {
        var part = boss.GetPart(Boss2PartId.LowerLeftHand);
        Set(part, typeof(Boss2PartController), "idleAnchor", root.transform);
        Assert.Throws<System.ArgumentException>(() => part.ReturnToIdle(1f, AnimationCurve.Linear(0f, 0f, 1f, 0f)));
        Assert.That(part.IsMoving, Is.False);
    }

    [Test]
    public void CannonUsesAnAvailableHandWhenOthersAreReservedOrOwned()
    {
        var cannon = root.AddComponent<Boss2CannonBeamPattern>();
        Assert.That(cannon.Kind, Is.EqualTo(Boss2PatternKind.Basic));
        boss.PartUsage.TryReserve(Boss2Parts.UpperHands | Boss2Parts.LowerLeftHand, Boss2PatternKind.Main, Boss2Parts.None, out var main);
        Assert.That(cannon.RequiredParts, Is.EqualTo(Boss2Parts.LowerRightHand));
        boss.PartUsage.TryActivate(main);
        Assert.That(cannon.RequiredParts, Is.EqualTo(Boss2Parts.LowerRightHand));
        boss.PartUsage.TryReserve(Boss2Parts.LowerRightHand, Boss2PatternKind.Main, Boss2Parts.None, out var right);
        Assert.That(cannon.RequiredParts, Is.EqualTo(Boss2Parts.None));
        boss.PartUsage.Release(main);
        Assert.That(cannon.RequiredParts, Is.EqualTo(Boss2Parts.UpperLeftHand));
        boss.PartUsage.Release(right);
    }

    [Test]
    public void FourHandsCanReserveAtTheSameTime()
    {
        var cannon = root.AddComponent<Boss2CannonBeamPattern>();
        Assert.That(boss.BeginCombat(), Is.True);
        for (int i = 0; i < 4; i++)
        {
            Assert.That(boss.TryRequestPattern(cannon, out var runner), Is.True);
            Assert.That(runner.HeldParts, Is.EqualTo(Boss2PartMask.FromId((Boss2PartId)i)));
        }
        Assert.That(boss.Executions.Count, Is.EqualTo(4));
        Assert.That(boss.TryRequestPattern(cannon, out _), Is.False);
        boss.EndCombat();
    }

    [Test]
    public void VolleyAimsOneHandAtPlayerAndOthersWithinRadius()
    {
        var cannon = root.AddComponent<Boss2CannonBeamPattern>();
        Assert.That(boss.BeginCombat(), Is.True);
        var runners = new System.Collections.Generic.List<Boss2PatternRunner>();
        for (int i = 0; i < 4; i++)
        {
            Assert.That(boss.TryRequestPattern(cannon, out var runner), Is.True);
            runners.Add(runner);
        }
        var prepare = typeof(Boss2CannonBeamPattern).GetMethod("PrepareScheduledAttacks", BindingFlags.Instance | BindingFlags.NonPublic);
        var aim = typeof(Boss2CannonBeamPattern).GetMethod("GetAimPosition", BindingFlags.Instance | BindingFlags.NonPublic);
        var previous = Random.state;
        try
        {
            Random.InitState(415);
            prepare.Invoke(cannon, new object[] { runners });
            int directCount = 0;
            for (int i = 0; i < 4; i++)
            {
                var part = boss.GetPart((Boss2PartId)i);
                Vector2 target = (Vector2)aim.Invoke(cannon, new object[] { part, new Vector2(10f, 5f) });
                Vector2 moved = (Vector2)aim.Invoke(cannon, new object[] { part, new Vector2(11f, 7f) });
                float distance = Vector2.Distance(target, new Vector2(10f, 5f));
                if (distance < 0.00001f) directCount++;
                Assert.That(distance, Is.LessThanOrEqualTo(1.00001f));
                Assert.That(Vector2.Distance(moved - target, new Vector2(1f, 2f)), Is.LessThan(0.00001f));
            }
            Assert.That(directCount, Is.EqualTo(1));
        }
        finally { Random.state = previous; boss.EndCombat(); }
    }

    [Test]
    public void GuardSuppressionStopsCannonWithoutTakingTheLowerHands()
    {
        var cannon = root.AddComponent<Boss2CannonBeamPattern>();
        boss.PartUsage.TryReserve(Boss2Parts.UpperHands | Boss2Parts.Ring,
            Boss2PatternKind.Main, Boss2Parts.LowerHands, out var guard);
        Assert.That(cannon.RequiredParts, Is.EqualTo(Boss2Parts.None));
        Assert.That(boss.PartUsage.GetOwner(Boss2PartId.LowerLeftHand), Is.Null);
        boss.PartUsage.Release(guard);
        Assert.That(cannon.RequiredParts, Is.EqualTo(Boss2Parts.UpperLeftHand));
    }

    [Test]
    public void VolleyExcludesReservedHandUntilTheNextAttack()
    {
        for (int i = 0; i < 4; i++)
        {
            var part = boss.GetPart((Boss2PartId)i);
            var aim = part.gameObject.AddComponent<Boss2PartAim>();
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(part.transform, false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(pivot, false);
            visual.gameObject.AddComponent<SpriteRenderer>();
            var animator = visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Boss/Boss2/Boss2LowerHandIdle.controller");
            visual.gameObject.AddComponent<Boss2CannonHandAnimation>();
            animator.Rebind();
            animator.Update(0f);
            var firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(visual, false);
            Set(aim, typeof(Boss2PartAim), "aimPivot", pivot);
            Set(aim, typeof(Boss2PartAim), "visualGroup", visual);
            Set(aim, typeof(Boss2PartAim), "firePoint", firePoint);
            Set(part, typeof(Boss2PartController), "aim", aim);
        }
        var cannon = root.AddComponent<Boss2CannonBeamPattern>();
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        var player = Object.Instantiate(playerPrefab);
        player.transform.SetParent(root.transform);
        player.transform.position = new Vector3(20f, 0f, 0f);
        var area = new GameObject("Attack area").AddComponent<Boss2BasicAttackArea>();
        area.transform.SetParent(root.transform);
        area.transform.position = Vector3.zero;
        Set(area, typeof(Boss2BasicAttackArea), "size", Vector2.zero);
        Set(area, typeof(Boss2BasicAttackArea), "minimumTravelDistance", 0f);
        var beam = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss2/Boss2CannonBeamVisual.prefab");
        Set(cannon, typeof(Boss2BasicPatternBase), "target", player.GetComponent<PlayerHealth>());
        Set(cannon, typeof(Boss2BasicPatternBase), "movementArea", area);
        Set(cannon, typeof(Boss2CannonBeamPattern), "beamPrefab", beam.GetComponent<Boss2CannonBeamVisual>());
        Set(cannon, typeof(Boss2BasicPatternBase), "remainingInterval", 0f);
        Assert.That(boss.BeginCombat(), Is.True);
        Assert.That(boss.PartUsage.TryReserve(Boss2Parts.UpperLeftHand, Boss2PatternKind.Main, Boss2Parts.None, out var main), Is.True);
        var update = typeof(Boss2BasicPatternBase).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        update.Invoke(cannon, null);
        Assert.That(boss.Executions.Count, Is.EqualTo(3));
        Assert.That(boss.PartUsage.GetReservation(Boss2PartId.UpperLeftHand), Is.SameAs(main));
        var tick = typeof(Boss2PatternRunner).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var runner in boss.Executions) tick.Invoke(runner, null);
        for (int i = 1; i < 4; i++)
        {
            var part = boss.GetPart((Boss2PartId)i);
            Assert.That(part.IsMoving, Is.True);
            Assert.That(part.GetComponentInChildren<Boss2CannonHandAnimation>().Active, Is.True);
        }
        boss.PartUsage.Release(main);
        Set(cannon, typeof(Boss2BasicPatternBase), "remainingInterval", 0f);
        update.Invoke(cannon, null);
        Assert.That(boss.Executions.Count, Is.EqualTo(3));
        foreach (var runner in new System.Collections.Generic.List<Boss2PatternRunner>(boss.Executions)) runner.Cancel();
        update.Invoke(cannon, null);
        Assert.That(boss.Executions.Count, Is.EqualTo(4));
        boss.EndCombat();
    }

    [Test]
    public void RandomDestinationsStayInsideTheAreaAndRespectBothDistances()
    {
        var area = root.AddComponent<Boss2BasicAttackArea>();
        root.transform.SetPositionAndRotation(new Vector3(3f, 2f, 0f), Quaternion.Euler(0f, 0f, 25f));
        Vector3 origin = root.transform.position;
        origin.z = 0.1f;
        Vector2 player = origin + Vector3.right;
        Random.State previous = Random.state;
        try
        {
            Random.InitState(415);
            for (int i = 0; i < 64; i++)
            {
                Assert.That(area.TryGetPosition(origin, player, out var point), Is.True);
                Vector3 local = root.transform.InverseTransformPoint(point);
                Assert.That(Mathf.Abs(local.x), Is.LessThanOrEqualTo(5.001f));
                Assert.That(Mathf.Abs(local.y), Is.LessThanOrEqualTo(1.901f));
                Assert.That(Vector2.Distance(point, origin), Is.GreaterThanOrEqualTo(1.5f));
                Assert.That(Vector2.Distance(point, player), Is.GreaterThanOrEqualTo(2f));
                Assert.That(point.z, Is.EqualTo(origin.z));
            }
        }
        finally { Random.state = previous; }
    }

    [Test]
    public void HandDestinationsStayOnTheirSideOfTheAttackArea()
    {
        var area = root.AddComponent<Boss2BasicAttackArea>();
        root.transform.position = new Vector3(3f, 0f, 0f);
        var previous = Random.state;
        try
        {
            Random.InitState(415);
            for (int i = 0; i < 32; i++)
            {
                Assert.That(area.TryGetPosition(new Vector3(20f, 20f), new Vector2(20f, 20f), null, true, out var left), Is.True);
                Assert.That(area.TryGetPosition(new Vector3(20f, 20f), new Vector2(20f, 20f), null, false, out var right), Is.True);
                Assert.That(left.x, Is.LessThanOrEqualTo(3f));
                Assert.That(right.x, Is.GreaterThanOrEqualTo(3f));
            }
        }
        finally { Random.state = previous; }
    }

    [Test]
    public void DestinationsPreferSeparationButAllowOverlapWhenNecessary()
    {
        var area = root.AddComponent<Boss2BasicAttackArea>();
        var others = new System.Collections.Generic.List<Vector3> { Vector3.zero };
        var previous = Random.state;
        try
        {
            Random.InitState(415);
            Assert.That(area.TryGetPosition(new Vector3(-10f, 0f), new Vector2(20f, 0f), others, out var separated), Is.True);
            Assert.That(Vector2.Distance(separated, Vector2.zero), Is.GreaterThanOrEqualTo(1f));
            Set(area, typeof(Boss2BasicAttackArea), "size", Vector2.zero);
            Assert.That(area.TryGetPosition(new Vector3(-10f, 0f), new Vector2(20f, 0f), others, out var overlapping), Is.True);
            Assert.That(overlapping, Is.EqualTo(Vector3.zero));
        }
        finally { Random.state = previous; }
    }

    [Test]
    public void ImpossibleAreaSkipsTheAttackInsteadOfUsingAnUnsafePoint()
    {
        var area = root.AddComponent<Boss2BasicAttackArea>();
        Set(area, typeof(Boss2BasicAttackArea), "size", Vector2.zero);
        Vector3 origin = Vector3.zero;
        Assert.That(area.TryGetPosition(origin, new Vector2(10f, 10f), out var point), Is.False);
        Assert.That(point, Is.EqualTo(origin));
    }
}

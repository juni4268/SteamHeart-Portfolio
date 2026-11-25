using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2TrackingLaserPatternTests
{
    private GameObject root;
    private Boss2Controller boss;
    private Boss2TrackingLaserPattern pattern;

    private static void Set(object target, System.Type type, string field, object value) =>
        type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Tracking Laser test");
        boss = root.AddComponent<Boss2Controller>();
        BossHealth health = root.GetComponent<BossHealth>();
        Set(health, typeof(BossHealth), "currentHp", health.MaxHp);
        Set(boss, typeof(Boss2Controller), "health", health);
        string[] fields = { "upperLeftHand", "upperRightHand", "lowerLeftHand", "lowerRightHand", "ring" };
        for (int i = 0; i < fields.Length; i++)
        {
            GameObject child = new GameObject(fields[i]);
            child.transform.SetParent(root.transform);
            Boss2PartController part = child.AddComponent<Boss2PartController>();
            Set(part, typeof(Boss2PartController), "partId", (Boss2PartId)i);
            Set(boss, typeof(Boss2Controller), fields[i], part);
        }
        pattern = root.AddComponent<Boss2TrackingLaserPattern>();
        Assert.That(boss.BeginCombat(), Is.True);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [Test]
    public void PrefersRightAndUsesLeftWhenRightIsReserved()
    {
        Assert.That(pattern.RequiredParts, Is.EqualTo(Boss2Parts.Ring | Boss2Parts.UpperRightHand));
        Assert.That(boss.PartUsage.TryReserve(Boss2Parts.UpperRightHand, Boss2PatternKind.Main,
            Boss2Parts.None, out var right), Is.True);
        Assert.That(pattern.RequiredParts, Is.EqualTo(Boss2Parts.Ring | Boss2Parts.UpperLeftHand));
        Assert.That(boss.PartUsage.TryReserve(Boss2Parts.UpperLeftHand, Boss2PatternKind.Main,
            Boss2Parts.None, out var left), Is.True);
        Assert.That(pattern.RequiredParts, Is.EqualTo(Boss2Parts.None));
        boss.PartUsage.Release(left);
        boss.PartUsage.Release(right);
    }

    [Test]
    public void QueuedRequestWaitsUntilAnUpperHandCanBeReserved()
    {
        Assert.That(boss.PartUsage.TryReserve(Boss2Parts.UpperHands, Boss2PatternKind.Main,
            Boss2Parts.None, out var busyHands), Is.True);
        Assert.That(boss.PartUsage.TryActivate(busyHands), Is.True);
        pattern.Request();
        InvokeUpdate();
        Assert.That(boss.Executions, Is.Empty);

        Assert.That(boss.PartUsage.ReleaseParts(busyHands, Boss2Parts.UpperLeftHand), Is.True);
        InvokeUpdate();
        Assert.That(boss.Executions.Count, Is.EqualTo(1));
        Assert.That(boss.Executions[0].HeldParts, Is.EqualTo(Boss2Parts.Ring | Boss2Parts.UpperLeftHand));
        boss.EndCombat();
        boss.PartUsage.Release(busyHands);
    }

    [Test]
    public void WarningAndFirstLaserUseThePlayerPositionWhenTheWarningStarts()
    {
        VerifyLaserStartAndHandRelease(Boss2PartId.UpperRightHand);
    }

    [Test]
    public void LeftHandAnimationAndReleaseAreUsedWhenRightIsUnavailable()
    {
        Assert.That(boss.PartUsage.TryReserve(Boss2Parts.UpperRightHand, Boss2PatternKind.Main,
            Boss2Parts.None, out var right), Is.True);
        try { VerifyLaserStartAndHandRelease(Boss2PartId.UpperLeftHand); }
        finally { boss.PartUsage.Release(right); }
    }

    [Test]
    public void RightHandBasicAttackFinishesBeforeTheMainPatternStarts()
    {
        Assert.That(boss.PartUsage.TryReserve(Boss2Parts.UpperRightHand, Boss2PatternKind.Basic,
            Boss2Parts.None, out var basic), Is.True);
        Assert.That(boss.PartUsage.TryActivate(basic), Is.True);
        pattern.Request();
        InvokeUpdate();
        Assert.That(boss.Executions.Count, Is.EqualTo(1));
        Assert.That(boss.Executions[0].HeldParts, Is.EqualTo(Boss2Parts.Ring | Boss2Parts.UpperRightHand));
        Assert.That(boss.Executions[0].State, Is.EqualTo(Boss2ExecutionState.WaitingForParts));
        Assert.That(boss.PartUsage.GetOwner(Boss2PartId.UpperRightHand), Is.SameAs(basic));
        boss.EndCombat();
        boss.PartUsage.Release(basic);
    }

    private void VerifyLaserStartAndHandRelease(Boss2PartId handId)
    {
        var hand = boss.GetPart(handId);
        var visual = new GameObject("Gesture visual");
        visual.transform.SetParent(hand.transform);
        visual.AddComponent<SpriteRenderer>();
        var animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2UpperHandIdle.controller");
        var gesture = visual.AddComponent<Boss2TrackingLaserHandAnimation>();
        animator.Rebind();
        animator.Update(0f);
        var ring = boss.GetPart(Boss2PartId.Ring);
        ring.gameObject.AddComponent<SpriteRenderer>();
        Set(ring, typeof(Boss2PartController), "idleAnchor", root.transform);
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.SetParent(root.transform);
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        player.transform.SetParent(root.transform);
        player.transform.position = new Vector3(1f, 0f);
        var ground = new GameObject("Ground");
        ground.transform.SetParent(root.transform);
        ground.layer = 31;
        ground.transform.position = new Vector3(0f, -5f);
        ground.AddComponent<BoxCollider2D>().size = new Vector2(20f, 1f);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss2/Boss2TrackingLaser.prefab");
        Set(pattern, typeof(Boss2TrackingLaserPattern), "gameplayCamera", camera);
        Set(pattern, typeof(Boss2TrackingLaserPattern), "target", player.GetComponent<PlayerHealth>());
        Set(pattern, typeof(Boss2TrackingLaserPattern), "laserPrefab", prefab.GetComponent<Boss2TrackingLaserVisual>());
        Set(pattern, typeof(Boss2TrackingLaserPattern), "groundLayers", (LayerMask)(1 << 31));
        Set(pattern, typeof(Boss2TrackingLaserPattern), "playerLayers", (LayerMask)(1 << player.layer));
        Physics2D.SyncTransforms();
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True);
        var tick = typeof(Boss2PatternRunner).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
        Boss2TrackingLaserVisual laser = null;
        try
        {
            tick.Invoke(runner, null);
            for (int i = 0; i < 180 && !gesture.ForwardCompleted; i++) animator.Update(1f / 60f);
            Assert.That(gesture.ForwardCompleted, Is.True);
            tick.Invoke(runner, null);
            Assert.That(ring.IsMoving, Is.True);
            player.transform.position = new Vector3(4f, 0f);
            var destination = (Vector3)typeof(Boss2PartController).GetField("worldTarget", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ring);
            Assert.That(destination.x, Is.EqualTo(boss.transform.position.x).Within(0.0001f),
                "The ring must rise above the boss, independently of the player position.");
            Assert.That(destination.y, Is.GreaterThan(camera.transform.position.y + camera.orthographicSize));
            ring.transform.position = destination;
            Set(ring, typeof(Boss2PartController), "motion", Boss2PartMotion.HoldingWorldPosition);
            Set(ring, typeof(Boss2PartController), "heldWorldPosition", destination);
            Physics2D.SyncTransforms();
            Set(pattern, typeof(Boss2TrackingLaserPattern), "warningDuration", float.MaxValue);
            tick.Invoke(runner, null);
            Assert.That(runner.Error, Is.Null);
            foreach (var candidate in Object.FindObjectsByType<Boss2TrackingLaserVisual>(FindObjectsSortMode.None)) laser = candidate;
            Assert.That(laser, Is.Not.Null);
            var serialized = new SerializedObject(laser);
            Assert.That(laser.DamageBounds.center.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(laser.FireEntered, Is.False);
            var warningArea = (Boss1DebrisWarningArea)serialized.FindProperty("warningArea").objectReferenceValue;
            Assert.That(warningArea.GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(warningArea.GetComponent<SpriteRenderer>().bounds.center.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(((SpriteRenderer)serialized.FindProperty("beamRenderer").objectReferenceValue).enabled, Is.False);
            var laserAnimator = laser.GetComponent<Animator>();
            player.transform.position = new Vector3(-2f, 0f);
            Physics2D.SyncTransforms();
            tick.Invoke(runner, null);
            Assert.That(laser.FireEntered, Is.False, "The warning must not enter the damaging attack early.");
            Assert.That(laser.DamageBounds.center.x, Is.EqualTo(4f).Within(0.0001f),
                "The warning must stay at the saved X instead of following the player.");
            Set(pattern, typeof(Boss2TrackingLaserPattern), "warningDuration", 0f);
            tick.Invoke(runner, null);
            Assert.That(laser.FireEntered, Is.False, "The pattern must await actual firing animation entry.");
            for (int i = 0; i < 180 && !laser.FireEntered; i++) laserAnimator.Update(1f / 60f);
            Assert.That(laser.FireEntered, Is.True);
            tick.Invoke(runner, null);
            Assert.That(laser.DamageBounds.center.x, Is.EqualTo(4f).Within(0.0001f),
                "The first attack must start at the displayed warning, before tracking begins.");
            for (int i = 0; i < 180 && !gesture.Completed; i++) animator.Update(1f / 60f);
            tick.Invoke(runner, null);
            Assert.That(boss.PartUsage.GetOwner(handId), Is.Null);
        }
        finally
        {
            if (laser != null) Object.DestroyImmediate(laser.gameObject);
            runner.Cancel();
        }
    }

    [Test]
    public void GesturePlaysForwardAndBackBeforeReportingCompletion()
    {
        var visual = new GameObject("Animated hand");
        visual.transform.SetParent(root.transform);
        var renderer = visual.AddComponent<SpriteRenderer>();
        var animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2UpperHandIdle.controller");
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        var animation = visual.AddComponent<Boss2TrackingLaserHandAnimation>();
        animator.Rebind();
        animator.Update(0f);

        animation.Begin();
        var forwardFrames = new System.Collections.Generic.List<Sprite>();
        for (int i = 0; i < 180 && !animation.ForwardCompleted; i++)
        {
            animator.Update(1f / 60f);
            if (forwardFrames.Count == 0 || forwardFrames[forwardFrames.Count - 1] != renderer.sprite)
                forwardFrames.Add(renderer.sprite);
        }
        Assert.That(animation.ForwardCompleted, Is.True, "The forward gesture did not complete.");
        Assert.That(animation.Interrupted, Is.False);
        Assert.That(forwardFrames.Count, Is.GreaterThan(2), "The gesture must have intermediate frames to verify reverse playback.");

        animation.BeginReturn();
        var reverseFrames = new System.Collections.Generic.List<Sprite>();
        for (int i = 0; i < 180 && !animation.Completed; i++)
        {
            animator.Update(1f / 60f);
            if (!animation.Completed && (reverseFrames.Count == 0 || reverseFrames[reverseFrames.Count - 1] != renderer.sprite))
                reverseFrames.Add(renderer.sprite);
        }
        Assert.That(animation.Interrupted, Is.False);
        forwardFrames.Reverse();
        CollectionAssert.AreEqual(forwardFrames, reverseFrames, "The return must display every gesture frame in reverse order.");
        var state = animator.GetCurrentAnimatorStateInfo(0);
        Assert.That(animation.Completed, Is.True,
            $"The reverse gesture did not finish and leave its state. time={state.normalizedTime}, speed={state.speed}, loop={state.loop}, transition={animator.IsInTransition(0)}, parameter={animator.GetInteger(animator.parameters[0].name)}");
    }

    [Test]
    public void CancellingTheReturnDoesNotReportNormalCompletion()
    {
        var visual = new GameObject("Interrupted hand");
        visual.transform.SetParent(root.transform);
        visual.AddComponent<SpriteRenderer>();
        var animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2UpperHandIdle.controller");
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        var animation = visual.AddComponent<Boss2TrackingLaserHandAnimation>();
        animator.Rebind();
        animator.Update(0f);
        animation.Begin();
        for (int i = 0; i < 180 && !animation.ForwardCompleted; i++) animator.Update(1f / 60f);
        Assert.That(animation.ForwardCompleted, Is.True);
        animation.BeginReturn();
        for (int i = 0; i < 5; i++) animator.Update(1f / 60f);
        animation.Cancel();
        for (int i = 0; i < 10; i++) animator.Update(1f / 60f);
        Assert.That(animation.Interrupted, Is.True);
        Assert.That(animation.Completed, Is.False);
    }

    private void InvokeUpdate() => typeof(Boss2TrackingLaserPattern)
        .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pattern, null);
}

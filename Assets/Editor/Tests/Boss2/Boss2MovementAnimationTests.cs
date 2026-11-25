using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class Boss2MovementAnimationTests
{
    private const string Folder = "Assets/Animations/Boss/Boss2/";
    private Scene scene;
    private GameObject root;
    private Boss2Controller boss;
    private Boss2BodyMovement bodyMovement;
    private readonly List<Visual> visuals = new List<Visual>();

    private sealed class Visual
    {
        public Animator Animator;
        public SpriteRenderer Renderer;
        public Boss2MovementAnimation Movement;
        public Transform Group;
        public Transform Muzzle;
    }

    private static void Set(object target, Type type, string field, object value) =>
        type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

    [SetUp]
    public void SetUp()
    {
        visuals.Clear();
        scene = EditorSceneManager.NewPreviewScene();
        root = new GameObject("Movement animation test");
        SceneManager.MoveGameObjectToScene(root, scene);
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
        bodyMovement = root.AddComponent<Boss2BodyMovement>();
        Set(bodyMovement, typeof(Boss2BodyMovement), "patrolling", true);
        Set(bodyMovement, typeof(Boss2BodyMovement), "currentSpeed", 1.5f);
        Assert.That(boss.BeginCombat(), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
    }

    private Visual CreateVisual(Boss2PartId? id)
    {
        Boss2PartController part = id.HasValue ? boss.GetPart(id.Value) : null;
        var group = new GameObject("Visual group");
        group.transform.SetParent(part != null ? part.transform : root.transform, false);
        bool left = id == Boss2PartId.UpperLeftHand || id == Boss2PartId.LowerLeftHand;
        group.transform.localScale = new Vector3(left ? -1f : 1f, 1f, 1f);
        var child = new GameObject("Visual");
        child.transform.SetParent(group.transform, false);
        string controller;
        switch (id)
        {
            case Boss2PartId.UpperLeftHand: controller = "Boss2UpperHandL.overrideController"; break;
            case Boss2PartId.UpperRightHand: controller = "Boss2UpperHandR.overrideController"; break;
            case Boss2PartId.LowerLeftHand: controller = "Boss2LowerHandL.overrideController"; break;
            case Boss2PartId.LowerRightHand: controller = "Boss2LowerHandR.overrideController"; break;
            case Boss2PartId.Ring: controller = "Boss2RingIdle.controller"; break;
            default: controller = "Boss2BodyIdle.controller"; break;
        }
        var renderer = child.AddComponent<SpriteRenderer>();
        var animator = child.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Folder + controller);
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(child.transform, false);
        var movement = child.AddComponent<Boss2MovementAnimation>();
        Set(movement, typeof(Boss2MovementAnimation), "controller", boss);
        Set(movement, typeof(Boss2MovementAnimation), "bodyMovement", bodyMovement);
        Set(movement, typeof(Boss2MovementAnimation), "part", part);
        Set(movement, typeof(Boss2MovementAnimation), "facingLeftParameter", "FacingLeft");
        animator.Rebind();
        animator.Update(0f);
        Invoke(movement, "OnEnable");
        var visual = new Visual { Animator = animator, Renderer = renderer, Movement = movement,
            Group = group.transform, Muzzle = muzzle };
        visuals.Add(visual);
        return visual;
    }

    private void Step(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            foreach (var visual in visuals) Invoke(visual.Movement, "Update");
            foreach (var visual in visuals) visual.Animator.Update(1f / 60f);
        }
    }

    private void StepUntil(Func<bool> condition)
    {
        for (int i = 0; i < 240 && !condition(); i++) Step();
        Assert.That(condition(), Is.True, "The Animator did not reach the requested action.");
    }

    private Boss2PartUsage.Reservation Own(Boss2PartId id, Boss2PatternKind kind)
    {
        Assert.That(boss.PartUsage.TryReserve(Boss2PartMask.FromId(id), kind, Boss2Parts.None, out var owner), Is.True);
        Assert.That(boss.PartUsage.TryActivate(owner), Is.True);
        return owner;
    }

    [Test]
    public void FourHandOverridesShareGraphsAndPlayTheirAuthoredSpritesWithCorrectFacing()
    {
        var ids = new[] { Boss2PartId.UpperLeftHand, Boss2PartId.UpperRightHand,
            Boss2PartId.LowerLeftHand, Boss2PartId.LowerRightHand };
        string[] parts = { "UpperNearHand", "UpperFarHand", "LowerNearHand", "LowerFarHand" };
        for (int i = 0; i < ids.Length; i++)
        {
            var visual = CreateVisual(ids[i]);
            Step(3);
            var overrides = (AnimatorOverrideController)visual.Animator.runtimeAnimatorController;
            var shared = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Folder +
                (i < 2 ? "Boss2UpperHandIdle.controller" : "Boss2LowerHandIdle.controller"));
            Assert.That(overrides.runtimeAnimatorController, Is.SameAs(shared));
            Assert.That(visual.Animator.GetCurrentAnimatorClipInfo(0)[0].clip,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Boss2Move" + parts[i] + ".anim")));
            Assert.That(AssetDatabase.GetAssetPath(visual.Renderer.sprite),
                Is.EqualTo("Assets/Graphics/Characters/Boss2/Boss2Move" + parts[i] + ".png"));
            Assert.That(visual.Renderer.flipX, Is.EqualTo(i % 2 == 0));
            Assert.That(visual.Group.localScale.x, Is.EqualTo(i % 2 == 0 ? -1f : 1f));
            Assert.That(visual.Muzzle.TransformVector(Vector3.right).normalized.x,
                Is.EqualTo(i % 2 == 0 ? -1f : 1f).Within(0.0001f));
        }
    }

    [Test]
    public void LeftwardMovementSwapsNearAndFarHandsWithoutMirroringTheRig()
    {
        var body = CreateVisual(null);
        var ids = new[] { Boss2PartId.UpperLeftHand, Boss2PartId.UpperRightHand,
            Boss2PartId.LowerLeftHand, Boss2PartId.LowerRightHand };
        var hands = new Visual[ids.Length];
        for (int i = 0; i < ids.Length; i++) hands[i] = CreateVisual(ids[i]);
        var ring = CreateVisual(Boss2PartId.Ring);
        Step(3);
        Assert.That(body.Renderer.flipX, Is.False);

        Set(bodyMovement, typeof(Boss2BodyMovement), "currentSpeed", -1.5f);
        Step(3);
        Assert.That(body.Renderer.flipX, Is.True);
        Assert.That(ring.Renderer.flipX, Is.True);
        string[] parts = { "UpperFarHand", "UpperNearHand", "LowerFarHand", "LowerNearHand" };
        for (int i = 0; i < hands.Length; i++)
        {
            Assert.That(hands[i].Animator.GetCurrentAnimatorClipInfo(0)[0].clip,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Boss2Move" + parts[i] + ".anim")));
            Assert.That(AssetDatabase.GetAssetPath(hands[i].Renderer.sprite),
                Is.EqualTo("Assets/Graphics/Characters/Boss2/Boss2Move" + parts[i] + ".png"));
            Assert.That(hands[i].Renderer.flipX, Is.EqualTo(i % 2 != 0));
            Assert.That(hands[i].Group.localScale.x, Is.EqualTo(i % 2 == 0 ? -1f : 1f));
            Assert.That(hands[i].Muzzle.TransformVector(Vector3.right).normalized.x,
                Is.EqualTo(i % 2 == 0 ? -1f : 1f).Within(0.0001f));
        }
        Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
        Set(bodyMovement, typeof(Boss2BodyMovement), "currentSpeed", 1.5f);
        Step(3);
        Assert.That(ring.Renderer.flipX, Is.False);
        Set(bodyMovement, typeof(Boss2BodyMovement), "currentSpeed", -1.5f);
        Step(3);
        Assert.That(ring.Renderer.flipX, Is.True);
        Set(bodyMovement, typeof(Boss2BodyMovement), "currentSpeed", 0f);
        Step(3);
        Assert.That(body.Renderer.flipX, Is.False);
        Assert.That(ring.Renderer.flipX, Is.False);
        foreach (var hand in hands) Assert.That(hand.Renderer.flipX, Is.False);
    }

    [Test]
    public void TheNearRightHandRestoresAttackFacingAndResumesItsLeftwardMove()
    {
        Set(bodyMovement, typeof(Boss2BodyMovement), "currentSpeed", -1.5f);
        var visual = CreateVisual(Boss2PartId.UpperRightHand);
        var cannon = visual.Animator.gameObject.AddComponent<Boss2CannonHandAnimation>();
        Set(cannon, typeof(Boss2CannonHandAnimation), "flipVerticallyDuringCannon", true);
        Step(3);
        Assert.That(visual.Renderer.flipX, Is.True);
        var owner = Own(Boss2PartId.UpperRightHand, Boss2PatternKind.Basic);
        cannon.Begin();
        StepUntil(() => cannon.Prepared);
        Assert.That(visual.Renderer.flipX, Is.False);
        cannon.BeginFire();
        StepUntil(() => cannon.FireCompleted);
        cannon.BeginReturn();
        StepUntil(() => cannon.Completed);
        Assert.That(cannon.Interrupted, Is.False);
        Assert.That(visual.Renderer.flipX, Is.False);
        Assert.That(visual.Animator.transform.localScale.y, Is.EqualTo(1f));
        boss.PartUsage.Release(owner);
        Step(3);
        Assert.That(visual.Renderer.flipX, Is.True);
        Assert.That(visual.Animator.GetCurrentAnimatorClipInfo(0)[0].clip,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Boss2MoveUpperNearHand.anim")));
        Assert.That(visual.Muzzle.TransformVector(Vector3.right).normalized.x, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void MovingLeftHandsEnterCannonDirectlyAndRestoreFacingBeforeReturningToMove()
    {
        foreach (var id in new[] { Boss2PartId.UpperLeftHand, Boss2PartId.LowerLeftHand })
        {
            var visual = CreateVisual(id);
            var cannon = visual.Animator.gameObject.AddComponent<Boss2CannonHandAnimation>();
            Set(cannon, typeof(Boss2CannonHandAnimation), "flipVerticallyDuringCannon", id == Boss2PartId.UpperLeftHand);
            Step(3);
            Assert.That(visual.Renderer.flipX, Is.True);
            var owner = Own(id, Boss2PatternKind.Basic);
            cannon.Begin();
            StepUntil(() => cannon.Prepared);
            Assert.That(visual.Renderer.flipX, Is.False);
            cannon.BeginFire();
            StepUntil(() => cannon.FireCompleted);
            cannon.BeginReturn();
            StepUntil(() => cannon.Completed);
            Assert.That(cannon.Interrupted, Is.False);
            Assert.That(visual.Renderer.flipX, Is.False);
            Assert.That(visual.Animator.transform.localScale.y, Is.EqualTo(1f));
            boss.PartUsage.Release(owner);
            Step(3);
            Assert.That(visual.Renderer.flipX, Is.True);
        }
    }

    [Test]
    public void TrackingGestureCanLeaveMoveAndPlayItsReturnWithoutInterruption()
    {
        var visual = CreateVisual(Boss2PartId.UpperLeftHand);
        var gesture = visual.Animator.gameObject.AddComponent<Boss2TrackingLaserHandAnimation>();
        Step(3);
        Own(Boss2PartId.UpperLeftHand, Boss2PatternKind.Main);
        gesture.Begin();
        StepUntil(() => gesture.ForwardCompleted);
        Assert.That(visual.Renderer.flipX, Is.False);
        gesture.BeginReturn();
        StepUntil(() => gesture.Completed);
        Assert.That(gesture.Interrupted, Is.False);
        Assert.That(visual.Renderer.flipX, Is.False);
    }

    [Test]
    public void RingCanLeaveMoveForTheFullBlackHoleSequence()
    {
        var visual = CreateVisual(Boss2PartId.Ring);
        var animation = visual.Animator.gameObject.AddComponent<Boss2BlackHoleAnimation>();
        Step(3);
        Own(Boss2PartId.Ring, Boss2PatternKind.Main);
        animation.Begin();
        StepUntil(() => animation.Prepared);
        animation.BeginActive();
        StepUntil(() => animation.ActiveEntered);
        animation.BeginReturn();
        StepUntil(() => animation.RecoveryCompleted);
        animation.BeginReturnTravel();
        StepUntil(() => animation.Completed);
        Assert.That(animation.Interrupted, Is.False);
        Assert.That(visual.Animator.GetBool("IsMoving"), Is.False);
    }

    [Test]
    public void OnlyFreeFollowingPartsUseLocomotionAndStoppingReturnsTheWholeBossToIdle()
    {
        var body = CreateVisual(null);
        var hand = CreateVisual(Boss2PartId.UpperRightHand);
        var ring = CreateVisual(Boss2PartId.Ring);
        Step(3);
        Assert.That(body.Animator.GetBool("IsMoving"), Is.True);
        Assert.That(hand.Animator.GetBool("IsMoving"), Is.True);
        var owner = Own(Boss2PartId.UpperRightHand, Boss2PatternKind.Basic);
        Step(3);
        Assert.That(hand.Animator.GetBool("IsMoving"), Is.False);
        Assert.That(ring.Animator.GetBool("IsMoving"), Is.True);
        boss.PartUsage.Release(owner);
        boss.GetPart(Boss2PartId.UpperRightHand).MoveToWorld(Vector3.right, 1f);
        Step(3);
        Assert.That(hand.Animator.GetBool("IsMoving"), Is.False);
        boss.GetPart(Boss2PartId.UpperRightHand).FollowIdle();
        Step(3);
        Assert.That(hand.Animator.GetBool("IsMoving"), Is.True);
        Set(bodyMovement, typeof(Boss2BodyMovement), "currentSpeed", 0f);
        Step(3);
        Assert.That(body.Animator.GetBool("IsMoving"), Is.False);
        Assert.That(hand.Animator.GetBool("IsMoving"), Is.False);
        Assert.That(ring.Animator.GetBool("IsMoving"), Is.False);
        Assert.That(body.Animator.GetCurrentAnimatorClipInfo(0)[0].clip,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Boss2BodyIdle.anim")));
    }
}

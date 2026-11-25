using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2RingBurstTests
{
    private GameObject arena;
    private Boss2RingBurstField field;
    private Collider2D[] walls;
    private PlayerData damageData;
    private int? originalRuntimeHealth;
    private static readonly Vector3 ArenaOffset = new Vector3(1000f, 1000f, 0f);
    private static readonly Vector3 Origin = ArenaOffset + new Vector3(0.5f, -2.51f, 0f);

    [SetUp]
    public void SetUp()
    {
        arena = new GameObject("Ring Burst test arena");
        walls = new[]
        {
            Wall(new Vector2(-5.75f, -2.35f), new Vector2(0.5f, 7f)),
            Wall(new Vector2(6.75f, -2.35f), new Vector2(0.5f, 7f)),
            Wall(new Vector2(0.46f, 1.4f), new Vector2(13.65f, 0.5f)),
            Wall(new Vector2(0.5f, -5.56f), new Vector2(13f, 1.1f))
        };
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss2/Boss2RingBurstField.prefab");
        field = Object.Instantiate(prefab).GetComponent<Boss2RingBurstField>();
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(field.gameObject);
        Object.DestroyImmediate(arena);
        if (damageData != null) Object.DestroyImmediate(damageData);
        if (originalRuntimeHealth.HasValue) PlayerRuntimeState.SetHealth(originalRuntimeHealth.Value);
        damageData = null;
        originalRuntimeHealth = null;
    }

    [TestCase(0f, 0f)]
    [TestCase(-0.05f, 0f)]
    [TestCase(0.05f, 0f)]
    public void FourRaysReflectWithoutSplittingAndRemainBounded(float x, float y)
    {
        Vector3 origin = Origin + new Vector3(x, y, 0f);
        field.Build(origin, walls, 0, 0, 3);
        Assert.That(field.Segments.Count, Is.EqualTo(10));
        var initial = field.Segments.Where(s => s.StartDistance == 0f).ToArray();
        Assert.That(initial.Length, Is.EqualTo(4));
        int[] reflections = { 1, 2, 2, 1 };
        for (int i = 0; i < initial.Length; i++)
        {
            var ray = initial[i];
            Assert.That(Vector2.Distance(ray.Start, origin), Is.LessThan(0.001f));
            for (int reflection = 0; reflection <= reflections[i]; reflection++)
            {
                var next = field.Segments.Where(s => Vector2.Distance(s.Start, ray.End) < 0.001f).ToArray();
                Assert.That(next.Length, Is.EqualTo(reflection < reflections[i] ? 1 : 0));
                if (next.Length == 0) break;
                Assert.That(next[0].StartDistance, Is.EqualTo(ray.EndDistance).Within(0.001f));
                ray = next[0];
            }
        }
        foreach (var segment in field.Segments)
        {
            Assert.That(segment.Length, Is.GreaterThan(0f));
            Vector2 localEnd = segment.End - (Vector2)ArenaOffset;
            Assert.That(localEnd.x, Is.InRange(-5.501f, 6.501f));
            Assert.That(localEnd.y, Is.InRange(-5.011f, 1.151f));
        }
    }

    [TestCase(0, 12)]
    [TestCase(1, 12)]
    [TestCase(2, 12)]
    [TestCase(3, 10)]
    public void EveryLayoutUsesItsAuthoredDirectionsAndRingHeight(int layoutIndex, int segmentCount)
    {
        Vector3 origin = field.GetRingDestination(layoutIndex, ArenaOffset + new Vector3(0.5f, -1.9f, 0f));
        Assert.That(origin.y - ArenaOffset.y, Is.EqualTo(layoutIndex == 3 ? -2.51f : -1.9f).Within(0.001f));
        field.Build(origin, walls, 0, 0, layoutIndex);
        Assert.That(field.Segments.Count, Is.EqualTo(segmentCount));
        var initial = field.Segments.Where(segment => segment.StartDistance == 0f).ToArray();
        Assert.That(initial.Length, Is.EqualTo(4));
        float[][] angles =
        {
            new[] { -35f, -80f, -160f, 35f },
            new[] { -20f, -110f, -140f, 80f },
            new[] { -23f, 43f, 135f, -164f },
            new[] { -7.5f, 47f, 133f, -172.5f }
        };
        for (int i = 0; i < initial.Length; i++)
        {
            Vector2 expected = Quaternion.Euler(0f, 0f, angles[layoutIndex][i]) * Vector2.right;
            Assert.That(Vector2.Angle(initial[i].End - initial[i].Start, expected), Is.LessThan(0.05f));
        }
    }

    [Test]
    public void RandomSelectionCanReachAllFourLayouts()
    {
        Random.State previous = Random.state;
        try
        {
            Random.InitState(12345);
            var selected = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 64; i++)
            {
                int index = field.SelectLayout();
                Assert.That(index, Is.InRange(0, 3));
                selected.Add(index);
            }
            Assert.That(selected, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
        }
        finally { Random.state = previous; }
    }

    [Test]
    public void SuccessiveSegmentsFollowTheWallReflection()
    {
        field.Build(Origin, walls, 0, 0, 3);
        foreach (var ray in field.Segments)
        {
            var next = field.Segments.Where(segment => Vector2.Distance(segment.Start, ray.End) < 0.001f).ToArray();
            if (next.Length == 0) continue;
            Assert.That(next.Length, Is.EqualTo(1));
            Vector2 direction = (ray.End - ray.Start).normalized;
            RaycastHit2D hit = Physics2D.RaycastAll(ray.Start, direction, ray.Length + 0.01f)
                .First(impact => walls.Contains(impact.collider) && impact.distance > 0.001f);
            Vector2 reflected = Vector2.Reflect(direction, hit.normal);
            Assert.That(Vector2.Angle(reflected, next[0].End - next[0].Start), Is.LessThan(0.05f));
        }
    }

    [Test]
    public void AllArrivalPointsRespectMinimumSpacing()
    {
        field.Build(Origin, walls, 0, 0, 3);
        float minimum = new SerializedObject(field).FindProperty("minimumEndpointSpacing").floatValue;
        for (int i = 0; i < field.Segments.Count; i++)
            for (int j = i + 1; j < field.Segments.Count; j++)
                Assert.That(Vector2.Distance(field.Segments[i].End, field.Segments[j].End),
                    Is.GreaterThanOrEqualTo(minimum - 0.001f));
    }

    [Test]
    public void CrowdedAuthoredArrivalsAreRejectedBeforeShowingWarning()
    {
        var so = new SerializedObject(field);
        so.FindProperty("minimumEndpointSpacing").floatValue = 15f;
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.Throws<System.InvalidOperationException>(() => field.Build(Origin, walls, 0, 0, 3));
        Assert.That(field.GetComponentsInChildren<Boss2CannonBeamVisual>(), Is.Empty);
    }

    [Test]
    public void DLayoutBlocksTheGroundAndLeavesACentralJumpWindow()
    {
        field.Build(Origin, walls, 0, 0, 3);
        var probe = new GameObject("Player collision probe", typeof(CapsuleCollider2D));
        probe.transform.SetParent(arena.transform);
        var collider = probe.GetComponent<CapsuleCollider2D>();
        collider.size = new Vector2(0.29f, 0.84f);
        float radius = new SerializedObject(field).FindProperty("beamWidth").floatValue * 0.5f;
        for (int i = 0; i <= 120; i++)
        {
            float x = Mathf.Lerp(-5.355f, 6.355f, i / 120f);
            probe.transform.position = ArenaOffset + new Vector3(x, -5.01f + 0.42f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(FieldHits(collider, radius), Is.True, $"Ground opening at {x}");
        }
        foreach (float x in new[] { 0.2f, 0.5f, 0.8f })
        {
            probe.transform.position = ArenaOffset + new Vector3(x, -5.01f + 1.1f + 0.42f, 0f);
            Physics2D.SyncTransforms();
            Assert.That(FieldHits(collider, radius), Is.False, $"Jump window blocked at {x}");
        }
    }

    [TestCase(0, 2f)]
    [TestCase(1, 0.5f)]
    [TestCase(2, -4.7f)]
    public void OtherLayoutsLeaveTheirAuthoredGroundOpening(int layoutIndex, float x)
    {
        Vector3 origin = field.GetRingDestination(layoutIndex, ArenaOffset + new Vector3(0.5f, -1.9f, 0f));
        field.Build(origin, walls, 0, 0, layoutIndex);
        var probe = new GameObject("Ground opening probe", typeof(CapsuleCollider2D));
        probe.transform.SetParent(arena.transform);
        var collider = probe.GetComponent<CapsuleCollider2D>();
        collider.size = new Vector2(0.29f, 0.84f);
        probe.transform.position = ArenaOffset + new Vector3(x, -5.01f + 0.42f, 0f);
        Physics2D.SyncTransforms();
        float radius = new SerializedObject(field).FindProperty("beamWidth").floatValue * 0.5f;
        Assert.That(FieldHits(collider, radius), Is.False);
    }

    [Test]
    public void WarningGrowsFromTheRingAndKeepsTheCompletedPath()
    {
        field.Build(Origin, walls, 0, 0, 3);
        var beams = field.GetComponentsInChildren<Boss2CannonBeamVisual>();
        field.ShowWarning(0f);
        Assert.That(beams.All(beam => !beam.GetComponent<SpriteRenderer>().enabled), Is.True);
        field.ShowWarning(0.05f);
        Assert.That(beams.Count(beam => beam.GetComponent<SpriteRenderer>().enabled), Is.EqualTo(4));
        field.ShowWarning(1f);
        Assert.That(field.GetComponentsInChildren<LineRenderer>(), Is.Empty);
        for (int i = 0; i < beams.Length; i++)
        {
            Assert.That(beams[i].GetComponent<SpriteRenderer>().enabled, Is.True);
            var so = new SerializedObject(beams[i]);
            var start = (Transform)so.FindProperty("startMarker").objectReferenceValue;
            var end = (Transform)so.FindProperty("endMarker").objectReferenceValue;
            Assert.That(Vector2.Distance(start.position, field.Segments[i].Start), Is.LessThan(0.001f));
            Assert.That(Vector2.Distance(end.position, field.Segments[i].End), Is.LessThan(0.001f));
        }
    }

    [Test]
    public void LaserEffectsStayHiddenUntilReleaseAndFollowOnlyReachedSegments()
    {
        field.Build(Origin, walls, 0, 0, 3);
        var glows = field.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Laser Glow").ToArray();
        Assert.That(glows.Length, Is.EqualTo(field.Segments.Count));
        field.ShowWarning(1f);
        Assert.That(glows.All(r => !r.enabled), Is.True, "The release effects must not flash during warning.");
        Assert.That(field.GetComponentInChildren<ParticleSystem>().particleCount, Is.Zero);
        PrepareField();
        field.Burst();
        AdvanceAttack(0.01f);
        AdvanceAttack(0.01f);
        Assert.That(glows.Count(r => r.enabled), Is.EqualTo(4));
        var properties = new MaterialPropertyBlock();
        foreach (var glow in glows.Where(r => r.enabled))
        {
            glow.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_HeadOn"), Is.EqualTo(1f));
            Assert.That(properties.GetFloat("_Head"), Is.GreaterThan(0f).And.LessThan(properties.GetFloat("_Length")));
        }
        field.gameObject.SetActive(false);
        typeof(Boss2RingBurstField).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(field, null); // Edit Mode does not dispatch runtime lifecycle callbacks.
        Assert.That(glows.All(r => !r.enabled), Is.True);
    }

    [Test]
    public void ReflectionImpactsAppearOnArrivalWithoutRepeatingAndClearOnCancel()
    {
        field.Build(Origin, walls, 0, 0, 3);
        PrepareField();
        var particles = field.GetComponentInChildren<ParticleSystem>();
        field.Burst();
        AdvanceAttack(0.01f);
        AdvanceAttack(0.01f);
        Assert.That(particles.particleCount, Is.Zero);
        float travel = new SerializedObject(field).FindProperty("attackTravelDuration").floatValue;
        float arrival = field.Segments.Min(segment => segment.EndDistance) / field.MaximumDistance * travel;
        AdvanceAttack(arrival);
        int count = particles.particleCount;
        Assert.That(count, Is.GreaterThan(0));
        field.TickAttack(0f);
        Assert.That(particles.particleCount, Is.EqualTo(count), "Holding a reached endpoint must not repeat its impact.");
        field.gameObject.SetActive(false);
        typeof(Boss2RingBurstField).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(field, null);
        Assert.That(particles.particleCount, Is.Zero);
        Assert.That(field.GetComponentsInChildren<MeshRenderer>().All(r => !r.enabled), Is.True);
    }

    [Test]
    public void CompletedAttackRemovesAllWarningAndAttackLines()
    {
        field.Build(Origin, walls, 0, 0, 3);
        PrepareField();
        field.Burst();
        for (int i = 0; i < 180 && !field.AttackCompleted; i++) AdvanceAttack(1f / 60f);
        Assert.That(field.AttackCompleted, Is.True);
        Assert.That(field.GetComponentsInChildren<SpriteRenderer>().All(renderer => !renderer.enabled), Is.True);
        Assert.That(field.GetComponentsInChildren<Boss2CannonBeamVisual>().All(beam => beam.Completed), Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void AttackImmediatelyHidesAllWarningsAndOnlyShowsTheTravellingPulse(int layoutIndex)
    {
        Vector3 origin = field.GetRingDestination(layoutIndex, ArenaOffset + new Vector3(.5f, -1.9f, 0f));
        field.Build(origin, walls, 0, 0, layoutIndex);
        field.ShowWarning(1f);
        var renderers = field.GetComponentsInChildren<SpriteRenderer>();
        Assert.That(renderers.Length, Is.EqualTo(field.Segments.Count));
        Assert.That(renderers.All(renderer => renderer.color.a < 1f), Is.True);
        PrepareField();
        field.Burst();
        Assert.That(renderers.All(renderer => !renderer.enabled), Is.True);
        field.ShowWarning(1f);
        Assert.That(renderers.All(renderer => !renderer.enabled), Is.True);
        AdvanceAttack(0.01f);
        AdvanceAttack(0.01f);
        var beams = field.GetComponentsInChildren<Boss2CannonBeamVisual>();
        for (int i = 0; i < field.Segments.Count; i++)
        {
            if (field.Segments[i].StartDistance == 0f)
            {
                Assert.That(renderers[i].enabled, Is.True);
                Assert.That(renderers[i].color.a, Is.EqualTo(1f));
                var end = (Transform)new SerializedObject(beams[i]).FindProperty("endMarker").objectReferenceValue;
                Assert.That(Vector2.Distance(end.position, field.Segments[i].Start), Is.LessThan(field.Segments[i].Length));
            }
            else Assert.That(renderers[i].enabled, Is.False);
        }
    }

    [Test]
    public void DamageWaitsForTheAttackFrontAndHitsEachPlayerOnlyOnce()
    {
        field.Build(Origin, walls, 0, 0, 3);
        PrepareField();
        var ray = field.Segments[0];
        var health = DamageTarget(ray.Start + (ray.End - ray.Start).normalized * 2f);
        field.Burst();
        AdvanceAttack(.01f);
        Assert.That(health.CurrentHP, Is.EqualTo(20));
        AdvanceAttack(.1f);
        Assert.That(health.CurrentHP, Is.EqualTo(19));
        AdvanceAttack(.02f);
        Assert.That(health.CurrentHP, Is.EqualTo(19));
    }

    [Test]
    public void FastAttackSweepsTheDistanceCrossedInALongFrame()
    {
        field.Build(Origin, walls, 0, 0, 3);
        PrepareField();
        var ray = field.Segments[0];
        var health = DamageTarget(ray.Start + (ray.End - ray.Start).normalized * 2f);
        field.Burst();
        AdvanceAttack(.001f);
        field.TickAttack(1f);
        Assert.That(health.CurrentHP, Is.EqualTo(19));
    }

    [Test]
    public void EnteringAPathAfterThePulsePassesDoesNotDamageThePlayer()
    {
        field.Build(Origin, walls, 0, 0, 3);
        PrepareField();
        field.Burst();
        for (int i = 0; i < 16; i++) AdvanceAttack(1f / 60f);
        var ray = field.Segments[0];
        var health = DamageTarget(ray.Start + (ray.End - ray.Start).normalized * .5f);
        AdvanceAttack(.01f);
        Assert.That(health.CurrentHP, Is.EqualTo(20));
    }

    [Test]
    public void ChargingBeamFiresAShortShotAndCompletesItsFade()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss2/Boss2RingBurstChargeBeam.prefab");
        var instance = Object.Instantiate(prefab);
        try
        {
            var animator = instance.GetComponent<Animator>();
            var beam = instance.GetComponent<Boss2CannonBeamVisual>();
            animator.Rebind();
            animator.Update(0f);
            beam.BeginAim();
            for (int i = 0; i < 90 && !beam.AimCompleted; i++) animator.Update(1f / 60f);
            Assert.That(beam.AimCompleted, Is.True);
            beam.BeginFire(0.15f);
            for (int i = 0; i < 30 && !beam.FireCompleted; i++) animator.Update(1f / 60f);
            Assert.That(beam.FireEntered, Is.True);
            Assert.That(beam.FireCompleted, Is.True);
            Assert.That(beam.Interrupted, Is.False);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).loop, Is.False);
            beam.BeginFade();
            for (int i = 0; i < 90 && !beam.Completed; i++) animator.Update(1f / 60f);
            Assert.That(beam.Completed, Is.True);
            Assert.That(beam.Interrupted, Is.False);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Prepare2WaitsForChargingAndPrepare1Completion(bool earlyChargeRequest)
    {
        var visual = new GameObject("Ring charge timing test", typeof(SpriteRenderer),
            typeof(Animator), typeof(Boss2RingBurstAnimation));
        visual.transform.SetParent(arena.transform, false);
        var animator = visual.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2RingIdle.controller");
        animator.Rebind();
        animator.Update(0f);
        var ring = visual.GetComponent<Boss2RingBurstAnimation>();
        ring.Begin();
        if (earlyChargeRequest) ring.BeginCharge();
        for (int i = 0; i < (earlyChargeRequest ? 10 : 120); i++) animator.Update(1f / 60f);
        Assert.That(ring.ChargeEntered, Is.False);
        Assert.That(ring.Prepared, Is.False);
        if (!earlyChargeRequest) ring.BeginCharge();
        for (int i = 0; i < 180 && !ring.Prepared; i++) animator.Update(1f / 60f);
        Assert.That(ring.ChargeEntered, Is.True);
        Assert.That(ring.Prepared, Is.True);
        Assert.That(ring.Interrupted, Is.False);
        ring.BeginLoop();
        for (int i = 0; i < 30 && !ring.LoopEntered; i++) animator.Update(1f / 60f);
        Assert.That(ring.LoopEntered, Is.True);
        Assert.That(ring.Interrupted, Is.False);
    }

    [Test]
    public void AttackWaitsForReturnThenReversesPrepare2BeforePrepare1()
    {
        var ring = RingReadyToReturn(out var animator);
        var renderer = ring.GetComponent<SpriteRenderer>();
        var prepare2 = RingSprites(RingState(animator, Boss2RingBurstStage.Charge).motion);
        var prepare1 = RingSprites(RingState(animator, Boss2RingBurstStage.Prepare).motion);
        for (int i = 0; i < 120; i++) animator.Update(1f / 60f);
        Assert.That(ring.Completed, Is.False);
        Assert.That(ring.ReturnChargeEntered, Is.False);
        Assert.That(ring.ReturnPrepareEntered, Is.False);

        ring.BeginReturn();
        for (int i = 0; i < 10 && !ring.ReturnChargeEntered; i++) animator.Update(1f / 60f);
        Assert.That(ring.ReturnChargeEntered, Is.True);
        Assert.That(ring.ReturnPrepareEntered, Is.False);
        int chargeStart = System.Array.IndexOf(prepare2, renderer.sprite);
        Assert.That(chargeStart, Is.GreaterThanOrEqualTo(prepare2.Length - 2));
        for (int i = 0; i < 12; i++) animator.Update(1f / 60f);
        Assert.That(System.Array.IndexOf(prepare2, renderer.sprite), Is.LessThan(chargeStart));

        for (int i = 0; i < 180 && !ring.ReturnPrepareEntered; i++) animator.Update(1f / 60f);
        Assert.That(ring.ReturnPrepareEntered, Is.True);
        Assert.That(ring.Completed, Is.False);
        int prepareStart = System.Array.IndexOf(prepare1, renderer.sprite);
        Assert.That(prepareStart, Is.GreaterThanOrEqualTo(prepare1.Length - 2));
        for (int i = 0; i < 10; i++) animator.Update(1f / 60f);
        Assert.That(System.Array.IndexOf(prepare1, renderer.sprite), Is.LessThan(prepareStart));
        for (int i = 0; i < 180 && !ring.Completed; i++) animator.Update(1f / 60f);
        Assert.That(ring.Completed, Is.True);
        Assert.That(ring.Active, Is.False);
        Assert.That(ring.Interrupted, Is.False);
    }

    [Test]
    public void ForwardAndReversePrepareShareTheSameEditableMotion()
    {
        var ring = RingReadyToReturn(out var animator);
        Assert.That(RingState(animator, Boss2RingBurstStage.ReturnCharge).motion,
            Is.SameAs(RingState(animator, Boss2RingBurstStage.Charge).motion));
        Assert.That(RingState(animator, Boss2RingBurstStage.ReturnPrepare).motion,
            Is.SameAs(RingState(animator, Boss2RingBurstStage.Prepare).motion));
        ring.Cancel();
    }

    [Test]
    public void CancellingTheReverseMotionDoesNotReportNormalCompletion()
    {
        var ring = RingReadyToReturn(out var animator);
        ring.BeginReturn();
        for (int i = 0; i < 10 && !ring.ReturnChargeEntered; i++) animator.Update(1f / 60f);
        Assert.That(ring.ReturnChargeEntered, Is.True);
        ring.Cancel();
        animator.Update(0f);
        Assert.That(ring.Interrupted, Is.True);
        Assert.That(ring.Completed, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RingReportsInterruptionWhenAnimatorBecomesUnavailable(bool removeController)
    {
        var visual = new GameObject("Ring interruption test", typeof(SpriteRenderer),
            typeof(Animator), typeof(Boss2RingBurstAnimation));
        visual.transform.SetParent(arena.transform, false);
        var animator = visual.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2RingIdle.controller");
        animator.Rebind();
        animator.Update(0f);
        var ring = visual.GetComponent<Boss2RingBurstAnimation>();
        ring.Begin();
        animator.Update(1f / 60f);
        Assert.That(ring.Active, Is.True);
        Assert.That(ring.Interrupted, Is.False);

        if (removeController) animator.runtimeAnimatorController = null;
        else animator.enabled = false;

        Assert.That(ring.Interrupted, Is.True);
        Assert.That(ring.Completed, Is.False);
    }

    private void PrepareField()
    {
        var animators = field.GetComponentsInChildren<Animator>();
        for (int i = 0; i < 90 && !field.Prepared; i++)
            foreach (Animator animator in animators) animator.Update(1f / 60f);
        Assert.That(field.Prepared, Is.True);
    }

    private Boss2RingBurstAnimation RingReadyToReturn(out Animator animator)
    {
        var visual = new GameObject("Ring reverse test", typeof(SpriteRenderer), typeof(Animator), typeof(Boss2RingBurstAnimation));
        visual.transform.SetParent(arena.transform, false);
        animator = visual.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Boss/Boss2/Boss2RingIdle.controller");
        animator.Rebind();
        animator.Update(0f);
        var ring = visual.GetComponent<Boss2RingBurstAnimation>();
        ring.Begin();
        ring.BeginCharge();
        for (int i = 0; i < 240 && !ring.Prepared; i++) animator.Update(1f / 60f);
        Assert.That(ring.Prepared, Is.True);
        ring.BeginLoop();
        for (int i = 0; i < 20 && !ring.LoopEntered; i++) animator.Update(1f / 60f);
        Assert.That(ring.LoopEntered, Is.True);
        ring.BeginAttack();
        for (int i = 0; i < 120 && !ring.AttackCompleted; i++) animator.Update(1f / 60f);
        Assert.That(ring.AttackCompleted, Is.True);
        Assert.That(ring.Released, Is.True);
        Assert.That(ring.Completed, Is.False);
        Assert.That(ring.Interrupted, Is.False);
        return ring;
    }

    private static UnityEditor.Animations.AnimatorState RingState(Animator animator, Boss2RingBurstStage stage)
    {
        var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
        var machines = new System.Collections.Generic.Queue<UnityEditor.Animations.AnimatorStateMachine>();
        machines.Enqueue(controller.layers[0].stateMachine);
        while (machines.Count > 0)
        {
            var machine = machines.Dequeue();
            foreach (var child in machine.stateMachines) machines.Enqueue(child.stateMachine);
            foreach (var child in machine.states)
            {
                var role = child.state.behaviours.OfType<Boss2RingBurstState>().SingleOrDefault();
                if (role != null && new SerializedObject(role).FindProperty("stage").intValue == (int)stage) return child.state;
            }
        }
        Assert.Fail($"Missing ring role: {stage}");
        return null;
    }

    private static Sprite[] RingSprites(Motion motion)
    {
        while (motion is UnityEditor.Animations.BlendTree tree) motion = tree.children.Single().motion;
        var clip = (AnimationClip)motion;
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .Single(curve => curve.type == typeof(SpriteRenderer) && curve.propertyName == "m_Sprite");
        return AnimationUtility.GetObjectReferenceCurve(clip, binding).Select(key => (Sprite)key.value).Distinct().ToArray();
    }

    private bool FieldHits(Collider2D collider, float radius)
    {
        return field.Segments.Any(segment => Physics2D.CircleCastAll(segment.Start, radius,
            (segment.End - segment.Start).normalized, segment.Length).Any(hit => hit.collider == collider));
    }

    private void AdvanceAttack(float deltaTime)
    {
        field.TickAttack(deltaTime);
        foreach (var animator in field.GetComponentsInChildren<Animator>()) animator.Update(deltaTime);
    }

    private PlayerHealth DamageTarget(Vector2 position)
    {
        if (damageData == null)
        {
            originalRuntimeHealth = PlayerRuntimeState.CurrentHP;
            damageData = ScriptableObject.CreateInstance<PlayerData>();
            var data = new SerializedObject(damageData);
            data.FindProperty("maxHP").intValue = 20;
            data.FindProperty("hitInvincibilityDuration").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var target = new GameObject("Attack damage target");
        target.SetActive(false);
        target.transform.SetParent(arena.transform);
        target.transform.position = position;
        var player = target.AddComponent<Player>();
        typeof(Player).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, damageData);
        var health = target.GetComponent<PlayerHealth>();
        typeof(PlayerHealth).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, player);
        typeof(PlayerHealth).GetField("currentHP", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 20);
        target.AddComponent<CapsuleCollider2D>().size = new Vector2(.29f, .84f);
        target.SetActive(true);
        var settings = new SerializedObject(field);
        settings.FindProperty("playerLayers").intValue = 1 << target.layer;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Physics2D.SyncTransforms();
        return health;
    }

    private Collider2D Wall(Vector2 offset, Vector2 size)
    {
        var go = new GameObject("Test wall");
        go.transform.SetParent(arena.transform);
        go.transform.position = ArenaOffset + (Vector3)offset;
        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;
        return collider;
    }
}

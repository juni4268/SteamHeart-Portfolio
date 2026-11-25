using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2PatternRunnerTests
{
    private GameObject root;
    private Boss2Controller boss;

    private static void Set(object target, Type type, string field, object value) =>
        type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static void Tick(Boss2PatternRunner runner) =>
        typeof(Boss2PatternRunner).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(runner, null);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Boss2 runner test");
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
        Assert.That(boss.BeginCombat(), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    private Boss2FoundationTestPattern Pattern(Boss2Parts parts,
        Func<Boss2PatternContext, Boss2PatternExecution> factory, Boss2Parts pause = Boss2Parts.None)
    {
        var pattern = root.AddComponent<Boss2FoundationTestPattern>();
        Set(pattern, typeof(Boss2PatternBase), "requiredParts", parts);
        Set(pattern, typeof(Boss2PatternBase), "pauseBasicParts", pause);
        pattern.Factory = factory;
        return pattern;
    }

    [Test]
    public void GroggyWaitsForThePatternsNormalReturnWithoutDisposingItsIterator()
    {
        bool returning = false, returned = false, disposed = false;
        int cleanups = 0;
        IEnumerator Routine(Boss2PatternContext context)
        {
            try
            {
                while (!context.FinishRequested) yield return null;
                returning = true;
                yield return null;
                returned = true;
            }
            finally { disposed = true; }
        }
        var pattern = Pattern(Boss2Parts.Ring, context => new Execution(Routine(context), result =>
        {
            Assert.That(result, Is.EqualTo(Boss2PatternResult.Completed));
            cleanups++;
        }));
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True);
        Tick(runner);
        var begin = typeof(Boss2Controller).GetMethod("BeginGroggyReturn", BindingFlags.NonPublic | BindingFlags.Instance);
        var enter = typeof(Boss2Controller).GetMethod("EnterGroggy", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(begin.Invoke(boss, null), Is.EqualTo(true));
        Assert.That(runner.Context.FinishRequested, Is.True);
        Assert.That(disposed, Is.False);
        Assert.That(enter.Invoke(boss, null), Is.EqualTo(false));
        Assert.That(boss.TryRequestPattern(pattern, out _), Is.False);

        Tick(runner);
        Assert.That(returning, Is.True);
        Assert.That(returned, Is.False);
        Assert.That(boss.PartUsage.GetOwner(Boss2PartId.Ring), Is.Not.Null);
        Assert.That(enter.Invoke(boss, null), Is.EqualTo(false));
        Tick(runner);
        Assert.That(returned, Is.True);
        Assert.That(disposed, Is.True);
        Assert.That(cleanups, Is.EqualTo(1));
        Assert.That(enter.Invoke(boss, null), Is.EqualTo(true));
        Assert.That(boss.CurrentState, Is.EqualTo(Boss2CombatState.Groggy));
    }

    [Test]
    public void GroggyCancelsAnUnstartedReservationWithoutStartingItsAttack()
    {
        bool created = false;
        var pattern = Pattern(Boss2Parts.Ring, _ => { created = true; return null; });
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True);
        typeof(Boss2Controller).GetMethod("BeginGroggyReturn", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(boss, null);
        Assert.That(created, Is.False);
        Assert.That(runner.Result, Is.EqualTo(Boss2PatternResult.Cancelled));
        Assert.That(boss.PartUsage.GetReservation(Boss2PartId.Ring), Is.Null);
    }

    [Test]
    public void CancellingInsideIteratorDefersDisposalAndCleansUpExactlyOnce()
    {
        Boss2PatternRunner runner = null;
        bool continued = false;
        bool disposedAfterStep = false;
        int cleanups = 0;
        IEnumerator Routine()
        {
            try
            {
                runner.Cancel();
                continued = true;
                yield return null;
            }
            finally { disposedAfterStep = continued; }
        }
        var pattern = Pattern(Boss2Parts.Ring, _ => new Execution(Routine(), result =>
        {
            Assert.That(result, Is.EqualTo(Boss2PatternResult.Cancelled));
            cleanups++;
        }));
        Assert.That(boss.TryRequestPattern(pattern, out runner), Is.True);
        Tick(runner);
        runner.Cancel();
        Tick(runner);

        Assert.That(disposedAfterStep, Is.True);
        Assert.That(cleanups, Is.EqualTo(1));
        Assert.That(boss.Executions, Is.Empty);
        Assert.That(boss.PartUsage.GetOwner(Boss2PartId.Ring), Is.Null);
    }

    [Test]
    public void CancellingRingDoesNotStopAReleasedHandNowUsedByAnotherPattern()
    {
        IEnumerator Beam(Boss2PatternContext context)
        {
            context.ReleaseParts(Boss2Parts.UpperLeftHand);
            while (true) yield return null;
        }
        IEnumerator Hands(Boss2PatternContext context)
        {
            context.GetPart(Boss2PartId.UpperLeftHand).MoveToWorld(Vector3.one, 1f);
            while (true) yield return null;
        }
        var beam = Pattern(Boss2Parts.UpperLeftHand | Boss2Parts.Ring, context => new Execution(Beam(context), _ => { }));
        var hands = Pattern(Boss2Parts.AllHands, context => new Execution(Hands(context), _ => { }));
        Set(hands, typeof(Boss2PatternBase), "kind", Boss2PatternKind.Basic);
        Assert.That(boss.TryRequestPattern(beam, out var beamRun), Is.True);
        Tick(beamRun);
        Assert.That(boss.TryRequestPattern(hands, out var handRun), Is.True);
        Tick(handRun);

        beamRun.Cancel();
        Assert.That(boss.GetPart(Boss2PartId.UpperLeftHand).IsMoving, Is.True);
        Assert.That(handRun.State, Is.EqualTo(Boss2ExecutionState.Running));
        Assert.That(boss.PartUsage.GetOwner(Boss2PartId.Ring), Is.Null);
        Assert.That(boss.PartUsage.GetOwner(Boss2PartId.UpperLeftHand), Is.Not.Null);
        boss.EndCombat();
        Assert.That(boss.Executions, Is.Empty);
        Assert.That(boss.GetPart(Boss2PartId.UpperLeftHand).CurrentMotion, Is.EqualTo(Boss2PartMotion.Stopped));
    }

    [Test]
    public void ControllerUpdateKeepsItsSnapshotWhenCallbacksReplaceExecutions()
    {
        int secondTicks = 0, replacementTicks = 0;
        IEnumerator Second() { secondTicks++; yield return null; }
        IEnumerator Replacement() { replacementTicks++; yield return null; }
        var replacement = Pattern(Boss2Parts.Ring, _ => new Execution(Replacement(), _ => { }));
        Set(replacement, typeof(Boss2PatternBase), "kind", Boss2PatternKind.Basic);
        Boss2PatternRunner replacementRun = null;
        IEnumerator First()
        {
            boss.CancelAllPatterns();
            Assert.That(boss.TryRequestPattern(replacement, out replacementRun), Is.True);
            yield return null;
        }
        var first = Pattern(Boss2Parts.UpperLeftHand, _ => new Execution(First(), _ => { }));
        var second = Pattern(Boss2Parts.LowerLeftHand, _ => new Execution(Second(), _ => { }));
        Set(first, typeof(Boss2PatternBase), "kind", Boss2PatternKind.Basic);
        Set(second, typeof(Boss2PatternBase), "kind", Boss2PatternKind.Basic);
        Assert.That(boss.TryRequestPattern(first, out var firstRun), Is.True);
        Assert.That(boss.TryRequestPattern(second, out var secondRun), Is.True);
        var update = typeof(Boss2Controller).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);

        update.Invoke(boss, null);
        Assert.That(firstRun.Result, Is.EqualTo(Boss2PatternResult.Cancelled));
        Assert.That(secondRun.Result, Is.EqualTo(Boss2PatternResult.Cancelled));
        Assert.That(secondTicks, Is.Zero);
        Assert.That(replacementTicks, Is.Zero, "New requests begin on the following update.");
        Assert.That(boss.Executions, Has.Count.EqualTo(1));
        update.Invoke(boss, null);
        Assert.That(replacementTicks, Is.EqualTo(1));
        replacementRun.Cancel();
    }

    [Test]
    public void MainRequestsAreExclusiveWhileWaitingAndRunningEvenWithDisjointParts()
    {
        IEnumerator Hold() { while (true) yield return null; }
        var first = Pattern(Boss2Parts.Ring, _ => new Execution(Hold(), _ => { }));
        var second = Pattern(Boss2Parts.UpperHands, _ => new Execution(Hold(), _ => { }));
        Assert.That(boss.TryRequestPattern(first, out var runner), Is.True);
        Assert.That(boss.HasMainPattern, Is.True);
        Assert.That(boss.TryRequestPattern(second, out _), Is.False);
        Tick(runner);
        Assert.That(runner.State, Is.EqualTo(Boss2ExecutionState.Running));
        Assert.That(boss.TryRequestPattern(second, out _), Is.False);
        runner.Cancel();
        Assert.That(boss.TryRequestPattern(second, out _), Is.True);
        boss.EndCombat();
    }

    [Test]
    public void FactoryFailureReleasesPartsAndRestoresBasicPermission()
    {
        var pattern = Pattern(Boss2Parts.UpperHands | Boss2Parts.Ring,
            _ => throw new InvalidOperationException("Expected test failure"), Boss2Parts.LowerHands);
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True);
        Tick(runner);

        Assert.That(runner.Result, Is.EqualTo(Boss2PatternResult.Failed));
        Assert.That(runner.Error, Is.TypeOf<InvalidOperationException>());
        Assert.That(boss.Executions, Is.Empty);
        Assert.That(boss.PartUsage.GetOwner(Boss2PartId.Ring), Is.Null);
        Assert.That(boss.PartUsage.IsBasicPaused(Boss2Parts.LowerHands), Is.False);
    }

    private sealed class Execution : Boss2PatternExecution
    {
        private readonly IEnumerator routine;
        private readonly Action<Boss2PatternResult> cleanup;
        public Action StopForDeathCallback;
        public Func<Boss2PatternContext, IEnumerator> DeathReturn;
        public Execution(IEnumerator routine, Action<Boss2PatternResult> cleanup)
        {
            this.routine = routine;
            this.cleanup = cleanup;
        }
        public override IEnumerator Execute(Boss2PatternContext context) => routine;
        public override void StopForDeath(Boss2PatternContext context) => StopForDeathCallback?.Invoke();
        public override IEnumerator ReturnAfterDeath(Boss2PatternContext context)
        {
            if (DeathReturn != null) yield return DeathReturn(context);
        }
        public override void Cleanup(Boss2PatternResult result) => cleanup(result);
    }

    [Test]
    public void DeathStopsEffectsImmediatelyAndRetainsOwnershipUntilReturnCompletes()
    {
        bool effect = true, finishReturn = false, returning = false;
        int cleanups = 0;
        IEnumerator Attack() { while (true) yield return null; }
        IEnumerator Return(Boss2PatternContext context)
        {
            returning = true;
            while (!finishReturn) yield return null;
        }
        var pattern = Pattern(Boss2Parts.Ring, _ => new Execution(Attack(), _ => cleanups++)
        { StopForDeathCallback = () => effect = false, DeathReturn = Return });
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True); Tick(runner);
        typeof(Boss2Controller).GetMethod("HandleDeath", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, null);
        Assert.That(effect, Is.False);
        Assert.That(runner.ReturningAfterDeath, Is.True);
        Assert.That(boss.PartUsage.GetOwner(Boss2PartId.Ring), Is.Not.Null);
        Assert.That(boss.TryRequestPattern(pattern, out _), Is.False);
        Tick(runner); Assert.That(returning, Is.True);
        Assert.That(cleanups, Is.Zero);
        finishReturn = true; Tick(runner);
        Assert.That(runner.Result, Is.EqualTo(Boss2PatternResult.Cancelled));
        Assert.That(cleanups, Is.EqualTo(1));
        Assert.That(boss.PartUsage.GetOwner(Boss2PartId.Ring), Is.Null);
        Assert.That(boss.GetPart(Boss2PartId.Ring).CurrentMotion, Is.EqualTo(Boss2PartMotion.FollowingAnchor));
    }

    [Test]
    public void DeathInsideAttackDefersDisposalUntilTheDamageCallbackReturns()
    {
        bool continued = false, disposedAfterStep = false, returned = false;
        IEnumerator Attack()
        {
            try
            {
                typeof(Boss2Controller).GetMethod("HandleDeath", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, null);
                continued = true;
                yield return null;
            }
            finally { disposedAfterStep = continued; }
        }
        IEnumerator Return(Boss2PatternContext context) { returned = true; yield break; }
        var pattern = Pattern(Boss2Parts.Ring, _ => new Execution(Attack(), _ => { }) { DeathReturn = Return });
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True); Tick(runner);
        Assert.That(disposedAfterStep, Is.True);
        Tick(runner);
        Assert.That(returned, Is.True);
        Assert.That(runner.Result, Is.EqualTo(Boss2PatternResult.Cancelled));
    }

    [Test]
    public void DeathAtPatternStartedReturnsWithoutStartingTheAttack()
    {
        bool attacked = false, returned = false;
        IEnumerator Attack() { attacked = true; yield return null; }
        IEnumerator Return(Boss2PatternContext context) { returned = true; yield break; }
        var pattern = Pattern(Boss2Parts.Ring, _ => new Execution(Attack(), _ => { }) { DeathReturn = Return });
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True);
        runner.Started += _ => typeof(Boss2Controller).GetMethod("HandleDeath", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, null);
        Tick(runner); Tick(runner);
        Assert.That(attacked, Is.False);
        Assert.That(returned, Is.True);
        Assert.That(runner.Result, Is.EqualTo(Boss2PatternResult.Cancelled));
    }

    [Test]
    public void DeathCancelsAWaitingReservationWithoutCreatingItsExecution()
    {
        bool created = false;
        boss.PartUsage.TryReserve(Boss2Parts.Ring, Boss2PatternKind.Basic, Boss2Parts.None, out var basic);
        boss.PartUsage.TryActivate(basic);
        var pattern = Pattern(Boss2Parts.Ring, _ => { created = true; return null; });
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True);
        Tick(runner);
        typeof(Boss2Controller).GetMethod("HandleDeath", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, null);
        Assert.That(created, Is.False);
        Assert.That(runner.Result, Is.EqualTo(Boss2PatternResult.Cancelled));
        Assert.That(boss.PartUsage.GetReservation(Boss2PartId.Ring), Is.Null);
        boss.PartUsage.Release(basic);
    }
}

[AddComponentMenu("")]
public sealed class Boss2FoundationTestPattern : Boss2PatternBase
{
    public Func<Boss2PatternContext, Boss2PatternExecution> Factory;
    public override Boss2PatternExecution CreateExecution(Boss2PatternContext context) => Factory(context);
}

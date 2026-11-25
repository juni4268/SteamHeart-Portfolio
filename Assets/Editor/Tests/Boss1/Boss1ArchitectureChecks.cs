using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>Editor-callable regression checks; no scene objects or Play Mode required.</summary>
public static class Boss1ArchitectureChecks
{
    public static string[] Run()
    {
        var passed = new List<string>();
        Check(Boss1PatternResult.Completed, false, false, passed);
        Check(Boss1PatternResult.Cancelled, false, false, passed);
        Check(Boss1PatternResult.Failed, true, false, passed);
        Check(Boss1PatternResult.Failed, false, true, passed);

        var runner = new Boss1PatternRunner();
        var cancelledInsideMoveNext = new ProbeExecution { OnStep = () => runner.Cancel() };
        Boss1PatternResult? result = null;
        runner.Finished += (value, error) => result = value;
        runner.TryStart(cancelledInsideMoveNext, null);
        runner.Tick();
        Require(result == Boss1PatternResult.Cancelled && cancelledInsideMoveNext.CleanupCount == 1,
            "Cancellation during MoveNext must finish safely.");
        Require(cancelledInsideMoveNext.Disposed, "Cancellation must dispose the nested routine.");
        passed.Add("Cancellation during MoveNext");

        var first = new ProbeExecution();
        var second = new ProbeExecution();
        runner.TryStart(first, null);
        Require(!runner.TryStart(second, null), "A runner must reject overlapping executions.");
        runner.Cancel();
        Require(runner.TryStart(second, null), "A runner must be reusable after cancellation.");
        runner.Tick();
        runner.Tick();
        Require(first.CleanupCount == 1 && second.CleanupCount == 1, "Execution state must be independent.");
        passed.Add("Exclusive execution and reuse");
        return passed.ToArray();
    }

    private static void Check(Boss1PatternResult expected, bool throwDuringRun, bool throwDuringCleanup,
        List<string> passed)
    {
        var runner = new Boss1PatternRunner();
        var execution = new ProbeExecution { ThrowDuringRun = throwDuringRun, ThrowDuringCleanup = throwDuringCleanup };
        int notifications = 0;
        Boss1PatternResult? result = null;
        Exception failure = null;
        runner.Finished += (value, error) => { notifications++; result = value; failure = error; };
        runner.TryStart(execution, null);
        runner.Tick();
        if (expected == Boss1PatternResult.Cancelled) runner.Cancel();
        else runner.Tick();
        runner.Cancel();
        runner.Tick();
        Require(result == expected, $"Expected {expected}, got {result}.");
        Require(notifications == 1 && execution.CleanupCount == 1, "Finish and cleanup must happen exactly once.");
        Require(execution.Disposed && !runner.IsRunning, "Nested iterator must be disposed and runner released.");
        Require((failure != null) == (expected == Boss1PatternResult.Failed), "Failure must carry its exception.");
        passed.Add($"{expected} (run exception: {throwDuringRun}, cleanup exception: {throwDuringCleanup})");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ProbeExecution : Boss1PatternExecution
    {
        public bool ThrowDuringRun;
        public bool ThrowDuringCleanup;
        public Action OnStep;
        public bool Disposed;
        public int CleanupCount;

        public override IEnumerator Execute(Boss1PatternContext context)
        {
            yield return Nested();
        }

        private IEnumerator Nested()
        {
            try
            {
                OnStep?.Invoke();
                yield return null;
                if (ThrowDuringRun) throw new InvalidOperationException("Expected test failure.");
            }
            finally { Disposed = true; }
        }

        public override void Cleanup(Boss1PatternResult result)
        {
            CleanupCount++;
            if (ThrowDuringCleanup) throw new InvalidOperationException("Expected cleanup failure.");
        }
    }
}

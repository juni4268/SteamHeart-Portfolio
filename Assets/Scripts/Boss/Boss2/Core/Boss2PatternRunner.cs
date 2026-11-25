using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>One pending/running invocation. The boss ticks several runners independently.</summary>
public sealed class Boss2PatternRunner
{
    private readonly Boss2Controller controller;
    private readonly Boss2PartUsage usage;
    private readonly Boss2PartUsage.Reservation reservation;
    private readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private Boss2PatternExecution execution;
    private bool isStepping;
    private bool isFinishing;
    private bool cancelRequested;
    private bool deathReturnRequested;
    private Exception deathReturnFailure;

    internal Boss2PatternRunner(Boss2Controller controller, Boss2PatternBase pattern,
        Boss2PartUsage usage, Boss2PartUsage.Reservation reservation)
    {
        this.controller = controller;
        Pattern = pattern;
        this.usage = usage;
        this.reservation = reservation;
    }

    public event Action<Boss2PatternRunner> Started;
    public event Action<Boss2PatternRunner> Finished;
    public Boss2PatternBase Pattern { get; }
    public Boss2PatternContext Context { get; private set; }
    public Boss2ExecutionState State { get; private set; } = Boss2ExecutionState.WaitingForParts;
    public Boss2PatternResult? Result { get; private set; }
    public Exception Error { get; private set; }
    public bool ReturningAfterDeath { get; private set; }
    public Boss2Parts HeldParts => reservation.HeldParts;

    internal void RequestFinish()
    {
        if (Context == null) Cancel();
        else Context.FinishRequested = true;
    }

    internal void Tick()
    {
        if (State == Boss2ExecutionState.Finished || isStepping || isFinishing) return;
        Exception failure = null;
        isStepping = true;
        try
        {
            if (State == Boss2ExecutionState.WaitingForParts)
            {
                if (Pattern.Kind == Boss2PatternKind.Main && !controller.CanStartMainPattern) return;
                if (!usage.TryActivate(reservation, id => controller.ArePartsReady(Boss2PartMask.FromId(id)))) return;
                Context = new Boss2PatternContext(controller, usage, reservation);
                execution = Pattern.CreateExecution(Context);
                if (execution == null) throw new InvalidOperationException("Pattern returned no execution.");
                IEnumerator routine = execution.Execute(Context);
                if (routine == null) throw new InvalidOperationException("Pattern returned no execution routine.");
                routines.Push(routine);
                State = Boss2ExecutionState.Running;
                if (!cancelRequested) Started?.Invoke(this);
            }

            while (routines.Count > 0 && !cancelRequested && !deathReturnRequested)
            {
                IEnumerator routine = routines.Peek();
                if (!routine.MoveNext())
                {
                    routines.Pop();
                    (routine as IDisposable)?.Dispose();
                    continue;
                }
                if (routine.Current is IEnumerator nested)
                {
                    routines.Push(nested);
                    continue;
                }
                if (routine.Current != null)
                    throw new InvalidOperationException("Yield null or an IEnumerator; Unity yield instructions are not supported.");
                break;
            }

            if (!cancelRequested && !deathReturnRequested && routines.Count == 0 && !controller.ArePartsReady(HeldParts))
                throw new InvalidOperationException("A pattern ended before its parts were ready. Wait for movement before completing.");
        }
        catch (Exception error) { failure = error; }
        finally { isStepping = false; }

        if (deathReturnRequested)
        {
            StartDeathReturn(Combine(failure, deathReturnFailure));
            return;
        }
        if (failure != null) Finish(Boss2PatternResult.Failed, failure);
        else if (cancelRequested) Finish(Boss2PatternResult.Cancelled, null);
        else if (routines.Count == 0)
            Finish(ReturningAfterDeath ? Boss2PatternResult.Cancelled : Boss2PatternResult.Completed, null);
    }

    internal void BeginDeathReturn()
    {
        if (State == Boss2ExecutionState.Finished || isFinishing || ReturningAfterDeath || deathReturnRequested) return;
        if (execution == null) { Cancel(); return; }
        deathReturnRequested = true;
        try { execution.StopForDeath(Context); }
        catch (Exception error) { deathReturnFailure = error; }
        // Death can be triggered by a damage callback inside MoveNext. Dispose only after it returns.
        if (!isStepping) StartDeathReturn(deathReturnFailure);
    }

    private void StartDeathReturn(Exception failure)
    {
        deathReturnRequested = false;
        deathReturnFailure = null;
        while (routines.Count > 0)
        {
            try { (routines.Pop() as IDisposable)?.Dispose(); }
            catch (Exception error) { failure = Combine(failure, error); }
        }
        if (failure != null) { Finish(Boss2PatternResult.Failed, failure); return; }
        try
        {
            IEnumerator routine = execution.ReturnAfterDeath(Context);
            if (routine == null) throw new InvalidOperationException("Pattern returned no death return routine.");
            ReturningAfterDeath = true;
            routines.Push(routine);
        }
        catch (Exception error) { Finish(Boss2PatternResult.Failed, error); }
    }

    public void Cancel()
    {
        if (State == Boss2ExecutionState.Finished || isFinishing) return;
        cancelRequested = true;
        if (!isStepping) Finish(Boss2PatternResult.Cancelled, null);
    }

    private void Finish(Boss2PatternResult result, Exception failure)
    {
        isFinishing = true;
        while (routines.Count > 0)
        {
            try { (routines.Pop() as IDisposable)?.Dispose(); }
            catch (Exception error) { failure = Combine(failure, error); }
        }
        if (failure != null) result = Boss2PatternResult.Failed;
        try { execution?.Cleanup(result); }
        catch (Exception error) { failure = Combine(failure, error); }
        if (failure != null) result = Boss2PatternResult.Failed;
        if (result != Boss2PatternResult.Completed && (!ReturningAfterDeath || failure != null || cancelRequested)) Context?.ResetOwnedParts();
        usage.Release(reservation);
        Result = result;
        Error = failure;
        State = Boss2ExecutionState.Finished;
        isFinishing = false;
        Finished?.Invoke(this);
    }

    private static Exception Combine(Exception first, Exception second) =>
        first == null ? second : second == null ? first : new AggregateException(first, second);
}

using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>Owns execution, nested iterator disposal and cleanup. Driven by the brain's Update.</summary>
public sealed class Boss1PatternRunner
{
    private readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    private Boss1PatternExecution execution;
    private bool isStepping;
    private bool cancelRequested;
    private bool isFinishing;

    public event Action Presented;
    public event Action<Boss1PatternResult, Exception> Finished;
    public bool IsRunning => execution != null;

    public bool TryStart(Boss1PatternExecution next, Boss1PatternContext context)
    {
        if (next == null || IsRunning || isFinishing) return false;

        execution = next;
        execution.Presented += HandlePresented;
        cancelRequested = false;
        try
        {
            IEnumerator routine = next.Execute(context);
            if (routine == null) throw new InvalidOperationException("Pattern returned no execution routine.");
            routines.Push(routine);
        }
        catch (Exception error)
        {
            Finish(Boss1PatternResult.Failed, error);
        }
        return true;
    }

    public void Tick()
    {
        if (!IsRunning || isStepping || isFinishing) return;

        Exception failure = null;
        isStepping = true;
        try
        {
            while (routines.Count > 0 && !cancelRequested)
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
                    throw new InvalidOperationException("Yield null, an IEnumerator, or WaitForDuration; Unity yield instructions are not supported by this runner.");
                break;
            }
        }
        catch (Exception error)
        {
            failure = error;
        }
        finally
        {
            isStepping = false;
        }

        if (failure != null) Finish(Boss1PatternResult.Failed, failure);
        else if (cancelRequested) Finish(Boss1PatternResult.Cancelled, null);
        else if (routines.Count == 0) Finish(Boss1PatternResult.Completed, null);
    }

    public bool Cancel()
    {
        if (!IsRunning || isFinishing) return false;

        cancelRequested = true;
        // Cancellation can be requested by damage/event callbacks inside MoveNext.
        if (!isStepping) Finish(Boss1PatternResult.Cancelled, null);
        return true;
    }

    private void HandlePresented()
    {
        if (!cancelRequested && !isFinishing) Presented?.Invoke();
    }

    private void Finish(Boss1PatternResult result, Exception failure)
    {
        isFinishing = true;
        execution.Presented -= HandlePresented;
        while (routines.Count > 0)
        {
            try { (routines.Pop() as IDisposable)?.Dispose(); }
            catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
        }

        if (failure != null) result = Boss1PatternResult.Failed;
        try { execution.Cleanup(result); }
        catch (Exception error)
        {
            failure = failure == null ? error : new AggregateException(failure, error);
            result = Boss1PatternResult.Failed;
        }

        execution = null;
        cancelRequested = false;
        isFinishing = false;
        Finished?.Invoke(result, failure);
    }
}

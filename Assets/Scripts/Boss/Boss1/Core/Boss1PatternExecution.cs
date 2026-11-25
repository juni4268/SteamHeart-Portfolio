using System;
using System.Collections;
using UnityEngine;

public enum Boss1PatternResult
{
    Completed,
    Cancelled,
    Failed
}

/// <summary>One invocation of a pattern. Mutable attack state belongs here, never in a config asset.</summary>
public abstract class Boss1PatternExecution
{
    private bool presented;

    public event Action Presented;

    // Report the first actual attack, independently of completion or cancellation.
    protected void MarkPresented()
    {
        if (presented) return;
        presented = true;
        Presented?.Invoke();
    }

    // Yield null for the next frame, or another IEnumerator. Use WaitForDuration for delays.
    public abstract IEnumerator Execute(Boss1PatternContext context);

    // Called once by the runner on every exit path. Implementations must tolerate partial execution.
    public abstract void Cleanup(Boss1PatternResult result);

    protected static IEnumerator WaitForDuration(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}

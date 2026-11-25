using System.Collections;
using UnityEngine;

public abstract class Boss2PatternExecution
{
    // Yield null or another IEnumerator. Animation completion must use its actual completion signal.
    public abstract IEnumerator Execute(Boss2PatternContext context);

    // Stop gameplay effects immediately, while retaining the part's animation for its return.
    public virtual void StopForDeath(Boss2PatternContext context) { }

    // Every pattern supplies the same return it uses normally. The boss keeps ticking it after death.
    public abstract IEnumerator ReturnAfterDeath(Boss2PatternContext context);

    // Called once on completion, cancellation or failure, including partial execution.
    // Installed effects such as gravity fields have their own lifetime after successful creation.
    public abstract void Cleanup(Boss2PatternResult result);

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

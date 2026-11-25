using UnityEngine;

/// <summary>Scene bindings and factory only. Each invocation gets a fresh execution object.</summary>
[RequireComponent(typeof(Boss1Controller))]
public abstract class Boss1PatternBase : MonoBehaviour
{
    [SerializeField, Min(0f)] private float cooldown = 30f;

    private Boss1Controller owner;
    private float cooldownEndTime;

    protected virtual float DefaultCooldown => 30f;

    protected virtual void Reset() => cooldown = DefaultCooldown;

    public Boss1Controller Owner => owner != null ? owner : owner = GetComponent<Boss1Controller>();
    public bool IsExecuting => Owner != null && Owner.CurrentPattern == this;
    public float Cooldown => cooldown;
    public bool IsOnCooldown => Time.time < cooldownEndTime;
    public float RemainingCooldown => Mathf.Max(0f, cooldownEndTime - Time.time);

    public abstract Boss1PatternExecution CreateExecution(Boss1PatternContext context);

    internal void StartCooldown()
    {
        cooldownEndTime = Time.time + cooldown;
    }

    protected virtual void OnDisable()
    {
        if (IsExecuting) Owner.CancelCurrentPattern();
    }

    protected virtual void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);
    }
}

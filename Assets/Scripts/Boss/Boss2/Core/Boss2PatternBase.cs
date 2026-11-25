using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Inspector configuration and factory. Every request gets an independent execution.</summary>
[RequireComponent(typeof(Boss2Controller))]
public abstract class Boss2PatternBase : MonoBehaviour
{
    [SerializeField] private Boss2PatternKind kind = Boss2PatternKind.Main;
    [SerializeField] private Boss2Parts requiredParts;
    [Tooltip("Block new basic attacks on these parts, without taking them from other main patterns.")]
    [SerializeField] private Boss2Parts pauseBasicParts;

    [Header("Cooldown")]
    [Tooltip("Minimum wait after this pattern finishes. Basic attacks also wait before their first attack.")]
    [FormerlySerializedAs("attackInterval")]
    [SerializeField, Min(0f)] private float minCooldown;
    [Tooltip("Maximum wait after this pattern finishes. A new duration is chosen for each cooldown.")]
    [SerializeField, Min(0f)] private float maxCooldown;

    private Boss2Controller owner;

    public Boss2Controller Owner => owner != null ? owner : owner = GetComponent<Boss2Controller>();
    public virtual Boss2PatternKind Kind => kind;
    public virtual Boss2Parts RequiredParts => requiredParts;
    public Boss2Parts PauseBasicParts => pauseBasicParts;

    public abstract Boss2PatternExecution CreateExecution(Boss2PatternContext context);

    internal virtual float SampleCooldown() => Random.Range(minCooldown, Mathf.Max(minCooldown, maxCooldown));

    protected virtual void OnValidate()
    {
        minCooldown = Mathf.Max(0f, minCooldown);
        maxCooldown = Mathf.Max(minCooldown, maxCooldown);
    }

    protected virtual void OnDisable()
    {
        if (owner != null) owner.CancelPattern(this);
    }
}

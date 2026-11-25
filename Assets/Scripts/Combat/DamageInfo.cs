using UnityEngine;

public readonly struct DamageInfo
{
    public int Amount { get; }
    public Vector2 SourcePosition { get; }
    public bool HasSource { get; }
    public Vector2 KnockbackVelocity { get; }
    public float ControlLockDuration { get; }
    public bool HasKnockback { get; }
    public bool IgnorePlayerDefenses { get; }

    public DamageInfo(int amount)
    {
        Amount = amount;
        SourcePosition = default;
        HasSource = false;
        KnockbackVelocity = default;
        ControlLockDuration = 0f;
        HasKnockback = false;
        IgnorePlayerDefenses = false;
    }

    public DamageInfo(int amount, bool ignorePlayerDefenses) : this(amount)
    {
        IgnorePlayerDefenses = ignorePlayerDefenses;
    }

    public DamageInfo(int amount, Vector2 sourcePosition) : this(amount)
    {
        SourcePosition = sourcePosition;
        HasSource = true;
    }

    public DamageInfo(int amount, Vector2 sourcePosition, Vector2 knockbackVelocity,
        float controlLockDuration) : this(amount, sourcePosition)
    {
        KnockbackVelocity = knockbackVelocity;
        ControlLockDuration = Mathf.Max(0f, controlLockDuration);
        HasKnockback = true;
    }
}

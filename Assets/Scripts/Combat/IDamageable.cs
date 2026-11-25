public interface IDamageable
{
    bool IsDead { get; }

    bool TryTakeDamage(DamageInfo damage);
}

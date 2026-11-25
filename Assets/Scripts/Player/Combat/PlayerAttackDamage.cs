using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerAttackDamage
{
    private readonly Transform attackPoint;
    private readonly Transform attacker;
    private readonly int enemyLayerMask;
    private readonly CombatAnimationEffect hitEffectPrefab;
    private readonly HashSet<IDamageable> hitDamageables = new HashSet<IDamageable>();
    private readonly List<Collider2D> hits = new List<Collider2D>();
    private PolygonCollider2D query;

    public PlayerAttackDamage(Transform attackPoint, Transform attacker, int enemyLayerMask,
        CombatAnimationEffect hitEffectPrefab = null)
    {
        this.attackPoint = attackPoint;
        this.attacker = attacker;
        this.enemyLayerMask = enemyLayerMask;
        this.hitEffectPrefab = hitEffectPrefab;
    }

    public void BeginAttack() => hitDamageables.Clear();
    public void EndAttack()
    {
        hitDamageables.Clear();
        if (query == null) return;
        query.enabled = false;
        if (Application.isPlaying) Object.Destroy(query.gameObject);
        else Object.DestroyImmediate(query.gameObject);
        query = null;
    }

    internal static int AddAttackPowerBonus(int baseDamage)
    {
        int attackBonus = PlayerPermanentProgress.AttackPowerBonus;
        return Mathf.Min(baseDamage, int.MaxValue - attackBonus) + attackBonus;
    }

    public void Apply(WeaponAttackData attackData, System.Action<Component> onHit = null)
    {
        if (attackPoint == null || attackData == null) return;

        Apply(attackPoint, attackData.AttackPolygon, attackData.Damage, onHit);
    }

    // Weapon attacks and skills share the same permanent attack bonus.
    public void Apply(Transform point, AttackPolygon polygon, int damage,
        System.Action<Component> onHit = null)
    {
        if (point == null || polygon == null || !polygon.IsValid) return;
        damage = AddAttackPowerBonus(damage);
        if (query == null)
        {
            var queryObject = new GameObject("Attack Polygon Query")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            query = queryObject.AddComponent<PolygonCollider2D>();
            query.enabled = false;
            query.isTrigger = true;
        }

        // Keep the query independent of the player's Rigidbody2D simulation state.
        polygon.SetWorldPaths(query, point);
        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = enemyLayerMask,
            useTriggers = Physics2D.queriesHitTriggers
        };
        // Enable only for this synchronous query, so it never generates physics contacts.
        try
        {
            query.enabled = true;
            Physics2D.SyncTransforms();
            query.Overlap(filter, hits);
        }
        finally
        {
            query.enabled = false;
        }

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead || !hitDamageables.Add(damageable)) continue;

            // ClosestPoint returns the attack center when it is inside multiple overlapping enemies.
            Vector3 hitPosition = hit.bounds.center;
            Vector2 contactPosition = hit.ClosestPoint(new Vector2(attacker.position.x, point.position.y));
            if (damageable.TryTakeDamage(new DamageInfo(damage, attacker.position)))
            {
                PlayHitEffect(contactPosition, attacker.position.x > hitPosition.x);
                DamageNumberDisplay.Show(damageable as Component, damage, hitPosition);
                onHit?.Invoke(damageable as Component);
            }
        }
    }

    private void PlayHitEffect(Vector2 position, bool flipX)
    {
        if (hitEffectPrefab == null) return;

        CombatAnimationEffect effect = Object.Instantiate(hitEffectPrefab, position, Quaternion.identity);
        effect.GetComponent<SpriteRenderer>().flipX = flipX;
    }
}

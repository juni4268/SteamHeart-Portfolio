using System;
using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class Boss2BlackHoleField : MonoBehaviour
{
    [SerializeField] private Transform origin;
    [Header("Suction")]
    [SerializeField, Min(0.1f)] private float radius = 3.5f;
    [SerializeField, Min(0f)] private float pullForce = 35f;
    [SerializeField, Min(0.1f)] private float maximumPullSpeed = 5f;

    [Header("Damage")]
    [SerializeField, Min(0.01f)] private float damageRadius = 1f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0.01f)] private float damageInterval = 0.5f;

    private Boss2Controller owner;
    private PlayerHealth target;
    private Rigidbody2D targetBody;
    private Collider2D targetCollider;
    private float damageCooldown;
    private Boss2Audio.Playback audioPlayback;
    public bool Active { get; private set; }
    public Vector3 OriginPosition => origin.position;

    public void Begin(Boss2Controller controller, PlayerHealth player)
    {
        if (origin == null || controller == null || player == null)
            throw new InvalidOperationException("Assign the Black Hole origin, boss and target.");
        targetBody = player.GetComponent<Rigidbody2D>();
        if (targetBody == null) throw new InvalidOperationException("The Black Hole target needs a Rigidbody2D.");
        targetCollider = player.GetComponent<Collider2D>();
        if (targetCollider == null) throw new InvalidOperationException("The Black Hole target needs a body Collider2D.");
        owner = controller;
        target = player;
        damageCooldown = 0f;
        Active = true;
        audioPlayback?.FadeOut(0.35f);
        audioPlayback = controller.GetComponent<Boss2Audio>()?.Play(Boss2Audio.Sound.BlackHole,
            loop: true, fadeIn: 0.3f);
    }

    public void Stop()
    {
        audioPlayback?.FadeOut(0.35f);
        audioPlayback = null;
        Active = false;
        target = null;
        targetBody = null;
        targetCollider = null;
        damageCooldown = 0f;
        owner = null;
    }

    private void OnDisable() => Stop();

    private void FixedUpdate()
    {
        if (!Active) return;
        if (owner == null || owner.IsDead || owner.CurrentState != Boss2CombatState.Fighting ||
            target == null || target.IsDead || !target.isActiveAndEnabled)
        {
            Stop();
            return;
        }
        if (HitStopManager.IsActive || Time.timeScale <= 0f || !targetBody.simulated) return;
        if (targetBody.bodyType == RigidbodyType2D.Dynamic)
        {
            Vector2 force = CalculateForce(targetBody.worldCenterOfMass, targetBody.linearVelocity,
                targetBody.mass, Time.fixedDeltaTime);
            targetBody.AddForce(force, ForceMode2D.Force);
        }
        TickDamage(Time.fixedDeltaTime);
    }

    private void TickDamage(float deltaTime)
    {
        if (!Active) return;
        damageCooldown = Mathf.Max(0f, damageCooldown - deltaTime);
        if (damageCooldown > 0f || !targetCollider.enabled || !targetCollider.gameObject.activeInHierarchy) return;
        Vector2 closest = targetCollider.ClosestPoint(origin.position);
        if (Vector2.SqrMagnitude(closest - (Vector2)origin.position) > damageRadius * damageRadius) return;
        // Consume the contact interval even when dodged/countered. Callbacks may stop this field.
        damageCooldown = damageInterval;
        target.TryTakeDamage(new DamageInfo(damage, origin.position));
    }

    private Vector2 CalculateForce(Vector2 position, Vector2 velocity, float mass, float deltaTime)
    {
        Vector2 offset = (Vector2)origin.position - position;
        float distance = offset.magnitude;
        if (distance < 0.01f || distance > radius || deltaTime <= 0f) return Vector2.zero;
        Vector2 direction = offset / distance;
        float remainingSpeed = maximumPullSpeed - Vector2.Dot(velocity, direction);
        if (remainingSpeed <= 0f) return Vector2.zero;
        return direction * Mathf.Min(pullForce, remainingSpeed * mass / deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (origin == null) return;
        Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.8f);
        Gizmos.DrawWireSphere(origin.position, radius);
        Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(origin.position, damageRadius);
    }
}

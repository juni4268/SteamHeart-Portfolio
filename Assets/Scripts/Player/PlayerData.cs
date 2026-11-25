using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Project K/Player Data")]
public class PlayerData : ScriptableObject
{
    [Header("Health Settings")]
    [SerializeField, Min(1)] private int maxHP = 15;
    [Tooltip("Invincibility after receiving damage, before permanent upgrades.")]
    [SerializeField, Min(0f)] private float hitInvincibilityDuration = 0f;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField, Min(0f)] private float jumpBufferDuration = 0.12f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.1f;
    [SerializeField] private float fallGravityMultiplier = 2.5f;
    [Range(0f, 1f)][SerializeField] private float groundLerpFactor = 0.5f;
    [Range(0f, 1f)][SerializeField] private float airLerpFactor = 0.2f;

    [Header("Input Settings")]
    [SerializeField, Min(0f)] private float actionBufferDuration = 0.15f;

    [Header("Dash Settings")]
    [SerializeField, Min(0f)] private float dashDistance = 1.5f;
    [SerializeField, Min(0f)] private float dashCooldown = 1f;
    [Tooltip("X: movement progress (0–1), Y: relative speed. Negative values are treated as zero. The curve is normalized to preserve dash distance.")]
    [SerializeField] private AnimationCurve dashSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    public int MaxHP => maxHP;
    public float HitInvincibilityDuration => Mathf.Max(0f, hitInvincibilityDuration);
    public float WalkSpeed => walkSpeed;
    public float JumpForce => jumpForce;
    public float JumpBufferDuration => jumpBufferDuration;
    public float CoyoteTime => coyoteTime;
    public float ActionBufferDuration => actionBufferDuration;
    public float FallGravityMultiplier => fallGravityMultiplier;
    public float GroundLerpFactor => groundLerpFactor;
    public float AirLerpFactor => airLerpFactor;
    public float DashDistance => dashDistance;
    public float DashCooldown => dashCooldown;
    public AnimationCurve DashSpeedCurve => dashSpeedCurve;
}

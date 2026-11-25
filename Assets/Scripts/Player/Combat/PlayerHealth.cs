using System;
using UnityEngine;

[RequireComponent(typeof(Player))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public event Action<int, int> HealthChanged;
    public event Action<int> Damaged;
    public event Action Died;

    // Keep existing Scene/Prefab settings; the controller owns the respawn behavior.
    [Header("Respawn")]
    [SerializeField] private SceneReference respawnScene = new SceneReference();
    [Tooltip("Wait after the death animation finishes before starting the respawn fade.")]
    [SerializeField, Min(0f)] private float respawnDelay = 1f;
    [SerializeField, Min(0f)] private float respawnFadeOutDuration = 0.35f;
    [SerializeField, Min(0f)] private float respawnFadeInDuration = 0.35f;
    [SerializeField] private Color respawnFadeColor = Color.black;

    private Player player;
    private PlayerMovement movement;
    private PlayerCounter counter;
    private PlayerRespawnController respawn;
    private int currentHP;
    private bool isDead;
    private float hitInvincibilityEndsAt = float.NegativeInfinity;

    public int CurrentHP => currentHP;
    public int MaxHP => player != null && player.Data != null
        ? Mathf.Min(player.Data.MaxHP, int.MaxValue - PlayerPermanentProgress.BonusMaxHP) +
          PlayerPermanentProgress.BonusMaxHP
        : 0;
    public bool IsDead => isDead;
    public float HitInvincibilityDuration => (player != null && player.Data != null
        ? player.Data.HitInvincibilityDuration : 0f) + PlayerPermanentProgress.HitInvincibilityBonus;
    public bool IsHitInvincible => Time.time < hitInvincibilityEndsAt;
    // Read-only check: querying protection must not consume Counter's counter window.
    public bool IsInvincible => IsHitInvincible
        || (player != null && player.dash != null && player.dash.IsInvincible)
        || (counter != null && counter.IsInvincible);

    private void Awake()
    {
        player = GetComponent<Player>();
        movement = GetComponent<PlayerMovement>();
        counter = GetComponent<PlayerCounter>();
        if (player.Data == null)
        {
            Debug.LogError($"{nameof(PlayerHealth)} requires {nameof(PlayerData)} on {nameof(Player)}.", this);
            enabled = false;
            return;
        }

        PlayerRuntimeState.InitializeIfNeeded(MaxHP);
        if (respawnScene != null && respawnScene.IsAssigned && gameObject.scene.path == respawnScene.Path)
            PlayerRuntimeState.SetHealth(MaxHP);
        currentHP = Mathf.Clamp(PlayerRuntimeState.CurrentHP, 0, MaxHP);
        respawn = new PlayerRespawnController(player, this, respawnScene?.Path,
            respawnFadeOutDuration, respawnFadeInDuration, respawnFadeColor, respawnDelay);
        Died += respawn.HandleDeath;
        PlayerPermanentProgress.MaxHealthIncreased += HandleMaxHealthIncreased;
    }

    private void OnDestroy()
    {
        PlayerPermanentProgress.MaxHealthIncreased -= HandleMaxHealthIncreased;
        if (respawn != null) Died -= respawn.HandleDeath;
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        respawnScene?.Validate();
#endif
    }

    private void Start()
    {
        if (currentHP <= 0) Die();
    }

    private void Update() => respawn?.Tick();

    public void TakeDamage(int damage = 1) => TryTakeDamage(new DamageInfo(damage));

    public bool TryTakeDamage(DamageInfo damage)
    {
        if (damage.Amount <= 0 || !TryReceiveHit(damage.IgnorePlayerDefenses)) return false;

        int previousHP = currentHP;
        currentHP = Mathf.Max(currentHP - damage.Amount, 0);
        // Start protection before callbacks so another hit in the same frame is rejected.
        hitInvincibilityEndsAt = Time.time + HitInvincibilityDuration;
        PlayerRuntimeState.SetHealth(currentHP);
        HealthChanged?.Invoke(currentHP, MaxHP);
        Damaged?.Invoke(previousHP - currentHP);

        if (currentHP <= 0) Die();
        else if (damage.HasKnockback && movement != null)
            movement.ApplyKnockback(damage.KnockbackVelocity, damage.ControlLockDuration);
        return true;
    }

    // Share defensive hit checks with attacks that apply HP damage after initial contact.
    public bool TryReceiveHit() => TryReceiveHit(false);

    private bool TryReceiveHit(bool ignorePlayerDefenses)
    {
        if (!isActiveAndEnabled || isDead || MaxHP <= 0) return false;
        if (ignorePlayerDefenses) return true;
        if (IsHitInvincible) return false;
        if (counter != null && counter.TryBlockDamage()) return false;
        return player.dash == null || !player.dash.IsInvincible;
    }

    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        int previousHP = currentHP;
        currentHP = Mathf.Clamp(currentHP + amount, 0, MaxHP);
        PlayerRuntimeState.SetHealth(currentHP);
        if (previousHP != currentHP) HealthChanged?.Invoke(currentHP, MaxHP);
    }

    private void HandleMaxHealthIncreased(int amount)
    {
        if (amount <= 0 || isDead) return;
        currentHP = Mathf.Min(currentHP, int.MaxValue - amount) + amount;
        currentHP = Mathf.Min(currentHP, MaxHP);
        PlayerRuntimeState.SetHealth(currentHP);
        HealthChanged?.Invoke(currentHP, MaxHP);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        player.CancelCombatActions();
        Died?.Invoke();
    }

    // Entry point retained for existing Animator behaviours and Animation Events.
    public void CompleteDeath() => respawn?.CompleteDeath();
}

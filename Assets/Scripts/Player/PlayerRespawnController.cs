using UnityEngine;

// Owned by PlayerHealth; existing serialized fade settings and animation callbacks stay intact.
public sealed class PlayerRespawnController
{
    private static readonly int DeathTrigger = Animator.StringToHash("Death");
    private readonly Player player;
    private readonly PlayerHealth health;
    private readonly string respawnScenePath;
    private readonly float fadeOutDuration;
    private readonly float fadeInDuration;
    private readonly Color fadeColor;
    private readonly float respawnDelay;
    private float respawnAt;
    private bool respawnPending;
    private bool isDeathComplete;

    public PlayerRespawnController(Player player, PlayerHealth health, string respawnScenePath,
        float fadeOutDuration, float fadeInDuration, Color fadeColor, float respawnDelay)
    {
        this.player = player;
        this.health = health;
        this.respawnScenePath = respawnScenePath;
        this.fadeOutDuration = fadeOutDuration;
        this.fadeInDuration = fadeInDuration;
        this.fadeColor = fadeColor;
        this.respawnDelay = Mathf.Max(0f, respawnDelay);
    }

    public void HandleDeath()
    {
        player.input?.Player.Disable();
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;
        PlayerDash dash = player.GetComponent<PlayerDash>();
        if (dash != null) dash.enabled = false;
        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        if (attack != null) attack.enabled = false;
        player.motor?.Stop();
        movement?.RefreshGravityScale();
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null) body.constraints |= RigidbodyConstraints2D.FreezePositionX;
        player.anim?.SetTrigger(DeathTrigger);
    }

    public void CompleteDeath()
    {
        if (!health.IsDead || isDeathComplete || respawnPending) return;
        respawnAt = Time.time + respawnDelay;
        respawnPending = true;
        Tick();
    }

    public void Tick()
    {
        if (!respawnPending || Time.time < respawnAt || SceneTransitionOverlay.IsTransitioning) return;

        // A death starts a fresh run in the explicitly assigned respawn scene.
        PlayerRuntimeState.ResetRunProgress();
        if (!SceneTransitionOverlay.TryLoadScene(
                respawnScenePath, fadeOutDuration, fadeInDuration, fadeColor))
        {
            respawnPending = false;
            return;
        }

        respawnPending = false;
        isDeathComplete = true;
        foreach (SpriteRenderer renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
            renderer.enabled = false;
        foreach (Collider2D collider in player.GetComponentsInChildren<Collider2D>(true))
            collider.enabled = false;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null) body.simulated = false;
    }
}

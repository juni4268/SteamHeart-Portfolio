using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class Boss1PropellerField : MonoBehaviour
{
    private readonly Boss1PropellerWindVisual windVisual = new Boss1PropellerWindVisual();

    private Boss1HandController hand;
    private Transform suctionOrigin;
    private PlayerHealth playerHealth;
    private Rigidbody2D playerBody;
    private Boss1PropellerConfig config;
    private float damageElapsed;
    private int processedDamageIntervals;
    private bool isPrepared;
    private bool isSuctionActive;
    private int suctionVersion;

    public bool IsSuctionActive => isSuctionActive;
    public Boss1HandController ActiveHand => hand;
    public bool HasActiveWind => windVisual.HasActiveStreams;

    private void Update()
    {
        if (isPrepared && (playerHealth == null || playerHealth.IsDead
            || hand == null || suctionOrigin == null))
        {
            Stop();
            return;
        }
        windVisual.Tick();
    }

    private void FixedUpdate()
    {
        if (!isSuctionActive || hand == null || playerHealth == null || playerHealth.IsDead
            || suctionOrigin == null || playerBody == null || config == null)
            return;

        Vector2 toHand = (Vector2)suctionOrigin.position - playerBody.worldCenterOfMass;
        float distance = toHand.magnitude;
        if (distance > 0.001f)
        {
            Vector2 direction = toHand / distance;
            float pullSpeed = Vector2.Dot(playerBody.linearVelocity, direction);
            if (config.MaximumPullSpeed <= 0f || pullSpeed < config.MaximumPullSpeed)
                playerBody.AddForce(direction * config.GetSuctionForce(distance), ForceMode2D.Force);
        }

        damageElapsed += Time.fixedDeltaTime;
        int version = suctionVersion;
        const float intervalTolerance = 0.0001f;
        while (damageElapsed + intervalTolerance >= config.DamageInterval)
        {
            damageElapsed = Mathf.Max(0f, damageElapsed - config.DamageInterval);
            processedDamageIntervals++;
            ApplyDamageIfInRange(distance);
            // Damage callbacks can stop or replace this suction before returning.
            if (version != suctionVersion || !isSuctionActive || playerHealth == null || playerHealth.IsDead)
                return;
        }
    }

    public void Prepare(Boss1HandController selectedHand, Transform origin, PlayerHealth target,
        Boss1PropellerConfig propellerConfig)
    {
        Stop();
        if (selectedHand == null || origin == null || target == null || propellerConfig == null) return;

        hand = selectedHand;
        suctionOrigin = origin;
        playerHealth = target;
        playerBody = target.GetComponent<Rigidbody2D>();
        config = propellerConfig;
        damageElapsed = 0f;
        processedDamageIntervals = 0;
        isPrepared = true;

        windVisual.Prepare(transform, selectedHand, origin, propellerConfig);
    }

    public void StartSuction()
    {
        if (!isPrepared) return;
        damageElapsed = 0f;
        processedDamageIntervals = 0;
        isSuctionActive = true;
        windVisual.StartSuction();
    }

    public void CompleteSuction()
    {
        if (!isSuctionActive || config == null || playerHealth == null || playerHealth.IsDead
            || playerBody == null)
            return;

        int expectedIntervals = Mathf.FloorToInt(
            (config.ActiveDuration + 0.0001f) / config.DamageInterval);
        int version = suctionVersion;
        while (processedDamageIntervals < expectedIntervals)
        {
            processedDamageIntervals++;
            float distance = suctionOrigin != null
                ? Vector2.Distance(suctionOrigin.position, playerBody.worldCenterOfMass)
                : float.PositiveInfinity;
            ApplyDamageIfInRange(distance);
            if (version != suctionVersion || !isSuctionActive || playerHealth == null || playerHealth.IsDead)
                return;
        }
    }

    public void FinishSuction()
    {
        if (!isPrepared) return;
        int version = suctionVersion;
        CompleteSuction();
        if (version != suctionVersion) return;
        isSuctionActive = false;
        if (playerHealth == null || playerHealth.IsDead)
        {
            Stop();
            return;
        }
        windVisual.Finish();
    }

    public void Stop()
    {
        suctionVersion++;
        isSuctionActive = false;
        isPrepared = false;
        damageElapsed = 0f;
        processedDamageIntervals = 0;

        windVisual.Stop();

        hand = null;
        suctionOrigin = null;
        playerHealth = null;
        playerBody = null;
        config = null;
    }

    private void ApplyDamageIfInRange(float distance)
    {
        if (distance <= config.AttackRadius)
            playerHealth.TryTakeDamage(new DamageInfo(config.DamagePerTick, suctionOrigin.position));
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        windVisual.DestroyVisuals();
    }
}

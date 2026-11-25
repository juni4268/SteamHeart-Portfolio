using UnityEngine;

[System.Serializable]
public sealed class PlayerAttackFeedback
{
    [SerializeField, Min(0f)] private float hitStopDuration = 0.04f;
    [SerializeField, Min(0f)] private float cameraImpactDuration = 0.1f;
    [SerializeField, Min(0f)] private float cameraShakeStrength = 0.08f;
    private Behaviour owner;

    public void Enable(Behaviour owner) => this.owner = owner;

    public void Play(Component target) => Play(target, 1f);

    public void Play(Component target, float cameraImpactMultiplier)
    {
        if (owner == null || !owner.isActiveAndEnabled) return;
        if (!(target is EnemyHealth) && !(target is BossHealth)) return;

        // A dying enemy already owns the stronger death flash.
        if (!(target is EnemyHealth enemy) || !enemy.IsDead)
        {
            target.GetComponent<HitImpactFlash>()?.Play();
        }
        HitStopManager.Request(owner, hitStopDuration);
        float multiplier = Mathf.Clamp01(cameraImpactMultiplier);
        HitCameraShake.Play(owner, cameraImpactDuration * multiplier,
            cameraShakeStrength * multiplier, new Vector2(0.5f, 1f));
    }

    public void Disable()
    {
        HitStopManager.Cancel(owner);
        HitCameraShake.Cancel(owner);
    }
}

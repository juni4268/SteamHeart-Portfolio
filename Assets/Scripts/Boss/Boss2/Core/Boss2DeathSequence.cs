using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Boss2Controller), typeof(Boss2BodyMovement))]
public sealed class Boss2DeathSequence : MonoBehaviour
{
    [SerializeField] private Boss2CoreBurstPattern coreBurst;
    [SerializeField] private Boss2DeathAnimation bodyAnimation;
    [SerializeField] private Boss2DeathAnimation ringAnimation;
    [SerializeField] private Boss2CoreBurstHandAnimation[] hands = Array.Empty<Boss2CoreBurstHandAnimation>();
    [SerializeField] private Transform coreAnchor;
    [SerializeField] private ParticleSystem explosionPrefab;
    [SerializeField] private Boss2DeathDebris debrisPrefab;
    [SerializeField] private Boss2DeadCore deadCorePrefab;
    [SerializeField] private PlayerHealth target;

    private Boss2DeadCore deadCore;
    private bool started;
    private Boss2Audio.Playback chargeAudio;
    private Boss2Audio.Playback explosionAudio;
    public bool Completed { get; private set; }

    public IEnumerator Play()
    {
        if (started) throw new InvalidOperationException("The boss death sequence has already started.");
        if (coreBurst == null || bodyAnimation == null || ringAnimation == null || hands.Length != 4 ||
            coreAnchor == null || explosionPrefab == null || debrisPrefab == null || deadCorePrefab == null || target == null)
            throw new InvalidOperationException("Assign the death animations, four hands, Core Burst, explosion, fragments, DeadCore and player.");
        started = true;
        var boss = GetComponent<Boss2Controller>();
        var movement = GetComponent<Boss2BodyMovement>();
        while (!boss.ArePartsReady(Boss2Parts.All)) yield return null;
        foreach (Boss2PartId id in Enum.GetValues(typeof(Boss2PartId))) boss.GetPart(id).FollowIdle();
        movement.MoveForDeath(coreBurst.PreparationPosition, coreBurst.PreparationMoveSpeed);
        while (movement.IsPatternMoving) yield return null;
        if (movement.IsPatternMovementBlocked)
            throw new InvalidOperationException("The dying boss cannot reach the Core Burst position.");

        bodyAnimation.MotionEntered += PlayChargeAudio;
        try
        {
            bodyAnimation.Begin();
            ringAnimation.Begin();
            foreach (var hand in hands) hand.Begin();
            while (!AllMotionsCompleted())
            {
                if (bodyAnimation.Interrupted || ringAnimation.Interrupted)
                    throw new InvalidOperationException("The boss death motion was interrupted.");
                foreach (var hand in hands)
                    if (hand.Interrupted) throw new InvalidOperationException("A death preparation hand motion was interrupted.");
                yield return null;
            }
        }
        finally
        {
            bodyAnimation.MotionEntered -= PlayChargeAudio;
            chargeAudio?.FadeOut(0.1f);
        }

        Vector3 explosionPosition = coreAnchor.position;
        var handRenderers = new SpriteRenderer[hands.Length];
        for (int i = 0; i < hands.Length; i++) handRenderers[i] = hands[i].GetComponent<SpriteRenderer>();
        var fragments = Instantiate(debrisPrefab, bodyAnimation.transform.position, Quaternion.identity);
        fragments.Launch(handRenderers);
        var impact = Instantiate(explosionPrefab, explosionPosition, Quaternion.identity);
        impact.Play(true);
        explosionAudio = GetComponent<Boss2Audio>()?.Play(Boss2Audio.Sound.DeathExplosion);
        HideBoss();
        deadCore = Instantiate(deadCorePrefab, explosionPosition, Quaternion.identity);
        deadCore.Begin(target.GetComponent<Collider2D>());
        while (!deadCore.Collected) yield return null;
        Destroy(deadCore.gameObject);
        deadCore = null;
        Completed = true;
    }

    private bool AllMotionsCompleted()
    {
        if (!bodyAnimation.Completed || !ringAnimation.Completed) return false;
        foreach (var hand in hands) if (!hand.Prepared) return false;
        return true;
    }

    private void PlayChargeAudio() => chargeAudio = GetComponent<Boss2Audio>()?.Play(
        Boss2Audio.Sound.DeathCharge, loop: true, fadeIn: 0.1f);

    private void HideBoss()
    {
        foreach (var animation in GetComponentsInChildren<Boss2MovementAnimation>(true)) animation.enabled = false;
        foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        foreach (Boss2PartId id in Enum.GetValues(typeof(Boss2PartId))) GetComponent<Boss2Controller>().GetPart(id).StopMovement();
        GetComponent<Boss2BodyMovement>().enabled = false;
        GetComponent<Rigidbody2D>().simulated = false;
    }

    private void OnDisable()
    {
        if (bodyAnimation != null) bodyAnimation.MotionEntered -= PlayChargeAudio;
        chargeAudio?.FadeOut();
        explosionAudio?.FadeOut();
        if (deadCore != null) Destroy(deadCore.gameObject);
    }
}

using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class Boss2DeadCore : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float flightDuration = 1.2f;
    [SerializeField, Min(0f)] private float arcHeight = 1.2f;
    [SerializeField, Min(0f)] private float collectionRadius = 0.1f;
    [SerializeField] private ParticleSystem acquisitionEffectPrefab;
    private Collider2D target;
    private Vector3 origin;
    private float elapsed;
    private bool flying;
    public bool Collected { get; private set; }

    public void Begin(Collider2D player)
    {
        if (player == null || !player.enabled || !player.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Assign the player's active collider to DeadCore.");
        target = player;
        origin = transform.position;
        elapsed = 0f;
        Collected = false;
        flying = true;
    }

    private void Update()
    {
        if (!HitStopManager.IsActive && Time.timeScale > 0f) Tick(Time.deltaTime);
    }

    private void Tick(float deltaTime)
    {
        if (!flying || target == null || !target.enabled) return;
        elapsed += deltaTime;
        float progress = Mathf.Clamp01(elapsed / flightDuration);
        Vector3 destination = target.bounds.center;
        destination.z = origin.z;
        transform.position = Vector3.Lerp(origin, destination, progress) +
            Vector3.up * (4f * arcHeight * progress * (1f - progress));
        if (Vector2.Distance(target.ClosestPoint(transform.position), transform.position) > collectionRadius) return;
        flying = false;
        Collected = true;
        if (acquisitionEffectPrefab != null)
            Instantiate(acquisitionEffectPrefab, destination, Quaternion.identity, target.transform);
        GetComponent<SpriteRenderer>().enabled = false;
        gameObject.SetActive(false);
    }
}

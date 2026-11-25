using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum DebrisDropPhase { Closing, Waiting, Opening, Attacking, Draining }

[DisallowMultipleComponent]
public sealed class DebrisDropTrap : MonoBehaviour
{
    [Header("Cycle")]
    [SerializeField, Min(0f)] private float waitDuration = 2f;
    [SerializeField, Min(0.01f)] private float attackDuration = 2f;

    [Header("Doors")]
    [SerializeField] private Animator doorAnimator;
    [Tooltip("Doors stay open until every cluster is below these Sprite bounds.")]
    [SerializeField] private SpriteRenderer[] doorRenderers = System.Array.Empty<SpriteRenderer>();
    [Tooltip("Bool parameter requesting the open or closed door pose.")]
    [SerializeField] private string doorsOpenParameter = "DoorsOpen";

    [Header("Debris")]
    [SerializeField] private DebrisCluster[] clusterPrefabs = System.Array.Empty<DebrisCluster>();
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Debris passes through the ground and is removed only after the whole cluster falls below this world Y.")]
    [SerializeField] private Transform endPoint;
    [SerializeField, Min(0f)] private float spawnWidth = 1.2f;
    [SerializeField, Min(1)] private int clustersPerWave = 3;
    [SerializeField, Min(0.01f)] private float waveInterval = 0.1f;
    [SerializeField] private Vector2 fallSpeedRange = new Vector2(4.5f, 5.5f);
    [FormerlySerializedAs("playerLayers")]
    [SerializeField] private LayerMask targetLayers;

    [Header("Knockback")]
    [SerializeField] private Vector2 knockbackVelocity = new Vector2(8f, 5f);
    [SerializeField, Min(0f)] private float controlLockDuration = 0.2f;

    private readonly List<DebrisCluster> activeClusters = new List<DebrisCluster>();
    private readonly Dictionary<IDamageable, Vector2> contacts = new Dictionary<IDamageable, Vector2>();
    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
    private float remainingTime;
    private float nextWaveTime;

    public DebrisDropPhase Phase { get; private set; }
    public int ActiveClusterCount => activeClusters.Count;

    private void Reset() => targetLayers = LayerMask.GetMask("Player", "Enemy");

    private void Awake()
    {
        if (doorAnimator == null || doorAnimator.runtimeAnimatorController == null
            || doorRenderers == null || doorRenderers.Length == 0
            || System.Array.Exists(doorRenderers, renderer => renderer == null)
            || string.IsNullOrEmpty(doorsOpenParameter) || spawnPoint == null || endPoint == null
            || clusterPrefabs == null || clusterPrefabs.Length == 0
            || System.Array.Exists(clusterPrefabs, prefab => prefab == null))
        {
            Debug.LogError($"{nameof(DebrisDropTrap)} requires a door Animator, door renderers, parameter, spawn point, end point and cluster prefabs.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        Phase = DebrisDropPhase.Closing;
        RequestDoors(false);
    }

    public void NotifyDoorPoseReached(Animator source, bool doorsOpen)
    {
        if (!isActiveAndEnabled || source != doorAnimator) return;
        if (doorsOpen && Phase == DebrisDropPhase.Opening)
        {
            Phase = DebrisDropPhase.Attacking;
            remainingTime = attackDuration;
            nextWaveTime = 0f;
        }
        else if (!doorsOpen && Phase == DebrisDropPhase.Closing)
        {
            Phase = DebrisDropPhase.Waiting;
            remainingTime = waitDuration;
        }
    }

    private void FixedUpdate()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f) return;
        float deltaTime = Time.fixedDeltaTime;
        if (Phase == DebrisDropPhase.Waiting)
        {
            remainingTime -= deltaTime;
            if (remainingTime <= 0f)
            {
                Phase = DebrisDropPhase.Opening;
                RequestDoors(true);
            }
        }
        else if (Phase == DebrisDropPhase.Attacking)
        {
            float activeStep = Mathf.Min(deltaTime, remainingTime);
            while (nextWaveTime < activeStep)
            {
                SpawnWave();
                nextWaveTime += waveInterval;
            }
            nextWaveTime -= activeStep;
            remainingTime -= deltaTime;
            if (remainingTime <= 0f)
            {
                Phase = DebrisDropPhase.Draining;
            }
        }

        AdvanceClusters(deltaTime);
        if (Phase == DebrisDropPhase.Draining)
        {
            Physics2D.SyncTransforms();
            if (HasClearedDoors())
            {
                Phase = DebrisDropPhase.Closing;
                RequestDoors(false);
            }
        }
    }

    private bool HasClearedDoors()
    {
        float bottom = float.PositiveInfinity;
        foreach (SpriteRenderer renderer in doorRenderers)
        {
            if (renderer == null) return false;
            bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        }
        foreach (DebrisCluster cluster in activeClusters)
            if (cluster != null && cluster.Top >= bottom) return false;
        return true;
    }

    private void RequestDoors(bool open)
    {
        if (doorAnimator != null && doorAnimator.isActiveAndEnabled
            && doorAnimator.runtimeAnimatorController != null && !string.IsNullOrEmpty(doorsOpenParameter))
            doorAnimator.SetBool(doorsOpenParameter, open);
    }

    private void SpawnWave()
    {
        for (int i = 0; i < clustersPerWave; i++)
        {
            float slot = (i + 0.5f) / clustersPerWave - 0.5f;
            float jitter = Random.Range(-0.1f, 0.1f) * spawnWidth / clustersPerWave;
            Vector3 position = spawnPoint.TransformPoint(new Vector3(slot * spawnWidth + jitter, 0f));
            DebrisCluster prefab = clusterPrefabs[Random.Range(0, clusterPrefabs.Length)];
            DebrisCluster cluster = Instantiate(prefab, position, Quaternion.identity);
            if (cluster.gameObject.scene != gameObject.scene)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cluster.gameObject, gameObject.scene);
            cluster.BeginFalling(Random.Range(fallSpeedRange.x, fallSpeedRange.y));
            activeClusters.Add(cluster);
        }
    }

    private void AdvanceClusters(float deltaTime)
    {
        contacts.Clear();
        Physics2D.SyncTransforms();
        for (int i = activeClusters.Count - 1; i >= 0; i--)
        {
            DebrisCluster cluster = activeClusters[i];
            if (cluster != null && cluster.Advance(deltaTime, endPoint.position.y, targetLayers, contacts)) continue;
            if (cluster != null)
            {
                cluster.gameObject.SetActive(false);
                Destroy(cluster.gameObject);
            }
            activeClusters.RemoveAt(i);
        }

        damagedTargets.IntersectWith(contacts.Keys);
        foreach (KeyValuePair<IDamageable, Vector2> contact in contacts)
        {
            Component component = contact.Key as Component;
            if (component == null || damagedTargets.Contains(contact.Key)) continue;
            float direction = component.transform.position.x >= contact.Value.x ? 1f : -1f;
            Vector2 velocity = new Vector2(direction * knockbackVelocity.x, knockbackVelocity.y);
            bool accepted = contact.Key.TryTakeDamage(new DamageInfo(1, contact.Value, velocity, controlLockDuration));
            if (!isActiveAndEnabled) return;
            if (accepted) damagedTargets.Add(contact.Key);
        }
    }

    private void OnDisable()
    {
        RequestDoors(false);
        foreach (DebrisCluster cluster in activeClusters)
        {
            if (cluster == null) continue;
            cluster.gameObject.SetActive(false);
            Destroy(cluster.gameObject);
        }
        activeClusters.Clear();
        contacts.Clear();
        damagedTargets.Clear();
        Phase = DebrisDropPhase.Closing;
    }

    private void OnValidate()
    {
        waitDuration = Mathf.Max(0f, waitDuration);
        attackDuration = Mathf.Max(0.01f, attackDuration);
        spawnWidth = Mathf.Max(0f, spawnWidth);
        clustersPerWave = Mathf.Max(1, clustersPerWave);
        waveInterval = Mathf.Max(0.01f, waveInterval);
        fallSpeedRange.x = Mathf.Max(0.01f, fallSpeedRange.x);
        fallSpeedRange.y = Mathf.Max(fallSpeedRange.x, fallSpeedRange.y);
        knockbackVelocity.x = Mathf.Max(0f, knockbackVelocity.x);
        knockbackVelocity.y = Mathf.Max(0f, knockbackVelocity.y);
        controlLockDuration = Mathf.Max(0f, controlLockDuration);
    }
}

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Boss1Controller))]
public sealed class Boss1DebrisField : MonoBehaviour
{
    public event System.Action<Boss1FallingDebris> DebrisBroken;

    [Header("Scene Anchors")]
    [SerializeField] private Transform ceilingAnchor;
    [SerializeField] private Transform leftDropLimit;
    [SerializeField] private Transform rightDropLimit;
    [SerializeField] private Transform runtimeParent;

    [Header("Debris")]
    [UnityEngine.Serialization.FormerlySerializedAs("scrapPrefab")]
    [SerializeField] private Boss1FallingDebris debrisPrefab;
    [SerializeField] private Boss1DebrisConfig config;
    [SerializeField] private CombatAnimationEffect landingEffectPrefab;

    private readonly List<Boss1FallingDebris> debrisPieces = new List<Boss1FallingDebris>();
    private readonly List<float> dropPositions = new List<float>();
    private readonly List<Boss1DebrisWarningArea> dropWarnings = new List<Boss1DebrisWarningArea>();
    private readonly List<RaycastHit2D> handMovementHits = new List<RaycastHit2D>();
    private readonly List<RaycastHit2D> launchMovementHits = new List<RaycastHit2D>();
    private Boss1Controller controller;
    private PlayerHealth player;
    private Collider2D playerCollider;
    private Collider2D floorCollider;
    private Boss1DebrisWarningArea warningArea;
    private GameObject runtimeRoot;
    private float minimumDropX;
    private float maximumDropX;
    private float debrisWidth;
    private float warningDuration;
    private float warningElapsed;

    public Collider2D FloorCollider => floorCollider;

    public bool HasLandedDebris
    {
        get
        {
            foreach (Boss1FallingDebris debris in debrisPieces)
                if (debris != null && debris.isActiveAndEnabled
                    && debris.State == Boss1DebrisState.Landed) return true;
            return false;
        }
    }

    public bool HasPendingDebris
    {
        get
        {
            if (dropPositions.Count > 0) return true;
            foreach (Boss1FallingDebris debris in debrisPieces)
                if (debris != null && debris.State == Boss1DebrisState.Falling) return true;
            return false;
        }
    }

    public bool TryFindFirstLandedDebris(Collider2D handCollider, Vector2 movement,
        out Boss1FallingDebris debris, out float hitDistance)
    {
        debris = null;
        hitDistance = movement.magnitude;
        if (!isActiveAndEnabled || handCollider == null || !handCollider.enabled) return false;

        Physics2D.SyncTransforms();
        foreach (Boss1FallingDebris candidate in debrisPieces)
        {
            if (candidate == null || !candidate.isActiveAndEnabled
                || candidate.State != Boss1DebrisState.Landed || !candidate.Body.enabled) continue;
            ColliderDistance2D contact = handCollider.Distance(candidate.Body);
            if (!contact.isValid || !contact.isOverlapped) continue;
            debris = candidate;
            hitDistance = 0f;
            return true;
        }

        if (hitDistance <= Mathf.Epsilon) return false;
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        handCollider.Cast(movement.normalized, filter, handMovementHits, hitDistance);
        foreach (RaycastHit2D hit in handMovementHits)
        {
            Boss1FallingDebris candidate = hit.collider.GetComponent<Boss1FallingDebris>();
            if (candidate == null || !candidate.isActiveAndEnabled
                || candidate.State != Boss1DebrisState.Landed || !debrisPieces.Contains(candidate)
                || hit.distance > hitDistance) continue;
            debris = candidate;
            hitDistance = hit.distance;
        }
        return debris != null;
    }

    public bool TryLaunchDebris(Boss1FallingDebris debris)
    {
        return isActiveAndEnabled && debris != null && debris.isActiveAndEnabled
            && debrisPieces.Contains(debris) && debris.BeginLaunch();
    }

    public bool TryFindSupportingDebris(Collider2D target, out Boss1FallingDebris supportingDebris)
    {
        supportingDebris = null;
        if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) return false;
        if (target.attachedRigidbody != null && target.attachedRigidbody.linearVelocity.y > 0.1f) return false;

        Physics2D.SyncTransforms();
        Bounds targetBounds = target.bounds;
        float tolerance = Mathf.Max(Physics2D.defaultContactOffset * 3f, 0.05f);
        foreach (Boss1FallingDebris candidate in debrisPieces)
        {
            if (candidate == null || !candidate.isActiveAndEnabled
                || candidate.State != Boss1DebrisState.Landed || !candidate.Body.enabled) continue;
            Bounds bounds = candidate.Body.bounds;
            if (targetBounds.max.x <= bounds.min.x || targetBounds.min.x >= bounds.max.x
                || Mathf.Abs(targetBounds.min.y - bounds.max.y) > tolerance) continue;
            supportingDebris = candidate;
            return true;
        }
        return false;
    }

    public bool AdvanceLaunchedDebris(Boss1FallingDebris debris, float direction,
        float movementDistance, Collider2D targetWall, out Vector2 movement)
    {
        if (debris == null || !debris.isActiveAndEnabled || !debrisPieces.Contains(debris)
            || debris.State != Boss1DebrisState.Flying)
            throw new System.InvalidOperationException("Flight needs active, registered flying debris.");
        if (targetWall == null || !targetWall.enabled || !targetWall.gameObject.activeInHierarchy)
            throw new System.InvalidOperationException("Flight needs an active destination wall.");

        Physics2D.SyncTransforms();
        Bounds bounds = debris.Body.bounds;
        float remainingDistance = direction > 0f ? targetWall.bounds.min.x - bounds.max.x
            : bounds.min.x - targetWall.bounds.max.x;
        float distance = Mathf.Min(Mathf.Max(0f, movementDistance), Mathf.Max(0f, remainingDistance));
        Vector2 movementDirection = direction > 0f ? Vector2.right : Vector2.left;
        movement = movementDirection * distance;

        // Handle initial overlaps as well as contacts anywhere along a fast movement segment.
        for (int i = debrisPieces.Count - 1; i >= 0; i--)
        {
            Boss1FallingDebris candidate = debrisPieces[i];
            if (candidate == null || candidate == debris || !candidate.isActiveAndEnabled
                || !candidate.Body.enabled) continue;
            ColliderDistance2D contact = debris.Body.Distance(candidate.Body);
            if (contact.isValid && contact.isOverlapped) BreakDebris(candidate);
        }
        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        Physics2D.BoxCast(bounds.center, bounds.size, 0f, movementDirection,
            filter, launchMovementHits, distance);
        foreach (RaycastHit2D hit in launchMovementHits)
        {
            Boss1FallingDebris candidate = hit.collider.GetComponent<Boss1FallingDebris>();
            if (candidate != null && candidate != debris) BreakDebris(candidate);
        }

        debris.MoveLaunched(movement);
        if (remainingDistance > Mathf.Max(0f, movementDistance)) return false;
        BreakDebris(debris);
        return true;
    }

    public void BreakDebris(Boss1FallingDebris debris)
    {
        if (debris == null || !debrisPieces.Contains(debris)) return;
        controller.GetComponent<Boss1Audio>()?.PlayDebrisBreak();
        DebrisBroken?.Invoke(debris);
        RemoveDebris(debris);
    }

    public void RemoveDebris(Boss1FallingDebris debris)
    {
        if (debris == null || !debrisPieces.Remove(debris)) return;
        debris.gameObject.SetActive(false);
        Destroy(debris.gameObject);
    }

    private void Awake()
    {
        controller = GetComponent<Boss1Controller>();
        ResolveWarningArea();
        warningArea?.Hide();
    }

    private void OnEnable()
    {
        if (controller == null) controller = GetComponent<Boss1Controller>();
        controller.StateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        controller.StateChanged -= HandleStateChanged;
        if (controller.CurrentPattern is Boss1SlamPattern || controller.CurrentPattern is Boss1OverdrivePattern)
            controller.CancelCurrentPattern();
        Clear();
    }

    public bool Prepare(PlayerHealth target, Collider2D floor)
    {
        Clear();
        ResolveWarningArea();
        if (!isActiveAndEnabled || config == null || ceilingAnchor == null
            || leftDropLimit == null || rightDropLimit == null || debrisPrefab == null
            || warningArea == null || target == null || runtimeParent == null || floor == null)
            return false;

        player = target;
        floorCollider = floor;
        playerCollider = target.GetComponent<Collider2D>();
        Vector2 debrisSize = debrisPrefab.WorldSize;
        float left = leftDropLimit.position.x;
        float right = rightDropLimit.position.x;
        BoxCollider2D prefabCollider = debrisPrefab.GetComponent<BoxCollider2D>();
        if (playerCollider == null || debrisSize.x <= 0f || debrisSize.y <= 0f
            || right - left < debrisSize.x || prefabCollider == null
            || debrisPrefab.GetComponent<Rigidbody2D>() == null
            || Quaternion.Angle(debrisPrefab.transform.rotation, Quaternion.identity) > 0.01f
            || floor.bounds.max.y >= ceilingAnchor.position.y)
        {
            Debug.LogWarning("Debris requires valid limits, a Rigidbody2D, and a BoxCollider2D.", this);
            return false;
        }

        minimumDropX = left + debrisSize.x * 0.5f;
        maximumDropX = right - debrisSize.x * 0.5f;
        debrisWidth = debrisSize.x;
        runtimeRoot = new GameObject("Boss1 Runtime Debris");
        runtimeRoot.transform.SetParent(runtimeParent, false);
        return true;
    }

    public bool StartDebrisSequence(int count, float warningDuration, float warningWidth)
    {
        if (runtimeRoot == null || player == null || dropPositions.Count > 0) return false;
        this.warningDuration = Mathf.Max(0f, warningDuration);
        warningElapsed = 0f;

        float spacing = debrisWidth + Physics2D.defaultContactOffset * 2f;
        float minimumGap = playerCollider.bounds.size.x + Physics2D.defaultContactOffset * 4f;
        int capacity = Mathf.FloorToInt((maximumDropX - minimumDropX) / spacing) + 1;
        count = Mathf.Clamp(count, 1, capacity);
        // Reserve the required spacing, randomize the free space, then restore each block's width.
        float randomMaximum = Mathf.Max(minimumDropX, maximumDropX - spacing * (count - 1));
        for (int i = 0; i < count; i++)
            dropPositions.Add(Random.Range(minimumDropX, randomMaximum));
        dropPositions.Sort();
        for (int i = 0; i < count; i++)
        {
            dropPositions[i] += spacing * i;
            // Close narrow gaps; leave open gaps wide enough for the player and both contact skins.
            if (i > 0 && dropPositions[i] - dropPositions[i - 1] - debrisWidth < minimumGap)
                dropPositions[i] = dropPositions[i - 1] + spacing;
        }

        // Keep the authored warning hidden as a style template, without runtime frame children.
        warningArea.Hide();
        controller.GetComponent<Boss1Audio>()?.PlayWarning();
        foreach (float x in dropPositions)
        {
            Boss1DebrisWarningArea warning = Instantiate(warningArea, runtimeRoot.transform);
            warning.gameObject.SetActive(true);
            warning.ShowAt(x, ceilingAnchor.position.y, floorCollider.bounds.max.y,
                ceilingAnchor.position.z, this.warningDuration, warningWidth);
            dropWarnings.Add(warning);
        }
        return true;
    }

    private void SpawnDebris(float x)
    {
        Vector3 position = new Vector3(x, ceilingAnchor.position.y, ceilingAnchor.position.z);
        Boss1FallingDebris debris = Instantiate(debrisPrefab, position, Quaternion.identity, runtimeRoot.transform);
        // Drop limits and warning positions describe the collider center, independently of the sprite pivot.
        debris.transform.position -= debris.transform.TransformVector(debris.GetComponent<BoxCollider2D>().offset);
        debris.BeginFalling(playerCollider);
        debrisPieces.Add(debris);
    }

    private void Update()
    {
        if (runtimeRoot == null || dropPositions.Count == 0) return;

        warningElapsed += Time.deltaTime;
        if (warningElapsed < warningDuration) return;

        foreach (Boss1DebrisWarningArea warning in dropWarnings)
        {
            warning.Hide();
            Destroy(warning.gameObject);
        }
        dropWarnings.Clear();
        controller.GetComponent<Boss1Audio>()?.StopWarning();
        foreach (float x in dropPositions) SpawnDebris(x);
        dropPositions.Clear();
    }

    private void FixedUpdate()
    {
        if (runtimeRoot == null) return;
        GameObject activeRoot = runtimeRoot;
        float movementDistance = Mathf.Max(0.01f, config.FallSpeed) * Time.fixedDeltaTime;
        for (int i = debrisPieces.Count - 1; i >= 0; i--)
        {
            Boss1FallingDebris debris = debrisPieces[i];
            if (debris == null) { debrisPieces.RemoveAt(i); continue; }
            if (debris.State == Boss1DebrisState.Flying) continue;

            // Resolve protection before MovePosition can press the player into the floor.
            float contactDistance = debris.CanDealDamage ? movementDistance : 0f;
            bool canAdvance = debris.CheckPlayerContact(player, playerCollider, contactDistance, config.DebrisDamage,
                config.DamageKnockbackSpeed, config.DamageKnockbackLift, config.DamageKnockbackDuration);
            // A lethal hit can synchronously clear the field through PlayerHealth.Died.
            if (runtimeRoot != activeRoot || player == null || player.IsDead) return;
            if (!canAdvance || debris.State != Boss1DebrisState.Falling) continue;
            debris.Advance(Time.fixedDeltaTime, config.FallSpeed, floorCollider, out bool breakOnLanding, out Vector2 landingPoint);
            if (breakOnLanding || debris.State == Boss1DebrisState.Landed)
                controller.GetComponent<Boss1Audio>()?.PlayDebrisLanding();
            if ((breakOnLanding || debris.State == Boss1DebrisState.Landed) && landingEffectPrefab != null)
                Instantiate(landingEffectPrefab, new Vector3(landingPoint.x, landingPoint.y, debris.transform.position.z),
                    Quaternion.identity, runtimeRoot.transform);
            if (breakOnLanding)
            {
                Boss1DebrisBreakEffect.Play(debris.GetComponent<SpriteRenderer>(), runtimeRoot.transform);
                BreakDebris(debris);
                config.LandingShake.Play(this);
                continue;
            }
            if (debris.State == Boss1DebrisState.Landed) config.LandingShake.Play(this);
        }
    }

    public void Clear()
    {
        if (dropPositions.Count > 0 && controller != null)
            controller.GetComponent<Boss1Audio>()?.StopWarning();
        HitCameraShake.Cancel(this);
        if (runtimeRoot != null)
        {
            runtimeRoot.SetActive(false);
            Destroy(runtimeRoot);
            runtimeRoot = null;
        }
        debrisPieces.Clear();
        dropPositions.Clear();
        dropWarnings.Clear();
        warningElapsed = 0f;
        warningArea?.Hide();
    }

    private void ResolveWarningArea()
    {
        if (warningArea == null) warningArea = GetComponentInChildren<Boss1DebrisWarningArea>(true);
    }

    private void HandleStateChanged(Boss1State previous, Boss1State next)
    {
        if (next == Boss1State.Dead || next == Boss1State.Disabled) Clear();
    }
}

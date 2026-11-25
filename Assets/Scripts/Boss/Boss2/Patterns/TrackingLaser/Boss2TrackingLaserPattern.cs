using System;
using System.Collections;
using UnityEngine;

/// <summary>Upper-hand gesture, ring departure and a vertical laser that tracks the player horizontally.</summary>
[DisallowMultipleComponent]
public sealed class Boss2TrackingLaserPattern : Boss2PatternBase
{
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private PlayerHealth target;
    [SerializeField] private Boss2TrackingLaserVisual laserPrefab;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private LayerMask playerLayers;

    [Header("Ring")]
    [SerializeField, Min(0.1f)] private float ringMoveSpeed = 6f;
    [SerializeField, Min(0.1f)] private float ringReturnSpeed = 6f;
    [SerializeField, Min(0f)] private float offscreenMargin = 0.25f;

    [Header("Laser")]
    [SerializeField, Min(0f)] private float warningDuration = 0.6f;
    [SerializeField, Min(0f)] private float trackingSpeed = 2f;
    [SerializeField, Min(0.1f)] private float trackingDuration = 3f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0.01f)] private float hitInterval = 0.5f;

    private bool requested;

    public override Boss2PatternKind Kind => Boss2PatternKind.Main;
    public override Boss2Parts RequiredParts
    {
        get
        {
            if (CanReserve(Boss2PartId.UpperRightHand)) return Boss2Parts.Ring | Boss2Parts.UpperRightHand;
            if (CanReserve(Boss2PartId.UpperLeftHand)) return Boss2Parts.Ring | Boss2Parts.UpperLeftHand;
            return Boss2Parts.None;
        }
    }

    private bool HasTarget => target != null && target.isActiveAndEnabled && !target.IsDead;

    /// <summary>Queues one request until its required parts can be reserved.</summary>
    public void Request() => requested = true;

    private void Update()
    {
        if (!requested || Owner.CurrentState != Boss2CombatState.Fighting || Owner.IsDead) return;
        if (Owner.TryRequestPattern(this, out _)) requested = false;
    }

    protected override void OnDisable()
    {
        requested = false;
        base.OnDisable();
    }

    private bool CanReserve(Boss2PartId id)
    {
        if (!Owner.HasActiveParts(Boss2PartMask.FromId(id))) return false;
        Boss2PartUsage.Reservation owner = Owner.PartUsage.GetOwner(id);
        return Owner.PartUsage.GetReservation(id) == null && (owner == null || owner.Kind == Boss2PatternKind.Basic);
    }

    public override Boss2PatternExecution CreateExecution(Boss2PatternContext context)
    {
        Boss2Parts handPart = context.HeldParts & Boss2Parts.UpperHands;
        Boss2PartId handId = handPart == Boss2Parts.UpperRightHand ? Boss2PartId.UpperRightHand : Boss2PartId.UpperLeftHand;
        var handAnimation = context.GetPart(handId).GetComponentInChildren<Boss2TrackingLaserHandAnimation>(true);
        if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled || handAnimation == null ||
            !handAnimation.isActiveAndEnabled || target == null || laserPrefab == null ||
            groundLayers.value == 0 || playerLayers.value == 0 || ringMoveSpeed <= 0f || ringReturnSpeed <= 0f)
            throw new InvalidOperationException("Assign the Tracking Laser camera, selected upper-hand animation, target, laser prefab and collision layers.");
        return new Execution(this, context, handPart, handAnimation);
    }

    private sealed class Execution : Boss2PatternExecution
    {
        private readonly Boss2TrackingLaserPattern pattern;
        private readonly Boss2PartController ring;
        private readonly Boss2Parts handPart;
        private readonly Boss2TrackingLaserHandAnimation handAnimation;
        private Boss2TrackingLaserVisual laser;
        private bool handReleased;

        public Execution(Boss2TrackingLaserPattern pattern, Boss2PatternContext context,
            Boss2Parts handPart, Boss2TrackingLaserHandAnimation handAnimation)
        {
            this.pattern = pattern;
            ring = context.GetPart(Boss2PartId.Ring);
            this.handPart = handPart;
            this.handAnimation = handAnimation;
        }

        public override IEnumerator Execute(Boss2PatternContext context)
        {
            SpriteRenderer ringRenderer = ring.GetComponentInChildren<SpriteRenderer>();
            if (ringRenderer == null || ring.IdleAnchor == null)
                throw new InvalidOperationException("Assign the ring visual and idle anchor.");

            handAnimation.Begin();
            while (!handAnimation.ForwardCompleted)
            {
                if (handAnimation.Interrupted || !handAnimation.isActiveAndEnabled)
                    throw new InvalidOperationException("The hand gesture was interrupted.");
                yield return null;
            }

            if (!pattern.HasTarget || context.FinishRequested)
            {
                handAnimation.BeginReturn();
                while (!handReleased) { ReleaseHandIfReady(context); yield return null; }
                yield break;
            }

            float departureX = context.Controller.transform.position.x;

            Vector3 viewport = pattern.gameplayCamera.WorldToViewportPoint(
                new Vector3(departureX, ring.transform.position.y, ring.transform.position.z));
            Vector3 top = pattern.gameplayCamera.ViewportToWorldPoint(new Vector3(viewport.x, 1f, viewport.z));
            Vector3 destination = new Vector3(departureX,
                top.y + ringRenderer.bounds.extents.y + pattern.offscreenMargin, ring.transform.position.z);
            ring.MoveToWorld(destination, Vector2.Distance(ring.transform.position, destination) / pattern.ringMoveSpeed);
            handAnimation.BeginReturn();

            while (ring.IsMoving)
            {
                ReleaseHandIfReady(context);
                yield return null;
            }

            if (pattern.HasTarget && !context.FinishRequested)
            {
                // Lock the warning and first hit to the player's position when the warning starts.
                float laserX = pattern.target.transform.position.x;
                laser = UnityEngine.Object.Instantiate(pattern.laserPrefab);
                laser.BindAudio(context.Controller.GetComponent<Boss2Audio>());
                laser.BeginWarning(pattern.warningDuration);
                UpdateLaserPosition(laserX);
                float warningElapsed = 0f;
                while (warningElapsed < pattern.warningDuration && pattern.HasTarget && !context.FinishRequested)
                {
                    EnsureLaserActive();
                    ReleaseHandIfReady(context);
                    warningElapsed += Time.deltaTime;
                    yield return null;
                }

                if (pattern.HasTarget && !context.FinishRequested)
                {
                    laser.BeginFire();
                    while (!laser.FireEntered)
                    {
                        EnsureLaserActive();
                        ReleaseHandIfReady(context);
                        yield return null;
                    }

                    float elapsed = 0f;
                    float hitCooldown = 0f;
                    while (elapsed < pattern.trackingDuration && pattern.HasTarget && !context.FinishRequested)
                    {
                        EnsureLaserActive();
                        ReleaseHandIfReady(context);
                        if (UpdateLaserPosition(laserX) && hitCooldown <= 0f)
                        {
                            Bounds bounds = laser.DamageBounds;
                            Collider2D contact = Physics2D.OverlapBox(bounds.center, bounds.size, 0f, pattern.playerLayers);
                            PlayerHealth player = contact != null ? contact.GetComponentInParent<PlayerHealth>() : null;
                            if (player != null)
                            {
                                player.TryTakeDamage(new DamageInfo(pattern.damage,
                                    new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)));
                                hitCooldown = pattern.hitInterval;
                            }
                        }
                        // The first damage check uses the saved X; tracking starts after that check.
                        if (pattern.HasTarget)
                            laserX = Mathf.MoveTowards(laserX, pattern.target.transform.position.x,
                                pattern.trackingSpeed * Time.deltaTime);
                        elapsed += Time.deltaTime;
                        hitCooldown -= Time.deltaTime;
                        yield return null;
                    }
                    laser.BeginFade();
                    while (!laser.Completed)
                    {
                        EnsureLaserActive();
                        ReleaseHandIfReady(context);
                        yield return null;
                    }
                }
                else laser.Cancel();
            }

            ring.ReturnToIdle(Vector2.Distance(ring.transform.position, ring.IdleAnchor.position) / pattern.ringReturnSpeed);
            while (ring.IsMoving || !handReleased)
            {
                ReleaseHandIfReady(context);
                yield return null;
            }
            yield return context.WaitForParts(Boss2Parts.Ring);
        }

        private void ReleaseHandIfReady(Boss2PatternContext context)
        {
            if (handReleased) return;
            if (handAnimation.Interrupted || !handAnimation.isActiveAndEnabled)
                throw new InvalidOperationException("The hand return animation was interrupted.");
            if (!handAnimation.Completed) return;
            context.ReleaseParts(handPart);
            handReleased = true;
        }

        private bool UpdateLaserPosition(float x)
        {
            float depth = pattern.gameplayCamera.WorldToViewportPoint(ring.transform.position).z;
            Vector3 screenTop = pattern.gameplayCamera.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth));
            Vector3 top = new Vector3(x, screenTop.y + pattern.offscreenMargin, ring.transform.position.z);
            RaycastHit2D floor = Physics2D.Raycast(top, Vector2.down, Mathf.Infinity, pattern.groundLayers);
            Vector3 ground = new Vector3(x, floor.point.y, top.z);
            if (floor.collider == null || ground.y >= top.y)
            {
                laser.HideGeometry();
                return false;
            }
            laser.SetGroundPosition(ground);
            // Keep the hidden ring above the displayed laser, so its return starts at the final tracked X.
            ring.MoveToWorld(new Vector3(x, ring.transform.position.y, ring.transform.position.z), 0f);
            return true;
        }

        private void EnsureLaserActive()
        {
            if (laser == null || !laser.isActiveAndEnabled || laser.Interrupted)
                throw new InvalidOperationException("The Tracking Laser animation was interrupted.");
        }

        public override void Cleanup(Boss2PatternResult result)
        {
            if (result != Boss2PatternResult.Completed && !handReleased) handAnimation.Cancel();
            if (laser != null)
            {
                laser.Cancel();
                UnityEngine.Object.Destroy(laser.gameObject);
            }
        }

        public override void StopForDeath(Boss2PatternContext context)
        {
            if (laser == null) return;
            laser.Cancel();
            UnityEngine.Object.Destroy(laser.gameObject);
            laser = null;
        }

        public override IEnumerator ReturnAfterDeath(Boss2PatternContext context)
        {
            if (!handReleased)
            {
                if (handAnimation.Active) handAnimation.BeginDeathReturn();
                else { context.ReleaseParts(handPart); handReleased = true; }
            }
            ring.ReturnToIdle(Vector2.Distance(ring.transform.position, ring.IdleAnchor.position) / pattern.ringReturnSpeed);
            while (ring.IsMoving || !handReleased)
            {
                ReleaseHandIfReady(context);
                yield return null;
            }
            yield return context.WaitForParts(Boss2Parts.Ring);
        }
    }
}

using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss2BlackHolePattern : Boss2PatternBase
{
    [SerializeField] private PlayerHealth target;
    [SerializeField] private Transform[] destinations = Array.Empty<Transform>();
    [Header("Animation Bindings")]
    [SerializeField] private Boss2BlackHoleAnimation leftHandAnimation;
    [SerializeField] private Boss2BlackHoleAnimation rightHandAnimation;
    [SerializeField] private Boss2BlackHoleAnimation ringAnimation;
    [Tooltip("Local +X must point along the extended finger. This marker follows the hand Animator.")]
    [SerializeField] private Transform leftPointingOrigin;
    [SerializeField] private Transform rightPointingOrigin;
    [SerializeField] private Boss2BlackHoleField field;
    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float ringMoveSpeed = 6f;
    [SerializeField, Min(0.1f)] private float ringReturnSpeed = 6f;
    [SerializeField, Min(1f)] private float pointingTurnSpeed = 180f;
    [Tooltip("Seconds to loop the active motion and apply suction/damage after the windup completes.")]
    [SerializeField, Min(0.1f)] private float suctionDuration = 3f;

    private Transform queuedDestination;
    private Vector3 queuedPosition;
    private bool requested;

    public override Boss2PatternKind Kind => Boss2PatternKind.Main;
    private bool HasTarget => target != null && target.isActiveAndEnabled && !target.IsDead;

    public override Boss2Parts RequiredParts
    {
        get
        {
            if (!SelectDestination()) return Boss2Parts.None;
            Boss2PartId id = queuedPosition.x < Owner.transform.position.x
                ? Boss2PartId.UpperLeftHand : Boss2PartId.UpperRightHand;
            Boss2Parts hand = Boss2PartMask.FromId(id);
            if (!Owner.HasActiveParts(hand | Boss2Parts.Ring) || Owner.PartUsage.GetReservation(id) != null)
                return Boss2Parts.None;
            var current = Owner.PartUsage.GetOwner(id);
            if (current != null && current.Kind != Boss2PatternKind.Basic) return Boss2Parts.None;
            return hand | Boss2Parts.Ring;
        }
    }

    private bool SelectDestination()
    {
        if (queuedDestination != null && Mathf.Abs(queuedPosition.x - Owner.transform.position.x) > 0.01f) return true;
        queuedDestination = null;
        if (destinations == null || destinations.Length == 0) return false;
        int first = UnityEngine.Random.Range(0, destinations.Length);
        for (int i = 0; i < destinations.Length; i++)
        {
            Transform candidate = destinations[(first + i) % destinations.Length];
            if (candidate == null || Mathf.Abs(candidate.position.x - Owner.transform.position.x) <= 0.01f) continue;
            queuedDestination = candidate;
            queuedPosition = candidate.position;
            return true;
        }
        return false;
    }

    public void Request() => requested = true;

    private void Update()
    {
        if (!requested || !HasTarget || Owner.CurrentState != Boss2CombatState.Fighting || Owner.IsDead) return;
        if (Owner.TryRequestPattern(this, out _)) requested = false;
    }

    protected override void OnDisable()
    {
        requested = false;
        queuedDestination = null;
        base.OnDisable();
    }

    public override Boss2PatternExecution CreateExecution(Boss2PatternContext context)
    {
        bool left = (context.HeldParts & Boss2Parts.UpperLeftHand) != 0;
        var animation = left ? leftHandAnimation : rightHandAnimation;
        var pointingOrigin = left ? leftPointingOrigin : rightPointingOrigin;
        if (queuedDestination == null || animation == null || !animation.isActiveAndEnabled ||
            pointingOrigin == null || ringAnimation == null || !ringAnimation.isActiveAndEnabled ||
            field == null || !field.isActiveAndEnabled || target == null)
            throw new InvalidOperationException("Assign the Black Hole destinations, target, hand/ring animations, pointing origins and field.");
        Vector3 destination = queuedPosition;
        queuedDestination = null;
        return new Execution(this, context, left ? Boss2PartId.UpperLeftHand : Boss2PartId.UpperRightHand,
            animation, pointingOrigin, destination);
    }

    private sealed class Execution : Boss2PatternExecution
    {
        private readonly Boss2BlackHolePattern pattern;
        private readonly Boss2PartController hand;
        private readonly Boss2PartController ring;
        private readonly Boss2Parts handMask;
        private readonly Boss2BlackHoleAnimation handAnimation;
        private readonly Transform pointingOrigin;
        private readonly Vector3 destination;
        private readonly SpriteRenderer ringRenderer;
        private readonly bool originalRingFlip;
        private bool handReleased;
        private bool handReturning;

        public Execution(Boss2BlackHolePattern pattern, Boss2PatternContext context, Boss2PartId handId,
            Boss2BlackHoleAnimation animation, Transform pointingOrigin, Vector3 destination)
        {
            this.pattern = pattern;
            hand = context.GetPart(handId);
            ring = context.GetPart(Boss2PartId.Ring);
            handMask = Boss2PartMask.FromId(handId);
            handAnimation = animation;
            this.pointingOrigin = pointingOrigin;
            this.destination = destination;
            ringRenderer = pattern.ringAnimation.GetComponent<SpriteRenderer>();
            if (ringRenderer == null) throw new InvalidOperationException("Assign the ring animation's SpriteRenderer.");
            originalRingFlip = ringRenderer.flipX;
        }

        public override IEnumerator Execute(Boss2PatternContext context)
        {
            handAnimation.Begin();
            bool aligned = false;
            while (!handAnimation.Prepared || !aligned)
            {
                EnsureAnimation(handAnimation);
                if (context.FinishRequested && handAnimation.Prepared)
                {
                    yield return ReturnParts(context, false);
                    yield break;
                }
                if (!pattern.HasTarget)
                {
                    BeginHandReturn(true);
                    while (!handReleased) { UpdateHandReturn(context); yield return null; }
                    yield break;
                }
                if (!hand.Aim.TryAimAt(destination, pointingOrigin, pattern.pointingTurnSpeed * Time.deltaTime, out aligned))
                    throw new InvalidOperationException("The Black Hole destination cannot be pointed at from this hand.");
                yield return null;
            }

            if (context.FinishRequested)
            {
                yield return ReturnParts(context, false);
                yield break;
            }

            FaceRingForTravel(destination, false);
            pattern.ringAnimation.Begin();
            Vector3 rootDestination = destination;
            rootDestination.z = ring.transform.position.z;
            rootDestination += ring.transform.position - pattern.field.OriginPosition;
            rootDestination.z = ring.transform.position.z;
            ring.MoveToWorld(rootDestination,
                Vector2.Distance(pattern.field.OriginPosition, destination) / pattern.ringMoveSpeed);
            BeginHandReturn(false);
            while (ring.IsMoving || !pattern.ringAnimation.Prepared)
            {
                EnsureAnimation(pattern.ringAnimation);
                UpdateHandReturn(context);
                yield return null;
            }
            if (pattern.HasTarget && !context.FinishRequested)
            {
                RestoreRingFacing();
                pattern.ringAnimation.BeginActive();
                while (!pattern.ringAnimation.ActiveEntered)
                {
                    EnsureAnimation(pattern.ringAnimation);
                    UpdateHandReturn(context);
                    yield return null;
                }
                pattern.field.Begin(context.Controller, pattern.target);
                float elapsed = 0f;
                while (elapsed < pattern.suctionDuration && pattern.HasTarget && !context.FinishRequested)
                {
                    EnsureAnimation(pattern.ringAnimation);
                    UpdateHandReturn(context);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            pattern.field.Stop();
            yield return ReturnParts(context, false);
        }

        private void BeginHandReturn(bool force)
        {
            if (handReleased || handReturning) return;
            hand.Aim.BeginReturnToIdle();
            handAnimation.BeginReturn(force);
            handReturning = true;
        }

        private void UpdateHandReturn(Boss2PatternContext context)
        {
            if (handReleased || !handReturning) return;
            EnsureAnimation(handAnimation);
            hand.Aim.UpdateReturnToIdle(handAnimation.ReturnProgress);
            if (handAnimation.Active && !handAnimation.Completed) return;
            hand.FollowIdle();
            context.ReleaseParts(handMask);
            handReleased = true;
        }

        private IEnumerator ReturnParts(Boss2PatternContext context, bool force)
        {
            // Death may arrive from PatternStarted before the first gesture has entered.
            if (force && !handReleased && !handAnimation.Active)
            {
                hand.FollowIdle();
                context.ReleaseParts(handMask);
                handReleased = true;
            }
            BeginHandReturn(force);
            bool recoverFirst = pattern.ringAnimation.Active && pattern.ringAnimation.ActiveEntered;
            if (!recoverFirst && pattern.ringAnimation.Active) FaceRingForTravel(ring.IdleAnchor.position, true);
            pattern.ringAnimation.BeginReturn(force);
            if (recoverFirst)
            {
                while (!pattern.ringAnimation.RecoveryCompleted)
                {
                    EnsureAnimation(pattern.ringAnimation);
                    UpdateHandReturn(context);
                    yield return null;
                }
                FaceRingForTravel(ring.IdleAnchor.position, true);
                pattern.ringAnimation.BeginReturnTravel();
            }
            ring.ReturnToIdle(Vector2.Distance(ring.transform.position, ring.IdleAnchor.position) / pattern.ringReturnSpeed);
            while (ring.IsMoving || pattern.ringAnimation.Active || !handReleased)
            {
                if (pattern.ringAnimation.Active) EnsureAnimation(pattern.ringAnimation);
                if (pattern.ringAnimation.Completed) RestoreRingFacing();
                UpdateHandReturn(context);
                yield return null;
            }
            yield return context.WaitForParts(Boss2Parts.Ring);
        }

        private void FaceRingForTravel(Vector3 targetPosition, bool returning)
        {
            float directionX = targetPosition.x - pattern.field.OriginPosition.x;
            if (Mathf.Abs(directionX) <= 0.0001f) return;
            // Authored travel faces right; the return sequence faces left.
            pattern.ringAnimation.SetTravelFacing(returning ? directionX > 0f : directionX < 0f);
        }

        private void RestoreRingFacing()
        {
            pattern.ringAnimation.ClearTravelFacing();
            if (ringRenderer != null) ringRenderer.flipX = originalRingFlip;
        }

        private static void EnsureAnimation(Boss2BlackHoleAnimation animation)
        {
            if (animation == null || !animation.isActiveAndEnabled || animation.Interrupted)
                throw new InvalidOperationException("The Black Hole animation was interrupted.");
        }

        public override void StopForDeath(Boss2PatternContext context) => pattern.field.Stop();
        public override IEnumerator ReturnAfterDeath(Boss2PatternContext context) => ReturnParts(context, true);

        public override void Cleanup(Boss2PatternResult result)
        {
            pattern.field.Stop();
            RestoreRingFacing();
            if (result == Boss2PatternResult.Completed) return;
            if (!handReleased) handAnimation.Cancel();
            pattern.ringAnimation.Cancel();
        }
    }
}

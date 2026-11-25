using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

[DisallowMultipleComponent]
public sealed class Boss2RingBurstPattern : Boss2PatternBase
{
    [Serializable]
    private sealed class HandBinding
    {
        public Boss2PartId part;
        public Transform destination;
        public Boss2CannonHandAnimation animation;
    }

    [Header("Bindings")]
    [SerializeField] private PlayerHealth target;
    [SerializeField] private Transform ringDestination;
    [SerializeField] private Transform ringOrigin;
    [SerializeField] private Boss2RingBurstAnimation ringAnimation;
    [SerializeField] private HandBinding[] hands = Array.Empty<HandBinding>();
    [SerializeField] private Boss2CannonBeamVisual chargeBeamPrefab;
    [SerializeField] private Boss2RingBurstField fieldPrefab;
    [SerializeField] private Collider2D[] boundaries = Array.Empty<Collider2D>();
    [SerializeField] private Boss2BodyMovement bodyMovement;
    [Tooltip("World-space body position during Ring Burst. Move this destination in the Scene to adjust X and Y.")]
    [SerializeField] private Transform bodyDestination;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
    [SerializeField, Min(0.1f)] private float returnSpeed = 6f;
    [SerializeField, Min(1f)] private float aimTurnSpeed = 180f;
    [SerializeField] private AnimationCurve returnCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Charging")]
    [Tooltip("Random delay after all five parts arrive and after each completed charging shot.")]
    [SerializeField] private Vector2 chargeDelayRange = new Vector2(0f, 0.5f);
    [SerializeField, Min(0.01f)] private float chargeBeamDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float chargeBeamWidth = 0.24f;
    [SerializeField, Min(1)] private int chargeDamage = 1;
    [SerializeField, Min(0.01f)] private float chargeHitInterval = 0.5f;
    [SerializeField] private LayerMask playerLayers;
    [Tooltip("Charging-loop gameplay duration, independent of the authored animation length.")]
    [SerializeField, Min(0.2f)] private float loopDuration = 3f;

    [Header("Warning and Release")]
    [Tooltip("Warning time after all charging beams end, before the ring's Attack animation starts.")]
    [SerializeField, Min(0.2f)] private float warningDuration = 3f;
    [Tooltip("Time for the longest reflected path to finish drawing; the rest of Warning Duration is a fixed warning hold.")]
    [SerializeField, Min(0.1f)] private float warningTravelDuration = 1.8f;

    [Header("Ring Rendering During Pattern")]
    [SerializeField] private string ringSortingLayer = "BossEffect";
    [SerializeField] private int ringSortingOrder = 4;

    private bool requested;
    public override Boss2PatternKind Kind => Boss2PatternKind.Main;
    public override Boss2Parts RequiredParts => Boss2Parts.AllHands | Boss2Parts.Ring;
    private bool HasTarget => target != null && target.isActiveAndEnabled && !target.IsDead;

    public void Request() => requested = true;

    private void Update()
    {
        if (!requested || !HasTarget || Owner.CurrentState != Boss2CombatState.Fighting || Owner.IsDead) return;
        if (Owner.TryRequestPattern(this, out _)) requested = false;
    }

    protected override void OnDisable()
    {
        requested = false;
        base.OnDisable();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        loopDuration = Mathf.Max(0.2f, loopDuration);
        warningDuration = Mathf.Max(0.2f, warningDuration);
        warningTravelDuration = Mathf.Clamp(warningTravelDuration, 0.1f, warningDuration - 0.1f);
        chargeDelayRange.x = Mathf.Max(0f, chargeDelayRange.x);
        chargeDelayRange.y = Mathf.Max(chargeDelayRange.x, chargeDelayRange.y);
    }

    public override Boss2PatternExecution CreateExecution(Boss2PatternContext context)
    {
        if (target == null || ringDestination == null || ringOrigin == null || ringAnimation == null ||
            chargeBeamPrefab == null || fieldPrefab == null || boundaries.Length == 0 || hands.Length != 4 ||
            bodyMovement == null || bodyDestination == null ||
            warningTravelDuration <= 0f || warningTravelDuration >= warningDuration)
            throw new InvalidOperationException("Assign the ring burst target, body and part destinations, animations, beam/field prefabs, boundaries, body movement and warning timing.");
        Boss2Parts assigned = Boss2Parts.None;
        foreach (HandBinding binding in hands)
        {
            if (binding == null || binding.part == Boss2PartId.Ring || binding.destination == null || binding.animation == null)
                throw new InvalidOperationException("Assign each of the four ring burst hands exactly once.");
            Boss2Parts mask = Boss2PartMask.FromId(binding.part);
            if ((assigned & mask) != 0) throw new InvalidOperationException("A ring burst hand is assigned twice.");
            assigned |= mask;
        }
        if (assigned != Boss2Parts.AllHands) throw new InvalidOperationException("Ring burst requires all four hands.");
        foreach (Collider2D boundary in boundaries)
            if (boundary == null || !boundary.enabled || boundary.isTrigger || !boundary.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Assign active, solid arena boundary colliders before requesting Ring Burst.");
        return new Execution(this, context);
    }

    private sealed class Hand
    {
        public HandBinding Binding;
        public Boss2PartController Part;
        public Boss2CannonBeamVisual Beam;
        public float Delay;
        public bool FireRequested;
        public bool HasFired;
        public bool Fading;
        public bool ReturnStarted;
    }

    private sealed class Execution : Boss2PatternExecution
    {
        private readonly Boss2RingBurstPattern pattern;
        private readonly Boss2PartController ring;
        private readonly SpriteRenderer ringRenderer;
        private readonly int originalSortingLayer;
        private readonly int originalSortingOrder;
        private readonly Hand[] hands;
        private readonly int layoutIndex;
        private readonly Vector3 ringDestination;
        private readonly Dictionary<PlayerHealth, float> nextContacts = new Dictionary<PlayerHealth, float>();
        private Boss2RingBurstField field;
        private float chargeClock;
        private bool chargeStarted;
        private bool released;
        private bool sortingOverridden;
        private bool ringReturnStarted;
        private float originalHeight;

        public Execution(Boss2RingBurstPattern pattern, Boss2PatternContext context)
        {
            this.pattern = pattern;
            ring = context.GetPart(Boss2PartId.Ring);
            ringRenderer = pattern.ringAnimation.GetComponent<SpriteRenderer>();
            if (ringRenderer == null)
                throw new InvalidOperationException("Assign the ring burst animation's SpriteRenderer.");
            bool sortingLayerFound = false;
            foreach (SortingLayer layer in SortingLayer.layers)
                if (layer.name == pattern.ringSortingLayer) sortingLayerFound = true;
            if (!sortingLayerFound)
                throw new InvalidOperationException("Assign an existing sorting layer for the ring during Ring Burst.");
            originalSortingLayer = ringRenderer.sortingLayerID;
            originalSortingOrder = ringRenderer.sortingOrder;
            if (!pattern.ringOrigin.IsChildOf(ring.transform) || !pattern.ringAnimation.transform.IsChildOf(ring.transform))
                throw new InvalidOperationException("Bind the ring burst animation and origin beneath the owned ring.");
            layoutIndex = pattern.fieldPrefab.SelectLayout();
            ringDestination = pattern.fieldPrefab.GetRingDestination(layoutIndex, pattern.ringDestination.position);
            hands = new Hand[pattern.hands.Length];
            for (int i = 0; i < hands.Length; i++)
            {
                HandBinding binding = pattern.hands[i];
                Boss2PartController part = context.GetPart(binding.part);
                if (part.Aim == null || !part.Aim.IsConfigured || !binding.animation.transform.IsChildOf(part.transform))
                    throw new InvalidOperationException("Bind the cannon animation, aim pivot and muzzle of each owned hand.");
                hands[i] = new Hand { Binding = binding, Part = part,
                    Delay = UnityEngine.Random.Range(pattern.chargeDelayRange.x, pattern.chargeDelayRange.y) };
            }
        }

        public override IEnumerator Execute(Boss2PatternContext context)
        {
            originalHeight = context.Controller.transform.position.y;
            pattern.bodyMovement.MoveForPattern(pattern.bodyDestination.position, pattern.moveSpeed);
            ringRenderer.sortingLayerName = pattern.ringSortingLayer;
            ringRenderer.sortingOrder = pattern.ringSortingOrder;
            sortingOverridden = true;
            // Hands prepare while travelling; ring preparation waits for the body and all five parts.
            Move(ring, pattern.ringOrigin.position, ringDestination);
            foreach (Hand hand in hands)
            {
                Move(hand.Part, hand.Part.Aim.PivotPosition, hand.Binding.destination.position);
                hand.Binding.animation.Begin();
            }
            while (!AllArrived())
            {
                EnsureBodyMovement();
                foreach (Hand hand in hands)
                    hand.Part.Aim.TryAimAt(pattern.ringOrigin.position, pattern.aimTurnSpeed * Time.deltaTime, out _);
                if (!pattern.HasTarget) { yield return ReturnParts(context, true); yield break; }
                yield return null;
            }
            EnsureBodyMovement();
            if (context.FinishRequested) { yield return FinishEarly(context); yield break; }
            pattern.ringAnimation.Begin();

            // Delays start only after all movement is complete. Preparation may finish independently.
            while (!AllHandsFired() || !pattern.ringAnimation.Prepared)
            {
                if (context.FinishRequested) { yield return FinishEarly(context); yield break; }
                EnsureRing();
                TickHands(true);
                if (pattern.Owner.IsDead) yield break;
                if (!pattern.HasTarget) { yield return ReturnParts(context, true); yield break; }
                chargeClock += Time.deltaTime;
                yield return null;
            }
            pattern.ringAnimation.BeginLoop();
            while (!pattern.ringAnimation.LoopEntered)
            {
                if (context.FinishRequested) { yield return FinishEarly(context); yield break; }
                EnsureRing();
                TickHands(true);
                if (pattern.Owner.IsDead) yield break;
                chargeClock += Time.deltaTime;
                yield return null;
            }

            float elapsed = 0f;
            while (elapsed < pattern.loopDuration)
            {
                if (context.FinishRequested) { yield return FinishEarly(context); yield break; }
                EnsureRing();
                TickHands(true);
                if (pattern.Owner.IsDead) yield break;
                if (!pattern.HasTarget) { yield return ReturnParts(context, true); yield break; }
                elapsed += Time.deltaTime;
                chargeClock += Time.deltaTime;
                yield return null;
            }
            StopCharge(false);
            while (!AllChargeCompleted())
            {
                EnsureRing();
                TickHands(false);
                if (pattern.Owner.IsDead) yield break;
                if (!pattern.HasTarget) { yield return ReturnParts(context, true); yield break; }
                yield return null;
            }

            field = Object.Instantiate(pattern.fieldPrefab);
            field.BindAudio(context.Controller.GetComponent<Boss2Audio>());
            field.Build(pattern.ringOrigin.position, pattern.boundaries,
                ringRenderer.sortingLayerID, ringRenderer.sortingOrder - 1, layoutIndex);
            elapsed = 0f;
            while (elapsed < pattern.warningDuration || !field.Prepared)
            {
                if (context.FinishRequested) { yield return FinishEarly(context); yield break; }
                EnsureRing();
                TickHands(false);
                if (pattern.Owner.IsDead) yield break;
                field.ShowWarning(elapsed / pattern.warningTravelDuration);
                if (!pattern.HasTarget) { yield return ReturnParts(context, true); yield break; }
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (context.FinishRequested) { yield return FinishEarly(context); yield break; }
            field.ShowWarning(1f);
            pattern.ringAnimation.BeginAttack();
            while (!pattern.ringAnimation.Completed || !released || field != null)
            {
                EnsureRing();
                if (!pattern.HasTarget) { yield return ReturnParts(context, true); yield break; }
                if (pattern.ringAnimation.AttackCompleted && !pattern.ringAnimation.Completed)
                {
                    pattern.ringAnimation.BeginReturn();
                }
                if (pattern.ringAnimation.Completed) StartRingReturn();
                if (!released && pattern.ringAnimation.Released)
                {
                    ReleaseField();
                }
                TickHands(false);
                if (pattern.Owner.IsDead) yield break;
                if (released && field != null)
                {
                    field.TickAttack(Time.deltaTime);
                    if (field.AttackCompleted) DestroyField();
                }
                else if (field != null) field.ShowWarning(1f);
                chargeClock += Time.deltaTime;
                yield return null;
            }
            yield return ReturnParts(context, false);
        }

        private void Move(Boss2PartController part, Vector3 pivot, Vector3 destination)
        {
            destination.z = pivot.z;
            part.MoveToWorld(destination + part.transform.position - pivot,
                Vector2.Distance(pivot, destination) / pattern.moveSpeed);
        }

        private bool AllArrived()
        {
            if (pattern.bodyMovement.IsPatternMoving || ring.IsMoving) return false;
            foreach (Hand hand in hands) if (hand.Part.IsMoving) return false;
            return true;
        }

        private void EnsureBodyMovement()
        {
            if (!pattern.bodyMovement.isActiveAndEnabled || pattern.bodyMovement.IsPatternMovementBlocked)
                throw new InvalidOperationException("Ring Burst body movement was interrupted or could not reach its destination.");
        }

        private void StartBodyReturn()
        {
            if (pattern.Owner.IsDead) return;
            Vector2 destination = pattern.Owner.transform.position;
            destination.y = originalHeight;
            pattern.bodyMovement.MoveForPattern(destination, pattern.returnSpeed);
        }

        private bool AllHandsFired()
        {
            foreach (Hand hand in hands) if (!hand.HasFired) return false;
            return true;
        }

        private bool AllChargeCompleted()
        {
            foreach (Hand hand in hands) if (hand.Beam != null && !hand.Beam.Completed) return false;
            return true;
        }

        private void TickHands(bool allowStart)
        {
            foreach (Hand hand in hands)
            {
                if (pattern.Owner.IsDead) return;
                EnsureHand(hand);
                hand.Part.Aim.TryAimAt(pattern.ringOrigin.position, pattern.aimTurnSpeed * Time.deltaTime, out bool aligned);
                if (allowStart && hand.Beam == null && chargeClock >= hand.Delay && hand.Binding.animation.Prepared && aligned)
                {
                    hand.Beam = Object.Instantiate(pattern.chargeBeamPrefab);
                    hand.Beam.BindAudio(pattern.Owner.GetComponent<Boss2Audio>());
                    hand.Beam.BeginAim();
                }
                if (hand.Beam == null) continue;
                if (hand.Beam.Completed)
                {
                    DestroyChargeBeam(hand);
                    hand.Delay = chargeClock + UnityEngine.Random.Range(pattern.chargeDelayRange.x, pattern.chargeDelayRange.y);
                    continue;
                }
                if (hand.Beam.Interrupted || !hand.Beam.isActiveAndEnabled)
                    throw new InvalidOperationException("A ring charging beam was interrupted.");
                Vector3 origin = hand.Part.Aim.FirePosition;
                Vector2 offset = pattern.ringOrigin.position - origin;
                hand.Beam.SetGeometry(origin, offset.normalized, offset.magnitude, pattern.chargeBeamWidth);
                if (allowStart && !hand.FireRequested && hand.Beam.AimCompleted &&
                    (!hand.HasFired || hand.Binding.animation.FireCompleted))
                {
                    if (hand.HasFired) hand.Binding.animation.RepeatFire();
                    else hand.Binding.animation.BeginFire();
                    hand.Beam.BeginFire(pattern.chargeBeamDuration);
                    hand.FireRequested = true;
                }
                if (hand.FireRequested && hand.Beam.FireEntered && hand.Binding.animation.FireEntered)
                {
                    hand.HasFired = true;
                    if (!chargeStarted)
                    {
                        chargeStarted = true;
                        pattern.ringAnimation.BeginCharge();
                    }
                }
                if (hand.FireRequested && hand.Beam.FireCompleted && !hand.Fading)
                {
                    hand.Fading = true;
                    hand.Beam.BeginFade();
                }
                if (hand.Beam.FireEntered && hand.Binding.animation.FireEntered && !hand.Fading && !released)
                    DamageCharge(origin, offset.normalized, offset.magnitude);
            }
        }

        private void DamageCharge(Vector3 origin, Vector2 direction, float distance)
        {
            foreach (RaycastHit2D hit in Physics2D.CircleCastAll(origin, pattern.chargeBeamWidth * 0.5f,
                direction, distance, pattern.playerLayers))
            {
                PlayerHealth player = hit.collider.GetComponentInParent<PlayerHealth>();
                if (player == null || (nextContacts.TryGetValue(player, out float next) && next > chargeClock)) continue;
                // One contact clock across all four supply beams, including dodged/countered contacts.
                nextContacts[player] = chargeClock + pattern.chargeHitInterval;
                player.TryTakeDamage(new DamageInfo(pattern.chargeDamage, origin));
            }
        }

        private void StopCharge(bool immediate)
        {
            foreach (Hand hand in hands)
            {
                if (hand.Beam == null) continue;
                if (immediate)
                {
                    hand.Fading = true;
                    hand.Beam.Cancel();
                    continue;
                }
                if (hand.Fading) continue;
                if (!hand.FireRequested) DestroyChargeBeam(hand);
                else if (hand.Beam.FireCompleted)
                {
                    hand.Fading = true;
                    hand.Beam.BeginFade();
                }
            }
        }

        private static void DestroyChargeBeam(Hand hand)
        {
            hand.Beam.Cancel();
            Object.Destroy(hand.Beam.gameObject);
            hand.Beam = null;
            hand.FireRequested = hand.Fading = false;
        }

        private IEnumerator FinishEarly(Boss2PatternContext context)
        {
            StopCharge(false);
            bool ready;
            do
            {
                if (pattern.ringAnimation.Active) EnsureRing();
                TickHands(false);
                ready = AllChargeCompleted() && (!pattern.ringAnimation.Active || pattern.ringAnimation.CanReturnEarly);
                foreach (Hand hand in hands)
                {
                    var animation = hand.Binding.animation;
                    ready &= animation.Prepared && (!animation.FireRequested || (animation.FireEntered && animation.FireCompleted));
                }
                if (!ready) yield return null;
            } while (!ready);
            if (pattern.ringAnimation.Active)
            {
                pattern.ringAnimation.BeginEarlyReturn();
                while (!pattern.ringAnimation.Completed) { EnsureRing(); yield return null; }
            }
            yield return ReturnParts(context, false);
        }

        private IEnumerator ReturnParts(Boss2PatternContext context, bool forced)
        {
            StopCharge(true);
            DestroyField();
            pattern.ringAnimation.Cancel();
            StartRingReturn();
            foreach (Hand hand in hands)
            {
                if (!hand.Binding.animation.Active) { StartHandReturn(hand); continue; }
                if (forced || !hand.Binding.animation.Prepared ||
                    (hand.Binding.animation.FireRequested && !hand.Binding.animation.FireCompleted)) hand.Binding.animation.BeginDeathReturn();
                else hand.Binding.animation.BeginReturn();
            }
            bool returning;
            do
            {
                returning = ring.IsMoving;
                if (!context.Controller.IsDead)
                {
                    EnsureBodyMovement();
                    returning |= pattern.bodyMovement.IsPatternMoving;
                }
                foreach (Hand hand in hands)
                {
                    if (!hand.ReturnStarted)
                    {
                        EnsureHand(hand);
                        if (hand.Binding.animation.Completed) StartHandReturn(hand);
                        else returning = true;
                    }
                    returning |= hand.Part.IsMoving;
                }
                if (returning) yield return null;
            } while (returning);
            ring.FollowIdle();
            foreach (Hand hand in hands) hand.Part.FollowIdle();
            pattern.bodyMovement.ResumePatrol();
        }

        private void StartRingReturn()
        {
            if (ringReturnStarted) return;
            ringReturnStarted = true;
            RestoreRingSorting();
            ring.ReturnToIdle(Vector2.Distance(ring.transform.position, ring.IdlePosition) / pattern.returnSpeed,
                pattern.returnCurve);
            StartBodyReturn();
        }

        private void StartHandReturn(Hand hand)
        {
            hand.ReturnStarted = true;
            hand.Part.ReturnToIdle(Vector2.Distance(hand.Part.transform.position, hand.Part.IdlePosition) / pattern.returnSpeed,
                pattern.returnCurve);
        }

        private void EnsureRing()
        {
            if (pattern.ringAnimation == null || !pattern.ringAnimation.isActiveAndEnabled || pattern.ringAnimation.Interrupted)
                throw new InvalidOperationException("The ring burst animation was interrupted.");
        }

        private static void EnsureHand(Hand hand)
        {
            if (hand.Binding.animation == null || !hand.Binding.animation.isActiveAndEnabled || hand.Binding.animation.Interrupted)
                throw new InvalidOperationException("A ring burst cannon hand animation was interrupted.");
        }

        public override void StopForDeath(Boss2PatternContext context)
        {
            StopCharge(true);
            DestroyField();
            pattern.bodyMovement.ResumePatrol();
        }

        public override IEnumerator ReturnAfterDeath(Boss2PatternContext context) => ReturnParts(context, true);

        private void RestoreRingSorting()
        {
            if (sortingOverridden && ringRenderer != null)
            {
                ringRenderer.sortingLayerID = originalSortingLayer;
                ringRenderer.sortingOrder = originalSortingOrder;
                sortingOverridden = false;
            }
        }

        public override void Cleanup(Boss2PatternResult result)
        {
            pattern.bodyMovement.ResumePatrol();
            RestoreRingSorting();
            pattern.ringAnimation.Cancel();
            DestroyField();
            foreach (Hand hand in hands)
            {
                if (hand.Binding.animation != null && hand.Binding.animation.Active) hand.Binding.animation.Cancel();
                if (hand.Beam == null) continue;
                hand.Beam.Cancel();
                Object.Destroy(hand.Beam.gameObject);
                hand.Beam = null;
            }
        }

        private void DestroyField()
        {
            if (field == null) return;
            field.gameObject.SetActive(false);
            Object.Destroy(field.gameObject);
            field = null;
        }

        private void ReleaseField()
        {
            if (released || field == null) return;
            released = true;
            field.Burst();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Launches debris, slams with the remaining hand, then lowers the exhausted body.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Boss1OverdriveField))]
public sealed class Boss1OverdrivePattern : Boss1PatternBase
{
    [Header("References")]
    [SerializeField] private Boss1OverdriveField overdriveField;
    [SerializeField] private Boss1OverdriveConfig config;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Charge Effect")]
    [SerializeField] private Boss1OverdriveChargeEffect chargeEffectPrefab;

    [Header("Impact Effects")]
    [Tooltip("주먹이 잔해에 처음 닿는 순간 재생합니다.")]
    [SerializeField] private CombatAnimationEffect punchImpactPrefab;
    [Tooltip("주먹 또는 밀린 잔해가 벽에 닿는 순간 재생합니다.")]
    [SerializeField] private CombatAnimationEffect wallImpactPrefab;
    [Tooltip("충전 중 잔해를 내려찍는 접촉부에서 90도 회전해 재생합니다.")]
    [SerializeField] private CombatAnimationEffect debrisSlamImpactPrefab;
    [Tooltip("후속 내려찍기가 바닥에 닿는 순간 재생합니다.")]
    [SerializeField] private CombatAnimationEffect floorSlamImpactPrefab;

    [Header("Animation")]
    [SerializeField] private Boss1BodyAnimationController bodyAnimation;
    [Tooltip("Punch와 Slam에서 공통으로 사용하는 Overdrive 애니메이션입니다.")]
    [UnityEngine.Serialization.FormerlySerializedAs("slamAnimationProfile")]
    [SerializeField] private Boss1HandAnimationProfile animationProfile = new Boss1HandAnimationProfile();

    public bool CanStart => config != null && overdriveField != null && overdriveField.CanStart
        && HasActivePlayer;

    private bool HasActivePlayer
    {
        get
        {
            if (playerHealth == null || !playerHealth.isActiveAndEnabled || playerHealth.IsDead) return false;
            PlayerMovement movement = playerHealth.GetComponent<PlayerMovement>();
            Collider2D collider = playerHealth.GetComponent<Collider2D>();
            return movement != null && movement.isActiveAndEnabled && collider != null && collider.enabled;
        }
    }

    protected override void Reset()
    {
        base.Reset();
        ResolveReferences();
    }
    private void Awake() => ResolveReferences();

    private void Update()
    {
        if (!IsExecuting) return;
        if (overdriveField == null || !overdriveField.HasValidBindings || !HasActivePlayer)
            Owner.CancelCurrentPattern();
    }

    public override Boss1PatternExecution CreateExecution(Boss1PatternContext context)
    {
        ResolveReferences();
        if (config == null || overdriveField == null || playerHealth == null
            || !playerHealth.isActiveAndEnabled || playerHealth.IsDead
            || !context.Controller.HasBothHands
            || !overdriveField.TrySelectStart(out Boss1HandSide side, out Transform startPoint,
                out Transform pullbackPoint, out Transform punchExitPoint, out Collider2D targetWall))
            throw new System.InvalidOperationException(
                "Overdrive needs a config, target, both hands, start, pullback and exit points, walls, body collider and floor.");

        Boss1HandController hand = side == Boss1HandSide.Left ? context.LeftHand : context.RightHand;
        Collider2D handCollider = hand.GetComponent<Collider2D>();
        SpriteRenderer handRenderer = hand.GetComponentInChildren<SpriteRenderer>();
        if (handCollider == null || !handCollider.enabled)
            throw new System.InvalidOperationException("Overdrive needs an enabled hand collider.");
        if (handRenderer == null)
            throw new System.InvalidOperationException("Overdrive charge needs a hand sprite renderer.");
        PlayerMovement playerMovement = playerHealth.GetComponent<PlayerMovement>();
        Collider2D playerCollider = playerHealth.GetComponent<Collider2D>();
        if (playerMovement == null || !playerMovement.isActiveAndEnabled || playerCollider == null)
            throw new System.InvalidOperationException("Overdrive needs player movement and a collider.");
        Boss1HandController slamHand = side == Boss1HandSide.Left ? context.RightHand : context.LeftHand;
        Boss1HandDamageHitbox slamHitbox = slamHand.GetComponent<Boss1HandDamageHitbox>();
        Boss1DebrisField debrisField = GetComponent<Boss1DebrisField>();
        Boss1SlamWarningArea warningArea = GetComponentInChildren<Boss1SlamWarningArea>(true);
        SpriteRenderer warningStyle = warningArea != null ? warningArea.GetComponent<SpriteRenderer>() : null;
        if (slamHitbox == null || !slamHitbox.isActiveAndEnabled || slamHitbox.AttackCollider == null
            || !slamHitbox.AttackCollider.enabled || debrisField.FloorCollider == null
            || !debrisField.FloorCollider.enabled || warningStyle == null)
            throw new System.InvalidOperationException("Overdrive needs a slam hand hitbox, debris floor and slam warning renderer.");
        return new Execution(hand, handCollider, handRenderer, startPoint, pullbackPoint, punchExitPoint, targetWall,
            debrisField, playerHealth, playerMovement, playerCollider, slamHand, slamHitbox,
            overdriveField.SlamExitPoint,
            overdriveField.LeftWall, overdriveField.RightWall, overdriveField.BodyCollider,
            overdriveField.BodyRoot, warningStyle, config,
            bodyAnimation,
            animationProfile?.GetTrack(hand.HandSide),
            animationProfile?.GetTrack(slamHand.HandSide),
            punchImpactPrefab, wallImpactPrefab, debrisSlamImpactPrefab, floorSlamImpactPrefab,
            chargeEffectPrefab);
    }

    private void ResolveReferences()
    {
        if (overdriveField == null) overdriveField = GetComponent<Boss1OverdriveField>();
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    private sealed class Execution : Boss1PatternExecution
    {
        private readonly Boss1HandController hand;
        private readonly Collider2D handCollider;
        private readonly SpriteRenderer handRenderer;
        private readonly Transform startPoint;
        private readonly Transform pullbackPoint;
        private readonly Transform punchExitPoint;
        private readonly Transform slamExitPoint;
        private readonly Collider2D targetWall;
        private readonly Boss1DebrisField debrisField;
        private readonly float placementDuration;
        private readonly float attackWaitDuration;
        private readonly float pullbackDuration;
        private readonly float debrisResponseWaitDuration;
        private readonly float debrisStandDetectionDuration;
        private readonly float strikeSpeed;
        private readonly PlayerHealth playerHealth;
        private readonly PlayerMovement playerMovement;
        private readonly Collider2D playerCollider;
        private readonly int wallDamage;
        private readonly float wallStunDuration;
        private readonly Boss1HandController slamHand;
        private readonly Boss1HandDamageHitbox slamHitbox;
        private readonly Collider2D floorCollider;
        private readonly Collider2D leftWall;
        private readonly Collider2D rightWall;
        private readonly float slamPreparationHeight;
        private readonly float slamTrackingDuration;
        private readonly float slamTrackingSpeed;
        private readonly float slamPullbackHeight;
        private readonly float slamPullbackDuration;
        private readonly float slamSpeed;
        private readonly int slamDamage;
        private readonly Collider2D bodyCollider;
        private readonly Transform body;
        private readonly Vector3 originalBodyLocalPosition;
        private readonly float groggyDescentDuration;
        private readonly float groggyGroundClearance;
        private readonly float groggyDuration;
        private readonly float recoveryDuration;
        private readonly Boss1BodyAnimationController bodyAnimation;
        private readonly Boss1HandAnimationTrack punchAnimationTrack;
        private readonly Boss1HandAnimationTrack slamAnimationTrack;
        private readonly List<RaycastHit2D> playerContactHits = new List<RaycastHit2D>();
        private readonly List<RaycastHit2D> debrisContactHits = new List<RaycastHit2D>();
        private readonly CombatAnimationEffect punchImpactPrefab;
        private readonly CombatAnimationEffect wallImpactPrefab;
        private readonly CombatAnimationEffect debrisSlamImpactPrefab;
        private readonly CombatAnimationEffect floorSlamImpactPrefab;
        private readonly List<CombatAnimationEffect> impactEffects = new List<CombatAnimationEffect>();
        private CombatAnimationEffect punchImpactEffect;
        private readonly Boss1OverdriveChargeEffect chargeEffectPrefab;
        private Boss1FallingDebris struckDebris;
        private bool debrisLaunched;
        private bool debrisReachedWall;
        private bool punchReachedWall;
        private float debrisHandPositionX;
        private Boss1Controller controller;
        private Boss1PatternScheduler scheduler;
        private int? wallStunVersion;
        private bool playerContactResolved;
        private bool carryingPlayer;
        private bool carryingWithDebris;
        private bool carryingBehindDebris;
        private bool ignoringDebrisTopContact;
        private bool slamHitResolved;
        private Boss1OverdriveChargeEffect chargeEffect;
        private Boss1OverdriveFeedback feedback;
        private readonly SpriteRenderer warningStyle;
        private readonly Boss1OverdriveConfig feedbackConfig;

        public Execution(Boss1HandController hand, Collider2D handCollider, SpriteRenderer handRenderer, Transform startPoint,
            Transform pullbackPoint, Transform punchExitPoint, Collider2D targetWall, Boss1DebrisField debrisField,
            PlayerHealth playerHealth, PlayerMovement playerMovement, Collider2D playerCollider,
            Boss1HandController slamHand, Boss1HandDamageHitbox slamHitbox, Transform slamExitPoint,
            Collider2D leftWall, Collider2D rightWall, Collider2D bodyCollider, Transform bodyRoot, SpriteRenderer warningStyle,
            Boss1OverdriveConfig config,
            Boss1BodyAnimationController bodyAnimation,
            Boss1HandAnimationTrack punchAnimationTrack, Boss1HandAnimationTrack slamAnimationTrack,
            CombatAnimationEffect punchImpactPrefab, CombatAnimationEffect wallImpactPrefab,
            CombatAnimationEffect debrisSlamImpactPrefab, CombatAnimationEffect floorSlamImpactPrefab,
            Boss1OverdriveChargeEffect chargeEffectPrefab)
        {
            this.hand = hand;
            this.handCollider = handCollider;
            this.handRenderer = handRenderer;
            this.startPoint = startPoint;
            this.pullbackPoint = pullbackPoint;
            this.punchExitPoint = punchExitPoint;
            this.slamExitPoint = slamExitPoint;
            this.targetWall = targetWall;
            this.debrisField = debrisField;
            this.playerHealth = playerHealth;
            this.playerMovement = playerMovement;
            this.playerCollider = playerCollider;
            this.slamHand = slamHand;
            this.slamHitbox = slamHitbox;
            floorCollider = debrisField.FloorCollider;
            this.leftWall = leftWall;
            this.rightWall = rightWall;
            this.warningStyle = warningStyle;
            feedbackConfig = config;
            placementDuration = config.PlacementDuration;
            attackWaitDuration = config.AttackWaitDuration;
            pullbackDuration = config.PullbackDuration;
            debrisResponseWaitDuration = config.DebrisResponseWaitDuration;
            debrisStandDetectionDuration = config.DebrisStandDetectionDuration;
            strikeSpeed = Mathf.Max(0.01f, config.StrikeSpeed);
            wallDamage = config.WallDamage;
            wallStunDuration = config.WallStunDuration;
            slamPreparationHeight = config.SlamPreparationHeight;
            slamTrackingDuration = config.SlamTrackingDuration;
            slamTrackingSpeed = config.SlamTrackingSpeed;
            slamPullbackHeight = config.SlamPullbackHeight;
            slamPullbackDuration = config.SlamPullbackDuration;
            slamSpeed = Mathf.Max(0.01f, config.SlamSpeed);
            slamDamage = config.SlamDamage;
            this.bodyCollider = bodyCollider;
            body = bodyRoot;
            originalBodyLocalPosition = body.localPosition;
            groggyDescentDuration = config.GroggyDescentDuration;
            groggyGroundClearance = config.GroggyGroundClearance;
            groggyDuration = config.GroggyDuration;
            recoveryDuration = config.RecoveryDuration;
            this.bodyAnimation = bodyAnimation;
            this.punchAnimationTrack = punchAnimationTrack;
            this.slamAnimationTrack = slamAnimationTrack;
            this.punchImpactPrefab = punchImpactPrefab;
            this.wallImpactPrefab = wallImpactPrefab;
            this.debrisSlamImpactPrefab = debrisSlamImpactPrefab;
            this.floorSlamImpactPrefab = floorSlamImpactPrefab;
            this.chargeEffectPrefab = chargeEffectPrefab;
        }

        public override IEnumerator Execute(Boss1PatternContext context)
        {
            controller = context.Controller;
            scheduler = controller.GetComponent<Boss1PatternScheduler>();
            feedback = Boss1OverdriveFeedback.Create(controller.transform, warningStyle, feedbackConfig);
            debrisField.DebrisBroken += HandleDebrisBroken;
            playerHealth.Died += HandlePlayerDied;
            slamHitbox.EndAttack();
            yield return PreparePunch();
            yield return ExecuteCharge();
            yield return ExecutePunch();
            yield return ExecuteSlam();
            yield return ExecuteGroggyAndRecovery();
        }

        private IEnumerator ExecutePunch()
        {
            hand.MoveTo(pullbackPoint.position, pullbackDuration);
            yield return hand.WaitForMotion();

            Vector2 targetPosition = hand.transform.position;
            targetPosition.x = punchExitPoint.position.x;
            float duration = Vector2.Distance(hand.transform.position, targetPosition) / strikeSpeed;
            hand.BeforeMove += CheckPunchContact;
            hand.PlayAttackAnimation();
            chargeEffect?.Release();
            controller.GetComponent<Boss1Audio>()?.PlayOverdriveRelease();
            hand.MoveTo(targetPosition, duration);
            while (hand.IsBusy || (debrisLaunched && !debrisReachedWall))
            {
                if (!playerHealth.isActiveAndEnabled || playerHealth.IsDead || !playerMovement.isActiveAndEnabled)
                {
                    controller.CancelCurrentPattern();
                    yield break;
                }
                yield return null;
            }
            hand.BeforeMove -= CheckPunchContact;
            chargeEffect?.CompleteAttack();
        }

        private void AdvancePunchedDebris(Vector2 nextHandPosition)
        {
            if (!debrisLaunched || debrisReachedWall) return;
            if (struckDebris == null || !struckDebris.isActiveAndEnabled)
            {
                controller.CancelCurrentPattern();
                return;
            }
            float direction = Mathf.Sign(punchExitPoint.position.x - startPoint.position.x);
            float distance = Mathf.Max(0f, direction * (nextHandPosition.x - debrisHandPositionX));
            debrisHandPositionX = nextHandPosition.x;
            Physics2D.SyncTransforms();
            Bounds debrisBounds = struckDebris.Body.bounds;
            ColliderDistance2D initialContact = playerCollider.enabled
                ? struckDebris.Body.Distance(playerCollider) : default;
            bool arrived = debrisField.AdvanceLaunchedDebris(struckDebris, direction,
                distance, targetWall, out Vector2 movement);
            // The hand keeps pushing; keep the short impact at their moving contact point.
            if (punchImpactEffect != null) punchImpactEffect.transform.position += (Vector3)movement;
            if (arrived)
            {
                feedbackConfig.WallImpactShake.Play(controller);
                // Arrival removes the debris; use its bounds captured before advancing it.
                PlayWallImpactEffect(debrisBounds.center.y);
            }
            CheckPlayerContact(debrisBounds, initialContact.isValid && initialContact.isOverlapped,
                movement, direction);
            if (arrived && carryingPlayer && carryingWithDebris) CompletePlayerCarry();
            if (arrived)
            {
                debrisReachedWall = true;
                scheduler?.ReserveDebrisRefill();
            }
        }

        private IEnumerator PreparePunch()
        {
            slamHand.PlayPrepareAnimation(slamAnimationTrack);
            MoveResponseHandToStandby();
            bool hasAnimation = hand.PlayPrepareAnimation(punchAnimationTrack);
            hand.MoveTo(startPoint.position, placementDuration);
            float angle = hand.HandSide == Boss1HandSide.Left ? 90f : -90f;
            float elapsed = 0f;
            do
            {
                elapsed += Time.deltaTime;
                float progress = hasAnimation ? hand.AnimationController.NormalizedProgress
                    : (placementDuration > 0f ? Mathf.Clamp01(elapsed / placementDuration) : 1f);
                hand.SetRotationOffset(angle * Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }
            while (hand.IsBusy || (hand.AnimationController != null && hand.AnimationController.IsPlaying)
                || slamHand.IsBusy || (slamHand.AnimationController != null && slamHand.AnimationController.IsPlaying));
            hand.SetRotationOffset(angle);
        }

        private IEnumerator ExecuteCharge()
        {
            if (attackWaitDuration <= 0f) yield break;
            controller.GetComponent<Boss1Audio>()?.StartOverdriveCharge();
            Vector2 direction = Vector2.right * Mathf.Sign(punchExitPoint.position.x - startPoint.position.x);
            if (chargeEffectPrefab != null)
                chargeEffect = Boss1OverdriveChargeEffect.Create(chargeEffectPrefab,
                    handRenderer, handCollider, feedbackConfig, direction);
            float buildupDuration = Mathf.Max(0f, attackWaitDuration - feedbackConfig.ChargeLockDuration);
            float elapsed = 0f;
            float standingDuration = 0f;
            Boss1FallingDebris standingDebris = null;
            IEnumerator response = null;
            try
            {
                while (elapsed < attackWaitDuration || response != null)
                {
                    elapsed = Mathf.Min(attackWaitDuration, elapsed + Time.deltaTime);
                    chargeEffect?.SetProgress(buildupDuration > 0f ? elapsed / buildupDuration : 1f);
                    if (elapsed >= buildupDuration) chargeEffect?.Lock();

                    if (response != null)
                    {
                        if (!response.MoveNext())
                        {
                            (response as System.IDisposable)?.Dispose();
                            response = null;
                            if (elapsed < attackWaitDuration) MoveResponseHandToStandby();
                        }
                    }
                    else if (elapsed < attackWaitDuration && elapsed >= debrisResponseWaitDuration
                        && !slamHand.IsBusy)
                    {
                        debrisField.TryFindSupportingDebris(playerCollider, out Boss1FallingDebris currentDebris);
                        if (currentDebris != standingDebris) standingDuration = 0f;
                        standingDebris = currentDebris;
                        standingDuration = currentDebris != null ? standingDuration + Time.deltaTime : 0f;
                        if (currentDebris != null && standingDuration >= debrisStandDetectionDuration)
                        {
                            response = ExecuteDebrisResponse(currentDebris);
                            standingDuration = 0f;
                            standingDebris = null;
                        }
                    }
                    yield return null;
                }
            }
            finally { (response as System.IDisposable)?.Dispose(); }
            // Finish any standby repositioning before the Punch without replaying Prepare.
            while (slamHand.IsBusy)
                yield return null;
        }

        private void MoveResponseHandToStandby()
        {
            Vector2 target = GetSlamTarget(slamHand.transform.position.x, floorCollider.bounds.max.y);
            target.y += slamPreparationHeight;
            slamHand.MoveTo(target, Vector2.Distance(slamHand.transform.position, target) / slamTrackingSpeed);
        }

        private IEnumerator ExecuteDebrisResponse(Boss1FallingDebris targetDebris)
        {
            if (targetDebris == null || !targetDebris.isActiveAndEnabled) yield break;
            // Lock the debris, not the player. Neither placement nor pullback/descent tracks a target.
            Bounds debrisBounds = targetDebris.Body.bounds;
            Vector2 lockedTarget = GetSlamTarget(debrisBounds.center.x, debrisBounds.max.y);
            Vector2 preparationTarget = lockedTarget + Vector2.up * slamPreparationHeight;
            slamHand.MoveTo(preparationTarget,
                Vector2.Distance(slamHand.transform.position, preparationTarget) / slamTrackingSpeed);
            while (slamHand.IsBusy) yield return null;
            if (targetDebris == null || !targetDebris.isActiveAndEnabled) yield break;

            feedback.FlashTarget(lockedTarget, slamHitbox.AttackCollider, slamHand.transform, debrisBounds.max.y);
            controller.GetComponent<Boss1Audio>()?.PlayWarning();
            while (feedback.IsTargetFlashing) yield return null;
            controller.GetComponent<Boss1Audio>()?.StopWarning();
            slamHand.MoveTo((Vector2)slamHand.transform.position + Vector2.up * slamPullbackHeight,
                slamPullbackDuration);
            while (slamHand.IsBusy) yield return null;

            slamHitResolved = false;
            slamHitbox.BeginAttack(playerHealth, ApplySlamDamage);
            if (playerHealth.IsDead) yield break;
            PlaySlamAttackAnimation();
            System.Action<Vector2> breakDebrisOnContact = nextPosition =>
            {
                if (targetDebris == null || !targetDebris.isActiveAndEnabled) return;
                Collider2D attack = slamHitbox.AttackCollider;
                Physics2D.SyncTransforms();
                ColliderDistance2D contact = attack.Distance(targetDebris.Body);
                bool hitTarget = contact.isValid && contact.isOverlapped;
                Vector2 movement = nextPosition - (Vector2)slamHand.transform.position;
                if (!hitTarget && movement.sqrMagnitude > Mathf.Epsilon)
                {
                    ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
                    filter.SetLayerMask(1 << targetDebris.gameObject.layer);
                    // Include the clearance that keeps the hand just above the target surface.
                    attack.Cast(movement.normalized, filter, debrisContactHits,
                        movement.magnitude + Physics2D.defaultContactOffset * 2f);
                    foreach (RaycastHit2D hit in debrisContactHits)
                        if (hit.collider == targetDebris.Body) { hitTarget = true; break; }
                }
                if (hitTarget)
                {
                    Bounds bounds = targetDebris.Body.bounds;
                    PlayImpactEffect(debrisSlamImpactPrefab,
                        new Vector2(Mathf.Clamp(attack.bounds.center.x, bounds.min.x, bounds.max.x), bounds.max.y),
                        90f);
                    debrisField.BreakDebris(targetDebris);
                }
            };
            slamHand.BeforeMove += breakDebrisOnContact;
            try
            {
                float duration = Vector2.Distance(slamHand.transform.position, lockedTarget) / slamSpeed;
                slamHand.MoveTo(lockedTarget, duration);
                while (slamHand.IsBusy) yield return null;
            }
            finally { slamHand.BeforeMove -= breakDebrisOnContact; }
            slamHitbox.EndAttack();
            feedbackConfig.SlamImpactShake.Play(controller);
            controller.GetComponent<Boss1Audio>()?.PlaySlam();
        }

        private IEnumerator ExecuteGroggyAndRecovery()
        {
            Physics2D.SyncTransforms();
            Vector3 groggyWorldPosition = body.position;
            float bottomOffset = groggyWorldPosition.y - bodyCollider.bounds.min.y;
            groggyWorldPosition.y = Mathf.Min(groggyWorldPosition.y,
                floorCollider.bounds.max.y + groggyGroundClearance + bottomOffset);
            Vector3 groggyLocalPosition = body.parent.InverseTransformPoint(groggyWorldPosition);

            yield return MoveBody(groggyLocalPosition, groggyDescentDuration);
            bodyAnimation?.SetGroggy(true);
            yield return WaitForDuration(groggyDuration);
            bodyAnimation?.RequestGroggyRecovery();
            bodyAnimation?.SetGroggy(false);

            hand.ReturnToIdle(recoveryDuration);
            slamHand.ReturnToIdle(recoveryDuration);
            yield return MoveBody(originalBodyLocalPosition, recoveryDuration);
            yield return controller.WaitForHands();
        }

        private IEnumerator MoveBody(Vector3 targetLocalPosition, float duration)
        {
            Vector3 startLocalPosition = body.localPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                body.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            body.localPosition = targetLocalPosition;
        }

        private IEnumerator ExecuteSlam()
        {
            Vector2 groundTarget = GetSlamTarget();
            float preparationY = groundTarget.y + slamPreparationHeight;

            float elapsed = 0f;
            do
            {
                if (!playerHealth.isActiveAndEnabled || playerHealth.IsDead || !playerMovement.isActiveAndEnabled)
                {
                    controller.CancelCurrentPattern();
                    yield break;
                }
                groundTarget = GetSlamTarget();
                Vector2 preparationTarget = new Vector2(groundTarget.x, preparationY);
                Vector2 nextPosition = Vector2.MoveTowards(slamHand.transform.position, preparationTarget,
                    slamTrackingSpeed * Time.deltaTime);
                elapsed += Time.deltaTime;
                slamHand.MoveTo(nextPosition, 0f);
                yield return null;
            }
            while (elapsed < slamTrackingDuration ||
                !Mathf.Approximately(slamHand.transform.position.y, preparationY));

            Vector2 lockedTarget = GetSlamTarget();
            lockedTarget.x = slamHand.transform.position.x;

            feedback.FlashTarget(lockedTarget, slamHitbox.AttackCollider, slamHand.transform,
                floorCollider.bounds.max.y);
            controller.GetComponent<Boss1Audio>()?.PlayWarning();
            while (feedback.IsTargetFlashing) yield return null;
            controller.GetComponent<Boss1Audio>()?.StopWarning();

            slamHand.MoveTo((Vector2)slamHand.transform.position + Vector2.up * slamPullbackHeight,
                slamPullbackDuration);
            yield return slamHand.WaitForMotion();

            if (!playerHealth.isActiveAndEnabled || playerHealth.IsDead || !playerMovement.isActiveAndEnabled)
            {
                controller.CancelCurrentPattern();
                yield break;
            }
            slamHitResolved = false;
            slamHitbox.BeginAttack(playerHealth, ApplySlamDamage);
            if (playerHealth.IsDead) yield break;
            PlaySlamAttackAnimation();
            Vector2 exitTarget = new Vector2(lockedTarget.x, slamExitPoint.position.y);
            bool floorReached = false;
            System.Action<Vector2> checkFloorImpact = nextPosition =>
            {
                if (floorReached || nextPosition.y > lockedTarget.y) return;
                floorReached = true;
                // Floor contact ends the hitbox, not the hand's continuous movement to the exit.
                slamHitbox.EndAttack();
                feedbackConfig.SlamImpactShake.Play(controller);
                controller.GetComponent<Boss1Audio>()?.PlaySlam();
                PlayImpactEffect(floorSlamImpactPrefab,
                    new Vector2(slamHitbox.AttackCollider.bounds.center.x, floorCollider.bounds.max.y));
            };
            slamHand.BeforeMove += checkFloorImpact;
            try
            {
                float duration = Vector2.Distance(slamHand.transform.position, exitTarget) / slamSpeed;
                slamHand.MoveTo(exitTarget, duration);
                yield return slamHand.WaitForMotion();
            }
            finally { slamHand.BeforeMove -= checkFloorImpact; }
            slamHitbox.EndAttack();
        }

        private void PlaySlamAttackAnimation()
        {
            // Keep the attack pose between slams; Return is requested only during final recovery.
            if (slamHand.AnimationController == null ||
                slamHand.AnimationController.CurrentPhase != Boss1HandAnimationPhase.Attack)
                slamHand.PlayAttackAnimation();
        }

        private Vector2 GetSlamTarget()
        {
            return GetSlamTarget(playerCollider.bounds.center.x, floorCollider.bounds.max.y);
        }

        private Vector2 GetSlamTarget(float targetX, float surfaceY)
        {
            Physics2D.SyncTransforms();
            Bounds bounds = slamHitbox.AttackCollider.bounds;
            Vector2 handPosition = slamHand.transform.position;
            float centerOffsetX = bounds.center.x - handPosition.x;
            float bottomOffset = handPosition.y - bounds.min.y;
            float clearance = Physics2D.defaultContactOffset;
            float minimumX = leftWall.bounds.max.x + bounds.extents.x - centerOffsetX + clearance;
            float maximumX = rightWall.bounds.min.x - bounds.extents.x - centerOffsetX - clearance;
            float x = Mathf.Clamp(targetX - centerOffsetX, minimumX, maximumX);
            return new Vector2(x, surfaceY + bottomOffset + clearance);
        }

        private void ApplySlamDamage(PlayerHealth target)
        {
            if (slamHitResolved) return;
            slamHitResolved = true;
            target.TryTakeDamage(new DamageInfo(slamDamage, slamHand.transform.position));
        }

        private void CheckPlayerContact(Bounds debrisBounds, bool initialOverlap, Vector2 movement, float direction)
        {
            if (!playerContactResolved)
            {
                if (!playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy) return;
                Bounds playerBounds = playerCollider.bounds;
                float topTolerance = Mathf.Max(Physics2D.defaultContactOffset * 3f, 0.05f);
                bool aboveDebris = playerBounds.min.y >= debrisBounds.max.y - topTolerance;
                bool horizontalOverlap = playerBounds.max.x > debrisBounds.min.x
                    && playerBounds.min.x < debrisBounds.max.x;
                Bounds handBounds = handCollider.bounds;
                bool overlapsHand = playerBounds.max.x > handBounds.min.x
                    && playerBounds.min.x < handBounds.max.x;
                // Let a rider fall clear of both the departing debris and the hand behind it.
                ignoringDebrisTopContact = (horizontalOverlap && aboveDebris)
                    || (ignoringDebrisTopContact && (horizontalOverlap || overlapsHand));
                if (aboveDebris || ignoringDebrisTopContact) return;
                bool contact = initialOverlap;
                if (!contact && movement.sqrMagnitude > Mathf.Epsilon)
                {
                    ContactFilter2D filter = new ContactFilter2D();
                    filter.SetLayerMask(1 << playerCollider.gameObject.layer);
                    filter.useTriggers = false;
                    Physics2D.BoxCast(debrisBounds.center, debrisBounds.size, 0f, movement.normalized,
                        filter, playerContactHits, movement.magnitude);
                    foreach (RaycastHit2D hit in playerContactHits)
                    {
                        if (hit.collider != playerCollider) continue;
                        contact = true;
                        break;
                    }
                }
                if (!contact) return;
                BeginPlayerCarry();
                carryingWithDebris = carryingPlayer;
            }
            if (!carryingWithDebris) return;
            if (carryingBehindDebris && carryingPlayer)
            {
                // A player caught by the hand stays on the same rear side of the launched debris.
                playerMovement.MoveForcedCarry((Vector2)playerMovement.transform.position + movement);
            }
            else MoveCarriedPlayer(debrisBounds, movement, direction);
        }

        private void BeginPlayerCarry()
        {
            // Punch and debris share one contact, including a successfully defended hit.
            playerContactResolved = true;
            if (!playerHealth.TryReceiveHit() || !playerMovement.TryBeginForcedCarry()) return;
            carryingPlayer = true;
        }

        private void MoveCarriedPlayer(Bounds pushingBounds, Vector2 movement, float direction,
            Boss1FallingDebris blockingDebris = null)
        {
            if (!carryingPlayer) return;

            Physics2D.SyncTransforms();
            Vector2 playerPosition = playerMovement.transform.position;
            Bounds playerBounds = playerCollider.bounds;
            float clearance = Physics2D.defaultContactOffset;
            float pushingFront = (direction > 0f ? pushingBounds.max.x : pushingBounds.min.x) + movement.x;
            float playerNearEdge = direction > 0f ? playerBounds.min.x : playerBounds.max.x;
            // The Punch hands its carried player over to the launched debris without releasing control.
            float remainingMovement = Mathf.Max(0f, direction * (pushingFront - playerNearEdge) + clearance);
            if (struckDebris == null && blockingDebris == null && remainingMovement > 0f
                && debrisField.TryFindFirstLandedDebris(playerCollider,
                    Vector2.right * (direction * (remainingMovement + clearance)),
                    out Boss1FallingDebris nextDebris, out float contactDistance))
            {
                blockingDebris = nextDebris;
                remainingMovement = Mathf.Min(remainingMovement, Mathf.Max(0f, contactDistance - clearance));
            }
            if (blockingDebris != null)
            {
                Bounds debrisBounds = blockingDebris.Body.bounds;
                float distanceToDebris = direction > 0f
                    ? debrisBounds.min.x - playerBounds.max.x - clearance
                    : playerBounds.min.x - debrisBounds.max.x - clearance;
                remainingMovement = Mathf.Min(remainingMovement, Mathf.Max(0f, distanceToDebris));
            }
            float distanceToWall = direction > 0f ? targetWall.bounds.min.x - playerBounds.max.x - clearance
                : playerBounds.min.x - targetWall.bounds.max.x - clearance;
            float carryDistance = Mathf.Min(remainingMovement, Mathf.Max(0f, distanceToWall));
            playerMovement.MoveForcedCarry(playerPosition + Vector2.right * (direction * carryDistance));
            if (distanceToWall > remainingMovement) return;

            CompletePlayerCarry();
        }

        private void CompletePlayerCarry()
        {
            if (!carryingPlayer) return;
            carryingPlayer = false;
            playerMovement.EndForcedCarry();
            if (playerHealth.TryTakeDamage(new DamageInfo(wallDamage, targetWall.transform.position,
                    Vector2.zero, wallStunDuration)) && !playerHealth.IsDead)
            {
                wallStunVersion = playerMovement.KnockbackVersion;
                playerMovement.BeginGroggy(wallStunVersion.Value);
                feedback.ShowStun(playerCollider, playerMovement, wallStunVersion.Value);
            }
        }

        private void HandleDebrisBroken(Boss1FallingDebris debris) =>
            feedback.PlayDebrisBreak(debris.GetComponent<SpriteRenderer>());

        private void HandlePlayerDied() => controller.CancelCurrentPattern();

        private void CheckPunchContact(Vector2 nextPosition)
        {
            if (punchReachedWall)
            {
                AdvancePunchedDebris(nextPosition);
                return;
            }
            Physics2D.SyncTransforms();
            Vector2 currentPosition = hand.transform.position;
            Vector2 movement = nextPosition - currentPosition;
            float direction = Mathf.Sign(punchExitPoint.position.x - startPoint.position.x);
            float distanceToWall = direction > 0f ? targetWall.bounds.min.x - handCollider.bounds.max.x
                : handCollider.bounds.min.x - targetWall.bounds.max.x;
            bool reachesWall = direction * movement.x >= Mathf.Max(0f, distanceToWall);
            Boss1FallingDebris debris = null;
            float hitDistance = 0f;
            bool struck = !debrisLaunched && debrisField.TryFindFirstLandedDebris(handCollider, movement,
                out debris, out hitDistance);
            // Hand-to-debris carry is resolved at first contact, but the hand's motion continues.
            if (struck) movement = movement.normalized * hitDistance;

            if (!playerContactResolved && !ignoringDebrisTopContact
                && playerCollider.enabled && playerCollider.gameObject.activeInHierarchy)
            {
                ColliderDistance2D overlap = handCollider.Distance(playerCollider);
                bool contact = overlap.isValid && overlap.isOverlapped;
                if (!contact && movement.sqrMagnitude > Mathf.Epsilon)
                {
                    ContactFilter2D filter = new ContactFilter2D();
                    filter.SetLayerMask(1 << playerCollider.gameObject.layer);
                    filter.useTriggers = false;
                    handCollider.Cast(movement.normalized, filter, playerContactHits, movement.magnitude);
                    foreach (RaycastHit2D hit in playerContactHits)
                    {
                        if (hit.collider != playerCollider) continue;
                        contact = true;
                        break;
                    }
                }
                if (contact) BeginPlayerCarry();
            }
            if (!carryingWithDebris)
                MoveCarriedPlayer(handCollider.bounds, movement,
                    direction, struck ? debris : null);
            // A wall hit can synchronously cancel this execution through PlayerHealth.Died.
            if (!controller.IsExecutingPattern || playerHealth.IsDead) return;
            if (struck)
            {
                struckDebris = debris;
                if (!debrisField.TryLaunchDebris(struckDebris))
                {
                    controller.CancelCurrentPattern();
                    return;
                }
                debrisLaunched = true;
                MarkPresented();
                // Only the remaining movement after contact pushes the debris in this first frame.
                debrisHandPositionX = currentPosition.x + movement.x;
                carryingWithDebris = carryingPlayer;
                carryingBehindDebris = carryingPlayer;
                feedbackConfig.PunchImpactShake.Play(controller);
                controller.GetComponent<Boss1Audio>()?.PlayPunchImpact();
                Bounds bounds = struckDebris.Body.bounds;
                punchImpactEffect = PlayImpactEffect(punchImpactPrefab,
                    new Vector2(direction > 0f ? bounds.min.x : bounds.max.x,
                        Mathf.Clamp(handCollider.bounds.center.y, bounds.min.y, bounds.max.y)));
            }
            // Run in BeforeMove so the hand, debris and carried player advance in the same frame.
            AdvancePunchedDebris(nextPosition);
            if (!controller.IsExecutingPattern || playerHealth.IsDead || !reachesWall) return;
            punchReachedWall = true;
            if (!debrisLaunched)
            {
                feedbackConfig.WallImpactShake.Play(controller);
                PlayWallImpactEffect(handCollider.bounds.center.y);
            }
            if (carryingPlayer && !carryingWithDebris) CompletePlayerCarry();
            scheduler?.ReserveDebrisRefill();
        }

        private void PlayWallImpactEffect(float y)
        {
            controller.GetComponent<Boss1Audio>()?.PlayWallImpact();
            float x = punchExitPoint.position.x > startPoint.position.x
                ? targetWall.bounds.min.x : targetWall.bounds.max.x;
            PlayImpactEffect(wallImpactPrefab, new Vector2(x, y));
        }

        private CombatAnimationEffect PlayImpactEffect(CombatAnimationEffect prefab, Vector2 position, float rotation = 0f)
        {
            if (prefab == null) return null;

            CombatAnimationEffect effect = Object.Instantiate(prefab,
                new Vector3(position.x, position.y, hand.transform.position.z),
                Quaternion.Euler(0f, 0f, rotation));
            impactEffects.Add(effect);
            return effect;
        }

        public override void Cleanup(Boss1PatternResult result)
        {
            HitCameraShake.Cancel(controller);
            foreach (CombatAnimationEffect effect in impactEffects)
            {
                if (effect != null) Object.Destroy(effect.gameObject);
            }
            impactEffects.Clear();
            punchImpactEffect = null;
            bodyAnimation?.SetGroggy(false);
            if (debrisField != null) debrisField.DebrisBroken -= HandleDebrisBroken;
            if (feedback != null) feedback.Dispose();
            feedback = null;
            chargeEffect?.Dispose();
            chargeEffect = null;
            if (playerHealth != null) playerHealth.Died -= HandlePlayerDied;
            if (carryingPlayer && playerMovement != null) playerMovement.EndForcedCarry();
            carryingPlayer = false;
            if (hand != null) hand.BeforeMove -= CheckPunchContact;
            if (hand != null && !controller.IsDead) hand.SetRotationOffset(0f);
            if (result != Boss1PatternResult.Completed)
            {
                if (hand != null) hand.StopMovement();
                if (slamHand != null) slamHand.StopMovement();
                if (playerMovement != null && wallStunVersion.HasValue)
                    playerMovement.ClearKnockback(wallStunVersion.Value);
                if (scheduler != null) scheduler.CancelDebrisRefill();
                if (debrisField != null && (playerHealth == null || !playerHealth.isActiveAndEnabled || playerHealth.IsDead))
                    debrisField.Clear();
            }
            if (slamHitbox != null) slamHitbox.EndAttack();
            if (body != null && !controller.IsDead) body.localPosition = originalBodyLocalPosition;
            if (debrisField != null && struckDebris != null && struckDebris.State == Boss1DebrisState.Flying)
                debrisField.RemoveDebris(struckDebris);
        }
    }
}

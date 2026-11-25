using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss1CrushPattern : Boss1PatternBase
{
    [Header("Scene Anchors")]
    [SerializeField] private Transform leftFirstPrepareAnchor;
    [SerializeField] private Transform rightFirstPrepareAnchor;
    [UnityEngine.Serialization.FormerlySerializedAs("leftPlacementAnchor")]
    [SerializeField] private Transform leftSecondPrepareAnchor;
    [UnityEngine.Serialization.FormerlySerializedAs("rightPlacementAnchor")]
    [SerializeField] private Transform rightSecondPrepareAnchor;
    [SerializeField] private Transform leftImpactAnchor;
    [SerializeField] private Transform rightImpactAnchor;

    [Header("References")]
    [SerializeField] private Collider2D leftWall;
    [SerializeField] private Collider2D rightWall;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Boss1HandDamageHitbox leftHitbox;
    [SerializeField] private Boss1HandDamageHitbox rightHitbox;
    [SerializeField] private Boss1HandDamageHitbox leftDebrisHitbox;
    [SerializeField] private Boss1HandDamageHitbox rightDebrisHitbox;
    [UnityEngine.Serialization.FormerlySerializedAs("warningArea")]
    [SerializeField] private Boss1CrushWarningArea crushWarningArea;

    [Header("Impact Effect")]
    [Tooltip("양쪽 잔해가 충돌하는 지점에서 한 번 재생하는 프레임 애니메이션 Prefab입니다.")]
    [SerializeField] private CombatAnimationEffect impactEffectPrefab;

    [Header("Animation")]
    [SerializeField] private Boss1HandAnimationProfile animationProfile = new Boss1HandAnimationProfile();

    [SerializeField] private Boss1CrushConfig config;

    protected override float DefaultCooldown => 5f;

    protected override void Reset()
    {
        base.Reset();
        ResolveReferences();
    }
    private void Awake() => ResolveReferences();

    public override Boss1PatternExecution CreateExecution(Boss1PatternContext context)
    {
        ResolveReferences();
        if (!HasRequiredReferences(context))
            throw new System.InvalidOperationException("Crush needs a config, anchors, walls, target, hand and held-debris hitboxes, and warning area.");
        return new Execution(this);
    }

    private bool HasRequiredReferences(Boss1PatternContext context)
    {
        bool hasReferences = context.LeftHand != null
            && context.RightHand != null
            && leftFirstPrepareAnchor != null
            && rightFirstPrepareAnchor != null
            && leftSecondPrepareAnchor != null
            && rightSecondPrepareAnchor != null
            && leftImpactAnchor != null
            && rightImpactAnchor != null
            && leftWall != null
            && rightWall != null
            && leftWall.bounds.max.x < rightWall.bounds.min.x
            && playerHealth != null
            && leftHitbox != null
            && rightHitbox != null
            && leftDebrisHitbox != null
            && rightDebrisHitbox != null
            && leftHitbox.AttackCollider != null
            && rightHitbox.AttackCollider != null
            && leftDebrisHitbox.AttackCollider != null
            && rightDebrisHitbox.AttackCollider != null
            && crushWarningArea != null;

        return hasReferences && config != null;
    }
    private void ResolveReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        Boss1HandDamageHitbox[] hitboxes = GetComponentsInChildren<Boss1HandDamageHitbox>(true);
        foreach (Boss1HandDamageHitbox hitbox in hitboxes)
        {
            Boss1HandController hand = hitbox.GetComponentInParent<Boss1HandController>();
            if (hand == null)
            {
                continue;
            }

            bool isHandHitbox = hitbox.gameObject == hand.gameObject;
            if (hand.HandSide == Boss1HandSide.Left)
            {
                if (isHandHitbox && leftHitbox == null) leftHitbox = hitbox;
                else if (!isHandHitbox && leftDebrisHitbox == null) leftDebrisHitbox = hitbox;
            }
            else if (hand.HandSide == Boss1HandSide.Right)
            {
                if (isHandHitbox && rightHitbox == null) rightHitbox = hitbox;
                else if (!isHandHitbox && rightDebrisHitbox == null) rightDebrisHitbox = hitbox;
            }
        }

        if (crushWarningArea == null)
        {
            crushWarningArea = GetComponentInChildren<Boss1CrushWarningArea>(true);
        }
    }

    private sealed class Execution : Boss1PatternExecution
    {
        private readonly Boss1CrushConfig config;
        private readonly Transform leftFirstPrepareAnchor;
        private readonly Transform rightFirstPrepareAnchor;
        private readonly Transform leftSecondPrepareAnchor;
        private readonly Transform rightSecondPrepareAnchor;
        private readonly Transform leftImpactAnchor;
        private readonly Transform rightImpactAnchor;
        private readonly Collider2D leftWall;
        private readonly Collider2D rightWall;
        private readonly PlayerHealth playerHealth;
        private readonly Boss1HandDamageHitbox leftHitbox;
        private readonly Boss1HandDamageHitbox rightHitbox;
        private readonly Boss1HandDamageHitbox leftDebrisHitbox;
        private readonly Boss1HandDamageHitbox rightDebrisHitbox;
        private readonly Boss1CrushWarningArea crushWarningArea;
        private readonly CombatAnimationEffect impactEffectPrefab;
        private readonly List<CombatAnimationEffect> impactEffects = new List<CombatAnimationEffect>();
        private readonly Boss1HandAnimationProfile animationProfile;
        private bool crushDamageApplied;
        private Boss1Controller controller;

        public Execution(Boss1CrushPattern bindings)
        {
            config = bindings.config;
            leftFirstPrepareAnchor = bindings.leftFirstPrepareAnchor;
            rightFirstPrepareAnchor = bindings.rightFirstPrepareAnchor;
            leftSecondPrepareAnchor = bindings.leftSecondPrepareAnchor;
            rightSecondPrepareAnchor = bindings.rightSecondPrepareAnchor;
            leftImpactAnchor = bindings.leftImpactAnchor;
            rightImpactAnchor = bindings.rightImpactAnchor;
            leftWall = bindings.leftWall;
            rightWall = bindings.rightWall;
            playerHealth = bindings.playerHealth;
            leftHitbox = bindings.leftHitbox;
            rightHitbox = bindings.rightHitbox;
            leftDebrisHitbox = bindings.leftDebrisHitbox;
            rightDebrisHitbox = bindings.rightDebrisHitbox;
            crushWarningArea = bindings.crushWarningArea;
            impactEffectPrefab = bindings.impactEffectPrefab;
            animationProfile = bindings.animationProfile;
        }

        public override IEnumerator Execute(Boss1PatternContext context)
        {
            controller = context.Controller;
            CleanupAttackState();

            for (int repetition = 0; repetition < config.RepeatCount; repetition++)
            {
                MoveHandsToFirstPrepare(context);
                yield return context.WaitForHands();

                PlayPrepareAnimations(context);
                yield return context.LeftHand.WaitForAnimation();
                yield return context.RightHand.WaitForAnimation();

                MoveHandsToPlacement(context);
                yield return context.WaitForHands();

                PlaySecondPrepareAnimations(context);
                yield return context.LeftHand.WaitForAnimation();
                yield return context.RightHand.WaitForAnimation();

                context.LeftHand.PlayAttackAnimation();
                context.RightHand.PlayAttackAnimation();
                context.LeftHand.StartTracking(
                    playerHealth.transform,
                    config.TrackingSpeed,
                    Boss1HandTrackingAxis.Vertical
                );
                context.RightHand.StartTracking(
                    playerHealth.transform,
                    config.TrackingSpeed,
                    Boss1HandTrackingAxis.Vertical
                );
                yield return WaitForDuration(config.TrackingDuration);

                context.LeftHand.StopMovement();
                context.RightHand.StopMovement();

                crushWarningArea.ShowAcross(GetCrushBounds(), config.WarningDuration);
                controller.GetComponent<Boss1Audio>()?.PlayWarning();
                yield return WaitForDuration(config.WarningDuration);
                crushWarningArea.Hide();
                controller.GetComponent<Boss1Audio>()?.StopWarning();

                context.LeftHand.MoveTo(
                    (Vector2)context.LeftHand.transform.position + Vector2.left * config.PullbackDistance,
                    config.PullbackDuration);
                context.RightHand.MoveTo(
                    (Vector2)context.RightHand.transform.position + Vector2.right * config.PullbackDistance,
                    config.PullbackDuration);
                yield return context.WaitForHands();

                crushDamageApplied = false;
                leftHitbox.BeginAttack(playerHealth, ApplyCrushDamage);
                rightHitbox.BeginAttack(playerHealth, ApplyCrushDamage);
                leftDebrisHitbox.BeginAttack(playerHealth, ApplyCrushDamage);
                rightDebrisHitbox.BeginAttack(playerHealth, ApplyCrushDamage);

                float lockedY = (context.LeftHand.transform.position.y
                    + context.RightHand.transform.position.y) * 0.5f;
                context.LeftHand.MoveTo(
                    new Vector2(leftImpactAnchor.position.x, lockedY),
                    config.CrushDuration
                );
                context.RightHand.MoveTo(
                    new Vector2(rightImpactAnchor.position.x, lockedY),
                    config.CrushDuration
                );
                yield return context.WaitForHands();

                MarkPresented();
                config.ImpactShake.Play(controller);
                controller.GetComponent<Boss1Audio>()?.PlayCrushImpact();
                PlayImpactEffect();
                CleanupAttackState();
                context.LeftHand.PlayReturnAnimation();
                context.RightHand.PlayReturnAnimation();
                yield return WaitForDuration(config.ImpactHoldDuration);
                yield return context.LeftHand.WaitForAnimation();
                yield return context.RightHand.WaitForAnimation();
            }

            if (config.RepeatCount > 0)
            {
                MoveHandsToFirstPrepare(context);
                yield return context.WaitForHands();
                context.LeftHand.ForceIdleAnimation();
                context.RightHand.ForceIdleAnimation();
            }

            context.LeftHand.ReturnToIdle(config.ReturnDuration, false);
            context.RightHand.ReturnToIdle(config.ReturnDuration, false);
            yield return context.WaitForHands();
        }

        public override void Cleanup(Boss1PatternResult result)
        {
            HitCameraShake.Cancel(controller);
            CleanupAttackState();
            foreach (CombatAnimationEffect effect in impactEffects)
            {
                if (effect != null) Object.Destroy(effect.gameObject);
            }
            impactEffects.Clear();
        }

        private void PlayImpactEffect()
        {
            if (impactEffectPrefab == null) return;

            Collider2D leftCollider = leftDebrisHitbox.AttackCollider;
            Collider2D rightCollider = rightDebrisHitbox.AttackCollider;
            Physics2D.SyncTransforms();
            Vector3 position = (leftCollider.bounds.center + rightCollider.bounds.center) * 0.5f;
            position.z = (leftDebrisHitbox.transform.position.z + rightDebrisHitbox.transform.position.z) * 0.5f;
            CombatAnimationEffect effect = Object.Instantiate(impactEffectPrefab, position, Quaternion.identity);
            impactEffects.Add(effect);
        }

        private void MoveHandsToPlacement(Boss1PatternContext context)
        {
            context.LeftHand.MoveTo(
                leftSecondPrepareAnchor.position,
                config.PlacementDuration
            );
            context.RightHand.MoveTo(
                rightSecondPrepareAnchor.position,
                config.PlacementDuration
            );
        }

        private void MoveHandsToFirstPrepare(Boss1PatternContext context)
        {
            context.LeftHand.MoveTo(
                new Vector2(leftFirstPrepareAnchor.position.x, leftFirstPrepareAnchor.position.y),
                config.PlacementDuration
            );
            context.RightHand.MoveTo(
                new Vector2(rightFirstPrepareAnchor.position.x, rightFirstPrepareAnchor.position.y),
                config.PlacementDuration
            );
        }

        private void PlayPrepareAnimations(Boss1PatternContext context)
        {
            context.LeftHand.PlayPrepareAnimation(GetAnimationTrack(context.LeftHand));
            context.RightHand.PlayPrepareAnimation(GetAnimationTrack(context.RightHand));
        }

        private void PlaySecondPrepareAnimations(Boss1PatternContext context)
        {
            context.LeftHand.PlaySecondPrepareAnimation(GetAnimationTrack(context.LeftHand));
            context.RightHand.PlaySecondPrepareAnimation(GetAnimationTrack(context.RightHand));
        }

        private Boss1HandAnimationTrack GetAnimationTrack(Boss1HandController hand)
        {
            return animationProfile?.GetTrack(hand.HandSide);
        }

        private Bounds GetCrushBounds()
        {
            Physics2D.SyncTransforms();
            Bounds leftHandBounds = leftHitbox.AttackCollider.bounds;
            Bounds rightHandBounds = rightHitbox.AttackCollider.bounds;
            float leftEdge = leftWall.bounds.max.x;
            float rightEdge = rightWall.bounds.min.x;
            float centerY = (leftHandBounds.center.y + rightHandBounds.center.y) * 0.5f;
            float height = Mathf.Min(leftHandBounds.size.y, rightHandBounds.size.y);

            return new Bounds(
                new Vector3((leftEdge + rightEdge) * 0.5f, centerY, 0f),
                new Vector3(rightEdge - leftEdge, height, 0f)
            );
        }

        private void ApplyCrushDamage(PlayerHealth hitPlayer)
        {
            if (crushDamageApplied || hitPlayer == null)
            {
                return;
            }

            crushDamageApplied = true;
            hitPlayer.TryTakeDamage(new DamageInfo(config.Damage, leftHitbox.transform.position));
        }

        private void CleanupAttackState()
        {
            leftHitbox?.EndAttack();
            rightHitbox?.EndAttack();
            leftDebrisHitbox?.EndAttack();
            rightDebrisHitbox?.EndAttack();
            crushWarningArea?.Hide();
            crushDamageApplied = false;
        }


    }
}

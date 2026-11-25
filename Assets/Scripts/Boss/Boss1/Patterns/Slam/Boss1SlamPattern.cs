using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Boss1DebrisField))]
public sealed class Boss1SlamPattern : Boss1PatternBase
{
    [Header("Scene Anchors")]
    [SerializeField] private Transform leftPreparationAnchor;
    [SerializeField] private Transform rightPreparationAnchor;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Boss1HandDamageHitbox leftHitbox;
    [SerializeField] private Boss1HandDamageHitbox rightHitbox;
    [UnityEngine.Serialization.FormerlySerializedAs("scrapField")]
    [SerializeField] private Boss1DebrisField debrisField;

    [Header("Floor Warning")]
    [SerializeField] private Collider2D floorCollider;
    [UnityEngine.Serialization.FormerlySerializedAs("floorWarningArea")]
    [SerializeField] private Boss1SlamWarningArea slamWarningArea;

    [Header("Impact Effect")]
    [Tooltip("양손의 바닥 접촉 지점에서 한 번씩 재생하는 프레임 애니메이션 Prefab입니다.")]
    [SerializeField] private CombatAnimationEffect impactEffectPrefab;

    [SerializeField] private Boss1FloorShakeEffect floorShakeEffect;

    [Header("Animation")]
    [SerializeField] private Boss1HandAnimationProfile animationProfile = new Boss1HandAnimationProfile();

    [SerializeField] private Boss1SlamConfig config;

    public PlayerHealth Target => playerHealth;

    protected override float DefaultCooldown => 0f;

    private void Awake()
    {
        debrisField = GetComponent<Boss1DebrisField>();
        slamWarningArea?.Hide();
    }

    public override Boss1PatternExecution CreateExecution(Boss1PatternContext context)
    {
        if (playerHealth == null || leftHitbox == null || rightHitbox == null
            || leftHitbox.AttackCollider == null || rightHitbox.AttackCollider == null || debrisField == null
            || leftPreparationAnchor == null || rightPreparationAnchor == null
            || floorCollider == null || slamWarningArea == null
            || !context.Controller.HasBothHands || config == null || !debrisField.isActiveAndEnabled)
            throw new System.InvalidOperationException("Slam needs a config, anchors, hands, target, floor warning and enabled debris field.");

        return new Execution(this);
    }

    private sealed class Execution : Boss1PatternExecution
    {
        private readonly Boss1SlamConfig config;
        private readonly Transform leftPreparationAnchor;
        private readonly Transform rightPreparationAnchor;
        private readonly PlayerHealth playerHealth;
        private readonly Boss1HandDamageHitbox leftHitbox;
        private readonly Boss1HandDamageHitbox rightHitbox;
        private readonly Boss1DebrisField debrisField;
        private readonly Collider2D floorCollider;
        private readonly Boss1SlamWarningArea slamWarningArea;
        private readonly CombatAnimationEffect impactEffectPrefab;
        private readonly Boss1FloorShakeEffect floorShakeEffect;
        private readonly List<CombatAnimationEffect> impactEffects = new List<CombatAnimationEffect>(2);
        private readonly Boss1HandAnimationProfile animationProfile;
        private Boss1SlamPathWarning leftPathWarning;
        private Boss1SlamPathWarning rightPathWarning;
        private bool slamDamageApplied;
        private Boss1Controller controller;
        private readonly List<RaycastHit2D> floorHits = new List<RaycastHit2D>();

        public Execution(Boss1SlamPattern bindings)
        {
            config = bindings.config;
            leftPreparationAnchor = bindings.leftPreparationAnchor;
            rightPreparationAnchor = bindings.rightPreparationAnchor;
            playerHealth = bindings.playerHealth;
            leftHitbox = bindings.leftHitbox;
            rightHitbox = bindings.rightHitbox;
            debrisField = bindings.debrisField;
            floorCollider = bindings.floorCollider;
            slamWarningArea = bindings.slamWarningArea;
            impactEffectPrefab = bindings.impactEffectPrefab;
            floorShakeEffect = bindings.floorShakeEffect;
            animationProfile = bindings.animationProfile;
        }

        public override IEnumerator Execute(Boss1PatternContext context)
        {
            controller = context.Controller;
            if (!debrisField.Prepare(playerHealth, floorCollider))
                throw new System.InvalidOperationException("Debris field preparation failed.");

            context.LeftHand.PlayPrepareAnimation(animationProfile?.GetTrack(Boss1HandSide.Left));
            context.RightHand.PlayPrepareAnimation(animationProfile?.GetTrack(Boss1HandSide.Right));
            context.LeftHand.MoveTo(leftPreparationAnchor.position, config.PreparationDuration);
            context.RightHand.MoveTo(rightPreparationAnchor.position, config.PreparationDuration);
            yield return context.WaitForHands();
            yield return context.LeftHand.WaitForAnimation();
            yield return context.RightHand.WaitForAnimation();

            ShowFloorWarning();
            controller.GetComponent<Boss1Audio>()?.PlayWarning();
            SpriteRenderer warningStyle = slamWarningArea.GetComponent<SpriteRenderer>();
            leftPathWarning = Boss1SlamPathWarning.Create(context.Controller.transform, warningStyle);
            rightPathWarning = Boss1SlamPathWarning.Create(context.Controller.transform, warningStyle);
            leftPathWarning.Show(context.LeftHand.transform.position.x, leftHitbox.AttackCollider,
                context.LeftHand.transform, floorCollider.bounds.max.y, config.PreparationWaitDuration);
            rightPathWarning.Show(context.RightHand.transform.position.x, rightHitbox.AttackCollider,
                context.RightHand.transform, floorCollider.bounds.max.y, config.PreparationWaitDuration);
            float preparationElapsed = 0f;
            while (preparationElapsed < config.PreparationWaitDuration)
            {
                preparationElapsed += Time.deltaTime;
                yield return null;
            }

            slamDamageApplied = false;
            HideFloorWarning();
            context.LeftHand.MoveTo((Vector2)context.LeftHand.transform.position + Vector2.up * config.PullbackHeight,
                config.PullbackDuration);
            context.RightHand.MoveTo((Vector2)context.RightHand.transform.position + Vector2.up * config.PullbackHeight,
                config.PullbackDuration);
            yield return context.WaitForHands();

            Physics2D.SyncTransforms();
            leftHitbox.BeginAttack(playerHealth, ApplySlamDamage);
            rightHitbox.BeginAttack(playerHealth, ApplySlamDamage);
            context.LeftHand.PlayAttackAnimation();
            context.RightHand.PlayAttackAnimation();
            context.LeftHand.MoveTo(GetFloorContactPosition(context.LeftHand, leftHitbox), config.SlamDuration,
                config.SlamMovementCurve);
            context.RightHand.MoveTo(GetFloorContactPosition(context.RightHand, rightHitbox), config.SlamDuration,
                config.SlamMovementCurve);

            // Hitboxes inspect each movement segment before the hands move.
            do
            {
                if (!context.Controller.AreHandsBusy) break;
                yield return null;
            } while (true);

            EndHandAttack();
            HideFloorWarning();
            MarkPresented();
            config.ImpactShake.Play(controller);
            controller.GetComponent<Boss1Audio>()?.PlaySlam();
            PlayImpactEffect(leftHitbox.AttackCollider);
            PlayImpactEffect(rightHitbox.AttackCollider);
            floorShakeEffect?.Play();
            ApplyFloorDamage();
            yield return WaitForDuration(config.ImpactHoldDuration);
            context.LeftHand.ReturnToIdle(config.ReturnDuration);
            context.RightHand.ReturnToIdle(config.ReturnDuration);

            int minimumCount = Mathf.Max(1, config.MinimumDebrisCount);
            int maximumCount = Mathf.Max(minimumCount, config.MaximumDebrisCount);

            int count = Random.Range(minimumCount, maximumCount + 1);
            if (!debrisField.StartDebrisSequence(
                    count,
                    config.DebrisWarningDuration,
                    config.DebrisWarningWidth))
                throw new System.InvalidOperationException("Could not start the debris sequence.");

            // The encounter ends after every debris block has landed.
            while (debrisField.HasPendingDebris) yield return null;
            yield return context.WaitForHands();
        }

        private Vector2 GetFloorContactPosition(Boss1HandController hand, Boss1HandDamageHitbox hitbox)
        {
            Collider2D collider = hitbox.AttackCollider;
            Physics2D.SyncTransforms();
            float bottomOffset = hand.transform.position.y - collider.bounds.min.y;
            return new Vector2(hand.transform.position.x, floorCollider.bounds.max.y + bottomOffset);
        }

        private void PlayImpactEffect(Collider2D handCollider)
        {
            if (impactEffectPrefab == null) return;

            Vector3 position = new Vector3(handCollider.bounds.center.x,
                floorCollider.bounds.max.y, handCollider.transform.position.z);
            CombatAnimationEffect effect = Object.Instantiate(impactEffectPrefab, position, Quaternion.identity);
            impactEffects.Add(effect);
        }

        public override void Cleanup(Boss1PatternResult result)
        {
            HitCameraShake.Cancel(controller);
            floorShakeEffect?.Stop();
            HideFloorWarning();
            if (leftPathWarning != null) leftPathWarning.Dispose();
            if (rightPathWarning != null) rightPathWarning.Dispose();
            EndHandAttack();
            foreach (CombatAnimationEffect effect in impactEffects)
            {
                if (effect != null) Object.Destroy(effect.gameObject);
            }
            impactEffects.Clear();
            if (result != Boss1PatternResult.Completed) debrisField?.Clear();
        }

        private void ApplySlamDamage(PlayerHealth target)
        {
            if (slamDamageApplied) return;
            slamDamageApplied = true;
            target.TryTakeDamage(new DamageInfo(config.SlamDamage, leftHitbox.transform.position));
        }

        private void ApplyFloorDamage()
        {
            if (slamDamageApplied || playerHealth == null || floorCollider == null) return;
            Collider2D playerCollider = playerHealth.GetComponent<Collider2D>();
            if (playerCollider == null || !playerCollider.enabled) return;
            Physics2D.SyncTransforms();
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(1 << floorCollider.gameObject.layer);
            filter.useTriggers = false;
            playerCollider.Cast(Vector2.down, filter, floorHits, config.FloorContactDistance);
            foreach (RaycastHit2D hit in floorHits)
            {
                if (hit.collider == floorCollider && hit.normal.y >= 0.5f)
                {
                    ApplySlamDamage(playerHealth);
                    return;
                }
            }
        }

        private void ShowFloorWarning()
        {
            slamWarningArea.ShowOver(
                floorCollider,
                config.FloorWarningHeight,
                config.PreparationWaitDuration
            );
        }

        private void HideFloorWarning()
        {
            slamWarningArea?.Hide();
            if (controller != null) controller.GetComponent<Boss1Audio>()?.StopWarning();
            if (leftPathWarning != null) leftPathWarning.Hide();
            if (rightPathWarning != null) rightPathWarning.Hide();
        }

        private void EndHandAttack()
        {
            leftHitbox?.EndAttack();
            rightHitbox?.EndAttack();
        }
    }

}

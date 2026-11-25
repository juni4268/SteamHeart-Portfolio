using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Boss1PropellerField))]
public sealed class Boss1PropellerPattern : Boss1PatternBase
{
    [Header("Scene Anchors")]
    [UnityEngine.Serialization.FormerlySerializedAs("leftSuctionAnchor")]
    [UnityEngine.Serialization.FormerlySerializedAs("leftVacuumAnchor")]
    [SerializeField] private Transform leftPropellerAnchor;
    [UnityEngine.Serialization.FormerlySerializedAs("rightSuctionAnchor")]
    [UnityEngine.Serialization.FormerlySerializedAs("rightVacuumAnchor")]
    [SerializeField] private Transform rightPropellerAnchor;

    [Header("Suction Origins")]
    [Tooltip("Left hand child Transform shared by suction, damage and visuals.")]
    [SerializeField] private Transform leftSuctionOrigin;
    [Tooltip("Right hand child Transform shared by suction, damage and visuals.")]
    [SerializeField] private Transform rightSuctionOrigin;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [UnityEngine.Serialization.FormerlySerializedAs("suctionField")]
    [SerializeField] private Boss1PropellerField propellerField;

    [Header("Animation")]
    [SerializeField] private Boss1HandAnimationProfile animationProfile = new Boss1HandAnimationProfile();

    [SerializeField] private Boss1PropellerConfig config;

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
        if (context.LeftHand == null || context.RightHand == null
            || leftPropellerAnchor == null || rightPropellerAnchor == null || playerHealth == null
            || leftSuctionOrigin == null || rightSuctionOrigin == null
            || propellerField == null || config == null)
            throw new System.InvalidOperationException(
                "Propeller needs a config, target, propeller field, two hands, placement anchors and suction origins.");

        return new Execution(leftPropellerAnchor, rightPropellerAnchor,
            leftSuctionOrigin, rightSuctionOrigin,
            playerHealth, propellerField, config, animationProfile);
    }

    private void ResolveReferences()
    {
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (propellerField == null) propellerField = GetComponent<Boss1PropellerField>();
    }

    private sealed class Execution : Boss1PatternExecution
    {
        private readonly PlayerHealth playerHealth;
        private readonly Boss1PropellerField propellerField;
        private readonly Boss1PropellerConfig config;
        private readonly Transform leftPropellerAnchor;
        private readonly Transform rightPropellerAnchor;
        private readonly Transform leftSuctionOrigin;
        private readonly Transform rightSuctionOrigin;
        private readonly Boss1HandAnimationProfile animationProfile;
        private Boss1HandController selectedHand;
        private Boss1Controller controller;

        public Execution(Transform leftAnchor, Transform rightAnchor,
            Transform leftOrigin, Transform rightOrigin,
            PlayerHealth target, Boss1PropellerField field,
            Boss1PropellerConfig propellerConfig, Boss1HandAnimationProfile handAnimationProfile)
        {
            leftPropellerAnchor = leftAnchor;
            rightPropellerAnchor = rightAnchor;
            leftSuctionOrigin = leftOrigin;
            rightSuctionOrigin = rightOrigin;
            playerHealth = target;
            propellerField = field;
            config = propellerConfig;
            animationProfile = handAnimationProfile;
        }

        public override IEnumerator Execute(Boss1PatternContext context)
        {
            controller = context.Controller;
            selectedHand = Random.Range(0, 2) == 0 ? context.LeftHand : context.RightHand;
            Transform propellerAnchor = selectedHand.HandSide == Boss1HandSide.Left
                ? leftPropellerAnchor
                : rightPropellerAnchor;
            selectedHand.PlayPrepareAnimation(animationProfile?.GetTrack(selectedHand.HandSide));
            controller.GetComponent<Boss1Audio>()?.PlayPropellerStart();
            selectedHand.MoveTo(propellerAnchor.position, config.PlacementDuration);
            yield return selectedHand.WaitForMotion();
            yield return selectedHand.WaitForAnimation();

            Transform suctionOrigin = selectedHand.HandSide == Boss1HandSide.Left
                ? leftSuctionOrigin
                : rightSuctionOrigin;
            propellerField.Prepare(selectedHand, suctionOrigin, playerHealth, config);
            controller.GetComponent<Boss1Audio>()?.PlayWarning();

            yield return WaitForDuration(config.WarningDuration);
            if (playerHealth.IsDead) yield break;

            selectedHand.PlayAttackAnimation();
            controller.GetComponent<Boss1Audio>()?.StopWarning();
            propellerField.StartSuction();
            MarkPresented();
            controller.GetComponent<Boss1Audio>()?.StartPropeller();
            float elapsed = 0f;
            while (elapsed < config.ActiveDuration && !playerHealth.IsDead)
            {
                config.SuctionShake.PlaySustained(controller, config.ActiveDuration - elapsed);
                elapsed += Time.deltaTime;
                yield return null;
            }
            HitCameraShake.Cancel(controller);

            if (!playerHealth.IsDead)
            {
                // End gameplay effects now; keep the hand still until the remaining wind fades.
                propellerField.FinishSuction();
                controller.GetComponent<Boss1Audio>()?.StopPropeller(true);
                while (propellerField.HasActiveWind && !playerHealth.IsDead)
                    yield return null;
            }
            propellerField.Stop();
            selectedHand.ReturnToIdle(config.ReturnDuration);
            yield return selectedHand.WaitForMotion();
        }

        public override void Cleanup(Boss1PatternResult result)
        {
            HitCameraShake.Cancel(controller);
            propellerField?.Stop();
            if (controller != null) controller.GetComponent<Boss1Audio>()?.StopPropeller(false);
        }
    }
}

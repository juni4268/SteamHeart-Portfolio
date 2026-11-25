using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss2CoreBurstPattern : Boss2PatternBase
{
    [SerializeField] private Boss2BodyMovement bodyMovement;
    [SerializeField] private Boss2CoreBurstPose bodyAnimation;
    [SerializeField] private Transform destination;
    [SerializeField, Min(0.1f)] private float moveSpeed = 2f;
    [SerializeField] private Transform coreAnchor;
    [SerializeField] private Boss2CoreBurstVisual corePrefab;
    [SerializeField] private Boss2CoreBurstImpact impactPrefab;
    [SerializeField] private PlayerHealth target;
    [SerializeField, Min(1)] private int damage = 10;

    [Header("Barrier")]
    [SerializeField] private Boss2CoreBurstBarrier barrierPrefab;
    [Tooltip("배리어가 착지할 수평 바닥입니다. 좌우 범위는 보스 이동의 Map Bounds도 함께 적용합니다.")]
    [SerializeField] private BoxCollider2D barrierGround;
    [Tooltip("포물선 이동 중 배리어가 넘지 않아야 할 수평 천장입니다.")]
    [SerializeField] private BoxCollider2D barrierCeiling;

    private Boss2CoreBurstBarrier preparedBarrier;

    public Vector2 PreparationPosition => destination.position;
    public float PreparationMoveSpeed => moveSpeed;

    public override Boss2PatternKind Kind => Boss2PatternKind.Main;
    public override Boss2Parts RequiredParts => Owner.CurrentPhase == Boss2Phase.Two ? Boss2Parts.All : Boss2Parts.None;

    protected override void OnDisable()
    {
        base.OnDisable();
        RemovePreparedBarrier();
    }

    public void DropBarrier()
    {
        if (preparedBarrier != null && !preparedBarrier.Completed && !preparedBarrier.Interrupted) return;
        if (barrierPrefab == null || barrierGround == null || barrierCeiling == null || coreAnchor == null || bodyMovement == null)
            throw new InvalidOperationException("Assign the Core Burst barrier prefab, ground, ceiling, anchor and body movement.");
        preparedBarrier = UnityEngine.Object.Instantiate(barrierPrefab);
        preparedBarrier.BindAudio(GetComponent<Boss2Audio>());
        preparedBarrier.Launch(coreAnchor.position, bodyMovement.HorizontalMapBounds, barrierGround, barrierCeiling, false);
    }

    public void RemovePreparedBarrier()
    {
        if (preparedBarrier != null) preparedBarrier.Take();
        preparedBarrier = null;
    }

    private Boss2CoreBurstBarrier GetBarrier()
    {
        DropBarrier();
        var barrier = preparedBarrier;
        preparedBarrier = null;
        return barrier;
    }

    public override Boss2PatternExecution CreateExecution(Boss2PatternContext context)
    {
        if (Owner.CurrentPhase != Boss2Phase.Two)
            throw new InvalidOperationException("Core Burst is available only in phase two.");
        if (bodyMovement == null || bodyAnimation == null || !bodyAnimation.isActiveAndEnabled ||
            destination == null || moveSpeed <= 0f || coreAnchor == null ||
            corePrefab == null || impactPrefab == null || target == null ||
            barrierPrefab == null || barrierGround == null || barrierCeiling == null)
            throw new InvalidOperationException("Assign Core Burst movement, destination, core visual, player, barrier prefab, landing ground and ceiling.");
        var hands = new Boss2CoreBurstHandAnimation[4];
        for (int i = 0; i < hands.Length; i++)
        {
            hands[i] = context.GetPart((Boss2PartId)i).GetComponentInChildren<Boss2CoreBurstHandAnimation>(true);
            if (hands[i] == null || !hands[i].isActiveAndEnabled)
                throw new InvalidOperationException("Assign Core Burst animation to each hand.");
        }
        var ringAnimation = context.GetPart(Boss2PartId.Ring).GetComponentInChildren<Boss2CoreBurstPose>(true);
        if (ringAnimation == null || !ringAnimation.isActiveAndEnabled)
            throw new InvalidOperationException("Assign the ring's Core Burst pose animation.");
        return new Execution(this, hands, ringAnimation);
    }

    private sealed class Execution : Boss2PatternExecution
    {
        private readonly Boss2CoreBurstPattern pattern;
        private readonly Boss2CoreBurstHandAnimation[] hands;
        private readonly Boss2CoreBurstPose ringAnimation;
        private Boss2CoreBurstVisual core;
        private Boss2CoreBurstImpact impact;
        private Boss2CoreBurstBarrier barrier;
        public Execution(Boss2CoreBurstPattern pattern, Boss2CoreBurstHandAnimation[] hands, Boss2CoreBurstPose ringAnimation)
        { this.pattern = pattern; this.hands = hands; this.ringAnimation = ringAnimation; }

        public override IEnumerator Execute(Boss2PatternContext context)
        {
            barrier = pattern.GetBarrier();
            barrier.Activate();
            float originalHeight = context.Controller.transform.position.y;
            yield return MoveBody(pattern.destination.position);
            pattern.bodyAnimation.Begin();
            ringAnimation.Begin();
            foreach (var hand in hands) hand.Begin();
            while (!AllPrepared() || !pattern.bodyAnimation.Entered || !ringAnimation.Entered)
            { EnsureAnimations(); yield return null; }
            while (barrier != null && barrier.IsFlying)
            { EnsureAnimations(); yield return null; }
            if (barrier == null || !barrier.isActiveAndEnabled || barrier.Interrupted)
                throw new InvalidOperationException("The Core Burst barrier flight was interrupted.");
            core = UnityEngine.Object.Instantiate(pattern.corePrefab, pattern.coreAnchor);
            core.BindAudio(context.Controller.GetComponent<Boss2Audio>());
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = Quaternion.identity;
            core.Begin();
            while (!core.Completed)
            {
                EnsureAnimations();
                if (core == null || !core.isActiveAndEnabled || core.Interrupted)
                    throw new InvalidOperationException("Core Burst animation was interrupted.");
                yield return null;
            }
            Vector3 burstPosition = core.transform.position;
            StopCore();
            impact = UnityEngine.Object.Instantiate(pattern.impactPrefab, burstPosition, Quaternion.identity);
            impact.BindAudio(context.Controller.GetComponent<Boss2Audio>());
            impact.Play(Camera.main);
            ApplyBurstDamage();
            foreach (var hand in hands) hand.BeginReturn();
            pattern.bodyAnimation.End();
            ringAnimation.End();
            while (!AllCompleted() || pattern.bodyAnimation.Active || ringAnimation.Active)
            { EnsureAnimations(); yield return null; }
            Vector3 currentPosition = context.Controller.transform.position;
            yield return MoveBody(new Vector2(currentPosition.x, originalHeight));
        }

        private IEnumerator MoveBody(Vector2 destination)
        {
            pattern.bodyMovement.MoveForPattern(destination, pattern.moveSpeed);
            while (pattern.bodyMovement.IsPatternMoving)
            {
                if (!pattern.bodyMovement.isActiveAndEnabled)
                    throw new InvalidOperationException("Core Burst body movement was interrupted.");
                yield return null;
            }
            if (pattern.bodyMovement.IsPatternMovementBlocked)
                throw new InvalidOperationException("Core Burst cannot reach its destination.");
        }

        private void ApplyBurstDamage()
        {
            bool protectedByBarrier = barrier != null && barrier.Protects(pattern.target);
            if (barrier != null) barrier.ReceiveCoreBurst();
            if (!protectedByBarrier && pattern.target != null)
                pattern.target.TryTakeDamage(new DamageInfo(pattern.damage, ignorePlayerDefenses: true));
        }
        private bool AllPrepared() { foreach (var hand in hands) if (!hand.Prepared) return false; return true; }
        private bool AllCompleted() { foreach (var hand in hands) if (!hand.Completed) return false; return true; }
        private void EnsureHands()
        {
            foreach (var hand in hands)
                if (hand == null || !hand.isActiveAndEnabled || hand.Interrupted)
                    throw new InvalidOperationException("Core Burst hand animation was interrupted.");
        }
        private void EnsureAnimations()
        {
            EnsureHands();
            if (!pattern.bodyAnimation.isActiveAndEnabled || pattern.bodyAnimation.Interrupted ||
                !ringAnimation.isActiveAndEnabled || ringAnimation.Interrupted)
                throw new InvalidOperationException("Core Burst body or ring animation was interrupted.");
        }
        private void StopCore() { if (core != null) { core.Cancel(); UnityEngine.Object.Destroy(core.gameObject); core = null; } }
        private void StopImpact() { if (impact != null) { impact.Cancel(); impact = null; } }
        private void TakeBarrier() { if (barrier != null) barrier.Take(); }
        public override void StopForDeath(Boss2PatternContext context) { StopCore(); StopImpact(); TakeBarrier(); pattern.bodyMovement.ResumePatrol(); }
        public override IEnumerator ReturnAfterDeath(Boss2PatternContext context)
        {
            foreach (var hand in hands) if (hand.Active) hand.BeginReturn(true);
            while (true)
            {
                bool returning = false;
                foreach (var hand in hands) if (hand.Active) returning = true;
                if (!returning) break;
                EnsureHands();
                yield return null;
            }
            pattern.bodyAnimation.End();
            ringAnimation.End();
            while (pattern.bodyAnimation.Active || ringAnimation.Active)
            {
                EnsureAnimations();
                yield return null;
            }
        }
        public override void Cleanup(Boss2PatternResult result)
        {
            StopCore();
            TakeBarrier();
            if (result != Boss2PatternResult.Completed)
            {
                StopImpact();
                foreach (var hand in hands) if (hand.Active) hand.Cancel();
                if (pattern.bodyAnimation.Active) pattern.bodyAnimation.Cancel();
                if (ringAnimation.Active) ringAnimation.Cancel();
            }
            pattern.bodyMovement.ResumePatrol();
        }
    }
}

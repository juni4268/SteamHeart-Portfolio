using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Basic-attack clock and move/aim/fire/return sequence.</summary>
public abstract class Boss2BasicPatternBase : Boss2PatternBase
{
    [Header("Basic Attack Bindings")]
    [SerializeField] private Boss2BasicAttackArea movementArea;
    [SerializeField] private PlayerHealth target;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
    [SerializeField, Min(0.1f)] private float returnSpeed = 8f;
    [Tooltip("Degrees per second while moving toward the attack position and finishing the aim.")]
    [SerializeField, Min(1f)] private float aimTurnSpeed = 180f;
    [SerializeField, Min(0f)] private float aimDuration = 0.7f;
    [SerializeField, Min(0.1f)] private float attackRange = 18f;

    private readonly List<Boss2PatternRunner> activeRunners = new List<Boss2PatternRunner>();
    private readonly Dictionary<Boss2PartController, Vector3> destinations = new Dictionary<Boss2PartController, Vector3>();
    private float remainingInterval;

    public override Boss2PatternKind Kind => Boss2PatternKind.Basic;
    protected abstract bool HasAttackBindings { get; }
    protected virtual Boss2Parts ScheduledParts => RequiredParts;

    protected virtual void OnEnable()
    {
        remainingInterval = SampleCooldown();
    }

    internal bool CanSchedulePart(Boss2Parts part)
    {
        int value = (int)part;
        return value > 0 && (value & (value - 1)) == 0 && (ScheduledParts & part) == part;
    }

    private void Update()
    {
        if (Owner.CurrentState != Boss2CombatState.Fighting || Owner.IsDead ||
            HitStopManager.IsActive || Time.timeScale <= 0f || target == null ||
            !target.isActiveAndEnabled || target.IsDead) return;
        bool hadActiveRunners = activeRunners.Count > 0;
        for (int i = activeRunners.Count - 1; i >= 0; i--)
        {
            if (activeRunners[i].State == Boss2ExecutionState.Finished) activeRunners.RemoveAt(i);
        }
        if (activeRunners.Count != 0) return;
        if (hadActiveRunners) remainingInterval = SampleCooldown();
        remainingInterval -= Time.deltaTime;
        if (remainingInterval > 0f || movementArea == null || !HasAttackBindings) return;

        destinations.Clear();
        Boss2Parts scheduledParts = ScheduledParts;
        foreach (Boss2PartId id in Enum.GetValues(typeof(Boss2PartId)))
        {
            Boss2Parts mask = Boss2PartMask.FromId(id);
            if ((scheduledParts & mask) == 0 || !Owner.ArePartsReady(mask) ||
                Owner.PartUsage.GetOwner(id) != null || Owner.PartUsage.GetReservation(id) != null ||
                Owner.PartUsage.IsBasicPaused(mask)) continue;
            Boss2PartController part = Owner.GetPart(id);
            if (part.Aim == null || !part.Aim.IsConfigured ||
                !movementArea.TryGetPosition(part.Aim.PivotPosition, target.transform.position,
                    destinations.Values, IsLeftHand(id), out Vector3 destination)) continue;
            if (!Owner.TryRequestBasicPattern(this, mask, out Boss2PatternRunner runner)) continue;
            destinations[part] = destination;
            activeRunners.Add(runner);
        }
        if (activeRunners.Count == 0) return;
        PrepareScheduledAttacks(activeRunners);
    }

    public sealed override Boss2PatternExecution CreateExecution(Boss2PatternContext context)
    {
        if (movementArea == null || target == null || !HasAttackBindings)
            throw new InvalidOperationException("Assign the basic attack area, target and attack prefabs.");
        return new Execution(this, context);
    }

    protected abstract Boss2PatternExecution CreateAttack(Boss2PartController source,
        Vector3 origin, Vector2 direction, float distance);

    protected virtual void BeginAttackPreparation(Boss2PartController source) { }
    protected virtual IEnumerator WaitAfterArrival(Boss2PartController source) { yield break; }
    protected virtual IEnumerator WaitForAttackPreparation(Boss2PartController source) { yield break; }
    protected virtual IEnumerator StartAttackPresentation(Boss2PartController source) { yield break; }
    protected virtual IEnumerator WaitForAttackPresentation(Boss2PartController source) { yield break; }
    protected virtual IEnumerator ReturnAttackPresentation(Boss2PartController source) { yield break; }
    protected virtual void StartReturnMovement(Boss2PartController source, float duration) => source.ReturnToIdle(duration, true);
    protected virtual void CleanupAttackPresentation(Boss2PartController source, Boss2PatternResult result) { }
    protected virtual void StopAttackForDeath(Boss2PartController source) { }
    protected virtual IEnumerator ReturnPresentationAfterDeath(Boss2PartController source) { yield break; }
    protected virtual void BeginAimPresentation(Boss2PartController source, Vector3 origin, Vector2 direction, float distance) { }
    protected virtual void UpdateAimPresentation(Boss2PartController source, Vector3 origin, Vector2 direction, float distance) { }
    protected virtual void PrepareScheduledAttacks(IReadOnlyList<Boss2PatternRunner> runners) { }
    protected virtual Vector2 GetAimPosition(Boss2PartController source, Vector2 playerPosition) => playerPosition;
    protected float GetAttackDistance(Vector3 origin, Vector2 direction) => attackRange;
    protected bool HasTarget => target != null && target.isActiveAndEnabled && !target.IsDead;

    protected void TrackTarget(Boss2PartController source)
    {
        if (HasTarget) source.Aim.TryAimAt(GetAimPosition(source, target.transform.position),
            aimTurnSpeed * Time.deltaTime, out _);
    }

    private static bool IsLeftHand(Boss2PartId id) =>
        id == Boss2PartId.UpperLeftHand || id == Boss2PartId.LowerLeftHand;

    private bool TryGetDestination(Boss2PartController part, out Vector3 destination)
    {
        if (destinations.TryGetValue(part, out destination))
        {
            destinations.Remove(part);
            return true;
        }
        return movementArea.TryGetPosition(part.Aim.PivotPosition, target.transform.position,
            null, IsLeftHand(part.PartId), out destination);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        activeRunners.Clear();
        destinations.Clear();
    }

    private sealed class Execution : Boss2PatternExecution
    {
        private readonly Boss2BasicPatternBase pattern;
        private readonly Boss2PartController part;
        private readonly Boss2PartAim aim;
        private readonly Boss2Parts parts;
        private Boss2PatternExecution attack;

        public Execution(Boss2BasicPatternBase pattern, Boss2PatternContext context)
        {
            this.pattern = pattern;
            parts = context.HeldParts;
            foreach (Boss2PartId id in Enum.GetValues(typeof(Boss2PartId)))
                if (parts == Boss2PartMask.FromId(id)) part = context.GetPart(id);
            if (part == null) throw new InvalidOperationException("A basic attack must use exactly one part.");
            aim = part.Aim;
            if (aim == null || !aim.IsConfigured)
                throw new InvalidOperationException("Assign the attacking part's aim pivot and fire point.");
        }

        public override IEnumerator Execute(Boss2PatternContext context)
        {
            if (!HasTarget || !pattern.TryGetDestination(part, out Vector3 destination)) yield break;

            Vector3 rootDestination = destination + part.transform.position - aim.PivotPosition;
            part.MoveToWorld(rootDestination, Vector2.Distance(aim.PivotPosition, destination) / pattern.moveSpeed);
            pattern.BeginAttackPreparation(part);
            while (part.IsMoving)
            {
                pattern.TrackTarget(part);
                yield return null;
            }
            yield return context.WaitForParts(parts);

            yield return pattern.WaitAfterArrival(part);

            // Lock the target after the pattern's arrival behavior, then finish turning toward it.
            bool aligned = false;
            if (HasTarget && !context.FinishRequested)
            {
                Vector2 lockedTarget = pattern.GetAimPosition(part, pattern.target.transform.position);
                while (HasTarget && !context.FinishRequested && aim.TryAimAt(lockedTarget,
                    pattern.aimTurnSpeed * Time.deltaTime, out aligned))
                {
                    if (aligned) break;
                    yield return null;
                }
            }
            yield return pattern.WaitForAttackPreparation(part);
            if (HasTarget && aligned && !context.FinishRequested)
            {
                // Lock the direction after arriving; animated muzzle positions may still change.
                Vector2 direction = aim.FireDirection;
                Vector3 initialOrigin = aim.FirePosition;
                pattern.BeginAimPresentation(part, initialOrigin, direction, pattern.GetAttackDistance(initialOrigin, direction));
                float elapsed = 0f;
                while (elapsed < pattern.aimDuration && !context.FinishRequested)
                {
                    Vector3 warningOrigin = aim.FirePosition;
                    float warningDistance = pattern.GetAttackDistance(warningOrigin, direction);
                    pattern.UpdateAimPresentation(part, warningOrigin, direction, warningDistance);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (HasTarget && !context.FinishRequested)
                {
                    yield return pattern.StartAttackPresentation(part);
                    if (HasTarget)
                    {
                        Vector3 origin = aim.FirePosition;
                        attack = pattern.CreateAttack(part, origin, direction, pattern.GetAttackDistance(origin, direction));
                        yield return attack.Execute(context);
                    }
                }
            }
            yield return pattern.WaitForAttackPresentation(part);
            yield return pattern.ReturnAttackPresentation(part);
            pattern.StartReturnMovement(part, Vector2.Distance(part.transform.position, part.IdlePosition) / pattern.returnSpeed);
            yield return context.WaitForParts(parts);
        }

        public override void Cleanup(Boss2PatternResult result)
        {
            try { attack?.Cleanup(result); }
            finally { pattern.CleanupAttackPresentation(part, result); }
        }

        public override void StopForDeath(Boss2PatternContext context) => pattern.StopAttackForDeath(part);

        public override IEnumerator ReturnAfterDeath(Boss2PatternContext context)
        {
            yield return pattern.ReturnPresentationAfterDeath(part);
            pattern.StartReturnMovement(part,
                Vector2.Distance(part.transform.position, part.IdlePosition) / pattern.returnSpeed);
            yield return context.WaitForParts(parts);
        }

        private bool HasTarget => pattern.HasTarget;

    }
}

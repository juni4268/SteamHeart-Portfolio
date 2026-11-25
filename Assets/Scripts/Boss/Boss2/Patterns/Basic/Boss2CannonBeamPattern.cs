using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

[DisallowMultipleComponent]
public sealed class Boss2CannonBeamPattern : Boss2BasicPatternBase
{
    [Header("Cannon Beam")]
    [SerializeField] private Boss2CannonBeamVisual beamPrefab;
    [SerializeField, Min(0.01f)] private float beamWidth = 0.24f;
    [SerializeField, Min(0.01f)] private float beamDuration = 0.45f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField] private LayerMask playerLayers;
    [SerializeField, Min(0f)] private float aimRadius = 1f;
    [Tooltip("Seconds to keep tracking the target after arrival, before locking the aim.")]
    [SerializeField, Min(0f)] private float arrivalTrackingDuration = 0.1f;
    [Tooltip("X: normalized return time (0-1). Y: relative speed (non-negative). Total return time is determined by Return Speed.")]
    [SerializeField] private AnimationCurve returnCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Phase Two")]
    [Tooltip("Multiplier for the sampled Min/Max Cooldown in phase two.")]
    [SerializeField, Range(0f, 1f)] private float phaseTwoCooldownMultiplier = 0.5f;

    private readonly Dictionary<Boss2PartController, Presentation> presentations = new Dictionary<Boss2PartController, Presentation>();
    private readonly Dictionary<Boss2PartController, Vector2> aimOffsets = new Dictionary<Boss2PartController, Vector2>();

    private sealed class Presentation
    {
        public Boss2CannonHandAnimation Hand;
        public Boss2CannonBeamVisual Beam;
        public Vector2 AimDirection;
    }

    public override Boss2Parts RequiredParts
    {
        get
        {
            foreach (Boss2PartId id in Enum.GetValues(typeof(Boss2PartId)))
                if (id != Boss2PartId.Ring && IsHandAvailable(id)) return Boss2PartMask.FromId(id);
            return Boss2Parts.None;
        }
    }

    protected override bool HasAttackBindings => beamPrefab != null;
    protected override Boss2Parts ScheduledParts => Boss2Parts.AllHands;

    internal override float SampleCooldown() =>
        base.SampleCooldown() * (Owner.CurrentPhase == Boss2Phase.Two ? phaseTwoCooldownMultiplier : 1f);

    protected override void PrepareScheduledAttacks(IReadOnlyList<Boss2PatternRunner> runners)
    {
        aimOffsets.Clear();
        int directIndex = UnityEngine.Random.Range(0, runners.Count);
        for (int i = 0; i < runners.Count; i++)
        {
            foreach (Boss2PartId id in Enum.GetValues(typeof(Boss2PartId)))
            {
                if (runners[i].HeldParts != Boss2PartMask.FromId(id)) continue;
                aimOffsets[Owner.GetPart(id)] = i == directIndex
                    ? Vector2.zero : UnityEngine.Random.insideUnitCircle * aimRadius;
                break;
            }
        }
    }

    protected override Vector2 GetAimPosition(Boss2PartController source, Vector2 playerPosition) =>
        playerPosition + (aimOffsets.TryGetValue(source, out Vector2 offset) ? offset : Vector2.zero);

    protected override IEnumerator WaitAfterArrival(Boss2PartController source)
    {
        float elapsed = 0f;
        while (HasTarget && elapsed < arrivalTrackingDuration)
        {
            TrackTarget(source);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    protected override void BeginAimPresentation(Boss2PartController source, Vector3 origin, Vector2 direction, float distance)
    {
        Presentation presentation = GetPresentation(source);
        presentation.AimDirection = direction;
        presentation.Beam = Object.Instantiate(beamPrefab);
        presentation.Beam.BindAudio(GetComponent<Boss2Audio>());
        presentation.Beam.BeginAim();
        presentation.Beam.SetGeometry(origin, direction, distance, beamWidth);
    }

    protected override void UpdateAimPresentation(Boss2PartController source, Vector3 origin, Vector2 direction, float distance)
    {
        Presentation presentation = GetPresentation(source);
        EnsureBeamActive(presentation);
        presentation.Beam.SetGeometry(origin, direction, distance, beamWidth);
    }

    private bool IsHandAvailable(Boss2PartId id)
    {
        Boss2Parts mask = Boss2PartMask.FromId(id);
        return Owner.ArePartsReady(mask) && Owner.PartUsage.GetOwner(id) == null &&
            Owner.PartUsage.GetReservation(id) == null && !Owner.PartUsage.IsBasicPaused(mask);
    }

    protected override void BeginAttackPreparation(Boss2PartController source)
    {
        var presentation = new Presentation();
        presentations.Add(source, presentation);
        presentation.Hand = source.GetComponentInChildren<Boss2CannonHandAnimation>(true);
        if (presentation.Hand == null)
            throw new InvalidOperationException("Assign the cannon hand animation component to the selected hand.");
        presentation.Hand.Begin();
    }

    protected override IEnumerator WaitForAttackPreparation(Boss2PartController source)
    {
        Presentation presentation = GetPresentation(source);
        EnsureAnimationActive(presentation);
        while (!presentation.Hand.Prepared)
        {
            EnsureAnimationActive(presentation);
            yield return null;
        }
    }

    protected override IEnumerator StartAttackPresentation(Boss2PartController source)
    {
        Presentation presentation = GetPresentation(source);
        EnsureAnimationActive(presentation);
        EnsureBeamActive(presentation);
        while (!presentation.Beam.AimCompleted)
        {
            EnsureBeamActive(presentation);
            yield return null;
        }
        Vector3 origin = source.Aim.FirePosition;
        presentation.Beam.SetGeometry(origin, presentation.AimDirection, GetAttackDistance(origin, presentation.AimDirection), beamWidth);
        presentation.Hand.BeginFire();
        presentation.Beam.BeginFire(beamDuration);
        while (!presentation.Hand.FireEntered || !presentation.Beam.FireEntered)
        {
            EnsureAnimationActive(presentation);
            EnsureBeamActive(presentation);
            yield return null;
        }
    }

    protected override IEnumerator WaitForAttackPresentation(Boss2PartController source)
    {
        if (!presentations.TryGetValue(source, out Presentation presentation)) yield break;
        if (presentation.Hand == null || !presentation.Hand.FireRequested)
        {
            HideBeamVisual(presentation);
            yield break;
        }
        while (!presentation.Hand.FireCompleted)
        {
            EnsureAnimationActive(presentation);
            yield return null;
        }
    }

    protected override IEnumerator ReturnAttackPresentation(Boss2PartController source)
    {
        if (!presentations.TryGetValue(source, out Presentation presentation) || presentation.Hand == null) yield break;
        EnsureAnimationActive(presentation);
        presentation.Hand.BeginReturn();
        while (!presentation.Hand.Completed)
        {
            EnsureAnimationActive(presentation);
            yield return null;
        }
        presentation.Hand = null;
    }

    protected override void StartReturnMovement(Boss2PartController source, float duration) =>
        source.ReturnToIdle(duration, returnCurve);

    protected override void StopAttackForDeath(Boss2PartController source)
    {
        if (presentations.TryGetValue(source, out Presentation presentation)) HideBeamVisual(presentation);
    }

    protected override IEnumerator ReturnPresentationAfterDeath(Boss2PartController source)
    {
        if (!presentations.TryGetValue(source, out Presentation presentation) || presentation.Hand == null) yield break;
        EnsureAnimationActive(presentation);
        presentation.Hand.BeginDeathReturn();
        while (presentation.Hand.Active && !presentation.Hand.Completed)
        {
            EnsureAnimationActive(presentation);
            yield return null;
        }
        presentation.Hand = null;
    }

    protected override void CleanupAttackPresentation(Boss2PartController source, Boss2PatternResult result)
    {
        if (!presentations.TryGetValue(source, out Presentation presentation)) return;
        if (presentation.Hand != null && result != Boss2PatternResult.Completed) presentation.Hand.Cancel();
        HideBeamVisual(presentation);
        presentations.Remove(source);
        aimOffsets.Remove(source);
    }

    private Presentation GetPresentation(Boss2PartController source)
    {
        if (!presentations.TryGetValue(source, out Presentation presentation))
            throw new InvalidOperationException("The cannon hand has no active presentation.");
        return presentation;
    }

    private static void EnsureAnimationActive(Presentation presentation)
    {
        if (presentation.Hand == null || !presentation.Hand.isActiveAndEnabled || presentation.Hand.Interrupted)
            throw new InvalidOperationException("The cannon hand animation was interrupted.");
    }

    private static void EnsureBeamActive(Presentation presentation)
    {
        if (presentation.Beam == null || !presentation.Beam.isActiveAndEnabled || presentation.Beam.Interrupted)
            throw new InvalidOperationException("The cannon beam animation was interrupted.");
    }

    private static void HideBeamVisual(Presentation presentation)
    {
        if (presentation.Beam == null) return;
        presentation.Beam.Cancel();
        Object.Destroy(presentation.Beam.gameObject);
        presentation.Beam = null;
    }

    protected override Boss2PatternExecution CreateAttack(Boss2PartController source,
        Vector3 origin, Vector2 direction, float distance)
    {
        return new Execution(this, GetPresentation(source).Beam, origin, direction, distance);
    }

    private sealed class Execution : Boss2PatternExecution
    {
        private readonly Boss2CannonBeamPattern pattern;
        private readonly Boss2CannonBeamVisual visual;
        private readonly Vector3 origin;
        private readonly Vector2 direction;
        private readonly float distance;

        public Execution(Boss2CannonBeamPattern pattern, Boss2CannonBeamVisual visual, Vector3 origin, Vector2 direction, float distance)
        {
            this.pattern = pattern;
            this.visual = visual;
            this.origin = origin;
            this.direction = direction;
            this.distance = distance;
        }

        public override IEnumerator Execute(Boss2PatternContext context)
        {
            bool contactedPlayer = false;
            while (!visual.FireCompleted)
            {
                if (visual.Interrupted || !visual.isActiveAndEnabled)
                    throw new InvalidOperationException("The cannon beam animation was interrupted.");
                if (!contactedPlayer && distance > 0f)
                {
                    RaycastHit2D hit = Physics2D.CircleCast(origin, pattern.beamWidth * 0.5f,
                        direction, distance, pattern.playerLayers);
                    PlayerHealth player = hit.collider != null ? hit.collider.GetComponentInParent<PlayerHealth>() : null;
                    if (player != null)
                    {
                        // A dodge or counter consumes this beam's contact as well.
                        contactedPlayer = true;
                        player.TryTakeDamage(new DamageInfo(pattern.damage, origin));
                    }
                }
                yield return null;
            }
            visual.BeginFade();
            while (!visual.Completed)
            {
                if (visual.Interrupted || !visual.isActiveAndEnabled)
                    throw new InvalidOperationException("The cannon beam fade was interrupted.");
                yield return null;
            }
        }

        public override void Cleanup(Boss2PatternResult result) { }
        public override IEnumerator ReturnAfterDeath(Boss2PatternContext context) { yield break; }
    }
}

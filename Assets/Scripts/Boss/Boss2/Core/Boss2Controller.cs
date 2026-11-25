using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BossHealth))]
public sealed class Boss2Controller : MonoBehaviour
{
    private static readonly Boss2PartId[] PartIds = (Boss2PartId[])Enum.GetValues(typeof(Boss2PartId));

    [Header("Parts")]
    [SerializeField] private Boss2PartController upperLeftHand;
    [SerializeField] private Boss2PartController upperRightHand;
    [SerializeField] private Boss2PartController lowerLeftHand;
    [SerializeField] private Boss2PartController lowerRightHand;
    [SerializeField] private Boss2PartController ring;

    [Header("Combat")]
    [SerializeField] private bool startAutomatically = true;
    [SerializeField, Range(0.01f, 1f)] private float phaseTwoHealthRatio = 0.5f;

    private readonly Boss2PartUsage partUsage = new Boss2PartUsage();
    private readonly List<Boss2PatternRunner> executions = new List<Boss2PatternRunner>();
    private readonly List<Boss2PatternRunner> updateSnapshot = new List<Boss2PatternRunner>();
    private IReadOnlyList<Boss2PatternRunner> executionView;
    private BossHealth health;
    private bool hasStarted;
    private bool cancellingPatterns;

    public event Action<Boss2CombatState, Boss2CombatState> StateChanged;
    public event Action<Boss2Phase> PhaseChanged;
    public event Action<Boss2PatternRunner> PatternStarted;
    public event Action<Boss2PatternRunner> PatternFinished;

    public Boss2CombatState CurrentState { get; private set; } = Boss2CombatState.Inactive;
    public Boss2Phase CurrentPhase { get; private set; } = Boss2Phase.One;
    public bool IsDead => CurrentState == Boss2CombatState.Dead || (health != null && health.IsDead);
    public Boss2PartUsage PartUsage => partUsage;
    public IReadOnlyList<Boss2PatternRunner> Executions => executionView ??= executions.AsReadOnly();
    internal bool CanStartMainPattern
    {
        get
        {
            Boss2BodyMovement movement = GetComponent<Boss2BodyMovement>();
            return movement == null || !movement.isActiveAndEnabled || movement.IsReadyForMainPattern;
        }
    }
    public bool HasMainPattern
    {
        get
        {
            foreach (Boss2PatternRunner runner in executions)
                if (runner.Pattern != null && runner.Pattern.Kind == Boss2PatternKind.Main) return true;
            return false;
        }
    }

    private void Awake() => health = GetComponent<BossHealth>();

    private void OnEnable()
    {
        health.HealthChanged += HandleHealthChanged;
        health.Died += HandleDeath;
        ChangeState(health.IsDead ? Boss2CombatState.Dead : Boss2CombatState.Inactive);
        if (hasStarted && startAutomatically) BeginCombat();
    }

    private void Start()
    {
        hasStarted = true;
        if (startAutomatically) BeginCombat();
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.HealthChanged -= HandleHealthChanged;
            health.Died -= HandleDeath;
        }
        ChangeState(IsDead ? Boss2CombatState.Dead : Boss2CombatState.Disabled);
        CancelAllPatterns();
        StopParts();
    }

    private void Update()
    {
        // Callbacks may remove runners or request new ones during a tick.
        updateSnapshot.AddRange(executions);
        try
        {
            foreach (Boss2PatternRunner runner in updateSnapshot)
            {
                if (runner.Pattern == null || !runner.Pattern.isActiveAndEnabled || !HasActiveParts(runner.HeldParts))
                    runner.Cancel();
                else if (!HitStopManager.IsActive && Time.timeScale > 0f) runner.Tick();
            }
        }
        finally { updateSnapshot.Clear(); }
    }

    public bool BeginCombat()
    {
        if (!isActiveAndEnabled || IsDead || health.CurrentHp <= 0) return false;
        if (CurrentState == Boss2CombatState.Fighting) return true;
        foreach (Boss2PartId id in PartIds) GetPart(id)?.FollowIdle();
        HandleHealthChanged(health.CurrentHp, health.MaxHp);
        ChangeState(Boss2CombatState.Fighting);
        return true;
    }

    public void EndCombat()
    {
        if (IsDead) return;
        ChangeState(Boss2CombatState.Inactive);
        CancelAllPatterns();
        StopParts();
    }

    internal bool BeginGroggyReturn()
    {
        if (CurrentState != Boss2CombatState.Fighting || IsDead) return false;
        ChangeState(Boss2CombatState.ReturningForGroggy);
        foreach (Boss2PatternRunner runner in executions.ToArray()) runner.RequestFinish();
        return true;
    }

    internal bool EnterGroggy()
    {
        if (CurrentState != Boss2CombatState.ReturningForGroggy || executions.Count != 0 ||
            !ArePartsReady(Boss2Parts.All)) return false;
        var movement = GetComponent<Boss2BodyMovement>();
        if (movement != null && movement.IsMoving) return false;
        ChangeState(Boss2CombatState.Groggy);
        return true;
    }

    internal void EndGroggy()
    {
        if (CurrentState == Boss2CombatState.Groggy || CurrentState == Boss2CombatState.ReturningForGroggy)
            ChangeState(Boss2CombatState.Fighting);
    }

    public bool TryRequestPattern(Boss2PatternBase pattern, out Boss2PatternRunner runner)
    {
        return TryRequestPattern(pattern, null, out runner);
    }

    internal bool TryRequestBasicPattern(Boss2BasicPatternBase pattern, Boss2Parts part, out Boss2PatternRunner runner)
    {
        if (pattern == null || !pattern.CanSchedulePart(part)) { runner = null; return false; }
        return TryRequestPattern(pattern, part, out runner);
    }

    private bool TryRequestPattern(Boss2PatternBase pattern, Boss2Parts? requestedParts, out Boss2PatternRunner runner)
    {
        runner = null;
        if (cancellingPatterns || CurrentState != Boss2CombatState.Fighting || !isActiveAndEnabled || IsDead ||
            HitStopManager.IsActive || Time.timeScale <= 0f || pattern == null ||
            !pattern.isActiveAndEnabled || pattern.Owner != this) return false;
        Boss2Parts requiredParts = requestedParts ?? pattern.RequiredParts;
        if (!HasActiveParts(requiredParts | pattern.PauseBasicParts)) return false;
        if (pattern.Kind == Boss2PatternKind.Main && HasMainPattern) return false;
        if (!partUsage.TryReserve(requiredParts, pattern.Kind, pattern.PauseBasicParts,
            out Boss2PartUsage.Reservation reservation)) return false;

        runner = new Boss2PatternRunner(this, pattern, partUsage, reservation);
        runner.Started += HandlePatternStarted;
        runner.Finished += HandlePatternFinished;
        executions.Add(runner);
        return true;
    }

    public void CancelPattern(Boss2PatternBase pattern)
    {
        foreach (Boss2PatternRunner runner in executions.ToArray())
            if (runner.Pattern == pattern) runner.Cancel();
    }

    public void CancelAllPatterns()
    {
        if (cancellingPatterns) return;
        cancellingPatterns = true;
        try
        {
            foreach (Boss2PatternRunner runner in executions.ToArray()) runner.Cancel();
        }
        finally { cancellingPatterns = false; }
    }

    public Boss2PartController GetPart(Boss2PartId id)
    {
        switch (id)
        {
            case Boss2PartId.UpperLeftHand: return upperLeftHand;
            case Boss2PartId.UpperRightHand: return upperRightHand;
            case Boss2PartId.LowerLeftHand: return lowerLeftHand;
            case Boss2PartId.LowerRightHand: return lowerRightHand;
            case Boss2PartId.Ring: return ring;
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
    }

    public bool HasActiveParts(Boss2Parts parts)
    {
        if (!Boss2PartMask.IsValid(parts)) return false;
        foreach (Boss2PartId id in PartIds)
        {
            if ((parts & Boss2PartMask.FromId(id)) == 0) continue;
            Boss2PartController part = GetPart(id);
            if (part == null || !part.isActiveAndEnabled || part.PartId != id) return false;
        }
        return true;
    }

    public bool ArePartsReady(Boss2Parts parts)
    {
        if (!HasActiveParts(parts)) return false;
        foreach (Boss2PartId id in PartIds)
            if ((parts & Boss2PartMask.FromId(id)) != 0 && !GetPart(id).IsReady) return false;
        return true;
    }

    private void HandleHealthChanged(int current, int maximum)
    {
        if (current <= 0 || maximum <= 0 || CurrentPhase == Boss2Phase.Two ||
            (float)current / maximum > phaseTwoHealthRatio) return;
        CurrentPhase = Boss2Phase.Two;
        PhaseChanged?.Invoke(CurrentPhase);
    }

    private void HandleDeath()
    {
        ChangeState(Boss2CombatState.Dead);
        foreach (Boss2PatternRunner runner in executions.ToArray()) runner.BeginDeathReturn();
    }

    private void StopParts()
    {
        foreach (Boss2PartId id in PartIds) GetPart(id)?.StopMovement();
    }

    private void HandlePatternStarted(Boss2PatternRunner runner) => PatternStarted?.Invoke(runner);

    private void HandlePatternFinished(Boss2PatternRunner runner)
    {
        executions.Remove(runner);
        runner.Started -= HandlePatternStarted;
        runner.Finished -= HandlePatternFinished;
        if (runner.Error != null) Debug.LogWarning($"Boss 2 pattern failed: {runner.Error}", this);
        PatternFinished?.Invoke(runner);
    }

    private void ChangeState(Boss2CombatState next)
    {
        if (CurrentState == next) return;
        Boss2CombatState previous = CurrentState;
        CurrentState = next;
        StateChanged?.Invoke(previous, next);
    }
}

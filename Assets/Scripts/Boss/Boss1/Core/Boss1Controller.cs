using System;
using System.Collections;
using UnityEngine;

public enum Boss1State
{
    Idle,
    ExecutingPattern,
    Dead,
    Disabled
}

/// <summary>Boss brain: state and scene references. The runner owns pattern execution.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BossHealth))]
public class Boss1Controller : MonoBehaviour
{
    [Header("Hands")]
    [SerializeField] private Boss1HandController leftHand;
    [SerializeField] private Boss1HandController rightHand;

    private readonly Boss1PatternRunner runner = new Boss1PatternRunner();
    private BossHealth bossHealth;
    private Boss1State currentState = Boss1State.Idle;
    private Boss1PatternBase currentPattern;
    private bool publishingResult;

    public event Action<Boss1State, Boss1State> StateChanged;
    public event Action<Boss1PatternBase> PatternStarted;
    public event Action<Boss1PatternBase> PatternPresented;
    public event Action<Boss1PatternBase, Boss1PatternResult> PatternFinished;

    public Boss1State CurrentState => currentState;
    public Boss1PatternBase CurrentPattern => currentPattern;
    public Boss1PatternResult? LastPatternResult { get; private set; }
    public string LastPatternError { get; private set; }
    public Boss1HandController LeftHand => leftHand;
    public Boss1HandController RightHand => rightHand;
    public bool HasBothHands => leftHand != null && rightHand != null;
    public bool AreHandsBusy => (leftHand != null && leftHand.IsBusy) || (rightHand != null && rightHand.IsBusy);
    public bool IsIdle => currentState == Boss1State.Idle;
    public bool IsExecutingPattern => runner.IsRunning;
    public bool IsDead => currentState == Boss1State.Dead || (bossHealth != null && bossHealth.IsDead);

    private void Awake()
    {
        bossHealth = GetComponent<BossHealth>();
        foreach (Boss1HandController hand in GetComponentsInChildren<Boss1HandController>(true))
        {
            if (hand.HandSide == Boss1HandSide.Left && leftHand == null) leftHand = hand;
            else if (hand.HandSide == Boss1HandSide.Right && rightHand == null) rightHand = hand;
        }
        runner.Presented += HandlePatternPresented;
        runner.Finished += HandlePatternFinished;
    }

    private void OnEnable()
    {
        bossHealth.Died += HandleBossDied;
        ChangeState(bossHealth.IsDead ? Boss1State.Dead : Boss1State.Idle);
    }

    private void Update()
    {
        if (runner.IsRunning && (currentPattern == null || !currentPattern.isActiveAndEnabled ||
            !HasBothHands || !leftHand.isActiveAndEnabled || !rightHand.isActiveAndEnabled))
            CancelCurrentPattern();
        if (!HitStopManager.IsActive) runner.Tick();
    }

    private void OnDisable()
    {
        if (bossHealth != null) bossHealth.Died -= HandleBossDied;
        ChangeState(IsDead ? Boss1State.Dead : Boss1State.Disabled);
        CancelCurrentPattern();
    }

    public bool TryStartPattern(Boss1PatternBase pattern)
    {
        if (HitStopManager.IsActive || !isActiveAndEnabled || IsDead || !IsIdle || runner.IsRunning || publishingResult ||
            pattern == null || !pattern.isActiveAndEnabled || pattern.Owner != this)
            return false;

        Boss1PatternContext context = new Boss1PatternContext(this, leftHand, rightHand);
        Boss1PatternExecution execution;
        try
        {
            if (!HasBothHands || !leftHand.isActiveAndEnabled || !rightHand.isActiveAndEnabled)
                throw new InvalidOperationException("Boss needs two active hands.");
            execution = pattern.CreateExecution(context);
            if (execution == null) throw new InvalidOperationException("Pattern factory returned no execution.");
        }
        catch (Exception error)
        {
            PublishResult(pattern, Boss1PatternResult.Failed, error);
            return false;
        }

        currentPattern = pattern;
        runner.TryStart(execution, context);
        // A factory/Execute failure can finish synchronously.
        if (!runner.IsRunning) return false;
        ChangeState(Boss1State.ExecutingPattern);
        if (!runner.IsRunning) return false;
        PatternStarted?.Invoke(pattern);
        return true;
    }

    public bool CancelCurrentPattern()
    {
        bool cancelled = runner.Cancel();
        if (!runner.IsRunning) ResetHands();
        return cancelled;
    }

    public IEnumerator WaitForHands()
    {
        while (AreHandsBusy) yield return null;
    }

    private void HandlePatternPresented() => PatternPresented?.Invoke(currentPattern);

    private void HandlePatternFinished(Boss1PatternResult result, Exception error)
    {
        Boss1PatternBase finishedPattern = currentPattern;
        currentPattern = null;
        if (result != Boss1PatternResult.Completed) ResetHands();
        ChangeState(IsDead ? Boss1State.Dead : (isActiveAndEnabled ? Boss1State.Idle : Boss1State.Disabled));
        PublishResult(finishedPattern, result, error);
    }

    private void PublishResult(Boss1PatternBase pattern, Boss1PatternResult result, Exception error)
    {
        LastPatternResult = result;
        LastPatternError = error?.Message;
        if (error != null) Debug.LogWarning($"Boss pattern failed: {error}", this);
        publishingResult = true;
        try { PatternFinished?.Invoke(pattern, result); }
        finally { publishingResult = false; }
    }

    private void HandleBossDied()
    {
        ChangeState(Boss1State.Dead);
        CancelCurrentPattern();
    }

    private void ChangeState(Boss1State nextState)
    {
        if (currentState == nextState) return;
        Boss1State previousState = currentState;
        currentState = nextState;
        StateChanged?.Invoke(previousState, nextState);
    }

    private void ResetHands()
    {
        // Death keeps the interrupted pose; the body animation drives hand shaking and falling.
        if (IsDead)
        {
            leftHand?.StopMovement();
            rightHand?.StopMovement();
            return;
        }
        leftHand?.ResetState();
        rightHand?.ResetState();
    }
}

using UnityEngine;

public enum Boss1PatternSelectionMode
{
    Sequential,
    Random
}

/// <summary>Schedules requests; selection and execution have separate owners.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Boss1Controller))]
public class Boss1PatternScheduler : MonoBehaviour
{
    [SerializeField] private Boss1Controller controller;
    [SerializeField] private Boss1PatternBase encounterPattern;
    [SerializeField] private Boss1PatternBase[] patterns;
    [SerializeField] private Boss1PatternSchedulerSettings settings;

    private readonly Boss1PatternSelector selector = new Boss1PatternSelector();
    private float nextPatternTime;
    private bool hasStarted;
    private bool encounterPatternCompleted;
    private bool refillingDebris;
    private Boss1DebrisField debrisField;
    private PlayerHealth playerHealth;
    public bool IsRunning { get; private set; }
    public bool IsDebrisRefillPending { get; private set; }

    private void Reset() => ResolveReferences();
    private void Awake() => ResolveReferences();

    private void OnEnable()
    {
        ResolveReferences();
        controller.StateChanged += HandleBossStateChanged;
        controller.PatternPresented += HandlePatternPresented;
        controller.PatternFinished += HandlePatternFinished;
        if (playerHealth != null) playerHealth.Died += HandlePlayerUnavailable;
        if (hasStarted && settings != null && settings.AutoStart) StartScheduling();
    }

    private void Start()
    {
        // Wait until scene components have finished Awake and OnEnable.
        hasStarted = true;
        if (settings != null && settings.AutoStart) StartScheduling();
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.Died -= HandlePlayerUnavailable;
        if (controller != null)
        {
            controller.StateChanged -= HandleBossStateChanged;
            controller.PatternPresented -= HandlePatternPresented;
            controller.PatternFinished -= HandlePatternFinished;
            if (refillingDebris || controller.CurrentPattern is Boss1OverdrivePattern)
                controller.CancelCurrentPattern();
        }
        refillingDebris = false;
        StopScheduling();
    }

    public bool StartScheduling()
    {
        if (IsRunning || !isActiveAndEnabled || controller == null ||
            !controller.isActiveAndEnabled || controller.IsDead || settings == null || !IsPlayerAvailable)
            return false;

        nextPatternTime = encounterPatternCompleted ? Time.time + settings.InitialDelay : Time.time;
        IsRunning = true;
        return true;
    }

    public void StopScheduling()
    {
        IsRunning = false;
        CancelDebrisRefill();
    }

    public void ReserveDebrisRefill()
    {
        if (!isActiveAndEnabled || controller == null || controller.IsDead || !IsPlayerAvailable
            || debrisField == null || !debrisField.isActiveAndEnabled
            || debrisField.HasLandedDebris || debrisField.HasPendingDebris
            || !(encounterPattern is Boss1SlamPattern) || encounterPattern.Owner != controller)
            return;
        IsDebrisRefillPending = true;
    }

    public void CancelDebrisRefill() => IsDebrisRefillPending = false;

    public void StopSchedulingAndCancelPattern()
    {
        StopScheduling();
        controller?.CancelCurrentPattern();
    }

    private void Update()
    {
        if (!IsPlayerAvailable)
        {
            if (IsRunning || IsDebrisRefillPending || controller.IsExecutingPattern
                || (debrisField != null && (debrisField.HasLandedDebris || debrisField.HasPendingDebris)))
                HandlePlayerUnavailable();
            return;
        }
        if (HitStopManager.IsActive || !IsRunning || !controller.IsIdle || !controller.isActiveAndEnabled || Time.time < nextPatternTime) return;
        Boss1PatternBase pattern = !encounterPatternCompleted || IsDebrisRefillPending
            ? encounterPattern
            : selector.Select(patterns, settings.SelectionMode, controller);
        if (pattern == null || !pattern.isActiveAndEnabled || pattern.Owner != controller) return;
        // A reserved refill uses the encounter path, independently of the normal pattern cooldown.
        refillingDebris = IsDebrisRefillPending;
        // Failed requests also wait before retrying.
        nextPatternTime = Time.time + settings.GetRandomPatternInterval();
        if (!controller.TryStartPattern(pattern)) refillingDebris = false;
    }

    private void HandlePatternPresented(Boss1PatternBase pattern)
    {
        if (pattern != null && pattern.Owner == controller) selector.MarkPresented(pattern);
    }

    private void HandlePatternFinished(Boss1PatternBase pattern, Boss1PatternResult result)
    {
        if (pattern == encounterPattern && result == Boss1PatternResult.Completed)
            encounterPatternCompleted = true;
        if (refillingDebris)
        {
            if (result != Boss1PatternResult.Completed
                || (debrisField != null && debrisField.HasLandedDebris && !debrisField.HasPendingDebris))
                CancelDebrisRefill();
            refillingDebris = false;
        }
        pattern?.StartCooldown();
        if (settings != null) nextPatternTime = Time.time + settings.GetRandomPatternInterval();
    }

    private void HandleBossStateChanged(Boss1State previous, Boss1State next)
    {
        if (next == Boss1State.Dead) selector.ResetPresentationHistory();
        if (next == Boss1State.Dead || next == Boss1State.Disabled) StopScheduling();
        else if (previous == Boss1State.Disabled && settings != null && settings.AutoStart) StartScheduling();
    }

    private void ResolveReferences()
    {
        if (controller == null) controller = GetComponent<Boss1Controller>();
        if (debrisField == null) debrisField = GetComponent<Boss1DebrisField>();
        if (playerHealth == null && encounterPattern is Boss1SlamPattern slam) playerHealth = slam.Target;
    }

    private bool IsPlayerAvailable => !(encounterPattern is Boss1SlamPattern)
        || (playerHealth != null && playerHealth.isActiveAndEnabled && !playerHealth.IsDead);

    private void HandlePlayerUnavailable()
    {
        StopSchedulingAndCancelPattern();
        selector.ResetPresentationHistory();
        debrisField?.Clear();
    }
}

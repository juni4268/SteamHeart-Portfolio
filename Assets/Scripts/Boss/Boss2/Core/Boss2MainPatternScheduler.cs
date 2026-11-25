using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Automatically requests one main pattern at a time, including its wait and return phases.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Boss2Controller))]
public sealed class Boss2MainPatternScheduler : MonoBehaviour
{
    [Serializable]
    private sealed class PatternEntry
    {
        [SerializeField] private Boss2PatternBase pattern;

        public Boss2PatternBase Pattern => pattern;
        public float RemainingCooldown { get; set; }
    }

    [SerializeField] private PlayerHealth target;
    [SerializeField, Min(0f)] private float initialDelay = 3f;
    [Tooltip("Minimum wait after a main pattern finishes before requesting the next main pattern.")]
    [SerializeField, Min(0f)] private float patternInterval = 5f;
    [Tooltip("Multiplier for the wait between main patterns in phase two.")]
    [SerializeField, Range(0f, 1f)] private float phaseTwoIntervalMultiplier = 0.5f;
    [SerializeField] private PatternEntry[] patterns = Array.Empty<PatternEntry>();

    private readonly List<PatternEntry> candidates = new List<PatternEntry>();
    private Boss2Controller owner;
    private float remainingDelay;

    private void OnEnable()
    {
        owner = GetComponent<Boss2Controller>();
        owner.StateChanged += HandleStateChanged;
        owner.PatternFinished += HandlePatternFinished;
        ResetTimers();
    }

    private void OnDisable()
    {
        if (owner == null) return;
        owner.StateChanged -= HandleStateChanged;
        owner.PatternFinished -= HandlePatternFinished;
    }

    private void Update() => Tick(Time.deltaTime);

    private void Tick(float deltaTime)
    {
        if (!isActiveAndEnabled || owner.CurrentState != Boss2CombatState.Fighting || owner.IsDead ||
            target == null || !target.isActiveAndEnabled || target.IsDead ||
            HitStopManager.IsActive || Time.timeScale <= 0f) return;

        foreach (PatternEntry entry in patterns)
            if (entry != null) entry.RemainingCooldown = Mathf.Max(0f, entry.RemainingCooldown - deltaTime);
        if (owner.HasMainPattern) return;
        remainingDelay = Mathf.Max(0f, remainingDelay - deltaTime);
        if (remainingDelay > 0f) return;

        candidates.Clear();
        foreach (PatternEntry entry in patterns)
        {
            if (entry == null || entry.Pattern == null || !entry.Pattern.isActiveAndEnabled ||
                entry.Pattern.Owner != owner || entry.Pattern.Kind != Boss2PatternKind.Main ||
                entry.RemainingCooldown > 0f) continue;
            candidates.Add(entry);
        }
        while (candidates.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            PatternEntry entry = candidates[index];
            candidates.RemoveAt(index);
            if (owner.TryRequestPattern(entry.Pattern, out _)) return;
        }
    }

    private void HandlePatternFinished(Boss2PatternRunner runner)
    {
        if (runner.Pattern == null || runner.Pattern.Kind != Boss2PatternKind.Main ||
            owner.CurrentState != Boss2CombatState.Fighting) return;
        remainingDelay = patternInterval * (owner.CurrentPhase == Boss2Phase.Two ? phaseTwoIntervalMultiplier : 1f);
        foreach (PatternEntry entry in patterns)
            if (entry != null && entry.Pattern == runner.Pattern)
                entry.RemainingCooldown = entry.Pattern.SampleCooldown();
    }

    private void HandleStateChanged(Boss2CombatState previous, Boss2CombatState next) => ResetTimers();

    private void ResetTimers()
    {
        remainingDelay = initialDelay;
        foreach (PatternEntry entry in patterns)
            if (entry != null) entry.RemainingCooldown = 0f;
        candidates.Clear();
    }
}

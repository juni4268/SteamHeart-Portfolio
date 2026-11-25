using System.Collections.Generic;
using UnityEngine;

/// <summary>Per-boss selection state; excludes disabled and foreign scene bindings.</summary>
public sealed class Boss1PatternSelector
{
    private int nextIndex;
    private Boss1PatternBase lastRandomPattern;
    private readonly HashSet<Boss1PatternBase> presentedPatterns = new HashSet<Boss1PatternBase>();

    public void MarkPresented(Boss1PatternBase pattern)
    {
        if (pattern != null) presentedPatterns.Add(pattern);
    }

    public void ResetPresentationHistory() => presentedPatterns.Clear();

    public Boss1PatternBase Select(Boss1PatternBase[] patterns, Boss1PatternSelectionMode mode,
        Boss1Controller owner)
    {
        if (patterns == null || patterns.Length == 0) return null;
        if (mode == Boss1PatternSelectionMode.Random)
        {
            // Overdrive requests a refill Slam; show every other active pattern before that sequence.
            bool deferOverdrive = false;
            foreach (Boss1PatternBase pattern in patterns)
            {
                if (pattern != null && pattern.isActiveAndEnabled && pattern.Owner == owner
                    && !(pattern is Boss1OverdrivePattern) && !presentedPatterns.Contains(pattern))
                {
                    deferOverdrive = true;
                    break;
                }
            }

            bool preferUnseen = false;
            foreach (Boss1PatternBase pattern in patterns)
            {
                if (IsCandidate(pattern, owner, true, deferOverdrive))
                {
                    preferUnseen = true;
                    break;
                }
            }

            bool hasAlternative = false;
            foreach (Boss1PatternBase pattern in patterns)
            {
                if (IsCandidate(pattern, owner, preferUnseen, deferOverdrive) && pattern != lastRandomPattern)
                {
                    hasAlternative = true;
                    break;
                }
            }

            Boss1PatternBase selected = null;
            int eligibleCount = 0;
            foreach (Boss1PatternBase pattern in patterns)
            {
                if (!IsCandidate(pattern, owner, preferUnseen, deferOverdrive) || (hasAlternative && pattern == lastRandomPattern))
                    continue;

                if (Random.Range(0, ++eligibleCount) == 0)
                    selected = pattern;
            }

            if (selected != null) lastRandomPattern = selected;
            return selected;
        }

        for (int offset = 0; offset < patterns.Length; offset++)
        {
            int index = (nextIndex + offset) % patterns.Length;
            if (!IsEligible(patterns[index], owner)) continue;
            nextIndex = (index + 1) % patterns.Length;
            return patterns[index];
        }
        return null;
    }

    private bool IsCandidate(Boss1PatternBase pattern, Boss1Controller owner, bool preferUnseen, bool deferOverdrive) =>
        !(deferOverdrive && pattern is Boss1OverdrivePattern)
        && IsEligible(pattern, owner) && (!preferUnseen || !presentedPatterns.Contains(pattern));

    private static bool IsEligible(Boss1PatternBase pattern, Boss1Controller owner)
    {
        return pattern != null && pattern.isActiveAndEnabled && !pattern.IsOnCooldown
            && pattern.Owner == owner
            && (!(pattern is Boss1OverdrivePattern overdrive) || overdrive.CanStart);
    }
}

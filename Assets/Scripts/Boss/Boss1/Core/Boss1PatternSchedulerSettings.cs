using UnityEngine;

[CreateAssetMenu(
    fileName = "Boss1PatternSchedulerSettings",
    menuName = "Boss/Boss1 Pattern Scheduler Settings")]
public sealed class Boss1PatternSchedulerSettings : ScriptableObject
{
    [Header("Schedule")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private Boss1PatternSelectionMode selectionMode = Boss1PatternSelectionMode.Random;
    [SerializeField, Min(0f)] private float initialDelay = 0.5f;

    [Header("Pattern Interval")]
    [SerializeField, Min(0f)] private float minPatternInterval = 3f;
    [SerializeField, Min(0f)] private float maxPatternInterval = 5f;

    public bool AutoStart => autoStart;
    public Boss1PatternSelectionMode SelectionMode => selectionMode;
    public float InitialDelay => initialDelay;
    public float MinPatternInterval => minPatternInterval;
    public float MaxPatternInterval => maxPatternInterval;

    public float GetRandomPatternInterval()
    {
        return Random.Range(minPatternInterval, maxPatternInterval);
    }

    private void OnValidate()
    {
        initialDelay = Mathf.Max(0f, initialDelay);
        minPatternInterval = Mathf.Max(0f, minPatternInterval);
        maxPatternInterval = Mathf.Max(minPatternInterval, maxPatternInterval);
    }
}

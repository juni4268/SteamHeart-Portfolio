using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2MainPatternSchedulerTests
{
    private GameObject root;
    private Boss2Controller boss;
    private Boss2MainPatternScheduler scheduler;
    private PlayerHealth target;
    private float originalTimeScale;
    private Random.State originalRandomState;

    private static void Set(object target, System.Type type, string field, object value) =>
        type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    [SetUp]
    public void SetUp()
    {
        originalTimeScale = Time.timeScale;
        originalRandomState = Random.state;
        Time.timeScale = 1f;
        root = new GameObject("Boss2 main scheduler test");
        boss = root.AddComponent<Boss2Controller>();
        var health = root.GetComponent<BossHealth>();
        Set(health, typeof(BossHealth), "currentHp", health.MaxHp);
        Set(boss, typeof(Boss2Controller), "health", health);
        string[] fields = { "upperLeftHand", "upperRightHand", "lowerLeftHand", "lowerRightHand", "ring" };
        for (int i = 0; i < fields.Length; i++)
        {
            var go = new GameObject(fields[i]);
            go.transform.SetParent(root.transform);
            var part = go.AddComponent<Boss2PartController>();
            Set(part, typeof(Boss2PartController), "partId", (Boss2PartId)i);
            Set(boss, typeof(Boss2Controller), fields[i], part);
        }
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        player.transform.SetParent(root.transform);
        target = player.GetComponent<PlayerHealth>();
        scheduler = root.AddComponent<Boss2MainPatternScheduler>();
        typeof(Boss2MainPatternScheduler).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(scheduler, null);
    }

    [TearDown]
    public void TearDown()
    {
        boss.EndCombat();
        typeof(Boss2MainPatternScheduler).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(scheduler, null);
        Object.DestroyImmediate(root);
        Time.timeScale = originalTimeScale;
        Random.state = originalRandomState;
    }

    private Boss2FoundationTestPattern Pattern(Boss2Parts parts, Boss2PatternKind kind = Boss2PatternKind.Main)
    {
        var pattern = root.AddComponent<Boss2FoundationTestPattern>();
        Set(pattern, typeof(Boss2PatternBase), "requiredParts", parts);
        Set(pattern, typeof(Boss2PatternBase), "kind", kind);
        pattern.Factory = _ => new EmptyExecution();
        return pattern;
    }

    private void Configure(float cooldown, params Boss2PatternBase[] patterns)
    {
        var serialized = new SerializedObject(scheduler);
        serialized.FindProperty("target").objectReferenceValue = target;
        serialized.FindProperty("initialDelay").floatValue = 0.5f;
        serialized.FindProperty("patternInterval").floatValue = 1f;
        var entries = serialized.FindProperty("patterns");
        entries.arraySize = patterns.Length;
        for (int i = 0; i < patterns.Length; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("pattern").objectReferenceValue = patterns[i];
            var patternSettings = new SerializedObject(patterns[i]);
            patternSettings.FindProperty("minCooldown").floatValue = cooldown;
            patternSettings.FindProperty("maxCooldown").floatValue = cooldown;
            patternSettings.ApplyModifiedPropertiesWithoutUndo();
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(boss.BeginCombat(), Is.True);
    }

    private void Advance(float seconds) => typeof(Boss2MainPatternScheduler)
        .GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(scheduler, new object[] { seconds });

    [Test]
    public void InitialDelayAndCooldownAreMeasuredFromCombatStartAndPatternFinish()
    {
        var pattern = Pattern(Boss2Parts.Ring);
        Configure(2f, pattern);
        Advance(0.25f);
        Assert.That(boss.Executions, Is.Empty);
        Advance(0.3f);
        Assert.That(boss.Executions.Count, Is.EqualTo(1));
        var runner = boss.Executions[0];
        Advance(10f);
        Assert.That(boss.Executions.Count, Is.EqualTo(1), "Waiting for parts must also block the next main request.");
        typeof(Boss2PatternRunner).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(runner, null);
        Assert.That(runner.Result, Is.EqualTo(Boss2PatternResult.Completed));
        Advance(1f);
        Assert.That(boss.Executions, Is.Empty);
        Advance(1.1f);
        Assert.That(boss.Executions.Count, Is.EqualTo(1));
    }

    [Test]
    public void RandomCooldownIsResampledAfterEachCompletion()
    {
        var pattern = Pattern(Boss2Parts.Ring);
        Configure(0f, pattern);
        Set(pattern, typeof(Boss2PatternBase), "minCooldown", 2f);
        Set(pattern, typeof(Boss2PatternBase), "maxCooldown", 4f);
        Random.InitState(1976);

        float CompletePattern()
        {
            typeof(Boss2PatternRunner).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(boss.Executions[0], null);
            var entries = (System.Array)typeof(Boss2MainPatternScheduler)
                .GetField("patterns", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scheduler);
            var entry = entries.GetValue(0);
            return (float)entry.GetType().GetProperty("RemainingCooldown").GetValue(entry);
        }

        Advance(1f);
        float first = CompletePattern();
        Assert.That(first, Is.InRange(2f, 4f));
        Advance(first - 0.01f);
        Assert.That(boss.Executions, Is.Empty);
        Advance(0.02f);
        Assert.That(boss.Executions.Count, Is.EqualTo(1));
        float second = CompletePattern();
        Assert.That(second, Is.InRange(2f, 4f));
        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public void ManualMainRequestAlsoBlocksAutomaticMainRequestsButAllowsBasicAttacks()
    {
        var automatic = Pattern(Boss2Parts.Ring);
        Configure(0f, automatic);
        var manual = Pattern(Boss2Parts.UpperHands);
        Assert.That(boss.TryRequestPattern(manual, out var main), Is.True);
        var basic = Pattern(Boss2Parts.LowerLeftHand, Boss2PatternKind.Basic);
        Assert.That(boss.TryRequestPattern(basic, out _), Is.True);
        Advance(10f);
        Assert.That(boss.Executions.Count, Is.EqualTo(2));
        main.Cancel();
        Advance(0.5f);
        Assert.That(boss.HasMainPattern, Is.False);
        Advance(0.6f);
        Assert.That(boss.HasMainPattern, Is.True);
        Assert.That(boss.Executions.Count, Is.EqualTo(2));
    }

    [Test]
    public void UnavailablePatternDoesNotBlockAnotherEligiblePattern()
    {
        var unavailable = Pattern(Boss2Parts.UpperHands);
        var available = Pattern(Boss2Parts.Ring);
        boss.GetPart(Boss2PartId.UpperLeftHand).enabled = false;
        Configure(0f, unavailable, available);
        Advance(1f);
        Assert.That(boss.Executions.Count, Is.EqualTo(1));
        Assert.That(boss.Executions[0].Pattern, Is.SameAs(available));
    }

    [Test]
    public void DeadTargetAndPauseFreezeTimersAndNewCombatResetsThem()
    {
        Configure(10f, Pattern(Boss2Parts.Ring));
        Set(target, typeof(PlayerHealth), "isDead", true);
        Advance(100f);
        Set(target, typeof(PlayerHealth), "isDead", false);
        Time.timeScale = 0f;
        Advance(100f);
        Time.timeScale = 1f;
        Advance(0.25f);
        Assert.That(boss.Executions, Is.Empty);
        Advance(0.3f);
        Assert.That(boss.Executions.Count, Is.EqualTo(1));
        boss.EndCombat();
        Assert.That(boss.Executions, Is.Empty);
        Assert.That(boss.BeginCombat(), Is.True);
        Advance(0.25f);
        Assert.That(boss.Executions, Is.Empty);
        Advance(0.3f);
        Assert.That(boss.Executions.Count, Is.EqualTo(1));
    }

    private sealed class EmptyExecution : Boss2PatternExecution
    {
        public override IEnumerator ReturnAfterDeath(Boss2PatternContext context) { yield break; }
        public override IEnumerator Execute(Boss2PatternContext context) { yield break; }
        public override void Cleanup(Boss2PatternResult result) { }
    }
}

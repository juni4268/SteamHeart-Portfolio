using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss1PatternPresentationTests
{
    private GameObject root;
    private Boss1Controller boss;
    private Boss1PatternSelector selector;
    private Random.State originalRandomState;
    private Boss1OverdriveConfig overdriveConfig;

    [SetUp]
    public void SetUp()
    {
        originalRandomState = Random.state;
        root = new GameObject("Boss1 presentation test") { hideFlags = HideFlags.HideAndDontSave };
        boss = root.AddComponent<Boss1Controller>();
        selector = new Boss1PatternSelector();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        if (overdriveConfig != null) Object.DestroyImmediate(overdriveConfig);
        Random.state = originalRandomState;
    }

    private Boss1PresentationTestPattern Pattern() => root.AddComponent<Boss1PresentationTestPattern>();

    private Boss1PatternBase Select(params Boss1PatternBase[] patterns) =>
        selector.Select(patterns, Boss1PatternSelectionMode.Random, boss);

    private static void Set(object target, System.Type type, string field, object value) =>
        type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private Transform Point(string name, Vector2 position)
    {
        var point = new GameObject(name).transform;
        point.SetParent(root.transform);
        point.position = position;
        return point;
    }

    private Boss1OverdrivePattern Overdrive()
    {
        var pattern = root.AddComponent<Boss1OverdrivePattern>();
        var field = root.GetComponent<Boss1OverdriveField>();
        var debris = root.GetComponent<Boss1DebrisField>();
        Set(field, typeof(Boss1OverdriveField), "leftOverdriveStartPoint", Point("Left start", new Vector2(-5f, 3f)));
        Set(field, typeof(Boss1OverdriveField), "rightOverdriveStartPoint", Point("Right start", new Vector2(5f, 3f)));
        Set(field, typeof(Boss1OverdriveField), "leftPunchPullbackPoint", Point("Left pullback", new Vector2(-7f, 3f)));
        Set(field, typeof(Boss1OverdriveField), "rightPunchPullbackPoint", Point("Right pullback", new Vector2(7f, 3f)));
        Set(field, typeof(Boss1OverdriveField), "leftPunchExitPoint", Point("Left exit", new Vector2(-12f, 3f)));
        Set(field, typeof(Boss1OverdriveField), "rightPunchExitPoint", Point("Right exit", new Vector2(12f, 3f)));
        Set(field, typeof(Boss1OverdriveField), "slamExitPoint", Point("Slam exit", new Vector2(0f, -6f)));
        Set(field, typeof(Boss1OverdriveField), "leftWall", Point("Left wall", new Vector2(-10f, 0f)).gameObject.AddComponent<BoxCollider2D>());
        Set(field, typeof(Boss1OverdriveField), "rightWall", Point("Right wall", new Vector2(10f, 0f)).gameObject.AddComponent<BoxCollider2D>());
        var body = Point("Body", Vector2.zero);
        Set(field, typeof(Boss1OverdriveField), "bodyRoot", body);
        Set(field, typeof(Boss1OverdriveField), "bodyCollider", body.gameObject.AddComponent<BoxCollider2D>());
        Set(field, typeof(Boss1OverdriveField), "debrisField", debris);
        Set(debris, typeof(Boss1DebrisField), "floorCollider", Point("Floor", new Vector2(0f, -4f)).gameObject.AddComponent<BoxCollider2D>());
        overdriveConfig = ScriptableObject.CreateInstance<Boss1OverdriveConfig>();
        Set(pattern, typeof(Boss1OverdrivePattern), "config", overdriveConfig);
        Set(pattern, typeof(Boss1OverdrivePattern), "overdriveField", field);
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), root.transform);
        Set(pattern, typeof(Boss1OverdrivePattern), "playerHealth", player.GetComponent<PlayerHealth>());
        Physics2D.SyncTransforms();
        Assert.That(pattern.CanStart, Is.True);
        return pattern;
    }

    [Test]
    public void OverdriveIsLastInTheFirstPassAndThenReturnsToTheRandomPool()
    {
        var first = Pattern();
        var second = Pattern();
        var overdrive = Overdrive();
        var patterns = new Boss1PatternBase[] { overdrive, first, second };
        for (int i = 0; i < 2; i++)
        {
            var selected = Select(patterns);
            Assert.That(selected, Is.Not.SameAs(overdrive));
            selector.MarkPresented(selected);
        }
        Assert.That(Select(patterns), Is.SameAs(overdrive));
        selector.MarkPresented(overdrive);
        Assert.That(Select(patterns), Is.Not.SameAs(overdrive), "Keep the existing immediate-repeat exclusion.");
        Assert.That(Select(overdrive), Is.SameAs(overdrive), "Overdrive must remain available after the first pass.");
    }

    [Test]
    public void UnseenPatternOnCooldownStillDefersOverdriveButDisabledPatternDoesNot()
    {
        var seen = Pattern();
        var unseen = Pattern();
        var overdrive = Overdrive();
        selector.MarkPresented(seen);
        Set(unseen, typeof(Boss1PatternBase), "cooldownEndTime", Time.time + 100f);
        Assert.That(Select(overdrive, seen, unseen), Is.SameAs(seen));
        Set(unseen, typeof(Boss1PatternBase), "cooldownEndTime", 0f);
        Assert.That(Select(overdrive, seen, unseen), Is.SameAs(unseen));
        unseen.enabled = false;
        Assert.That(Select(overdrive, seen, unseen), Is.SameAs(overdrive));
    }

    [Test]
    public void SelectingWithoutPresentingKeepsTheLastUnseenPatternFirst()
    {
        var seen = Pattern();
        var unseen = Pattern();
        selector.MarkPresented(seen);
        Assert.That(Select(seen, unseen), Is.SameAs(unseen));
        Assert.That(Select(seen, unseen), Is.SameAs(unseen), "Selection alone must not count as exposure.");
        selector.MarkPresented(unseen);
        Assert.That(Select(seen, unseen), Is.SameAs(seen), "After exposure, resume the existing no-repeat random policy.");
    }

    [Test]
    public void UnavailableUnseenPatternsDoNotBlockSeenPatterns()
    {
        var seen = Pattern();
        var unseen = Pattern();
        selector.MarkPresented(seen);
        unseen.enabled = false;
        Assert.That(Select(seen, unseen), Is.SameAs(seen));
        unseen.enabled = true;
        typeof(Boss1PatternBase).GetField("cooldownEndTime", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(unseen, Time.time + 100f);
        Assert.That(Select(seen, unseen), Is.SameAs(seen));
        typeof(Boss1PatternBase).GetField("cooldownEndTime", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(unseen, 0f);
        Assert.That(Select(seen, unseen), Is.SameAs(unseen));
    }

    [Test]
    public void SequentialSelectionKeepsItsConfiguredOrder()
    {
        var first = Pattern();
        var second = Pattern();
        selector.MarkPresented(first);
        var patterns = new Boss1PatternBase[] { first, second };
        Assert.That(selector.Select(patterns, Boss1PatternSelectionMode.Sequential, boss), Is.SameAs(first));
        Assert.That(selector.Select(patterns, Boss1PatternSelectionMode.Sequential, boss), Is.SameAs(second));
    }

    [Test]
    public void NewEncounterMakesPreviouslySeenPatternsUnseenAgain()
    {
        var first = Pattern();
        var second = Pattern();
        selector.MarkPresented(first);
        Assert.That(Select(first, second), Is.SameAs(second));
        selector.ResetPresentationHistory();
        var choices = new HashSet<Boss1PatternBase>();
        for (int i = 0; i < 4; i++) choices.Add(Select(first, second));
        Assert.That(choices, Is.EquivalentTo(new[] { first, second }));
    }

    [Test]
    public void PresentationIsReportedOnceAfterPreparationAndSurvivesCancellation()
    {
        var pattern = Pattern();
        var alternative = Pattern();
        var execution = new PresentationExecution();
        var runner = new Boss1PatternRunner();
        int presentations = 0;
        runner.Presented += () => { presentations++; selector.MarkPresented(pattern); };
        runner.TryStart(execution, null);
        runner.Tick();
        Assert.That(presentations, Is.Zero);
        runner.Tick();
        Assert.That(presentations, Is.EqualTo(1));
        runner.Cancel();
        Assert.That(presentations, Is.EqualTo(1));
        Assert.That(Select(pattern, alternative), Is.SameAs(alternative));
    }

    [Test]
    public void CancellingPreparationDetachesThePresentationSignal()
    {
        var runner = new Boss1PatternRunner();
        var cancelled = new PresentationExecution();
        int presentations = 0;
        runner.Presented += () => presentations++;
        runner.TryStart(cancelled, null);
        runner.Tick();
        runner.Cancel();
        cancelled.Present();
        Assert.That(presentations, Is.Zero);
        runner.TryStart(new PresentationExecution(), null);
        runner.Tick();
        runner.Tick();
        Assert.That(presentations, Is.EqualTo(1));
        runner.Cancel();
    }

    private sealed class PresentationExecution : Boss1PatternExecution
    {
        public void Present() => MarkPresented();
        public override IEnumerator Execute(Boss1PatternContext context)
        {
            yield return null;
            MarkPresented();
            MarkPresented();
            yield return null;
        }
        public override void Cleanup(Boss1PatternResult result) { }
    }
}

[AddComponentMenu("")]
public sealed class Boss1PresentationTestPattern : Boss1PatternBase
{
    public override Boss1PatternExecution CreateExecution(Boss1PatternContext context) => null;
}

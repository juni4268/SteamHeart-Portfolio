using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class Boss2BodyMovementTests
{
    private const float DeltaTime = 0.02f;
    private Scene scene;
    private GameObject root;
    private Boss2Controller boss;
    private Boss2BodyMovement movement;
    private Rigidbody2D body;
    private CapsuleCollider2D collider;
    private UnityEngine.Random.State randomState;
    private float timeScale;

    private static void Set(object target, Type type, string field, object value) =>
        type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

    [SetUp]
    public void SetUp()
    {
        randomState = UnityEngine.Random.state;
        timeScale = Time.timeScale;
        Time.timeScale = 1f;
        UnityEngine.Random.InitState(1234);
        scene = EditorSceneManager.NewPreviewScene();
        Assert.That(scene.GetPhysicsScene2D(), Is.Not.EqualTo(Physics2D.defaultPhysicsScene));
        root = new GameObject("Body movement test");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = new Vector3(0.5f, -3f);
        boss = root.AddComponent<Boss2Controller>();
        var health = root.GetComponent<BossHealth>();
        Set(health, typeof(BossHealth), "currentHp", health.MaxHp);
        Set(boss, typeof(Boss2Controller), "health", health);
        string[] fields = { "upperLeftHand", "upperRightHand", "lowerLeftHand", "lowerRightHand", "ring" };
        for (int i = 0; i < fields.Length; i++)
        {
            var child = new GameObject(fields[i]);
            child.transform.SetParent(root.transform);
            var part = child.AddComponent<Boss2PartController>();
            Set(part, typeof(Boss2PartController), "partId", (Boss2PartId)i);
            Set(boss, typeof(Boss2Controller), fields[i], part);
        }
        body = root.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        collider = root.AddComponent<CapsuleCollider2D>();
        collider.size = new Vector2(0.5f, 1.25f);
        movement = root.AddComponent<Boss2BodyMovement>();
        Set(movement, typeof(Boss2BodyMovement), "movementCollider", collider);
        Set(movement, typeof(Boss2BodyMovement), "obstacleLayers", (LayerMask)(1 << 31));
        Set(movement, typeof(Boss2BodyMovement), "moveDistanceRange", new Vector2(2f, 2f));
        Set(movement, typeof(Boss2BodyMovement), "initialDelay", 0f);
        Set(movement, typeof(Boss2BodyMovement), "waitDurationRange", new Vector2(0.2f, 0.2f));
        Set(movement, typeof(Boss2BodyMovement), "postPatternDelayRange", new Vector2(0.6f, 0.6f));
        typeof(Boss2BodyMovement).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(movement, null);
        Assert.That(boss.BeginCombat(), Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        UnityEngine.Random.state = randomState;
        Time.timeScale = timeScale;
    }

    private void Step(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            foreach (var runner in boss.Executions.ToArray())
                typeof(Boss2PatternRunner).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(runner, null);
            typeof(Boss2BodyMovement).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(movement, new object[] { DeltaTime });
            Assert.That(scene.GetPhysicsScene2D().Simulate(DeltaTime), Is.True);
        }
    }

    private Boss2PatternRunner Request(Boss2PatternKind kind, IEnumerator routine)
    {
        var pattern = root.AddComponent<Boss2FoundationTestPattern>();
        Set(pattern, typeof(Boss2PatternBase), "kind", kind);
        Set(pattern, typeof(Boss2PatternBase), "requiredParts", Boss2Parts.Ring);
        pattern.Factory = _ => new Execution(routine);
        Assert.That(boss.TryRequestPattern(pattern, out var runner), Is.True);
        return runner;
    }

    [Test]
    public void EachDestinationIsRelativeAndTheWholeColliderStaysInsideMapBounds()
    {
        body.position = new Vector2(2.2f, -3f);
        float start = body.position.x;
        Step();
        for (int i = 0; i < 200 && !Get<bool>(movement, "waiting"); i++) Step();
        Assert.That(Get<bool>(movement, "waiting"), Is.True);
        Assert.That(Mathf.Abs(body.position.x - start), Is.EqualTo(2f).Within(0.005f));
        for (int i = 0; i < 1000; i++)
        {
            Step();
            Assert.That(body.position.y, Is.EqualTo(-3f).Within(0.0001f));
            Assert.That(collider.bounds.min.x, Is.GreaterThanOrEqualTo(-4f));
            Assert.That(collider.bounds.max.x, Is.LessThanOrEqualTo(5f));
        }
    }

    [Test]
    public void PatternMovementReachesItsDestinationAndCanResumePatrol()
    {
        var destination = body.position + new Vector2(1f, 0.4f);
        movement.MoveForPattern(destination, 2f);
        for (int i = 0; i < 100 && movement.IsPatternMoving; i++) Step();
        Assert.That(movement.IsPatternMoving, Is.False);
        Assert.That(movement.IsPatternMovementBlocked, Is.False);
        Assert.That(Vector2.Distance(body.position, destination), Is.LessThan(0.001f));
        movement.ResumePatrol();
        Step();
        Assert.That(movement.IsPatrolPaused, Is.False);
    }

    [Test]
    public void PatternMovementCanRiseAndReturnBeforeGroggyBegins()
    {
        Vector2 start = body.position;
        Vector2 raised = start + Vector2.up * 2f;
        movement.MoveForPattern(raised, 2f);
        Step(5);
        var request = typeof(Boss2Controller).GetMethod("BeginGroggyReturn", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(request.Invoke(boss, null), Is.True);
        for (int i = 0; i < 100 && movement.IsPatternMoving; i++) Step();
        Assert.That(movement.IsPatternMoving, Is.False);
        Assert.That(movement.IsPatternMovementBlocked, Is.False);
        Assert.That(Vector2.Distance(body.position, raised), Is.LessThan(0.001f));

        movement.MoveForPattern(start, 2f);
        for (int i = 0; i < 100 && movement.IsPatternMoving; i++) Step();
        Assert.That(movement.IsPatternMoving, Is.False);
        Assert.That(movement.IsPatternMovementBlocked, Is.False);
        Assert.That(Vector2.Distance(body.position, start), Is.LessThan(0.001f));
    }

    [Test]
    public void AWallAddedDuringTravelStopsTheBossAndTheNextMoveGoesAwayFromIt()
    {
        float start = body.position.x;
        Step();
        float direction = Mathf.Sign(Get<float>(movement, "targetX") - start);
        var wall = new GameObject("Unexpected wall");
        SceneManager.MoveGameObjectToScene(wall, scene);
        wall.layer = 31;
        wall.transform.position = new Vector3(start + direction * 1.2f, -3f);
        var wallCollider = wall.AddComponent<BoxCollider2D>();
        wallCollider.size = new Vector2(0.2f, 4f);
        Physics2D.SyncTransforms();
        for (int i = 0; i < 200 && !Get<bool>(movement, "waiting"); i++) Step();
        Assert.That(Get<bool>(movement, "waiting"), Is.True);
        float stoppedX = body.position.x;
        float gap = direction > 0f ? wallCollider.bounds.min.x - collider.bounds.max.x
            : collider.bounds.min.x - wallCollider.bounds.max.x;
        Assert.That(gap, Is.GreaterThan(0.01f));
        Step(40);
        Assert.That((body.position.x - stoppedX) * direction, Is.LessThan(-0.2f));
    }

    [Test]
    public void MainPatternWaitsForDecelerationAndHoldsUntilReturnAndResumeDelayFinish()
    {
        Step(30);
        Assert.That(Mathf.Abs(Get<float>(movement, "currentSpeed")), Is.GreaterThan(1f));
        bool attacked = false, finishAttack = false, returning = false, finishReturn = false;
        IEnumerator Routine()
        {
            attacked = true;
            while (!finishAttack) yield return null;
            returning = true;
            while (!finishReturn) yield return null;
        }
        var runner = Request(Boss2PatternKind.Main, Routine());
        Step();
        Assert.That(attacked, Is.False);
        Assert.That(runner.State, Is.EqualTo(Boss2ExecutionState.WaitingForParts));
        for (int i = 0; i < 25 && !attacked; i++) Step();
        Assert.That(attacked, Is.True);
        Vector2 stopped = body.position;
        Step(30);
        Assert.That(Vector2.Distance(body.position, stopped), Is.LessThan(0.0001f));
        finishAttack = true;
        Step(30);
        Assert.That(returning, Is.True);
        Assert.That(Vector2.Distance(body.position, stopped), Is.LessThan(0.0001f));
        finishReturn = true;
        Step();
        Assert.That(runner.Result, Is.EqualTo(Boss2PatternResult.Completed));
        Step(20);
        Assert.That(Vector2.Distance(body.position, stopped), Is.LessThan(0.0001f));
        Step(30);
        Assert.That(Mathf.Abs(body.position.x - stopped.x), Is.GreaterThan(0.05f));
    }

    [Test]
    public void BasicPatternsContinueWhileTheBodyMoves()
    {
        IEnumerator Routine() { while (true) yield return null; }
        float start = body.position.x;
        var runner = Request(Boss2PatternKind.Basic, Routine());
        Step(30);
        Assert.That(runner.State, Is.EqualTo(Boss2ExecutionState.Running));
        Assert.That(Mathf.Abs(body.position.x - start), Is.GreaterThan(0.4f));
    }

    [Test]
    public void AStoppedBodyStillWaitsForTheExistingBasicAttackToFinish()
    {
        bool finishBasic = false, mainStarted = false;
        IEnumerator Basic() { while (!finishBasic) yield return null; }
        IEnumerator Main() { mainStarted = true; while (true) yield return null; }
        var basic = Request(Boss2PatternKind.Basic, Basic());
        Step(30);
        var main = Request(Boss2PatternKind.Main, Main());
        Step(20);
        Assert.That(Get<float>(movement, "currentSpeed"), Is.Zero);
        Assert.That(basic.State, Is.EqualTo(Boss2ExecutionState.Running));
        Assert.That(main.State, Is.EqualTo(Boss2ExecutionState.WaitingForParts));
        Assert.That(mainStarted, Is.False);
        finishBasic = true;
        Step();
        Assert.That(basic.Result, Is.EqualTo(Boss2PatternResult.Completed));
        Assert.That(mainStarted, Is.True);
    }

    [Test]
    public void ExplicitPatternControlCanMoveVerticallyAndResumeAtTheNewHeight()
    {
        IEnumerator Routine() { while (true) yield return null; }
        var runner = Request(Boss2PatternKind.Main, Routine());
        Step(2);
        Assert.That(runner.State, Is.EqualTo(Boss2ExecutionState.Running));
        movement.PausePatrol();
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.None));
        body.linearVelocity = Vector2.up;
        Step(10);
        Assert.That(body.position.y, Is.GreaterThan(-2.9f));
        float newHeight = body.position.y;
        movement.ResumePatrol();
        Step(2);
        runner.Cancel();
        Step(60);
        Assert.That(body.position.y, Is.EqualTo(newHeight).Within(0.0001f));
        boss.EndCombat();
        Step();
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
    }

    private sealed class Execution : Boss2PatternExecution
    {
        private readonly IEnumerator routine;
        public Execution(IEnumerator routine) => this.routine = routine;
        public override IEnumerator Execute(Boss2PatternContext context) => routine;
        public override IEnumerator ReturnAfterDeath(Boss2PatternContext context) { yield break; }
        public override void Cleanup(Boss2PatternResult result) { }
    }
}

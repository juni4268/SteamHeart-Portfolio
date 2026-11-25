using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class Boss2MovementPoseTests
{
    private Scene scene;
    private GameObject root;
    private Boss2BodyMovement movement;
    private readonly Boss2PartController[] parts = new Boss2PartController[4];
    private readonly Transform[] idleAnchors = new Transform[4];
    private readonly Transform[] nearAnchors = new Transform[4];
    private readonly Transform[] farAnchors = new Transform[4];

    private static void Set(object target, Type type, string field, object value) =>
        type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static void Tick(Boss2PartController part, float deltaTime) =>
        typeof(Boss2PartController).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(part, new object[] { deltaTime });

    private void Speed(float value) => Set(movement, typeof(Boss2BodyMovement), "currentSpeed", value);

    [SetUp]
    public void SetUp()
    {
        scene = EditorSceneManager.NewPreviewScene();
        root = new GameObject("Movement pose test");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = new Vector3(0.5f, -3f);
        var boss = root.AddComponent<Boss2Controller>();
        var health = root.GetComponent<BossHealth>();
        Set(health, typeof(BossHealth), "currentHp", health.MaxHp);
        Set(boss, typeof(Boss2Controller), "health", health);
        movement = root.AddComponent<Boss2BodyMovement>();
        typeof(Boss2BodyMovement).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(movement, null);
        string[] fields = { "upperLeftHand", "upperRightHand", "lowerLeftHand", "lowerRightHand" };
        for (int i = 0; i < parts.Length; i++)
        {
            idleAnchors[i] = new GameObject("Idle anchor").transform;
            idleAnchors[i].SetParent(root.transform, false);
            idleAnchors[i].localPosition = new Vector3(i % 2 == 0 ? -1f : 1f, i < 2 ? 0.5f : -0.5f);
            nearAnchors[i] = new GameObject("Move anchor").transform;
            nearAnchors[i].SetParent(root.transform, false);
            Vector3 position = idleAnchors[i].localPosition;
            position.x *= 0.8f;
            nearAnchors[i].localPosition = position;
            farAnchors[i] = new GameObject("Far move anchor").transform;
            farAnchors[i].SetParent(root.transform, false);
            position.x = idleAnchors[i].localPosition.x * 0.6f;
            farAnchors[i].localPosition = position;
            var child = new GameObject(fields[i]);
            child.transform.SetParent(root.transform, false);
            parts[i] = child.AddComponent<Boss2PartController>();
            Set(parts[i], typeof(Boss2PartController), "partId", (Boss2PartId)i);
            Set(parts[i], typeof(Boss2PartController), "idleAnchor", idleAnchors[i]);
            var pose = child.AddComponent<Boss2MovementPose>();
            Set(pose, typeof(Boss2MovementPose), "bodyMovement", movement);
            Set(pose, typeof(Boss2MovementPose), "leftMovementAnchor", i % 2 == 0 ? farAnchors[i] : nearAnchors[i]);
            Set(pose, typeof(Boss2MovementPose), "rightMovementAnchor", i % 2 == 0 ? nearAnchors[i] : farAnchors[i]);
            Set(parts[i], typeof(Boss2PartController), "movementPose", pose);
            Set(boss, typeof(Boss2Controller), fields[i], parts[i]);
        }
        Assert.That(boss.BeginCombat(), Is.True);
        Set(movement, typeof(Boss2BodyMovement), "patrolling", true);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
    }

    [Test]
    public void SpeedBlendsAllHandPositionsWithoutChangingHeightOrAuthoredAnchors()
    {
        foreach (float speed in new[] { 0f, 0.75f, 1.5f, -0.75f, -1.5f, 0f })
        {
            Speed(speed);
            float blend = Mathf.Abs(speed) / 1.5f;
            for (int i = 0; i < parts.Length; i++)
            {
                Vector3 original = new Vector3(i % 2 == 0 ? -1f : 1f, i < 2 ? 0.5f : -0.5f);
                Tick(parts[i], 0.02f);
                bool farSide = (speed > 0f && i % 2 != 0) || (speed < 0f && i % 2 == 0);
                float pull = farSide ? 0.4f : 0.2f;
                Vector3 expected = root.transform.TransformPoint(new Vector3(original.x * (1f - pull * blend), original.y));
                Assert.That(Vector3.Distance(parts[i].transform.position, expected), Is.LessThan(0.0001f));
                Assert.That(idleAnchors[i].localPosition, Is.EqualTo(original));
            }
        }
    }

    [Test]
    public void WorldSpaceAttacksKeepTheirTargetsWhileFreeHandsChangePose()
    {
        foreach (var part in parts) Tick(part, 0f);
        Vector3 start = parts[0].transform.position;
        Vector3 attack = new Vector3(-3f, 2f);
        parts[0].MoveToWorld(attack, 0.2f);
        Tick(parts[0], 0.1f);
        Assert.That(Vector3.Distance(parts[0].transform.position, Vector3.Lerp(start, attack, 0.5f)), Is.LessThan(0.0001f));
        Speed(1.5f);
        for (int i = 1; i < parts.Length; i++) Tick(parts[i], 0.02f);
        Tick(parts[0], 0.1f);
        Assert.That(Vector3.Distance(parts[0].transform.position, attack), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(parts[1].transform.position, farAnchors[1].position), Is.LessThan(0.0001f));
        Speed(-1.5f);
        foreach (var part in parts) Tick(part, 0.02f);
        Assert.That(Vector3.Distance(parts[0].transform.position, attack), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(parts[1].transform.position, nearAnchors[1].position), Is.LessThan(0.0001f));
        Speed(0f);
        foreach (var part in parts) Tick(part, 0.02f);
        Assert.That(Vector3.Distance(parts[0].transform.position, attack), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(parts[1].transform.position, idleAnchors[1].position), Is.LessThan(0.0001f));
    }

    [Test]
    public void ReturnTracksTheCurrentPoseAndDoesNotSnapWhenFollowingResumes()
    {
        Speed(1.5f);
        Vector3 start = new Vector3(-4f, 1f);
        parts[0].transform.position = start;
        parts[0].ReturnToIdle(0.4f);
        Tick(parts[0], 0.2f);
        Assert.That(Vector3.Distance(parts[0].transform.position, Vector3.Lerp(start, nearAnchors[0].position, 0.5f)), Is.LessThan(0.0001f));
        Speed(-0.75f);
        Tick(parts[0], 0.2f);
        Vector3 target = Vector3.Lerp(idleAnchors[0].position, farAnchors[0].position, 0.5f);
        Assert.That(parts[0].CurrentMotion, Is.EqualTo(Boss2PartMotion.FollowingAnchor));
        Assert.That(Vector3.Distance(parts[0].transform.position, target), Is.LessThan(0.0001f));
        Tick(parts[0], 0.02f);
        Assert.That(Vector3.Distance(parts[0].transform.position, target), Is.LessThan(0.0001f));
        Speed(0f);
        Tick(parts[0], 0.02f);
        Assert.That(Vector3.Distance(parts[0].transform.position, idleAnchors[0].position), Is.LessThan(0.0001f));
    }

    [Test]
    public void EditedMoveAnchorsAreUsedAndPausingMovementRestoresIdle()
    {
        Speed(1.5f);
        nearAnchors[0].localPosition = new Vector3(-0.6f, 0.5f);
        root.transform.position += new Vector3(2f, 1f);
        Tick(parts[0], 0.02f);
        Assert.That(Vector3.Distance(parts[0].transform.position, nearAnchors[0].position), Is.LessThan(0.0001f));
        Assert.That(idleAnchors[0].localPosition, Is.EqualTo(new Vector3(-1f, 0.5f)));
        farAnchors[0].localPosition = new Vector3(-0.4f, 0.5f);
        Speed(-1.5f);
        Tick(parts[0], 0.02f);
        Assert.That(Vector3.Distance(parts[0].transform.position, farAnchors[0].position), Is.LessThan(0.0001f));
        Assert.That(nearAnchors[0].localPosition, Is.EqualTo(new Vector3(-0.6f, 0.5f)));
        movement.PausePatrol();
        Tick(parts[0], 0.02f);
        Assert.That(Vector3.Distance(parts[0].transform.position, idleAnchors[0].position), Is.LessThan(0.0001f));
    }

    private Boss2PartController CreateRing(out Transform idle, out Transform left)
    {
        idle = new GameObject("Ring idle anchor").transform;
        idle.SetParent(root.transform, false);
        idle.localPosition = new Vector3(0.035f, 0.235f, 0.1f);
        left = new GameObject("Ring move anchor").transform;
        left.SetParent(root.transform, false);
        left.localPosition = idle.localPosition;
        var child = new GameObject("Ring");
        child.transform.SetParent(root.transform, false);
        var ring = child.AddComponent<Boss2PartController>();
        Set(ring, typeof(Boss2PartController), "partId", Boss2PartId.Ring);
        Set(ring, typeof(Boss2PartController), "idleAnchor", idle);
        var pose = child.AddComponent<Boss2MovementPose>();
        Set(pose, typeof(Boss2MovementPose), "bodyMovement", movement);
        Set(pose, typeof(Boss2MovementPose), "leftMovementAnchor", left);
        Set(pose, typeof(Boss2MovementPose), "mirrorLeftPoseForRight", true);
        Set(ring, typeof(Boss2PartController), "movementPose", pose);
        return ring;
    }

    [Test]
    public void RingPreservesTheLeftPoseAndMirrorsRightInBodySpaceAsSpeedChanges()
    {
        var ring = CreateRing(out var idle, out var left);
        root.transform.rotation = Quaternion.Euler(0f, 0f, 20f);
        root.transform.localScale = new Vector3(2f, 1.5f, 1f);
        Vector3 original = left.localPosition;
        foreach (float speed in new[] { -1.5f, -0.75f, 0f, 0.75f, 1.5f, 0f })
        {
            Speed(speed);
            Tick(ring, 0.02f);
            Vector3 target = original;
            if (speed > 0f) target.x = -target.x;
            Vector3 expected = Vector3.Lerp(idle.position, root.transform.TransformPoint(target), Mathf.Abs(speed) / 1.5f);
            Assert.That(Vector3.Distance(ring.transform.position, expected), Is.LessThan(0.0001f));
            Assert.That(left.localPosition, Is.EqualTo(original));
            Assert.That(idle.localPosition, Is.EqualTo(original));
            Assert.That(ring.transform.localScale, Is.EqualTo(Vector3.one));
        }
    }

    [Test]
    public void RingAttacksKeepTheirWorldTargetAndReturnToTheCurrentMovementPose()
    {
        var ring = CreateRing(out var idle, out var left);
        Vector3 attack = new Vector3(-3f, 2f);
        ring.MoveToWorld(attack, 0.2f);
        Speed(1.5f);
        Tick(ring, 0.2f);
        Speed(-1.5f);
        Tick(ring, 0.02f);
        Assert.That(Vector3.Distance(ring.transform.position, attack), Is.LessThan(0.0001f));

        Speed(1.5f);
        ring.ReturnToIdle(0.4f);
        Tick(ring, 0.2f);
        Vector3 right = root.transform.TransformPoint(new Vector3(-left.localPosition.x, left.localPosition.y, left.localPosition.z));
        Assert.That(Vector3.Distance(ring.transform.position, Vector3.Lerp(attack, right, 0.5f)), Is.LessThan(0.0001f));
        left.localPosition += new Vector3(0.1f, 0.05f);
        Speed(-1.5f);
        Tick(ring, 0.2f);
        Tick(ring, 0.02f);
        Assert.That(ring.CurrentMotion, Is.EqualTo(Boss2PartMotion.FollowingAnchor));
        Assert.That(Vector3.Distance(ring.transform.position, left.position), Is.LessThan(0.0001f));
        movement.PausePatrol();
        Tick(ring, 0.02f);
        Assert.That(Vector3.Distance(ring.transform.position, idle.position), Is.LessThan(0.0001f));
    }
}

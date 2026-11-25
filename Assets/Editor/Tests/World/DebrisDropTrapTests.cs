using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[Category("DebrisDropTrap")]
public sealed class DebrisDropTrapTests
{
    private static readonly Vector3 Origin = new Vector3(1000f, 1000f);
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

    private static T Get<T>(object target, string name) =>
        (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);

    private static void Step(DebrisDropTrap trap, int count = 1)
    {
        MethodInfo tick = typeof(DebrisDropTrap).GetMethod("FixedUpdate", PrivateInstance);
        for (int i = 0; i < count; i++) tick.Invoke(trap, null);
    }

    private static void Animate(Animator animator, float duration)
    {
        animator.Update(0f);
        animator.Update(duration);
        animator.Update(0f);
    }

    [UnitySetUp]
    public IEnumerator EnterTestScene()
    {
        yield return new EnterPlayMode();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        Time.timeScale = 1f;
        yield return new ExitPlayMode();
    }

    private static DebrisDropTrap CreateTrap(out Animator animator)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/DebrisDropTrap.prefab");
        var instance = Object.Instantiate(prefab, Origin, Quaternion.identity);
        var trap = instance.GetComponent<DebrisDropTrap>();
        animator = Get<Animator>(trap, "doorAnimator");
        Set(trap, "waitDuration", Time.fixedDeltaTime * 2f);
        Set(trap, "attackDuration", Time.fixedDeltaTime * 2f);
        Set(trap, "waveInterval", Time.fixedDeltaTime);
        Set(trap, "clustersPerWave", 3);
        animator.Rebind();
        Animate(animator, 0.001f);
        return trap;
    }

    private static Player CreatePlayer(out BoxCollider2D first, out BoxCollider2D second)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        var instance = Object.Instantiate(prefab, Origin, Quaternion.identity);
        instance.GetComponent<Rigidbody2D>().gravityScale = 0f;
        instance.GetComponent<PlayerMovement>().enabled = false;
        var player = instance.GetComponent<Player>();
        player.input.Player.Disable();
        Set(player.health, "currentHP", 100);
        foreach (Collider2D collider in instance.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        first = AddPlayerCollider(instance.transform, -0.1f);
        second = AddPlayerCollider(instance.transform, 0.1f);
        return player;
    }

    private static BoxCollider2D AddPlayerCollider(Transform parent, float offset)
    {
        var child = new GameObject("Debris test player collider");
        child.layer = LayerMask.NameToLayer("Player");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.right * offset;
        var collider = child.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * 0.1f;
        return collider;
    }

    private static DebrisCluster CreateCluster(Vector3 position, float speed)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/DebrisClusters/DebrisCluster1.prefab");
        var cluster = Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<DebrisCluster>();
        cluster.BeginFalling(speed);
        return cluster;
    }

    [Test]
    public void DoorsReachAuthoredAnglesBeforeDenseWavesAndClosingDoesNotStopExistingDebris()
    {
        DebrisDropTrap trap = CreateTrap(out Animator animator);
        Assert.That(trap.Phase, Is.EqualTo(DebrisDropPhase.Waiting));
        Step(trap, 2);
        Assert.That(trap.Phase, Is.EqualTo(DebrisDropPhase.Opening));
        Assert.That(trap.ActiveClusterCount, Is.Zero);
        Animate(animator, 0.1f);
        Assert.That(trap.Phase, Is.EqualTo(DebrisDropPhase.Opening));
        Step(trap);
        Assert.That(trap.ActiveClusterCount, Is.Zero, "Requesting a door opening is not completion.");

        Animate(animator, 0.35f);
        Assert.That(trap.Phase, Is.EqualTo(DebrisDropPhase.Attacking));
        Assert.That(Mathf.DeltaAngle(0f, animator.transform.Find("drop_door_L").localEulerAngles.z), Is.EqualTo(-90f).Within(0.01f));
        Assert.That(Mathf.DeltaAngle(0f, animator.transform.Find("drop_door_R").localEulerAngles.z), Is.EqualTo(90f).Within(0.01f));
        Step(trap);
        Assert.That(trap.ActiveClusterCount, Is.EqualTo(3));
        var clusters = Get<List<DebrisCluster>>(trap, "activeClusters");
        foreach (var cluster in clusters) Assert.That(cluster.GetComponentsInChildren<SpriteRenderer>(), Is.Not.Empty);
        Step(trap);
        Assert.That(trap.Phase, Is.EqualTo(DebrisDropPhase.Draining));
        Assert.That(trap.ActiveClusterCount, Is.EqualTo(6));
        float previousY = clusters[0].transform.position.y;

        Time.timeScale = 0f;
        Step(trap, 5);
        Assert.That(clusters[0].transform.position.y, Is.EqualTo(previousY));
        Time.timeScale = 1f;
        Step(trap);
        Assert.That(clusters[0].transform.position.y, Is.LessThan(previousY));
        Assert.That(trap.ActiveClusterCount, Is.EqualTo(6));
        float doorBottom = float.PositiveInfinity;
        foreach (var renderer in Get<SpriteRenderer[]>(trap, "doorRenderers"))
            doorBottom = Mathf.Min(doorBottom, renderer.bounds.min.y);
        foreach (var cluster in clusters)
            cluster.transform.position += Vector3.up * (doorBottom - 0.05f - cluster.Top);
        Step(trap);
        Assert.That(trap.Phase, Is.EqualTo(DebrisDropPhase.Closing));
        Assert.That(trap.ActiveClusterCount, Is.EqualTo(6), "Closing does not wait for the endpoint.");
        Animate(animator, 0.35f);
        Assert.That(trap.Phase, Is.EqualTo(DebrisDropPhase.Waiting));
        Assert.That(Mathf.DeltaAngle(0f, animator.transform.Find("drop_door_L").localEulerAngles.z), Is.EqualTo(0f).Within(0.01f));
        Assert.That(Mathf.DeltaAngle(0f, animator.transform.Find("drop_door_R").localEulerAngles.z), Is.EqualTo(0f).Within(0.01f));

        trap.enabled = false;
        Assert.That(trap.ActiveClusterCount, Is.Zero);
        trap.enabled = true;
        Animate(animator, 0.01f);
        Assert.That(trap.Phase, Is.EqualTo(DebrisDropPhase.Waiting), "An already closed Animator must support re-enabling the trap.");
    }

    [Test]
    public void OverlappingClustersShareHitsUntilAllPlayerCollidersLeaveAndRespectProtection()
    {
        Player player = CreatePlayer(out BoxCollider2D first, out _);
        DebrisDropTrap trap = CreateTrap(out _);
        Set(trap, "remainingTime", 1000f);
        var clusters = Get<List<DebrisCluster>>(trap, "activeClusters");
        clusters.Add(CreateCluster(Origin + Vector3.left * 0.1f, 0f));
        clusters.Add(CreateCluster(Origin + Vector3.right * 0.1f, 0f));
        int initialHP = player.health.CurrentHP;
        Vector2 expectedKnockback = Get<Vector2>(trap, "knockbackVelocity");

        Set(player.health, "hitInvincibilityEndsAt", Time.time + 10f);
        Step(trap, 2);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP));
        Set(player.health, "hitInvincibilityEndsAt", float.NegativeInfinity);
        Step(trap);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1));
        Assert.That(player.motor.Velocity.y, Is.EqualTo(expectedKnockback.y));
        Assert.That(Mathf.Abs(player.motor.Velocity.x), Is.EqualTo(expectedKnockback.x));
        Set(player.health, "hitInvincibilityEndsAt", float.NegativeInfinity);
        Step(trap, 5);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1));

        first.transform.position = Origin + Vector3.right * 5f;
        Step(trap);
        first.transform.position = Origin + Vector3.left * 0.1f;
        Step(trap);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 1), "A second player collider still touches the debris.");
        player.transform.position = Origin + Vector3.right * 5f;
        Step(trap);
        player.transform.position = Origin;
        Step(trap);
        Assert.That(player.health.CurrentHP, Is.EqualTo(initialHP - 2));
        DebrisCluster survivor = clusters[0];
        trap.enabled = false;
        Assert.That(survivor.gameObject.activeSelf, Is.False);
        Assert.That(trap.ActiveClusterCount, Is.Zero);
    }

    [Test]
    public void FastDebrisPassesGroundAndSweepsPlayerUntilItsWholeShapePassesEndPoint()
    {
        Player player = CreatePlayer(out _, out _);
        player.transform.position = Origin + Vector3.down;
        var floorObject = new GameObject("Debris test landing floor");
        floorObject.layer = LayerMask.NameToLayer("Ground");
        floorObject.transform.position = Origin + Vector3.down * 3f;
        var floor = floorObject.AddComponent<BoxCollider2D>();
        floor.size = new Vector2(10f, 0.2f);
        DebrisCluster cluster = CreateCluster(Origin + Vector3.up, 1000f);
        var contacts = new Dictionary<IDamageable, Vector2>();
        Physics2D.SyncTransforms();
        float endY = Origin.y - 4f;
        bool stillFalling = cluster.Advance(Time.fixedDeltaTime, endY, LayerMask.GetMask("Player"), contacts);
        Assert.That(stillFalling, Is.False);
        Assert.That(contacts.ContainsKey(player.health), Is.True, "The player must not be skipped by a fast fall.");
        Physics2D.SyncTransforms();
        Assert.That(cluster.Top, Is.EqualTo(endY).Within(0.001f));
        Assert.That(cluster.GetComponent<BoxCollider2D>().bounds.min.y, Is.LessThan(floor.bounds.max.y));

        player.transform.position = Origin + Vector3.down * 10f;
        cluster.transform.position = Origin + Vector3.up;
        contacts.Clear();
        Physics2D.SyncTransforms();
        cluster.Advance(Time.fixedDeltaTime, endY, LayerMask.GetMask("Player"), contacts);
        Assert.That(contacts, Is.Empty, "A fast sweep must stop when the whole cluster has passed the endpoint.");

        DebrisDropTrap trap = CreateTrap(out _);
        Set(trap, "remainingTime", 1000f);
        cluster.transform.position = Origin + Vector3.up;
        cluster.BeginFalling(1000f);
        Get<List<DebrisCluster>>(trap, "activeClusters").Add(cluster);
        Step(trap);
        Assert.That(trap.ActiveClusterCount, Is.Zero);
        Assert.That(cluster.gameObject.activeSelf, Is.False);
    }
}

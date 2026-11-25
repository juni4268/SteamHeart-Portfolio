using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// These checks need runtime Animator callbacks. They can also run directly in an isolated Play Mode scene.
public sealed class TrapAnimationAndEndpointTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector3 Origin = new Vector3(1000f, 1000f);
    private Scene originalScene;
    private Scene testScene;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        originalScene = SceneManager.GetActiveScene();
        testScene = SceneManager.CreateScene("TrapAnimationAndEndpointTests");
        SceneManager.SetActiveScene(testScene);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.SetActiveScene(originalScene);
        yield return SceneManager.UnloadSceneAsync(testScene);
        yield return new ExitPlayMode();
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

    private static void Invoke(object target, string name) =>
        target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);

    private static void Step(SteamVentTrap trap, int count = 1)
    {
        for (int i = 0; i < count; i++) Invoke(trap, "FixedUpdate");
    }

    private static void Animate(Animator animator, float duration)
    {
        animator.Update(0f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(duration / 0.01f));
        for (int i = 0; i < steps; i++) animator.Update(duration / steps);
        animator.Update(0f);
    }

    private static SteamVentTrap CreateVent(out Animator animator, out SpriteRenderer renderer, out BoxCollider2D area)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/SteamVentTrap.prefab");
        var instance = Object.Instantiate(prefab, Origin, Quaternion.identity);
        var trap = instance.GetComponent<SteamVentTrap>();
        Set(trap, "waitDuration", Time.fixedDeltaTime * 2f);
        Set(trap, "attackDuration", Time.fixedDeltaTime * 2f);
        Set(trap, "targetLayers", (LayerMask)0);
        var data = new SerializedObject(trap);
        animator = (Animator)data.FindProperty("attackAnimator").objectReferenceValue;
        renderer = (SpriteRenderer)data.FindProperty("attackRenderer").objectReferenceValue;
        area = (BoxCollider2D)data.FindProperty("attackCollider").objectReferenceValue;
        animator.Rebind();
        Animate(animator, 0.001f);
        return trap;
    }

    [Test]
    public void VentRemainsVisibleAndDamageStartsOnlyAfterTheStartingMotion()
    {
        var trap = CreateVent(out Animator animator, out SpriteRenderer renderer, out BoxCollider2D area);
        Sprite waitingSprite = renderer.sprite;
        Assert.That(trap.Phase, Is.EqualTo(SteamVentPhase.Waiting));
        Assert.That(renderer.enabled, Is.True);
        Assert.That(waitingSprite, Is.Not.Null);
        Assert.That(area.enabled, Is.False);

        Step(trap, 2);
        Assert.That(trap.Phase, Is.EqualTo(SteamVentPhase.Starting));
        Assert.That(area.enabled, Is.False, "An animation request must not activate damage.");
        Animate(animator, 0.05f);
        Assert.That(trap.Phase, Is.EqualTo(SteamVentPhase.Starting));
        Assert.That(renderer.enabled, Is.True);
        Assert.That(area.enabled, Is.False);
        Animate(animator, 0.25f);
        Assert.That(trap.Phase, Is.EqualTo(SteamVentPhase.Attacking));
        Assert.That(area.enabled, Is.True);
        Step(trap);
        Assert.That(area.enabled, Is.True, "Starting motion must not consume the configured attack time.");
        Step(trap);
        Assert.That(trap.Phase, Is.EqualTo(SteamVentPhase.Ending));
        Assert.That(renderer.enabled, Is.True);
        Assert.That(area.enabled, Is.False);
        Animate(animator, 0.3f);
        Assert.That(trap.Phase, Is.EqualTo(SteamVentPhase.Waiting));
        Assert.That(renderer.sprite, Is.EqualTo(waitingSprite));
        Assert.That(renderer.enabled, Is.True);

        trap.enabled = false;
        trap.enabled = true;
        Animate(animator, 0.001f);
        Assert.That(trap.Phase, Is.EqualTo(SteamVentPhase.Waiting));
        Assert.That(area.enabled, Is.False);
    }

    [Test]
    public void InterruptingAttackAnimationCancelsDamageWithoutWaitingForTheTimer()
    {
        var trap = CreateVent(out Animator animator, out SpriteRenderer renderer, out BoxCollider2D area);
        Set(trap, "attackDuration", 10f);
        Step(trap, 2);
        Animate(animator, 0.3f);
        Assert.That(trap.IsAttacking, Is.True);
        var data = new SerializedObject(trap);
        animator.SetBool(data.FindProperty("attackParameter").stringValue, false);
        Animate(animator, 0.2f);
        Assert.That(trap.IsAttacking, Is.False);
        Assert.That(area.enabled, Is.False);
        Assert.That(renderer.enabled, Is.True);
        Animate(animator, 0.3f);
        Assert.That(trap.Phase, Is.EqualTo(SteamVentPhase.Waiting));
    }

    [Test]
    public void DebrisPassesGroundAndRemainsUntilEveryVisualPartPassesEndPoint()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/DebrisClusters/DebrisCluster1.prefab");
        var instance = Object.Instantiate(prefab, Origin + Vector3.up, Quaternion.identity);
        var cluster = instance.GetComponent<DebrisCluster>();
        cluster.BeginFalling(1f);
        var floor = new GameObject("Intermediate ground");
        floor.layer = LayerMask.NameToLayer("Ground");
        floor.transform.position = Origin + Vector3.down;
        floor.AddComponent<BoxCollider2D>().size = new Vector2(10f, 0.1f);
        var contacts = new Dictionary<IDamageable, Vector2>();
        float endY = Origin.y - 4f;
        Physics2D.SyncTransforms();
        Assert.That(cluster.Advance(1f, endY, 0, contacts), Is.True);
        Physics2D.SyncTransforms();
        Assert.That(cluster.GetComponent<BoxCollider2D>().bounds.min.y, Is.LessThan(floor.transform.position.y));

        // A smaller gameplay collider must not remove still-visible artwork early.
        cluster.GetComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
        float top = float.NegativeInfinity;
        foreach (var renderer in cluster.GetComponentsInChildren<SpriteRenderer>()) top = Mathf.Max(top, renderer.bounds.max.y);
        cluster.transform.position += Vector3.up * (endY + 0.1f - top);
        Physics2D.SyncTransforms();
        Assert.That(cluster.Advance(0.05f, endY, 0, contacts), Is.True);
        Physics2D.SyncTransforms();
        Assert.That(cluster.Advance(0.2f, endY, 0, contacts), Is.False);
        foreach (var renderer in cluster.GetComponentsInChildren<SpriteRenderer>())
            Assert.That(renderer.bounds.max.y, Is.LessThanOrEqualTo(endY + 0.001f));

        cluster.transform.position = Origin + Vector3.up;
        cluster.BeginFalling(0.01f);
        Physics2D.SyncTransforms();
        Assert.That(cluster.Advance(20f, endY, 0, contacts), Is.True, "Lifetime must not delete debris before its endpoint.");
    }

    [Test]
    public void BothTrapsDamageEnemiesOncePerContactIncludingOverlappingClusters()
    {
        var vent = CreateVent(out Animator animator, out _, out _);
        Set(vent, "attackDuration", 10f);
        Set(vent, "targetLayers", (LayerMask)LayerMask.GetMask("Enemy"));
        var enemyObject = new GameObject("Trap damage enemy");
        enemyObject.transform.position = Origin;
        var health = enemyObject.AddComponent<EnemyHealth>();
        for (int i = 0; i < 2; i++)
        {
            var child = new GameObject("Enemy contact collider");
            child.layer = LayerMask.NameToLayer("Enemy");
            child.transform.SetParent(enemyObject.transform, false);
            child.transform.localPosition = Vector3.right * (i == 0 ? -0.1f : 0.1f);
            child.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
        }
        int initialHP = health.CurrentHp;
        Step(vent, 2);
        Animate(animator, 0.3f);
        Step(vent, 3);
        Assert.That(health.CurrentHp, Is.EqualTo(initialHP - 1));
        enemyObject.transform.position = Origin + Vector3.right * 5f;
        Step(vent);
        enemyObject.transform.position = Origin;
        Step(vent);
        Assert.That(health.CurrentHp, Is.EqualTo(initialHP - 2));
        vent.enabled = false;

        var dropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/DebrisDropTrap.prefab");
        var drop = Object.Instantiate(dropPrefab, Origin, Quaternion.identity).GetComponent<DebrisDropTrap>();
        Set(drop, "targetLayers", (LayerMask)LayerMask.GetMask("Enemy"));
        var clusters = (List<DebrisCluster>)typeof(DebrisDropTrap).GetField("activeClusters", PrivateInstance).GetValue(drop);
        var clusterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/DebrisClusters/DebrisCluster1.prefab");
        for (int i = 0; i < 2; i++)
        {
            var cluster = Object.Instantiate(clusterPrefab, Origin, Quaternion.identity).GetComponent<DebrisCluster>();
            cluster.BeginFalling(0f);
            clusters.Add(cluster);
        }
        Invoke(drop, "FixedUpdate");
        Invoke(drop, "FixedUpdate");
        Assert.That(health.CurrentHp, Is.EqualTo(initialHP - 3));
        enemyObject.transform.position = Origin + Vector3.right * 5f;
        Invoke(drop, "FixedUpdate");
        enemyObject.transform.position = Origin;
        Invoke(drop, "FixedUpdate");
        Assert.That(health.CurrentHp, Is.EqualTo(initialHP - 4));
    }

    [Test]
    public void DropStopsSpawningAndWaitsForTheLastClusterToClearTheDoors()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/DebrisDropTrap.prefab");
        var drop = Object.Instantiate(prefab, Origin, Quaternion.identity).GetComponent<DebrisDropTrap>();
        Set(drop, "waitDuration", Time.fixedDeltaTime * 2f);
        Set(drop, "attackDuration", Time.fixedDeltaTime * 2f);
        Set(drop, "waveInterval", Time.fixedDeltaTime);
        Set(drop, "clustersPerWave", 1);
        Set(drop, "targetLayers", (LayerMask)0);
        var data = new SerializedObject(drop);
        var animator = (Animator)data.FindProperty("doorAnimator").objectReferenceValue;
        string parameter = data.FindProperty("doorsOpenParameter").stringValue;
        animator.Rebind();
        Animate(animator, 0.001f);
        Invoke(drop, "FixedUpdate");
        Invoke(drop, "FixedUpdate");
        Animate(animator, 0.35f);
        Assert.That(drop.Phase, Is.EqualTo(DebrisDropPhase.Attacking));
        Invoke(drop, "FixedUpdate");
        Invoke(drop, "FixedUpdate");
        Assert.That(drop.Phase, Is.EqualTo(DebrisDropPhase.Draining));
        Assert.That(drop.ActiveClusterCount, Is.EqualTo(2));
        Assert.That(animator.GetBool(parameter), Is.True);

        var clusters = (List<DebrisCluster>)typeof(DebrisDropTrap).GetField("activeClusters", PrivateInstance).GetValue(drop);
        var doors = (SpriteRenderer[])typeof(DebrisDropTrap).GetField("doorRenderers", PrivateInstance).GetValue(drop);
        float bottom = float.PositiveInfinity;
        foreach (var door in doors) bottom = Mathf.Min(bottom, door.bounds.min.y);
        for (int i = 0; i < clusters.Count; i++)
        {
            clusters[i].BeginFalling(0f);
            clusters[i].transform.position += Vector3.up * (bottom + (i == 0 ? 0.2f : -0.2f) - clusters[i].Top);
        }
        Invoke(drop, "FixedUpdate");
        Assert.That(drop.Phase, Is.EqualTo(DebrisDropPhase.Draining), "One remaining cluster must keep the doors open.");
        Assert.That(drop.ActiveClusterCount, Is.EqualTo(2), "The drain phase must not spawn more debris.");
        Assert.That(animator.GetBool(parameter), Is.True);

        clusters[0].transform.position += Vector3.down * 0.3f;
        Invoke(drop, "FixedUpdate");
        Assert.That(drop.Phase, Is.EqualTo(DebrisDropPhase.Closing));
        Assert.That(animator.GetBool(parameter), Is.False);
        Assert.That(drop.ActiveClusterCount, Is.EqualTo(2), "Debris below the doors continues to its endpoint.");
        Animate(animator, 0.35f);
        Assert.That(drop.Phase, Is.EqualTo(DebrisDropPhase.Waiting));
    }
}

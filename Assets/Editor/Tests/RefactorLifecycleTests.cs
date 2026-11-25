using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class RefactorLifecycleTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Fields).SetValue(target, value);
    private static void Tick(object target) =>
        target.GetType().GetMethod("FixedUpdate", Fields).Invoke(target, null);

    private static Player CreatePlayer()
    {
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        instance.transform.position = new Vector3(1000f, 1000f);
        instance.GetComponent<PlayerMovement>().enabled = false;
        var body = instance.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        var player = instance.GetComponent<Player>();
        Set(player.health, "currentHP", player.health.MaxHP);
        Set(player.health, "respawn", null);
        return player;
    }

    private static void Isolate()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
    }

    [Test]
    public void ProgressLoadDistinguishesMissingInvalidAndUnreadableFilesWithoutChangingThem()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ProjectKProgress_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "progress.json");
        var load = typeof(PlayerProgressStore).GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic);
        PlayerProgressLoadResult Read(string file) =>
            (PlayerProgressLoadResult)load.Invoke(null, new object[] { file, null });
        try
        {
            Assert.That(Read(path), Is.EqualTo(PlayerProgressLoadResult.Missing));
            string invalid = "{\"version\":99,\"scrap\":999}";
            File.WriteAllText(path, invalid);
            LogAssert.Expect(LogType.Warning, "Progress save has an unsupported version or invalid data.");
            Assert.That(Read(path), Is.EqualTo(PlayerProgressLoadResult.Failed));
            Assert.That(File.ReadAllText(path), Is.EqualTo(invalid));
            File.WriteAllText(path, "{}");
            LogAssert.Expect(LogType.Warning, "Progress save has an unsupported version or invalid data.");
            Assert.That(Read(path), Is.EqualTo(PlayerProgressLoadResult.Failed));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{}"));
            File.WriteAllText(path, "invalid JSON");
            LogAssert.Expect(LogType.Warning, new Regex("Could not read progress:"));
            Assert.That(Read(path), Is.EqualTo(PlayerProgressLoadResult.Failed));
            Assert.That(File.ReadAllText(path), Is.EqualTo("invalid JSON"));
            File.WriteAllText(path, "{\"version\":1,\"coins\":37,\"healthShards\":6}");
            var args = new object[] { path, null };
            Assert.That(load.Invoke(null, args), Is.EqualTo(PlayerProgressLoadResult.Loaded));
            var data = (PlayerProgressData)args[1];
            Assert.That(data.scrap, Is.EqualTo(37));
            Assert.That(data.healthPieces, Is.EqualTo(6));
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                LogAssert.Expect(LogType.Warning, new Regex("Could not read progress:"));
                Assert.That(Read(path), Is.EqualTo(PlayerProgressLoadResult.Failed));
            }
            var save = typeof(PlayerProgressStore).GetMethod("Save", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(save.Invoke(null, new object[] { path, new PlayerProgressData { scrap = 12 } }), Is.True);
            Assert.That(Read(path), Is.EqualTo(PlayerProgressLoadResult.Loaded));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }
        finally { Directory.Delete(folder, true); }
    }

    [UnityTest]
    public IEnumerator EnemyStunAndAttackReleaseOnlyTheirOwnStateOnDisable()
    {
        yield return new EnterPlayMode();
        Isolate();
        CreatePlayer();
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyS.prefab"));
        instance.transform.position = new Vector3(1003f, 1000f);
        instance.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var ai = instance.GetComponent<EnemyAI>();
        var health = instance.GetComponent<EnemyHealth>();
        var attack = instance.GetComponent<EnemyAttack>();
        Set(health, "hitStunDuration", 0.02f);
        Set(health, "currentHp", 100);
        ai.enabled = false;
        health.TakeDamage(1);
        yield return new WaitForSeconds(0.05f);
        Assert.That(ai.enabled, Is.False, "An originally disabled AI must remain disabled.");
        ai.enabled = true;
        health.TakeDamage(1);
        health.TakeDamage(1);
        Assert.That(ai.enabled, Is.False);
        health.enabled = false;
        Assert.That(ai.enabled, Is.True, "Repeated hits must preserve the original enabled state.");
        health.enabled = true;
        health.TakeDamage(1);
        instance.SetActive(false);
        instance.SetActive(true);
        Assert.That(ai.enabled, Is.True);
        ai.enabled = false;
        Set(attack, "attackCooldown", 0f);
        Assert.That(attack.TryAttack(), Is.True);
        attack.BeginAttackHitbox();
        attack.enabled = false;
        Assert.That(attack.IsAttacking, Is.False);
        Assert.That(attack.TryAttack(), Is.False);
        attack.BeginAttackHitbox();
        Assert.That(attack.IsAttacking, Is.False);
        attack.enabled = true;
        Assert.That(attack.TryAttack(), Is.True);
        instance.GetComponentInChildren<Animator>().enabled = false;
        yield return null;
        Assert.That(attack.IsAttacking, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator LethalPropellerDamageCanStopSuctionInsideItsIntervalLoop()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("Boss1_Map");
        yield return null;
        Assert.DoesNotThrow(RunLethalPropellerScenario);
        yield return new ExitPlayMode();
    }

    private static Player PrepareBossDamageScenario()
    {
        var boss = Object.FindFirstObjectByType<Boss1Controller>();
        Assert.That(boss, Is.Not.Null, "Actual Boss1 Controller");
        boss.GetComponent<Boss1PatternScheduler>().StopSchedulingAndCancelPattern();
        boss.GetComponent<Boss1PatternScheduler>().enabled = false;
        boss.enabled = false;
        var player = Object.FindFirstObjectByType<Player>();
        Assert.That(player, Is.Not.Null, "Actual Boss1 Player");
        Assert.That(player.health, Is.Not.Null, "Player Awake health binding");
        player.GetComponent<PlayerMovement>().enabled = false;
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        Set(player.health, "respawn", null);
        Set(player.health, "currentHP", 1);
        return player;
    }

    private static void RunLethalPropellerScenario()
    {
        var player = PrepareBossDamageScenario();
        var field = Object.FindFirstObjectByType<Boss1PropellerField>();
        var hand = field.GetComponent<Boss1Controller>().LeftHand;
        var config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Boss1PropellerConfig>("Assets/Data/Boss/Boss1PropellerConfig.asset"));
        try
        {
            hand.transform.position = player.transform.position;
            field.Prepare(hand, hand.transform, player.health, config);
            field.StartSuction();
            player.health.Died += field.Stop;
            Set(field, "damageElapsed", config.DamageInterval * 3f);
            Tick(field);
            Assert.That(player.health.IsDead, Is.True);
            Assert.That(field.IsSuctionActive, Is.False);
        }
        finally { Object.DestroyImmediate(config); }
    }

    [UnityTest]
    public IEnumerator LethalDebrisDamageCanClearMultiplePiecesInsideItsContactLoop()
    {
        yield return new EnterPlayMode();
        SceneManager.LoadScene("Boss1_Map");
        yield return null;
        Assert.DoesNotThrow(RunLethalDebrisScenario);
        yield return new ExitPlayMode();
    }

    private static void RunLethalDebrisScenario()
    {
        var player = PrepareBossDamageScenario();
        var field = Object.FindFirstObjectByType<Boss1DebrisField>();
        var root = new GameObject("Runtime debris test");
        var floor = new GameObject("Debris test floor").AddComponent<BoxCollider2D>();
        floor.transform.position = player.transform.position + Vector3.down * 50f;
        var config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Boss1DebrisConfig>("Assets/Data/Boss/Boss1DebrisConfig.asset"));
        var pieces = (List<Boss1FallingDebris>)typeof(Boss1DebrisField).GetField("debrisPieces", Fields).GetValue(field);
        Set(field, "runtimeRoot", root);
        Set(field, "config", config);
        Set(field, "player", player.health);
        Set(field, "playerCollider", player.GetComponent<Collider2D>());
        Set(field, "floorCollider", floor);
        try
        {
            for (int i = 0; i < 2; i++)
            {
                var debris = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss1/Boss1Debris.prefab"), root.transform).GetComponent<Boss1FallingDebris>();
                debris.transform.position = player.transform.position;
                debris.BeginFalling(player.GetComponent<Collider2D>());
                pieces.Add(debris);
            }
            player.health.Died += field.Clear;
            Physics2D.SyncTransforms();
            Tick(field);
            Assert.That(player.health.IsDead, Is.True);
            Assert.That(pieces, Is.Empty);
        }
        finally { Object.DestroyImmediate(config); }
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying)
        {
            HitStopManager.PrepareForPause();
            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }
    }
}

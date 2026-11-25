using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PlayerWeaponSkillDamageTests
{
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void RestoreProgress(PlayerProgressData progress) => typeof(PlayerPermanentProgress)
        .GetMethod("Restore", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { progress });

    [UnityTest]
    public IEnumerator SkillsUseTheEquippedWeaponDamageAndAddUpgradeBonusOnce()
    {
        yield return new EnterPlayMode();
        VerifyRuntime();
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    // The same assertions can run through Unity MCP without switching or saving the Editor scene.
    public static List<object> VerifyRuntime()
    {
        Assert.That(Application.isPlaying, Is.True);
        PlayerProgressData originalProgress = PlayerPermanentProgress.CreateSnapshot();
        Random.State originalRandom = Random.state;
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var activeRoots = new List<GameObject>();
        foreach (GameObject root in roots)
        {
            if (!root.activeSelf) continue;
            activeRoots.Add(root);
            root.SetActive(false);
        }
        PlayerRuntimeState.ResetRunProgress();
        Time.timeScale = 1f;
        GameObject actor = null;
        GameObject enemyObject = null;
        GameObject camera = null;
        try
        {
            camera = new GameObject("Weapon skill damage test camera", typeof(Camera));
            camera.tag = "MainCamera";
            actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
            actor.GetComponent<WeaponUnlockOnSceneStart>().enabled = false;
            actor.transform.position = new Vector3(1000f, 1000f);
            actor.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            actor.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.None;
            Player player = actor.GetComponent<Player>();
            PlayerMovement movement = actor.GetComponent<PlayerMovement>();
            PlayerGroundSlam groundSlam = actor.GetComponent<PlayerGroundSlam>();
            PlayerCounter counter = actor.GetComponent<PlayerCounter>();
            PlayerWeaponController weapon = actor.GetComponent<PlayerWeaponController>();
            Set(player.health, "currentHP", player.health.MaxHP);
            Set(movement, "isGrounded", true);
            Set(groundSlam, "cooldown", 0f);
            Set(counter, "cooldown", 0f);
            weapon.EquipWeapon();
            player.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            player.anim.SetBool("isGrounded", true);
            player.anim.Update(0f);

            enemyObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyS.prefab"));
            enemyObject.GetComponent<EnemyAI>().enabled = false;
            enemyObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            EnemyHealth enemy = enemyObject.GetComponent<EnemyHealth>();
            Set(enemy, "currentHp", 1000000);
            Set(enemy, "scrapReward", 0);
            var results = new List<object>();
            for (int stageIndex = 0; stageIndex < weapon.CurrentWeapon.WeaponStages.Length; stageIndex++)
            {
                weapon.SetWeaponStage(stageIndex);
                player.anim.Rebind();
                player.anim.SetBool("isGrounded", true);
                player.anim.Update(0f);
                WeaponStageData stage = weapon.CurrentWeaponStage;
                foreach (int bonus in new[] { 0, 7 })
                {
                    var progress = JsonUtility.FromJson<PlayerProgressData>(JsonUtility.ToJson(originalProgress));
                    progress.attackPowerBonus = bonus;
                    RestoreProgress(progress);
                    int appliedBonus = PlayerPermanentProgress.AttackPowerBonus;
                    enemyObject.transform.position = actor.GetComponent<PlayerAttack>().AttackPoint.position;
                    Physics2D.SyncTransforms();

                    Assert.That(groundSlam.TryUse(), Is.True);
                    Random.State roll = Random.state;
                    int expectedA = Random.Range(stage.GroundSlamMinDamage, stage.GroundSlamMaxDamage + 1) + appliedBonus;
                    Random.state = roll;
                    int previousHP = enemy.CurrentHp;
                    groundSlam.ApplyGroundSlamDamage();
                    int actualA = previousHP - enemy.CurrentHp;
                    Assert.That(actualA, Is.EqualTo(expectedA), stage.StageName + " Ground slam");
                    groundSlam.ApplyGroundSlamDamage();
                    Assert.That(enemy.CurrentHp, Is.EqualTo(previousHP - expectedA), "Ground slam must hit once per execution.");
                    groundSlam.Cancel();
                    HitStopManager.PrepareForPause();

                    Assert.That(counter.TryUse(), Is.True);
                    counter.BeginCounterWindow();
                    Assert.That(counter.TryBlockDamage(), Is.True);
                    previousHP = enemy.CurrentHp;
                    counter.ApplyCounterDamage();
                    int actualS = previousHP - enemy.CurrentHp;
                    int expectedS = stage.CounterDamage + appliedBonus;
                    Assert.That(actualS, Is.EqualTo(expectedS), stage.StageName + " Counter");
                    counter.ApplyCounterDamage();
                    Assert.That(enemy.CurrentHp, Is.EqualTo(previousHP - expectedS), "Counter must hit once per execution.");
                    counter.Cancel();
                    HitStopManager.PrepareForPause();
                    results.Add(new { stage = stage.StageName, bonus = appliedBonus, groundSlam = actualA, counter = actualS });
                }
            }
            PlayerRuntimeState.ResetRunProgress();
            Assert.That(groundSlam.TryUse(), Is.False, "Ground slam needs an equipped weapon stage.");
            Assert.That(counter.TryUse(), Is.False, "Counter needs an equipped weapon stage.");
            return results;
        }
        finally
        {
            HitStopManager.PrepareForPause();
            RestoreProgress(originalProgress);
            Random.state = originalRandom;
            if (actor != null) Object.Destroy(actor);
            if (enemyObject != null) Object.Destroy(enemyObject);
            if (camera != null) Object.Destroy(camera);
            foreach (GameObject root in activeRoots) if (root != null) root.SetActive(true);
        }
    }
}

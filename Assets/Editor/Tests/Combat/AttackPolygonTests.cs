using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AttackPolygonTests
{
    private Scene testScene;
    private Scene originalScene;
    private PlayerProgressData originalProgress;

    private static void RestoreProgress(PlayerProgressData progress) => typeof(PlayerPermanentProgress)
        .GetMethod("Restore", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { progress });

    [SetUp]
    public void OpenTestScene()
    {
        originalProgress = PlayerPermanentProgress.CreateSnapshot();
        RestoreProgress(new PlayerProgressData());
        originalScene = SceneManager.GetActiveScene();
        // Unity's runner already supplies an untitled scene. Preview scenes do not
        // participate in the default Physics2D queries used by PlayerAttackDamage.
        testScene = string.IsNullOrEmpty(originalScene.path) ? originalScene :
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(testScene);
    }

    [Test]
    public void DamageExcludesConcaveGapsAndPreservesMirroringColliderEdgesAndDeduplication()
    {
        var point = new GameObject("Polygon test attack point");
        SceneManager.MoveGameObjectToScene(point, testScene);
        point.transform.position = new Vector3(1000f, 1000f);
        // A query must still work when the attack owner's physics simulation is suspended.
        point.AddComponent<Rigidbody2D>().simulated = false;
        var polygon = new AttackPolygon(new[]
        {
            new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 2f), new Vector2(0f, 2f)
        }, new[]
        {
            new Vector2(3f, 0f), new Vector2(4f, 0f),
            new Vector2(4f, 0.5f), new Vector2(3f, 0.5f)
        });
        var damage = new PlayerAttackDamage(point.transform, point.transform, LayerMask.GetMask("Enemy"));
        var positions = new[]
        {
            new Vector2(1.5f, 0.25f), // Inside the first path.
            new Vector2(1.5f, 1.5f),  // Inside its bounding box, outside its concave outline.
            new Vector2(2.6f, 0.25f), // Empty space between paths.
            new Vector2(3.5f, 0.25f), // Inside the second path.
            new Vector2(2.02f, 0.25f) // Center outside; collider edge overlaps the first path.
        };
        var enemies = new List<EnemyHealth>();
        foreach (Vector2 position in positions)
        {
            var enemy = new GameObject("Polygon test enemy");
            SceneManager.MoveGameObjectToScene(enemy, testScene);
            EnemyHealth health = enemy.AddComponent<EnemyHealth>();
            for (int i = 0; i < 2; i++)
            {
                var child = new GameObject("Enemy collider");
                child.transform.SetParent(enemy.transform, false);
                child.layer = LayerMask.NameToLayer("Enemy");
                child.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
            }
            enemies.Add(health);
        }

        try
        {
            foreach (float facing in new[] { 1f, -1f })
            {
                point.transform.localScale = new Vector3(2f * facing, 1.5f, 1f);
                point.transform.rotation = Quaternion.Euler(0f, 0f, 30f);
                for (int i = 0; i < enemies.Count; i++)
                {
                    enemies[i].transform.position = point.transform.TransformPoint(positions[i]);
                    typeof(EnemyHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(enemies[i], 100);
                }
                int accepted = 0;
                damage.BeginAttack();
                damage.Apply(point.transform, polygon, 7, _ => accepted++);
                damage.Apply(point.transform, polygon, 7, _ => accepted++);
                Assert.That(accepted, Is.EqualTo(3));
                Assert.That(enemies[0].CurrentHp, Is.EqualTo(93));
                Assert.That(enemies[1].CurrentHp, Is.EqualTo(100), "Concave gap must miss.");
                Assert.That(enemies[2].CurrentHp, Is.EqualTo(100), "Disconnected paths must keep their gap.");
                Assert.That(enemies[3].CurrentHp, Is.EqualTo(93));
                Assert.That(enemies[4].CurrentHp, Is.EqualTo(93), "Use collider overlap, not target center.");
                damage.EndAttack();
            }
            damage.Apply(point.transform, new AttackPolygon(), 7);
            Assert.That(enemies[0].CurrentHp, Is.EqualTo(93), "An unconfigured polygon must not hit.");
        }
        finally
        {
            damage.EndAttack();
            Object.DestroyImmediate(point);
            foreach (EnemyHealth enemy in enemies) Object.DestroyImmediate(enemy.gameObject);
        }
    }

    [Test]
    public void WeaponAndSkillDamageReceiveThePermanentAttackBonusOnce()
    {
        PlayerProgressData original = PlayerPermanentProgress.CreateSnapshot();
        MethodInfo restore = typeof(PlayerPermanentProgress).GetMethod("Restore",
            BindingFlags.Static | BindingFlags.NonPublic);
        var point = new GameObject("Polygon bonus test point");
        SceneManager.MoveGameObjectToScene(point, testScene);
        point.transform.position = new Vector3(1000f, 1000f);
        var enemyObject = new GameObject("Polygon bonus test enemy");
        SceneManager.MoveGameObjectToScene(enemyObject, testScene);
        enemyObject.transform.position = point.transform.position;
        enemyObject.layer = LayerMask.NameToLayer("Enemy");
        enemyObject.AddComponent<BoxCollider2D>();
        EnemyHealth enemy = enemyObject.AddComponent<EnemyHealth>();
        typeof(EnemyHealth).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(enemy, 100);
        var polygon = new AttackPolygon(new[]
        {
            new Vector2(-1f, -1f), new Vector2(1f, -1f),
            new Vector2(1f, 1f), new Vector2(-1f, 1f)
        });
        var weapon = new WeaponAttackData();
        typeof(WeaponAttackData).GetField("attackPolygon", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(weapon, polygon);
        typeof(WeaponAttackData).GetField("damage", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(weapon, 3);
        var damage = new PlayerAttackDamage(point.transform, point.transform, LayerMask.GetMask("Enemy"));
        try
        {
            restore.Invoke(null, new object[] { new PlayerProgressData { attackPowerBonus = 7 } });
            damage.BeginAttack();
            damage.Apply(weapon);
            Assert.That(enemy.CurrentHp, Is.EqualTo(90));
            damage.BeginAttack();
            damage.Apply(point.transform, polygon, 3);
            Assert.That(enemy.CurrentHp, Is.EqualTo(80));
        }
        finally
        {
            damage.EndAttack();
            restore.Invoke(null, new object[] { original });
            Object.DestroyImmediate(point);
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void AllWeaponAttacksAndBothSkillsHaveConfiguredPolygons()
    {
        WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapon/WD_Weapon.asset");
        foreach (WeaponStageData stage in weapon.WeaponStages)
        {
            Assert.That(stage.AirAttack.AttackPolygon.IsValid, Is.True);
            foreach (WeaponAttackData attack in stage.ComboAttacks)
                Assert.That(attack.AttackPolygon.IsValid, Is.True);
        }
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        foreach (Component skill in new Component[] { player.GetComponent<PlayerGroundSlam>(), player.GetComponent<PlayerCounter>() })
        {
            var polygon = (AttackPolygon)skill.GetType().GetField("attackPolygon",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(skill);
            Assert.That(polygon.IsValid, Is.True, skill.GetType().Name);
        }
    }

    [TearDown]
    public void CloseTestScene()
    {
        if (testScene.IsValid() && testScene != originalScene)
        {
            SceneManager.SetActiveScene(originalScene);
            EditorSceneManager.CloseScene(testScene, true);
        }
        if (originalProgress != null) RestoreProgress(originalProgress);
    }
}

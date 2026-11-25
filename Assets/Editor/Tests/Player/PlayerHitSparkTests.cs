using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class PlayerHitSparkTests
{
    private const string EffectPath = "Assets/Prefabs/CombatEffects/PipeScrapHit.prefab";
    private PlayerProgressData originalProgress;

    private static void RestoreProgress(PlayerProgressData progress) =>
        typeof(PlayerPermanentProgress).GetMethod("Restore", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { progress });

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
        if (originalProgress != null)
        {
            RestoreProgress(originalProgress);
            originalProgress = null;
        }
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void PlayerUsesPixelFxPrefabWithCrispFramesAndAnIndependentLifetime()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath);
        Assert.That(player.GetComponent<PlayerAttack>().HitEffectPrefab,
            Is.EqualTo(prefab.GetComponent<CombatAnimationEffect>()));
        Assert.That(prefab.GetComponent<SpriteRenderer>(), Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<Collider2D>(true), Is.Empty);

        var animator = prefab.GetComponent<Animator>();
        Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
        Assert.That(animator.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal));
        var clip = animator.runtimeAnimatorController.animationClips.Single();
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        Assert.That(keys.Length, Is.EqualTo(15));
        Assert.That(clip.frameRate, Is.EqualTo(60));
        Assert.That(clip.isLooping, Is.False);
        Assert.That(AnimationUtility.GetAnimationEvents(clip).Single().functionName,
            Is.EqualTo(nameof(CombatAnimationEffect.FinishEffect)));

        foreach (var key in keys)
        {
            var sprite = (Sprite)key.value;
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(64));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(40, 36)));
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
        }
    }

    [UnityTest]
    public IEnumerator EveryEnemyAndBossGetsOneSparkPerAcceptedHitAndEffectsCleanUp()
    {
        yield return new EnterPlayMode();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        originalProgress = PlayerPermanentProgress.CreateSnapshot();
        RestoreProgress(new PlayerProgressData());

        var effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath).GetComponent<CombatAnimationEffect>();
        var source = new GameObject("Hit spark test attacker");
        source.transform.position = new Vector3(1000, 1000);
        var point = new GameObject("Hit spark test query");
        point.transform.position = source.transform.position;
        var polygon = new AttackPolygon(new[]
        {
            new Vector2(-1, -3), new Vector2(10, -3),
            new Vector2(10, 3), new Vector2(-1, 3)
        });
        var enemies = new EnemyHealth[3];
        string[] names = { "EnemyS", "EnemyL", "EnemyB" };
        for (int i = 0; i < names.Length; i++)
        {
            var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemy/{names[i]}.prefab"));
            enemy.transform.position = new Vector3(1002 + i * 2, 1000);
            if (enemy.TryGetComponent(out EnemyAI ai)) ai.enabled = false;
            if (enemy.TryGetComponent(out EnemyBChargeController charge)) charge.enabled = false;
            enemy.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            enemies[i] = enemy.GetComponent<EnemyHealth>();
            Set(enemies[i], "currentHp", 1000000);
            Set(enemies[i], "scrapReward", 0);
            var extra = new GameObject("Duplicate hurt collider");
            extra.transform.SetParent(enemy.transform, false);
            extra.layer = LayerMask.NameToLayer("Enemy");
            extra.AddComponent<BoxCollider2D>().isTrigger = true;
        }
        var boss = new GameObject("Hit spark test boss");
        boss.transform.position = new Vector3(1008, 1000);
        boss.layer = LayerMask.NameToLayer("Enemy");
        var bossCollider = boss.AddComponent<BoxCollider2D>();
        bossCollider.isTrigger = true;
        bossCollider.size = new Vector2(2, 8);
        bossCollider.offset = new Vector2(0, 3);
        boss.AddComponent<BossHealth>();

        var damage = new PlayerAttackDamage(point.transform, source.transform, LayerMask.GetMask("Enemy"), effectPrefab);
        damage.BeginAttack();
        damage.Apply(point.transform, polygon, 0);
        Assert.That(Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None), Is.Empty);

        damage.BeginAttack();
        damage.Apply(point.transform, polygon, 1);
        damage.Apply(point.transform, polygon, 1);
        var first = Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None);
        Assert.That(first.Length, Is.EqualTo(4), "Child colliders and repeated hit windows must not duplicate sparks.");
        Assert.That(first.All(effect => !effect.GetComponent<SpriteRenderer>().flipX), Is.True);
        Assert.That(first.All(effect => effect.transform.parent == null), Is.True);
        Assert.That(first.OrderBy(effect => effect.transform.position.x).Last().transform.position.y,
            Is.EqualTo(point.transform.position.y).Within(0.001f),
            "A tall boss must show the spark at the attack height, not at its body center.");

        source.transform.position = new Vector3(1010, 1000);
        damage.BeginAttack();
        damage.Apply(point.transform, polygon, 1);
        var all = Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None);
        var second = all.Except(first).ToArray();
        Assert.That(second.Length, Is.EqualTo(4));
        Assert.That(second.All(effect => effect.GetComponent<SpriteRenderer>().flipX), Is.True);
        Assert.That(second.Min(effect => effect.transform.position.x), Is.GreaterThan(first.Min(effect => effect.transform.position.x)));
        damage.EndAttack();

        // Edit Mode UnityTests must yield frames after EnterPlayMode to advance Animators.
        float deadline = Time.realtimeSinceStartup + 1f;
        while (all.Any(effect => effect != null) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None), Is.Empty,
            "The animation completion event must remove sparks even when they are off camera.");
    }
}

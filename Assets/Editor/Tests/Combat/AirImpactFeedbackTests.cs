using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

[Category("PlayerRefactor")]
public sealed class AirImpactFeedbackTests
{
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static T Get<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

    [UnityTest]
    public IEnumerator AirHitFreezesGameplayWhileVisualsContinueAndRestoresOwnership()
    {
        yield return new EnterPlayMode();
        // These are play-mode copies; the user's unsaved Editor scene is preserved on exit.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        var cameraObject = new GameObject("Impact test camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        var playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        Player player = playerObject.GetComponent<Player>();
        playerObject.transform.position = new Vector3(1000f, 1000f);
        var enemyObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyS.prefab"));
        enemyObject.transform.position = new Vector3(1010f, 1000f);
        enemyObject.GetComponent<EnemyAI>().enabled = false;
        EnemyHealth health = enemyObject.GetComponent<EnemyHealth>();
        Set(health, "currentHp", 1000000);
        Set(health, "scrapReward", 0);
        var flash = enemyObject.GetComponent<HitImpactFlash>();
        var sprite = enemyObject.transform.Find("Animator").GetComponent<SpriteRenderer>();
        Material original = sprite.sharedMaterial;
        var initialBlock = new MaterialPropertyBlock();
        initialBlock.SetFloat("_ImpactTestSentinel", 0.42f);
        sprite.SetPropertyBlock(initialBlock);
        var point = new GameObject("Impact test attack point");
        point.transform.position = enemyObject.transform.position;
        var data = new WeaponAttackData();
        Set(data, "attackPolygon", new AttackPolygon(new[]
        {
            new Vector2(-2f, -2f), new Vector2(2f, -2f),
            new Vector2(2f, 2f), new Vector2(-2f, 2f)
        }));
        var damage = new PlayerAttackDamage(point.transform, player.transform, LayerMask.GetMask("Enemy"));
        var feedback = new PlayerAttackFeedback();
        feedback.Enable(player);
        yield return null;
        Physics2D.SyncTransforms();

        // Damage without a feedback callback does not trigger visual effects by itself.
        int hp = health.CurrentHp;
        damage.BeginAttack();
        damage.Apply(data);
        Assert.That(health.CurrentHp, Is.LessThan(hp));
        Assert.That(HitStopManager.IsActive, Is.False);
        Assert.That(flash.IsPlaying, Is.False);

        // A miss and rejected damage must not call the air-hit feedback.
        point.transform.position += Vector3.right * 100f;
        damage.BeginAttack();
        damage.Apply(data, feedback.Play);
        Assert.That(HitStopManager.IsActive, Is.False);
        point.transform.position = enemyObject.transform.position;
        Set(data, "damage", -int.MaxValue);
        damage.BeginAttack();
        damage.Apply(data, feedback.Play);
        Assert.That(HitStopManager.IsActive, Is.False);
        Set(data, "damage", 1);

        // Production default restores the previous non-unit speed.
        Time.timeScale = 0.5f;
        damage.BeginAttack();
        damage.Apply(data, feedback.Play);
        Assert.That(HitStopManager.IsActive, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(flash.IsPlaying, Is.True);
        Assert.That(player.CanMove, Is.False);
        Assert.That(player.CanStartAction, Is.False);
        Assert.That(sprite.sharedMaterial.shader.name, Is.EqualTo("ProjectK/SpriteLitHitImpactFlash"));
        var manager = Object.FindFirstObjectByType<HitStopManager>();
        float deadline = Get<float>(manager, "endTime");
        HitStopManager.Request(player, 10f);
        Assert.That(Get<float>(manager, "endTime"), Is.EqualTo(deadline));
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(Time.timeScale, Is.EqualTo(0.5f));
        Assert.That(flash.IsPlaying, Is.False);
        Assert.That(sprite.sharedMaterial, Is.SameAs(original));
        var block = new MaterialPropertyBlock();
        sprite.GetPropertyBlock(block);
        Assert.That(block.GetFloat("_ImpactTestSentinel"), Is.EqualTo(0.42f));

        // A longer observation window verifies independent clocks across actual frames.
        HitStopManager.Request(player, 0.3f);
        flash.Play();
        HitCameraShake.Play(player, 0.2f, 0.08f, Vector2.one);
        Rigidbody2D body = enemyObject.GetComponent<Rigidbody2D>();
        Vector2 frozenPosition = body.position;
        Animator animator = enemyObject.GetComponentInChildren<Animator>();
        yield return null;
        float frozenAnimation = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        Set(playerObject.GetComponent<PlayerMovement>(), "moveInput", Vector2.left);
        Vector3 facing = player.transform.localScale;
        yield return new WaitForSecondsRealtime(0.16f);
        Assert.That(body.position, Is.EqualTo(frozenPosition));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(frozenAnimation).Within(0.0001f));
        Assert.That(player.transform.localScale, Is.EqualTo(facing));
        sprite.GetPropertyBlock(block);
        Assert.That(block.GetFloat("_FlashAmount"), Is.LessThan(0.65f));
        var shake = camera.GetComponent<HitCameraShake>();
        Assert.That(Get<Vector3>(shake, "appliedOffset").sqrMagnitude, Is.GreaterThan(0f));
        Assert.That(camera.orthographicSize, Is.EqualTo(5f));
        HitCameraShake.Cancel(player);
        Assert.That(camera.transform.position, Is.EqualTo(Vector3.zero));

        // Pause takes the original speed without being undone by a stale stop timer.
        float resumeScale = HitStopManager.PrepareForPause();
        Assert.That(resumeScale, Is.EqualTo(0.5f));
        Time.timeScale = 0f;
        HitStopManager.Request(player, 0.04f);
        yield return new WaitForSecondsRealtime(0.35f);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(HitStopManager.IsActive, Is.False);
        Time.timeScale = resumeScale;
        HitStopManager.Request(player, 1f);
        Time.timeScale = 0.25f;
        yield return null;
        Assert.That(Time.timeScale, Is.EqualTo(0.25f));
        Assert.That(HitStopManager.IsActive, Is.False);

        // Disable restores Flash and releases a stop owned by the disabled object.
        flash.Play();
        enemyObject.SetActive(false);
        Assert.That(sprite.sharedMaterial, Is.SameAs(original));
        HitStopManager.Request(player, 1f);
        player.enabled = false;
        yield return null;
        Assert.That(HitStopManager.IsActive, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(0.25f));
        player.enabled = true;
        enemyObject.SetActive(true);
        Set(health, "currentHp", 1);
        point.transform.position = enemyObject.transform.position;
        Physics2D.SyncTransforms();
        damage.BeginAttack();
        damage.Apply(data, feedback.Play);
        Assert.That(health.IsDead, Is.True);
        Assert.That(HitStopManager.IsActive, Is.True);
        Assert.That(camera.GetComponent<HitCameraShake>(), Is.Not.Null);
        float removalDeadline = Time.realtimeSinceStartup + Get<float>(health, "deathFlashDuration") + 1f;
        while (enemyObject != null && Time.realtimeSinceStartup < removalDeadline) yield return null;
        Assert.That(enemyObject == null, Is.True, "The enemy must be removed after its death flash.");
        yield return new WaitForSecondsRealtime(0.15f);
        Assert.That(Time.timeScale, Is.EqualTo(0.25f));

        var temporaryScene = SceneManager.CreateScene("Impact unload verification");
        HitStopManager.Request(player, 1f);
        yield return SceneManager.UnloadSceneAsync(temporaryScene);
        Assert.That(HitStopManager.IsActive, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(0.25f));
        Time.timeScale = 1f;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator BossAirImpactUsesConfiguredRenderersAndPlayerAttackEntryPoint()
    {
        yield return new EnterPlayMode();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        Scene scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/Scenes/Boss1_Map.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        yield return null;
        BossHealth boss = null;
        Player player = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            BossHealth candidateBoss = root.GetComponentInChildren<BossHealth>(true);
            Player candidatePlayer = root.GetComponentInChildren<Player>(true);
            if (candidateBoss != null) boss = candidateBoss;
            else if (candidatePlayer != null) player = candidatePlayer;
            else if (root.GetComponent<Canvas>() != null) root.SetActive(false);
        }
        Assert.That(boss, Is.Not.Null);
        Assert.That(player, Is.Not.Null);
        var scheduler = boss.GetComponent<Boss1PatternScheduler>();
        if (scheduler != null)
        {
            scheduler.StopSchedulingAndCancelPattern();
            scheduler.enabled = false;
        }
        yield return null;
        var flash = boss.GetComponent<HitImpactFlash>();
        var renderers = Get<SpriteRenderer[]>(flash, "targetRenderers");
        Assert.That(renderers, Is.Not.Empty);
        foreach (SpriteRenderer renderer in renderers)
        {
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.transform.IsChildOf(boss.transform), Is.True);
            Assert.That(renderer.transform.IsChildOf(boss.transform.Find("Warnings")), Is.False);
        }

        PlayerAttack attack = player.GetComponent<PlayerAttack>();
        PlayerAirAttack air = Get<PlayerAirAttack>(attack, "airAttack");
        var data = new WeaponAttackData();
        Set(data, "attackPolygon", new AttackPolygon(new[]
        {
            new Vector2(-50f, -50f), new Vector2(50f, -50f),
            new Vector2(50f, 50f), new Vector2(-50f, 50f)
        }));
        Set(air, "attackData", data);
        attack.AttackPoint.position = boss.transform.position;
        Physics2D.SyncTransforms();
        int hp = boss.CurrentHp;
        attack.ApplyAirAttackDamage();
        Assert.That(boss.CurrentHp, Is.LessThan(hp));
        Assert.That(flash.IsPlaying, Is.True);
        Assert.That(HitStopManager.IsActive, Is.True);
        Assert.That(player.CanStartAction, Is.False);
        var block = new MaterialPropertyBlock();
        foreach (SpriteRenderer renderer in renderers)
        {
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_FlashAmount"), Is.GreaterThan(0f));
        }
        attack.FinishAirAttack();
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(HitStopManager.IsActive, Is.False);
        Assert.That(flash.IsPlaying, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        yield return new ExitPlayMode();
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

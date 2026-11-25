using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[Category("PlayerRefactor")]
public sealed class PlayerFunctionalRefactorTests
{
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void RestoreProgress(PlayerProgressData progress) =>
        typeof(PlayerPermanentProgress).GetMethod("Restore", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { progress });

    private static void DisableSceneRoots()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
    }

    private static Player CreatePlayer()
    {
        GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        instance.transform.position = new Vector3(1000f, 1000f);
        instance.GetComponent<Rigidbody2D>().simulated = false;
        instance.GetComponent<PlayerMovement>().enabled = false;
        Player player = instance.GetComponent<Player>();
        Set(player.health, "currentHP", player.health.MaxHP);
        return player;
    }

    [UnityTest]
    public IEnumerator DamageDeduplicatesChildCollidersAndAppliesUpgradeBonusToWeaponsAndSkills()
    {
        yield return new EnterPlayMode();
        DisableSceneRoots();
        PlayerProgressData original = PlayerPermanentProgress.CreateSnapshot();
        try
        {
            RestoreProgress(new PlayerProgressData { attackPowerBonus = 7 });
            Player player = CreatePlayer();
            GameObject enemyObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyS.prefab"));
            enemyObject.transform.position = new Vector3(1003f, 1000f);
            enemyObject.GetComponent<EnemyAI>().enabled = false;
            EnemyHealth enemy = enemyObject.GetComponent<EnemyHealth>();
            Set(enemy, "currentHp", 1000000);
            Set(enemy, "scrapReward", 0);
            var extraCollider = new GameObject("Additional enemy collider");
            extraCollider.transform.SetParent(enemyObject.transform, false);
            extraCollider.layer = LayerMask.NameToLayer("Enemy");
            extraCollider.AddComponent<BoxCollider2D>().isTrigger = true;
            // Keep colliders queryable while preventing enemy physics from moving them.
            enemyObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var point = new GameObject("Refactor damage point");
            point.transform.position = enemyObject.transform.position;
            var weapon = new WeaponAttackData();
            Set(weapon, "damage", 3);
            var polygon = new AttackPolygon(new[]
            {
                new Vector2(-2f, -2f), new Vector2(2f, -2f),
                new Vector2(2f, 2f), new Vector2(-2f, 2f)
            });
            Set(weapon, "attackPolygon", polygon);
            var damage = new PlayerAttackDamage(point.transform, player.transform, LayerMask.GetMask("Enemy"));
            Physics2D.SyncTransforms();
            int originalHP = enemy.CurrentHp;
            int acceptedHits = 0;
            damage.BeginAttack();
            damage.Apply(weapon, _ => acceptedHits++);
            damage.Apply(weapon, _ => acceptedHits++);
            Assert.That(enemy.CurrentHp, Is.EqualTo(originalHP - 10));
            Assert.That(acceptedHits, Is.EqualTo(1));
            damage.EndAttack();
            damage.BeginAttack();
            damage.Apply(point.transform, polygon, 3, _ => acceptedHits++);
            damage.Apply(point.transform, polygon, 3, _ => acceptedHits++);
            Assert.That(enemy.CurrentHp, Is.EqualTo(originalHP - 20));
            Assert.That(acceptedHits, Is.EqualTo(2));
            damage.EndAttack();
        }
        finally { RestoreProgress(original); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SkillInterruptionsReleaseControlAndDoNotRestartCooldownOnRepeatedCleanup()
    {
        yield return new EnterPlayMode();
        DisableSceneRoots();
        Player player = CreatePlayer();
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        PlayerGroundSlam groundSlam = player.GetComponent<PlayerGroundSlam>();
        PlayerCounter counter = player.GetComponent<PlayerCounter>();
        player.GetComponent<PlayerWeaponController>().EquipWeapon();
        Set(groundSlam, "cooldown", 5f);
        Set(counter, "cooldown", 7f);
        Set(movement, "isGrounded", false);
        Assert.That(counter.TryUse(), Is.False);
        Set(movement, "isGrounded", true);
        Set(movement, "hasAppliedJumpForce", false);
        player.anim.SetBool("isGrounded", true);
        player.anim.Play("Base Layer.PlayerIdle", 0, 0f);
        player.anim.Update(0f);
        Assert.That(groundSlam.TryUse(), Is.True);
        Assert.That(player.CanMove, Is.False);
        Assert.That(player.CanTurn, Is.False);
        Assert.That(counter.TryUse(), Is.False);
        player.CancelCombatActions();
        Assert.That(groundSlam.IsExecuting, Is.False);
        Assert.That(player.CanStartAction, Is.True);
        yield return null;
        float remaining = groundSlam.CooldownRemaining;
        groundSlam.Cancel();
        Assert.That(groundSlam.CooldownRemaining, Is.EqualTo(remaining));
        Assert.That(groundSlam.TryUse(), Is.False);
        Assert.That(counter.TryUse(), Is.True);
        counter.BeginCounterWindow();
        Assert.That(player.health.TryTakeDamage(new DamageInfo(1)), Is.False);
        Assert.That(counter.ReceivedAttack, Is.True);
        player.CancelCombatActions();
        Assert.That(counter.IsExecuting, Is.False);
        Assert.That(counter.IsInvincible, Is.False);
        yield return null;
        remaining = counter.CooldownRemaining;
        counter.Cancel();
        Assert.That(counter.CooldownRemaining, Is.EqualTo(remaining));
        Assert.That(counter.TryUse(), Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator GroundSupportAndPlatformDropKeepCollisionUntilTheSurfaceIsCleared()
    {
        yield return new EnterPlayMode();
        DisableSceneRoots();
        var playerObject = new GameObject("Ground sensor player");
        playerObject.transform.position = new Vector3(1000f, 1000f);
        playerObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var playerCollider = playerObject.AddComponent<BoxCollider2D>();
        var surfaceObject = new GameObject("One-way test platform");
        surfaceObject.layer = LayerMask.NameToLayer("Ground");
        surfaceObject.transform.position = new Vector3(1000f, 999.45f);
        var surface = surfaceObject.AddComponent<BoxCollider2D>();
        surface.size = new Vector2(3f, 0.1f);
        surfaceObject.AddComponent<PlatformEffector2D>().useOneWay = true;
        surface.usedByEffector = true;
        var solidObject = new GameObject("Solid test surface");
        solidObject.layer = surfaceObject.layer;
        solidObject.transform.position = new Vector3(1000f, 998f);
        var solid = solidObject.AddComponent<BoxCollider2D>();
        var drop = new PlayerPlatformDrop(playerCollider);
        var sensor = new PlayerGroundSensor(playerCollider);
        int groundLayer = LayerMask.GetMask("Ground");
        Physics2D.SyncTransforms();
        Assert.That(sensor.Probe(0.05f, 0.9f, 0.5f, groundLayer, drop), Is.True);
        Assert.That(sensor.SupportingCollider, Is.SameAs(surface));
        Assert.That(drop.TryBegin(solid, groundLayer), Is.False);
        Assert.That(drop.TryBegin(sensor.SupportingCollider, groundLayer), Is.True);
        Assert.That(Physics2D.GetIgnoreCollision(playerCollider, surface), Is.True);
        Assert.That(sensor.Probe(0.05f, 0.9f, 0.5f, groundLayer, drop), Is.False);
        drop.RestoreClearedCollisions(0.05f);
        Assert.That(Physics2D.GetIgnoreCollision(playerCollider, surface), Is.True);
        playerObject.transform.position += Vector3.up * 2f;
        Physics2D.SyncTransforms();
        drop.RestoreClearedCollisions(0.05f);
        Assert.That(Physics2D.GetIgnoreCollision(playerCollider, surface), Is.False);
        Assert.That(drop.TryBegin(surface, groundLayer), Is.True);
        drop.RestoreAllCollisions();
        Assert.That(Physics2D.GetIgnoreCollision(playerCollider, surface), Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PauseOwnershipRestoresOnlySuspendedInputAudioAndEventSystems()
    {
        yield return new EnterPlayMode();
        DisableSceneRoots();
        Player player = CreatePlayer();
        var otherOwner = new GameObject("Other pause owner").AddComponent<Camera>();
        var eventSystem = new GameObject("Active event system").AddComponent<EventSystem>();
        var disabledEvents = new GameObject("Disabled event system").AddComponent<EventSystem>();
        disabledEvents.enabled = false;
        Time.timeScale = 0.4f;
        bool originalAudioPause = AudioListener.pause;
        GameplayPause.Handle first = null;
        GameplayPause.Handle second = null;
        try
        {
            AudioListener.pause = false;
            Assert.That(GameplayPause.TryAcquire(player, out first), Is.True);
            first.PausePlayer(player);
            first.PauseAudio();
            first.PauseEventSystems();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(player.input.Player.enabled, Is.False);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(eventSystem.enabled, Is.False);
            Assert.That(GameplayPause.TryAcquire(otherOwner, out _), Is.False);
            first.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(0.4f));
            Assert.That(player.input.Player.enabled, Is.True);
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(eventSystem.enabled, Is.True);
            Assert.That(disabledEvents.enabled, Is.False);
            player.input.Player.Disable();
            Assert.That(GameplayPause.TryAcquire(otherOwner, out second), Is.True);
            second.PausePlayer(player);
            first.Dispose();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(GameplayPause.IsActive, Is.True);
            Time.timeScale = 0.75f;
            second.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
            Assert.That(player.input.Player.enabled, Is.False);
        }
        finally
        {
            first?.Dispose();
            second?.Dispose();
            AudioListener.pause = originalAudioPause;
            Time.timeScale = 1f;
        }
        yield return new ExitPlayMode();
    }

    [Test]
    public void ResettingARunPreservesPermanentProgressAndItsSaveSchema()
    {
        PlayerProgressData permanent = PlayerPermanentProgress.CreateSnapshot();
        bool initialized = PlayerRuntimeState.IsInitialized;
        int hp = PlayerRuntimeState.CurrentHP;
        bool hasWeapon = PlayerRuntimeState.HasWeapon;
        int stage = PlayerRuntimeState.WeaponStageIndex;
        try
        {
            RestoreProgress(new PlayerProgressData { version = 1, scrap = 31, attackPowerBonus = 4, healthPieces = 7 });
            PlayerRuntimeState.InitializeIfNeeded(10);
            PlayerRuntimeState.SetHealth(3);
            PlayerRuntimeState.UnlockWeapon();
            PlayerRuntimeState.SetWeaponStage(2);
            PlayerRuntimeState.ResetRunProgress();
            Assert.That(PlayerRuntimeState.IsInitialized, Is.False);
            Assert.That(PlayerRuntimeState.CurrentHP, Is.Zero);
            Assert.That(PlayerRuntimeState.HasWeapon, Is.False);
            Assert.That(PlayerRuntimeState.WeaponStageIndex, Is.Zero);
            Assert.That(PlayerPermanentProgress.Scrap, Is.EqualTo(31));
            Assert.That(PlayerPermanentProgress.AttackPowerBonus, Is.EqualTo(4));
            Assert.That(PlayerPermanentProgress.BonusMaxHP, Is.EqualTo(2));
            string json = JsonUtility.ToJson(PlayerPermanentProgress.CreateSnapshot());
            Assert.That(PlayerProgressStore.IsValid(JsonUtility.FromJson<PlayerProgressData>(json)), Is.True);
            Assert.That(json, Does.Not.Contain("CurrentHP").And.Not.Contain("currentHP").And.Not.Contain("weapon"));
        }
        finally
        {
            RestoreProgress(permanent);
            PlayerRuntimeState.ResetRunProgress();
            if (initialized) PlayerRuntimeState.InitializeIfNeeded(hp);
            PlayerRuntimeState.SetHealth(hp);
            if (hasWeapon) PlayerRuntimeState.UnlockWeapon();
            PlayerRuntimeState.SetWeaponStage(stage);
        }
    }

    [TestCase("Assets/Scenes/Respawn_Map.unity", true)]
    [TestCase("Assets/Scenes/X_Map.unity", false)]
    public void SceneEntryRestoresHealthOnlyInTheConfiguredRespawnScene(string scenePath, bool restoresHealth)
    {
        PlayerProgressData permanent = PlayerPermanentProgress.CreateSnapshot();
        bool initialized = PlayerRuntimeState.IsInitialized;
        int hp = PlayerRuntimeState.CurrentHP;
        bool hasWeapon = PlayerRuntimeState.HasWeapon;
        int stage = PlayerRuntimeState.WeaponStageIndex;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(scenePath);
        PlayerHealth health = null;
        try
        {
            RestoreProgress(new PlayerProgressData { bonusMaxHP = 3 });
            PlayerRuntimeState.InitializeIfNeeded(10);
            PlayerRuntimeState.SetHealth(2);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                health = root.GetComponentInChildren<PlayerHealth>(true);
                if (health != null) break;
            }
            Assert.That(health, Is.Not.Null);
            typeof(PlayerHealth).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(health, null);

            Assert.That(health.MaxHP, Is.EqualTo(health.GetComponent<Player>().Data.MaxHP + 3));
            int expectedHP = restoresHealth ? health.MaxHP : 2;
            Assert.That(health.CurrentHP, Is.EqualTo(expectedHP));
            Assert.That(PlayerRuntimeState.CurrentHP, Is.EqualTo(expectedHP));
        }
        finally
        {
            if (health != null)
                typeof(PlayerHealth).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(health, null);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            RestoreProgress(permanent);
            PlayerRuntimeState.ResetRunProgress();
            if (initialized) PlayerRuntimeState.InitializeIfNeeded(hp);
            PlayerRuntimeState.SetHealth(hp);
            if (hasWeapon) PlayerRuntimeState.UnlockWeapon();
            PlayerRuntimeState.SetWeaponStage(stage);
        }
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

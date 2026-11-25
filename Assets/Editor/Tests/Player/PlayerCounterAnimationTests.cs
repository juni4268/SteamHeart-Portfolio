using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[Category("PlayerRefactor")]
public sealed class PlayerCounterAnimationTests
{
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static void AdvanceToEvent(Animator animator, string eventName)
    {
        animator.Update(0f);
        animator.Update(0.001f);
        AnimationClip clip = animator.GetCurrentAnimatorClipInfo(0).Single().clip;
        float eventTime = AnimationUtility.GetAnimationEvents(clip)
            .Single(e => e.functionName == eventName).time;
        float elapsed = animator.GetCurrentAnimatorStateInfo(0).normalizedTime * clip.length;
        animator.Update(Mathf.Max(0f, eventTime - elapsed) + 0.001f);
    }

    [UnityTest]
    public IEnumerator MissFinishesAndCounterKeepsControlLockedUntilItsAttackEnds()
    {
        yield return new EnterPlayMode();
        // Work on play-mode copies; preserve the user's unsaved Editor scene.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        var cameraObject = new GameObject("Counter test camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        var playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        playerObject.transform.position = new Vector3(1000f, 1000f);
        Player player = playerObject.GetComponent<Player>();
        PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
        PlayerCounter skill = playerObject.GetComponent<PlayerCounter>();
        movement.enabled = false;
        playerObject.GetComponent<Rigidbody2D>().simulated = false;
        Set(player.health, "currentHP", player.health.MaxHP - 3);
        Set(skill, "cooldown", 0f);
        PlayerWeaponController weapon = playerObject.GetComponent<PlayerWeaponController>();
        weapon.EquipWeapon();
        Set(movement, "isGrounded", true);
        Set(movement, "hasAppliedJumpForce", false);
        Animator animator = player.anim;
        animator.SetBool("isGrounded", true);
        animator.Rebind();
        animator.Update(0f);

        var enemyObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyS.prefab"));
        enemyObject.GetComponent<EnemyAI>().enabled = false;
        enemyObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        enemyObject.transform.position = playerObject.GetComponent<PlayerAttack>().AttackPoint.position;
        EnemyHealth enemy = enemyObject.GetComponent<EnemyHealth>();
        Set(enemy, "currentHp", 1000000);
        Set(enemy, "scrapReward", 0);
        // Multiple colliders on one enemy must not grant additional healing.
        enemyObject.AddComponent<BoxCollider2D>();
        var secondEnemyObject = Object.Instantiate(enemyObject);
        EnemyHealth secondEnemy = secondEnemyObject.GetComponent<EnemyHealth>();
        Set(secondEnemy, "currentHp", 1000000);
        Physics2D.SyncTransforms();

        // Reject airborne input without entering the skill.
        Set(movement, "isGrounded", false);
        Assert.That(skill.TryUse(), Is.False);
        Set(movement, "isGrounded", true);

        int enemyHP = enemy.CurrentHp;
        int playerHP = player.health.CurrentHP;
        Assert.That(skill.TryUse(), Is.True);
        AdvanceToEvent(animator, nameof(PlayerCounterAnimationEvents.BeginCounterWindow));
        Assert.That(skill.IsInvincible, Is.True);
        AdvanceToEvent(animator, nameof(PlayerCounterAnimationEvents.EndCounterWindow));
        Assert.That(skill.IsInvincible, Is.False);
        Assert.That(skill.IsExecuting, Is.True);
        Assert.That(player.CanMove, Is.False);
        animator.Update(animator.GetCurrentAnimatorStateInfo(0).length);
        animator.Update(0f);
        Assert.That(skill.IsExecuting, Is.False);
        Assert.That(enemy.CurrentHp, Is.EqualTo(enemyHP));
        Assert.That(secondEnemy.CurrentHp, Is.EqualTo(enemyHP));
        Assert.That(player.health.CurrentHP, Is.EqualTo(playerHP), "An unused counter must not heal.");
        Assert.That(HitStopManager.IsActive, Is.False);

        // The first blocked hit switches to the counter and closes invincibility.
        Assert.That(skill.TryUse(), Is.True);
        AdvanceToEvent(animator, nameof(PlayerCounterAnimationEvents.BeginCounterWindow));
        Assert.That(skill.IsInvincible, Is.True);
        Assert.That(player.health.TryTakeDamage(new DamageInfo(1)), Is.False);
        Assert.That(player.health.CurrentHP, Is.EqualTo(playerHP));
        Assert.That(skill.ReceivedAttack, Is.True);
        Assert.That(skill.IsInvincible, Is.False);
        animator.Update(0f);
        animator.Update(0f);
        Assert.That(AnimationUtility.GetAnimationEvents(animator.GetCurrentAnimatorClipInfo(0).Single().clip)
            .Any(e => e.functionName == nameof(PlayerCounterAnimationEvents.ApplyCounterDamage)), Is.True,
            "The Animator must enter the counter motion that owns its damage event");
        Assert.That(skill.IsExecuting, Is.True);
        Assert.That(player.CanMove, Is.False);
        Assert.That(player.CanTurn, Is.False);
        Assert.That(player.CanStartAction, Is.False);

        // The attack clip event applies damage and feedback exactly once.
        AnimationClip counterClip = animator.GetCurrentAnimatorClipInfo(0).Single().clip;
        float hitTime = AnimationUtility.GetAnimationEvents(counterClip)
            .Single(e => e.functionName == nameof(PlayerCounterAnimationEvents.ApplyCounterDamage)).time;
        animator.Update(hitTime + 0.01f);
        int expectedDamage = (int)System.Math.Min(int.MaxValue,
            (long)weapon.CurrentWeaponStage.CounterDamage + PlayerPermanentProgress.AttackPowerBonus);
        Assert.That(enemy.CurrentHp, Is.EqualTo(enemyHP - expectedDamage));
        Assert.That(secondEnemy.CurrentHp, Is.EqualTo(enemyHP - expectedDamage));
        Assert.That(player.health.CurrentHP, Is.EqualTo(playerHP + 2),
            "Counter damage must heal one HP per enemy, regardless of collider count.");
        Assert.That(PlayerRuntimeState.CurrentHP, Is.EqualTo(player.health.CurrentHP));
        Assert.That(HitStopManager.IsActive, Is.True);
        Assert.That(enemyObject.GetComponent<HitImpactFlash>().IsPlaying, Is.True);
        skill.ApplyCounterDamage();
        Assert.That(enemy.CurrentHp, Is.EqualTo(enemyHP - expectedDamage));
        Assert.That(player.health.CurrentHP, Is.EqualTo(playerHP + 2),
            "Duplicate damage events must not grant additional healing.");
        animator.Update(animator.GetCurrentAnimatorStateInfo(0).length);
        animator.Update(0f);
        Assert.That(skill.IsExecuting, Is.False);
        Assert.That(skill.IsInvincible, Is.False);
        Assert.That(player.CanStartAction, Is.True);

        // Disabling the event receiver releases an interrupted counter window.
        Assert.That(skill.TryUse(), Is.True);
        AdvanceToEvent(animator, nameof(PlayerCounterAnimationEvents.BeginCounterWindow));
        Assert.That(skill.IsInvincible, Is.True);
        animator.GetComponent<PlayerCounterAnimationEvents>().enabled = false;
        Assert.That(skill.IsExecuting, Is.False);
        Assert.That(skill.IsInvincible, Is.False);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        if (Application.isPlaying)
        {
            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }
    }
}

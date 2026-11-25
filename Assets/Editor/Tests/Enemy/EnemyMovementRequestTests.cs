using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemyMovementRequestTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

    private static void Call(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, PrivateInstance).Invoke(target, arguments);

    private static void ChangeState(EnemyAI ai, string state)
    {
        Type stateType = typeof(EnemyAI).GetNestedType("EnemyState", BindingFlags.NonPublic);
        Call(ai, "ChangeState", Enum.Parse(stateType, state));
    }

    [UnityTest]
    public IEnumerator MovementAndAnimationFollowPatrolChaseAndAttackRequests()
    {
        yield return new EnterPlayMode();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;

        GameObject target = new GameObject("Enemy movement test target");
        GameObject enemy = null;
        try
        {
            foreach (string name in new[] { "EnemyS", "EnemyL" })
            {
                enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemy/{name}.prefab"));
                var ai = enemy.GetComponent<EnemyAI>();
                ai.enabled = false;
                var body = enemy.GetComponent<Rigidbody2D>();
                body.simulated = false;
                var animator = enemy.GetComponentInChildren<Animator>();
                var attack = enemy.GetComponent<EnemyAttack>();
                Assert.That(enemy.GetComponent<EnemyFrameMovement>(), Is.Not.Null, name + " requires curve movement.");
                animator.Rebind();
                Call(ai, "Start");
                Set(ai, "groundCheckPoint", null);
                Set(ai, "wallCheckPoint", null);
                Set(ai, "wallLayer", (LayerMask)0);
                Set(ai, "patrolRange", 10f);
                Set(ai, "player", null);
                ChangeState(ai, "Patrol");
                Call(ai, "SetFacingDirection", 1);

                Call(ai, "Update");
                Assert.That(animator.GetBool("IsMoving"), Is.True, name + " patrol request");
                Assert.That(body.linearVelocity.x, Is.Zero, "Movement waits for the physics step.");
                Call(ai, "FixedUpdate");
                Assert.That(body.linearVelocity.x, Is.Zero, name);
                animator.Update(0f);
                float beforeStep = body.position.x;
                animator.Update(0.12f);
                Assert.That(body.position.x, Is.GreaterThan(beforeStep));

                Set(ai, "patrolRange", 0f);
                Call(ai, "FixedUpdate");
                Assert.That(animator.GetBool("IsMoving"), Is.False, "Patrol boundaries stop the animation in the same step.");
                Assert.That(body.linearVelocity.x, Is.Zero);
                float stoppedX = body.position.x;
                animator.Update(0.12f);
                Assert.That(body.position.x, Is.EqualTo(stoppedX), name + " stopped movement");

                target.transform.position = enemy.transform.position + Vector3.right * 3f;
                Set(ai, "player", target.transform);
                Set(attack, "attackRange", 1f);
                Set(attack, "attackYOffset", 0f);
                Set(attack, "attackCooldown", 60f);
                Set(attack, "lastAttackEndTime", Time.time);
                ChangeState(ai, "Chase");
                Call(ai, "Update");
                Call(ai, "FixedUpdate");
                Assert.That(animator.GetBool("IsMoving"), Is.True, name + " chase request");

                target.transform.position = enemy.transform.position + Vector3.right * 0.5f;
                Call(ai, "Update");
                Call(ai, "FixedUpdate");
                Assert.That(attack.IsAttacking, Is.False, "The attack is still on cooldown.");
                Assert.That(animator.GetBool("IsMoving"), Is.False, "Waiting in attack range stops movement.");
                Assert.That(body.linearVelocity.x, Is.Zero);

                Set(attack, "attackCooldown", 0f);
                Assert.That(attack.TryAttack(), Is.True);
                target.transform.position = enemy.transform.position + Vector3.right * 3f;
                Call(ai, "Update");
                Call(ai, "FixedUpdate");
                Assert.That(animator.GetBool("IsMoving"), Is.False, "An active attack blocks chase outside attack range.");
                Assert.That(body.linearVelocity.x, Is.Zero);

                attack.CancelAttack();
                target.transform.position = enemy.transform.position + Vector3.left * 3f;
                Call(ai, "Update");
                Call(ai, "FixedUpdate");
                Assert.That(animator.GetBool("IsMoving"), Is.True);
                Assert.That(enemy.transform.localScale.x, Is.LessThan(0f));
                Assert.That(body.linearVelocity.x, Is.Zero);

                ai.enabled = true;
                var health = enemy.GetComponent<EnemyHealth>();
                Assert.That(attack.TryAttack(), Is.True);
                Set(health, "superArmor", true);
                int previousHp = health.CurrentHp;
                Vector2 previousVelocity = new Vector2(0.5f, 0.25f);
                body.linearVelocity = previousVelocity;
                Assert.That(health.TryTakeDamage(new DamageInfo(1, enemy.transform.position,
                    new Vector2(2f, 1f), 0.1f)), Is.True);
                Assert.That(health.CurrentHp, Is.EqualTo(previousHp - 1));
                Assert.That(health.IsControlLocked, Is.False);
                Assert.That(ai.enabled, Is.True);
                Assert.That(attack.IsAttacking, Is.True, "Super armor keeps the current attack active.");
                Assert.That(body.linearVelocity, Is.EqualTo(previousVelocity));

                Set(health, "superArmor", false);
                float hitStartedAt = Time.time;
                Assert.That(health.TryTakeDamage(new DamageInfo(1, enemy.transform.position,
                    new Vector2(2f, 1f), 0.1f)), Is.True);
                Assert.That(health.IsControlLocked, Is.True);
                Assert.That(attack.IsAttacking, Is.False, "Turning super armor off restores attack interruption.");
                Assert.That(ai.enabled, Is.False, "Ordinary enemies still suspend their AI during knockback.");
                Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(2f, 1f)));
                // EnterPlayMode does not make the EditMode runner honor WaitForSeconds.
                // Advance actual game time before checking the MonoBehaviour coroutine.
                float deadline = Time.realtimeSinceStartup + 2f;
                while (Time.time - hitStartedAt < 0.15f)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Game time must advance during knockback.");
                    yield return null;
                }
                yield return null;
                Assert.That(health.IsControlLocked, Is.False,
                    name + " must release knockback control after its requested duration.");
                Assert.That(ai.enabled, Is.True, "Ordinary enemies restore their AI when control unlocks.");
                ai.enabled = false;
                Assert.That(animator.GetBool("IsMoving"), Is.False, "Disabling AI clears its movement request.");
                Assert.That(body.linearVelocity.x, Is.Zero);
                Object.DestroyImmediate(enemy);
                enemy = null;
            }
        }
        finally
        {
            if (enemy != null) Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(target);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}

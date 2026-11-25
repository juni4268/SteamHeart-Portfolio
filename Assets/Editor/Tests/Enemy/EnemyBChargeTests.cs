using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemyBChargeTests
{
    private const float Step = 1f / 240f;

    private static GameObject CreateEnemy()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        var enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyB.prefab"));
        Assert.That(enemy.GetComponent<EnemyAI>(), Is.Null, "EnemyB owns its detection and never patrols.");
        Assert.That(enemy.GetComponent<EnemyAttack>(), Is.Null, "EnemyB owns its moving attack.");
        enemy.GetComponent<Rigidbody2D>().simulated = false;
        var animator = enemy.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.fireEvents = false;
        animator.Rebind();
        animator.Update(0f);
        return enemy;
    }

    private static void Advance(Animator animator, float seconds)
    {
        for (float remaining = seconds; remaining > 0f; remaining -= Step)
            animator.Update(Mathf.Min(remaining, Step));
    }

    private static void AdvanceTo(EnemyBChargeController charge, Animator animator, EnemyBChargePhase phase)
    {
        for (int i = 0; i < 2400 && charge.Phase != phase; i++) animator.Update(Step);
        Assert.That(charge.Phase, Is.EqualTo(phase));
    }

    private static void CheckAttack(EnemyBChargeController charge) =>
        typeof(EnemyBChargeController).GetMethod("CheckAttackDamage", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(charge, null);

    [UnityTest]
    public IEnumerator DetectionOnlyStartsFromIdleAndDeathStopsTheCharge()
    {
        yield return new EnterPlayMode();
        var enemy = CreateEnemy();
        GameObject playerObject = null;
        int originalHealth = PlayerRuntimeState.CurrentHP;
        try
        {
            var charge = enemy.GetComponent<EnemyBChargeController>();
            var animator = enemy.GetComponentInChildren<Animator>();
            var update = typeof(EnemyBChargeController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 100; i++)
            {
                update.Invoke(charge, null);
                animator.Update(0.1f);
            }
            Assert.That(charge.IsExecuting, Is.False, "Without a player, waiting never turns into patrol.");

            playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
            foreach (var component in playerObject.GetComponentsInChildren<MonoBehaviour>()) component.enabled = false;
            playerObject.transform.position = enemy.transform.position + Vector3.left * 100f;
            update.Invoke(charge, null);
            Assert.That(charge.IsExecuting, Is.False, "The player must be detected before starting.");
            playerObject.transform.position = enemy.transform.position + Vector3.left * 2f;
            Physics2D.SyncTransforms();
            update.Invoke(charge, null);
            Assert.That(charge.IsExecuting, Is.True);
            Assert.That(enemy.transform.localScale.x, Is.LessThan(0f));
            AdvanceTo(charge, animator, EnemyBChargePhase.Preparing);
            playerObject.transform.position = enemy.transform.position + Vector3.right * 100f;
            AdvanceTo(charge, animator, EnemyBChargePhase.Walking);
            var clip = animator.GetCurrentAnimatorClipInfo(0).Single().clip;
            for (int i = 0; i < 3; i++)
            {
                update.Invoke(charge, null);
                Advance(animator, clip.length);
            }
            Assert.That(charge.Phase, Is.EqualTo(EnemyBChargePhase.Walking), "Leaving detection range does not cancel an active charge.");
            Assert.That(enemy.transform.localScale.x, Is.LessThan(0f), "The starting direction stays fixed.");

            typeof(EnemyBChargeController).GetMethod("RequestWallReturn", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(charge, null);
            AdvanceTo(charge, animator, EnemyBChargePhase.Returning);
            AdvanceTo(charge, animator, EnemyBChargePhase.Preparing);
            Assert.That(charge.IsExecuting, Is.True, "Once detected, combat continues outside the detection range.");
            Assert.That(enemy.transform.localScale.x, Is.GreaterThan(0f), "The next charge faces the player's current position.");
            AdvanceTo(charge, animator, EnemyBChargePhase.Walking);
            Advance(animator, clip.length * 2f);
            Assert.That(charge.Phase, Is.EqualTo(EnemyBChargePhase.Walking), "Clear the previous wall-return request for the next charge.");

            var health = enemy.GetComponent<EnemyHealth>();
            typeof(EnemyHealth).GetField("scrapReward", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 0);
            health.TryTakeDamage(new DamageInfo(health.MaxHp));
            Assert.That(health.IsDead, Is.True);
            Assert.That(charge.enabled, Is.False);
            Assert.That(charge.IsExecuting, Is.False);
            Assert.That(animator.enabled, Is.False);
        }
        finally
        {
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(enemy);
            PlayerRuntimeState.SetHealth(originalHealth);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SuccessfulHitFinishesOneExtraCycleWithRenamedStates()
    {
        yield return new EnterPlayMode();
        var enemy = CreateEnemy();
        GameObject playerObject = null;
        int originalHealth = PlayerRuntimeState.CurrentHP;
        string folder = "Assets/Editor/Tests/__EnemyBCharge_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets/Editor/Tests", folder.Substring(folder.LastIndexOf('/') + 1));
        try
        {
            var charge = enemy.GetComponent<EnemyBChargeController>();
            var body = enemy.GetComponent<Rigidbody2D>();
            var animator = enemy.GetComponentInChildren<Animator>();
            string controllerPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
            Assert.That(AssetDatabase.CopyAsset(controllerPath, folder + "/Renamed.controller"), Is.True);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + "/Renamed.controller");
            foreach (var state in controller.layers[0].stateMachine.states)
                state.state.name = Guid.NewGuid().ToString("N");
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            Assert.That(charge.TryStart(1), Is.True);
            float originX = body.position.x;
            AdvanceTo(charge, animator, EnemyBChargePhase.Walking);
            Assert.That(body.position.x, Is.EqualTo(originX).Within(0.0001f), "Prepare stays in place.");
            var walkClip = animator.GetCurrentAnimatorClipInfo(0).Single().clip;
            var binding = AnimationUtility.GetCurveBindings(walkClip).Single(b =>
                b.type == typeof(EnemyAnimationEventReceiver) && b.propertyName == "movementDistance");
            var distance = AnimationUtility.GetEditorCurve(walkClip, binding);
            float cycleDistance = distance.Evaluate(walkClip.length) - distance.Evaluate(0f);
            Advance(animator, walkClip.length * 4.3f);
            Assert.That(charge.Phase, Is.EqualTo(EnemyBChargePhase.Walking), "Walk loops until an exit condition occurs.");

            playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
            var health = playerObject.GetComponent<PlayerHealth>();
            foreach (var component in playerObject.GetComponentsInChildren<MonoBehaviour>())
                if (component != health) component.enabled = false;
            playerObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            health.Heal(health.MaxHP);
            var invincibility = typeof(PlayerHealth).GetField("hitInvincibilityEndsAt", BindingFlags.Instance | BindingFlags.NonPublic);
            invincibility.SetValue(health, Time.time + 10f);
            playerObject.transform.position = charge.AttackHitboxCenter;
            Physics2D.SyncTransforms();
            int beforeHit = health.CurrentHP;
            CheckAttack(charge);
            Assert.That(health.CurrentHP, Is.EqualTo(beforeHit));
            Advance(animator, walkClip.length * 2f);
            Assert.That(charge.Phase, Is.EqualTo(EnemyBChargePhase.Walking), "A rejected hit must not end the charge.");

            invincibility.SetValue(health, float.NegativeInfinity);
            playerObject.transform.position = charge.AttackHitboxCenter;
            Physics2D.SyncTransforms();
            int finishLoop = Mathf.FloorToInt(animator.GetCurrentAnimatorStateInfo(0).normalizedTime) + 2;
            CheckAttack(charge);
            Assert.That(health.CurrentHP, Is.LessThan(beforeHit), "Use a real accepted damage result.");
            float lastWalkTime = 0f;
            for (int i = 0; i < 2400 && charge.Phase == EnemyBChargePhase.Walking; i++)
            {
                lastWalkTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                animator.Update(Step);
            }
            Assert.That(charge.Phase, Is.EqualTo(EnemyBChargePhase.Returning));
            Assert.That(lastWalkTime, Is.InRange(finishLoop - 0.05f, finishLoop + 0.05f),
                "Finish the hit cycle plus one complete additional cycle, even after many previous loops.");
            Assert.That(body.position.x - originX, Is.EqualTo(finishLoop * cycleDistance).Within(0.001f));
            float returnX = body.position.x;
            playerObject.transform.position = enemy.transform.position + Vector3.left * 100f;
            AdvanceTo(charge, animator, EnemyBChargePhase.Preparing);
            Assert.That(body.position.x, Is.EqualTo(returnX).Within(0.0001f), "Return does not add movement.");
            Assert.That(charge.IsExecuting, Is.True);
            Assert.That(enemy.transform.localScale.x, Is.LessThan(0f));
            AdvanceTo(charge, animator, EnemyBChargePhase.Walking);
            Advance(animator, walkClip.length * 2f);
            Assert.That(charge.Phase, Is.EqualTo(EnemyBChargePhase.Walking), "Clear the previous hit's cycle limit before attacking again.");
        }
        finally
        {
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(enemy);
            AssetDatabase.DeleteAsset(folder);
            PlayerRuntimeState.SetHealth(originalHealth);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator WallCollisionReturnsImmediately()
    {
        yield return new EnterPlayMode();
        var enemy = CreateEnemy();
        var wall = new GameObject("EnemyB test wall");
        try
        {
            var charge = enemy.GetComponent<EnemyBChargeController>();
            var animator = enemy.GetComponentInChildren<Animator>();
            var body = enemy.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            enemy.transform.position = new Vector3(200f, 200f);
            body.position = enemy.transform.position;
            wall.layer = LayerMask.NameToLayer("Wall");
            wall.transform.position = enemy.transform.position + Vector3.right * 1.2f;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 4f);
            body.simulated = true;
            Physics2D.SyncTransforms();
            Assert.That(charge.TryStart(1), Is.True);
            AdvanceTo(charge, animator, EnemyBChargePhase.Walking);
            // A single large animation update must not teleport the body through a thin wall.
            animator.Update(animator.GetCurrentAnimatorClipInfo(0).Single().clip.length);
            animator.Update(0f);
            Assert.That(charge.Phase, Is.EqualTo(EnemyBChargePhase.Returning));
            Assert.That(charge.TryStart(1), Is.False, "Wall return must finish before another charge can start.");
            Assert.That(body.position.x, Is.LessThan(wall.transform.position.x), "Stop against the wall instead of passing through it.");
            AdvanceTo(charge, animator, EnemyBChargePhase.Preparing);
            Assert.That(charge.IsExecuting, Is.True, "Wall return goes straight into the next preparation without Idle.");
        }
        finally
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(wall);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator KnockbackKeepsWalkTimeAndResumesAfterControlUnlocks()
    {
        yield return new EnterPlayMode();
        var enemy = CreateEnemy();
        var ground = new GameObject("EnemyB test ground");
        try
        {
            var charge = enemy.GetComponent<EnemyBChargeController>();
            var animator = enemy.GetComponentInChildren<Animator>();
            var health = enemy.GetComponent<EnemyHealth>();
            var body = enemy.GetComponent<Rigidbody2D>();
            enemy.transform.position = new Vector3(200f, 20f);
            body.position = enemy.transform.position;
            ground.layer = LayerMask.NameToLayer("Ground");
            ground.transform.position = new Vector3(200f, 19.48f);
            ground.AddComponent<BoxCollider2D>().size = new Vector2(100f, 0.2f);
            body.simulated = true;
            Physics2D.SyncTransforms();
            Assert.That(charge.TryStart(1), Is.True);
            AdvanceTo(charge, animator, EnemyBChargePhase.Walking);
            for (int hit = 0; hit < 2; hit++)
            {
                float beforeTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                Assert.That(health.TryTakeDamage(new DamageInfo(1, body.position + Vector2.right,
                    new Vector2(-1f, 1f), 0.6f)), Is.True);
                Assert.That(health.IsControlLocked, Is.True);
                Assert.That(body.linearVelocity.x, Is.EqualTo(-1f).Within(0.0001f));
                yield return new WaitForSeconds(0.35f);
                Assert.That(health.IsControlLocked, Is.True);
                Assert.That(charge.Phase, Is.EqualTo(EnemyBChargePhase.Walking));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThan(beforeTime),
                    $"Hit={hit}, Mode={animator.updateMode}, Speed={animator.speed}, Enabled={animator.isActiveAndEnabled}, " +
                    $"Phase={charge.Phase}, Time={Time.time}, FixedTime={Time.fixedTime}, Awake={body.IsAwake()}");
                Assert.That(body.position.y, Is.EqualTo(20f).Within(0.1f), "Falling and landing keep the physics position.");
                float deadline = Time.realtimeSinceStartup + 3f;
                while (health.IsControlLocked && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(health.IsControlLocked, Is.False);
                float resumedX = body.position.x;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                deadline = Time.realtimeSinceStartup + 1f;
                while (body.position.x <= resumedX + 0.001f && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(body.position.x, Is.GreaterThan(resumedX), "Resume movement as soon as control is available.");
                Assert.That(body.position.x - resumedX, Is.LessThan(0.8f), "Resume without accumulating the locked movement distance.");
                Assert.That(charge.Phase, Is.EqualTo(EnemyBChargePhase.Walking));
            }
        }
        finally
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(ground);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}

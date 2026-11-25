using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class AnimationRoleTests
{
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [UnityTest]
    public IEnumerator EnemyRenderedSpriteAndMovementStaySynchronized()
    {
        yield return new EnterPlayMode();
        yield return VerifyRenderedEnemyMovement();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyRenderedEnemyMovement()
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        GameObject enemy = null;
        GameObject cameraObject = null;
        RenderTexture texture = null;
        Action<ScriptableRenderContext, Camera> observe = null;
        try
        {
            enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyL.prefab"));
            enemy.GetComponent<EnemyAI>().enabled = false;
            var body = enemy.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var animator = enemy.GetComponentInChildren<Animator>();
            var renderer = animator.GetComponent<SpriteRenderer>();
            var movement = enemy.GetComponent<EnemyFrameMovement>();
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            var clip = Machines(controller.layers[0].stateMachine).SelectMany(m => m.states)
                .Single(s => s.state.behaviours.Any(b => b is EnemyMovementStateBehaviour)).state.motion as AnimationClip;
            var distanceBinding = AnimationUtility.GetCurveBindings(clip).Single(b =>
                b.type == typeof(EnemyAnimationEventReceiver) && b.propertyName == "movementDistance");
            AnimationCurve distanceCurve = AnimationUtility.GetEditorCurve(clip, distanceBinding);
            var spriteBinding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single(b =>
                b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite");
            var spriteKeys = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);
            var distanceBySprite = spriteKeys.ToDictionary(k => (Sprite)k.value,
                k => distanceCurve.Evaluate(k.time + 0.00001f));
            float cycleDistance = distanceCurve.Evaluate(clip.length);

            cameraObject = new GameObject("Enemy Movement Test Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.transform.position = enemy.transform.position + Vector3.back * 10f;
            texture = new RenderTexture(32, 32, 16);
            camera.targetTexture = texture;

            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.SetBool("IsMoving", true);
            animator.Update(0f);
            float originX = enemy.transform.position.x;
            movement.Move(1);
            int samples = 0;
            int spriteChanges = 0;
            Sprite previousSprite = null;
            float largestError = 0f;
            string mismatch = null;
            observe = (_, renderedCamera) =>
            {
                if (renderedCamera != camera || !distanceBySprite.TryGetValue(renderer.sprite, out float distance)) return;
                int loop = Mathf.FloorToInt(animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
                float expectedX = originX + loop * cycleDistance + distance;
                float error = Mathf.Abs(enemy.transform.position.x - expectedX);
                if (error > largestError)
                {
                    largestError = error;
                    mismatch = $"Sprite={renderer.sprite.name}, Transform={enemy.transform.position.x}, " +
                        $"Rigidbody={body.position.x}, expected={expectedX}";
                }
                if (renderer.sprite != previousSprite) spriteChanges++;
                previousSprite = renderer.sprite;
                samples++;
            };
            RenderPipelineManager.beginCameraRendering += observe;
            float deadline = Time.realtimeSinceStartup + clip.length + 0.2f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                // Drive this off-screen test camera even when the Editor's Game View is not repainting.
                RenderPipeline.SubmitRenderRequest(camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            }

            Assert.That(samples, Is.GreaterThan(5), "The test must observe actual rendered frames.");
            Assert.That(spriteChanges, Is.GreaterThan(5));
            Assert.That(largestError, Is.LessThan(0.0001f), mismatch);
        }
        finally
        {
            if (observe != null) RenderPipelineManager.beginCameraRendering -= observe;
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (texture != null) Object.Destroy(texture);
            if (enemy != null) Object.Destroy(enemy);
        }
    }

    [UnityTest]
    public IEnumerator EnemyFrameMovementUsesConstantCurve()
    {
        yield return new EnterPlayMode();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        string folder = "Assets/Editor/Tests/__EnemyMovement_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets/Editor/Tests", folder.Substring(folder.LastIndexOf('/') + 1));
        GameObject enemy = null;
        try
        {
            var controller = CopyAndRename("Assets/Animations/Enemies/AC_EnemyL.controller", folder + "/Enemy.controller");
            var movementClip = Machines(controller.layers[0].stateMachine).SelectMany(m => m.states)
                .Single(s => s.state.behaviours.Any(b => b is EnemyMovementStateBehaviour)).state.motion as AnimationClip;
            var distanceBinding = AnimationUtility.GetCurveBindings(movementClip).Single(b =>
                b.type == typeof(EnemyAnimationEventReceiver) && b.propertyName == "movementDistance");
            AnimationCurve distanceCurve = AnimationUtility.GetEditorCurve(movementClip, distanceBinding);
            enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyL.prefab"));
            enemy.GetComponent<EnemyAI>().enabled = false;
            var body = enemy.GetComponent<Rigidbody2D>();
            body.simulated = false;
            var movement = enemy.GetComponent<EnemyFrameMovement>();
            var animator = enemy.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.SetBool("IsMoving", true);
            animator.Update(0f);
            movement.Move(1);
            float startX = body.position.x;

            animator.Update(0.1f);
            Assert.That(body.position.x, Is.EqualTo(startX).Within(0.0001f), "Hold position between Constant keys.");
            animator.Update(0.02f);
            float firstDistance = distanceCurve.Evaluate(0.12f);
            Assert.That(body.position.x, Is.EqualTo(startX + firstDistance).Within(0.0001f), "Apply the key in the same animation update.");
            Assert.That(body.linearVelocity.x, Is.Zero);

            movement.Stop();
            animator.Update(0.12f);
            Assert.That(body.position.x, Is.EqualTo(startX + firstDistance).Within(0.0001f), "Stopped movement must not accumulate distance.");
            movement.Move(-1);
            animator.Update(0.12f);
            float reverseDistance = distanceCurve.Evaluate(0.36f) - distanceCurve.Evaluate(0.24f);
            Assert.That(body.position.x, Is.EqualTo(startX + firstDistance - reverseDistance).Within(0.0001f), "Apply the next step in the requested direction.");

            animator.SetBool("IsMoving", false);
            animator.Update(0f);
            animator.SetBool("IsMoving", true);
            animator.Update(0f);
            movement.Move(1);
            startX = body.position.x;
            for (int i = 0; i < 251; i++) animator.Update(0.02f);
            float elapsed = 251 * 0.02f;
            float expectedDistance = Mathf.FloorToInt(elapsed / movementClip.length) * distanceCurve.Evaluate(movementClip.length)
                + distanceCurve.Evaluate(Mathf.Repeat(elapsed, movementClip.length));
            Assert.That(body.position.x - startX, Is.EqualTo(expectedDistance).Within(0.001f), "Loop without moving backwards at the curve reset.");

            movement.enabled = false;
            movement.enabled = true;
            animator.Update(0f);
            movement.Move(1);
            startX = body.position.x;
            animator.Update(0.12f);
            Assert.That(body.position.x, Is.GreaterThan(startX), "Resume after re-enabling the Component.");
            animator.enabled = false;
            startX = body.position.x;
            movement.Move(1);
            Assert.That(body.position.x, Is.EqualTo(startX));
        }
        finally
        {
            if (enemy != null) Object.Destroy(enemy);
            AssetDatabase.DeleteAsset(folder);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PlayerAndEnemyActionsSurviveRenamedStatesAndSubStateMachines()
    {
        yield return new EnterPlayMode();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        string folder = "Assets/Editor/Tests/__RoleMigration_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets/Editor/Tests", folder.Substring(folder.LastIndexOf('/') + 1));
        try
        {
            var playerController = CopyAndRename("Assets/Animations/Player/AC_Player.controller", folder + "/Player.controller");
            var playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
            var player = playerObject.GetComponent<Player>();
            var movement = playerObject.GetComponent<PlayerMovement>();
            movement.enabled = false;
            playerObject.GetComponent<Rigidbody2D>().simulated = false;
            Set(player.health, "currentHP", player.health.MaxHP);
            Set(movement, "isGrounded", true);
            Set(movement, "hasAppliedJumpForce", false);
            var animator = player.anim;
            // Prefab startup may equip its planned weapon and apply an override Controller.
            yield return null;
            animator.runtimeAnimatorController = playerController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.SetBool("isGrounded", true);
            animator.Update(0f);
            var skill = playerObject.GetComponent<PlayerCounter>();
            Set(skill, "cooldown", 0f);
            Assert.That(skill.TryUse(), Is.True);
            animator.Update(0f);
            animator.Update(0.01f);
            skill.BeginCounterWindow();
            Assert.That(player.health.TryTakeDamage(new DamageInfo(1)), Is.False);
            animator.Update(0.01f);
            yield return null;
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(playerController));
            Assert.That(skill.IsExecuting, Is.True);
            Assert.That(player.CanMove, Is.False);
            for (int i = 0; i < 4 && skill.IsExecuting; i++)
            {
                animator.Update(animator.GetCurrentAnimatorStateInfo(0).length + 0.1f);
                animator.Update(0f);
                yield return null;
            }
            var observed = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(skill.IsExecuting, Is.False, "Counter state: hash=" + observed.fullPathHash
                + " time=" + observed.normalizedTime + " length=" + observed.length
                + " loop=" + observed.loop + " speed=" + animator.speed
                + " transitioning=" + animator.IsInTransition(0)
                + " clip=" + string.Join(",", animator.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name))
                + " controller=" + animator.runtimeAnimatorController.name);
            var dash = playerObject.GetComponent<PlayerDash>();
            typeof(PlayerDash).GetMethod("StartDash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dash, null);
            animator.Update(0f);
            animator.Update(0.01f);
            dash.EndDashMovement();
            animator.Update(0.01f);
            yield return null;
            Assert.That(dash.IsDashing, Is.True);
            for (int i = 0; i < 4 && dash.IsDashing; i++)
            {
                animator.Update(animator.GetCurrentAnimatorStateInfo(0).length + 0.1f);
                animator.Update(0f);
                yield return null;
            }
            Assert.That(dash.IsDashing, Is.False);

            var enemyController = CopyAndRename("Assets/Animations/Enemies/AC_EnemyL.controller", folder + "/Enemy.controller");
            var enemyObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyL.prefab"));
            enemyObject.GetComponent<EnemyAI>().enabled = false;
            var enemyAnimator = enemyObject.GetComponentInChildren<Animator>();
            enemyAnimator.runtimeAnimatorController = enemyController;
            enemyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var frameMovement = enemyObject.GetComponent<EnemyFrameMovement>();
            enemyAnimator.SetBool("IsMoving", true);
            enemyAnimator.Update(0f);
            enemyAnimator.Update(0.01f);
            var enemyBody = enemyObject.GetComponent<Rigidbody2D>();
            frameMovement.Move(1);
            float beforeMove = enemyBody.position.x;
            enemyAnimator.Update(0.12f);
            Assert.That(enemyBody.position.x, Is.GreaterThan(beforeMove));
            Assert.That(enemyBody.linearVelocity.x, Is.Zero);
            frameMovement.enabled = false;
            frameMovement.enabled = true;
            enemyAnimator.Update(0.01f);
            frameMovement.Move(1);
            beforeMove = enemyBody.position.x;
            enemyAnimator.Update(0.12f);
            Assert.That(enemyBody.position.x, Is.GreaterThan(beforeMove));
            enemyAnimator.enabled = false;
            beforeMove = enemyBody.position.x;
            frameMovement.Move(1);
            Assert.That(enemyBody.position.x, Is.EqualTo(beforeMove));
        }
        finally { AssetDatabase.DeleteAsset(folder); }
        yield return new ExitPlayMode();
    }

    private static AnimatorController CopyAndRename(string source, string path)
    {
        Assert.That(AssetDatabase.CopyAsset(source, path), Is.True);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        foreach (var machine in Machines(controller.layers[0].stateMachine))
        {
            if (machine != controller.layers[0].stateMachine) machine.name = "Renamed section " + Guid.NewGuid();
            foreach (var entry in machine.states)
            {
                var state = entry.state;
                if (!state.behaviours.Any(b => b is PlayerDashStateBehaviour ||
                    b is PlayerCounterStateBehaviour || b is EnemyMovementStateBehaviour)) continue;
                state.name = "Renamed role " + Guid.NewGuid();
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(machine);
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        return controller;
    }

    private static IEnumerable<AnimatorStateMachine> Machines(AnimatorStateMachine m)
    {
        yield return m;
        foreach (var c in m.stateMachines) foreach (var n in Machines(c.stateMachine)) yield return n;
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) { Time.timeScale = 1f; yield return new ExitPlayMode(); }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemyMovementCurveTests
{
    private static void Call(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);

    [UnityTest]
    public IEnumerator LinearClipMovementStaysContinuousAcrossAnimationLoops()
    {
        yield return new EnterPlayMode();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        string folder = "Assets/Editor/Tests/__LinearMovement_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets/Editor/Tests", folder.Substring(folder.LastIndexOf('/') + 1));
        GameObject enemy = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyL.prefab");
            var sourceController = (AnimatorController)prefab.GetComponentInChildren<Animator>().runtimeAnimatorController;
            var sourceClip = (AnimationClip)sourceController.layers.SelectMany(l => Machines(l.stateMachine)).SelectMany(m => m.states)
                .Single(s => s.state.behaviours.Any(b => b is EnemyMovementStateBehaviour)).state.motion;
            Assert.That(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceController), folder + "/Enemy.controller"), Is.True);
            Assert.That(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceClip), folder + "/Walk.anim"), Is.True);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + "/Enemy.controller");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/Walk.anim");
            var distanceBinding = AnimationUtility.GetCurveBindings(clip).Single(b =>
                b.type == typeof(EnemyAnimationEventReceiver) && b.propertyName == EnemyMovementCurveSync.DistanceProperty);
            var curve = AnimationCurve.Linear(0f, 0f, clip.length, 2f);
            AnimationUtility.SetEditorCurve(clip, distanceBinding, curve);
            foreach (var state in controller.layers.SelectMany(l => Machines(l.stateMachine)).SelectMany(m => m.states))
                if (state.state.behaviours.Any(b => b is EnemyMovementStateBehaviour)) state.state.motion = clip;

            enemy = Object.Instantiate(prefab);
            enemy.GetComponent<EnemyAI>().enabled = false;
            var body = enemy.GetComponent<Rigidbody2D>();
            body.simulated = false;
            var movement = enemy.GetComponent<EnemyFrameMovement>();
            var animator = enemy.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.fireEvents = false;
            animator.Rebind();
            animator.SetBool("IsMoving", true);
            animator.Update(0f);
            for (int i = 0; i < 4 && !animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip == clip); i++)
                animator.Update(animator.GetCurrentAnimatorStateInfo(0).length + 0.01f);
            Assert.That(animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip == clip), Is.True);

            movement.Move(1);
            float startX = body.position.x;
            float initialTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            for (int i = 0; i < 20; i++)
            {
                animator.Update(0.13f);
                float elapsedLoops = animator.GetCurrentAnimatorStateInfo(0).normalizedTime - initialTime;
                Assert.That(body.position.x - startX, Is.EqualTo(elapsedLoops * 2f).Within(0.0001f), "Linear movement must keep the tail of every animation loop.");
            }
            movement.Stop();
            float stoppedX = body.position.x;
            animator.Update(0.13f);
            Assert.That(body.position.x, Is.EqualTo(stoppedX).Within(0.0001f));
            movement.Move(-1);
            animator.Update(0.13f);
            Assert.That(body.position.x, Is.EqualTo(stoppedX - 2f * 0.13f / clip.length).Within(0.0001f));
        }
        finally
        {
            if (enemy != null) Object.DestroyImmediate(enemy);
            AssetDatabase.DeleteAsset(folder);
        }
        yield return new ExitPlayMode();
    }

    [Test]
    public void ContinuousMovementPreservesDistanceAcrossLoopsAndStops()
    {
        var enemy = new GameObject("Curve movement test");
        var state = ScriptableObject.CreateInstance<EnemyMovementStateBehaviour>();
        try
        {
            var movement = enemy.AddComponent<EnemyFrameMovement>();
            var body = enemy.GetComponent<Rigidbody2D>();
            body.simulated = false;
            Call(movement, "Awake");
            Call(movement, "NotifyMovementEntered", state, 0f);
            movement.Move(1);

            Call(movement, "NotifyMovementUpdated", state, 0.98f);
            Call(movement, "ApplyMovement", 0.98f, 1f);
            Call(movement, "NotifyMovementUpdated", state, 1.02f);
            Call(movement, "ApplyMovement", 0.02f, 1f);
            Assert.That(body.position.x, Is.EqualTo(1.02f).Within(0.00001f), "Keep the final distance from the previous loop.");

            Call(movement, "NotifyMovementUpdated", state, 3.02f);
            Call(movement, "ApplyMovement", 0.02f, 1f);
            Assert.That(body.position.x, Is.EqualTo(3.02f).Within(0.00001f), "Include complete loops skipped by a long animation update.");

            movement.Stop();
            Call(movement, "NotifyMovementUpdated", state, 4.7f);
            Call(movement, "ApplyMovement", 0.7f, 1f);
            Assert.That(body.position.x, Is.EqualTo(3.02f).Within(0.00001f));
            movement.Move(-1);
            Call(movement, "NotifyMovementUpdated", state, 4.8f);
            Call(movement, "ApplyMovement", 0.8f, 1f);
            Assert.That(body.position.x, Is.EqualTo(2.92f).Within(0.00001f), "Resume with the next delta in the requested direction.");
            Assert.That(body.linearVelocity.x, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(state);
        }
    }

    [TestCase(AnimationUtility.TangentMode.Constant)]
    [TestCase(AnimationUtility.TangentMode.Linear)]
    public void SceneEditingPreservesCurveInterpolation(AnimationUtility.TangentMode mode)
    {
        var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyBroken(curve, i, true);
            AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
            AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
        }

        EnemyMovementCurvePreview.SetDistanceKey(curve, 0.5f, 0.75f, 0.0001f);
        Assert.That(curve.Evaluate(0.25f), Is.EqualTo(mode == AnimationUtility.TangentMode.Constant ? 0f : 0.375f).Within(0.0001f));
        Assert.That(curve.Evaluate(0.75f), Is.EqualTo(mode == AnimationUtility.TangentMode.Constant ? 0.75f : 0.875f).Within(0.0001f));
        EnemyMovementCurvePreview.SetDistanceKey(curve, 0.5f, 0.5f, 0.0001f);
        Assert.That(curve.Evaluate(0.25f), Is.EqualTo(mode == AnimationUtility.TangentMode.Constant ? 0f : 0.25f).Within(0.0001f));
        Assert.That(Enumerable.Range(0, curve.length).All(i =>
            AnimationUtility.GetKeyLeftTangentMode(curve, i) == mode &&
            AnimationUtility.GetKeyRightTangentMode(curve, i) == mode), Is.True);
    }

    [Test]
    public void LoopDistanceAutomaticallyFollowsTheMovementCurve()
    {
        var clip = new AnimationClip();
        try
        {
            EnemyMovementCurveSync.Synchronize(clip);
            var distance = EditorCurveBinding.FloatCurve(string.Empty, typeof(EnemyAnimationEventReceiver), EnemyMovementCurveSync.DistanceProperty);
            var total = EditorCurveBinding.FloatCurve(string.Empty, typeof(EnemyAnimationEventReceiver), EnemyMovementCurveSync.LoopDistanceProperty);
            AnimationUtility.SetEditorCurve(clip, distance, AnimationCurve.Linear(0f, 0f, 1f, 2f));
            Assert.That(AnimationUtility.GetEditorCurve(clip, total).Evaluate(0.5f), Is.EqualTo(2f));
            AnimationUtility.SetEditorCurve(clip, distance, AnimationCurve.Linear(0f, 0f, 2f, 5f));
            Assert.That(AnimationUtility.GetEditorCurve(clip, total).Evaluate(1f), Is.EqualTo(5f));
            Assert.That(clip.length, Is.EqualTo(2f), "Generated metadata must not extend or lock the clip length.");
            AnimationUtility.SetEditorCurve(clip, distance, null);
            Assert.That(AnimationUtility.GetEditorCurve(clip, total), Is.Null, "Removing movement must also remove its loop distance.");
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void NewEnemyAIRequiresCurveMovementAndRigidbody()
    {
        var enemy = new GameObject("New enemy test");
        try
        {
            enemy.AddComponent<EnemyAI>();
            Assert.That(enemy.GetComponent<EnemyFrameMovement>(), Is.Not.Null);
            Assert.That(enemy.GetComponent<Rigidbody2D>(), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(enemy);
        }
    }

    [TestCase("EnemyS")]
    [TestCase("EnemyL")]
    [TestCase("EnemyB")]
    public void EnemyPrefabMovementClipsHaveSynchronizedLoopDistances(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemy/{name}.prefab");
        Assert.That(prefab.GetComponent<EnemyFrameMovement>(), Is.Not.Null);
        var controller = (AnimatorController)prefab.GetComponentInChildren<Animator>().runtimeAnimatorController;
        var states = controller.layers.SelectMany(l => Machines(l.stateMachine)).SelectMany(m => m.states)
            .Where(s => s.state.behaviours.Any(b => b is EnemyMovementStateBehaviour)).ToArray();
        Assert.That(states, Is.Not.Empty, "A movement State must identify its role through EnemyMovementStateBehaviour.");
        foreach (var entry in states)
        {
            var clip = entry.state.motion as AnimationClip;
            Assert.That(clip, Is.Not.Null);
            var binding = AnimationUtility.GetCurveBindings(clip).Single(b =>
                b.type == typeof(EnemyAnimationEventReceiver) && b.propertyName == EnemyMovementCurveSync.DistanceProperty);
            AnimationCurve distance = AnimationUtility.GetEditorCurve(clip, binding);
            var totalBinding = EditorCurveBinding.FloatCurve(binding.path, binding.type, EnemyMovementCurveSync.LoopDistanceProperty);
            Assert.That(AnimationUtility.GetEditorCurve(clip, totalBinding).Evaluate(0f),
                Is.EqualTo(distance.Evaluate(clip.length) - distance.Evaluate(0f)).Within(0.0001f));
        }
    }

    private static IEnumerable<AnimatorStateMachine> Machines(AnimatorStateMachine machine)
    {
        yield return machine;
        foreach (var child in machine.stateMachines)
            foreach (var nested in Machines(child.stateMachine)) yield return nested;
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}

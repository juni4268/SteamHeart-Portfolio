using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class EnemyMovementCurvePreviewTests
{
    [UnityTest]
    public IEnumerator ScenePreviewRecordsConstantKeysAndRestoresPosition()
    {
        var window = EditorWindow.GetWindow<AnimationWindow>();
        Object[] originalSelection = Selection.objects;
        AnimationClip originalClip = window.animationClip;
        int originalFrame = window.frame;
        bool wasPreviewing = window.previewing;
        bool wasRecording = window.recording;
        bool wasPlaying = window.playing;
        bool toolsWereHidden = Tools.hidden;
        string folder = "Assets/Editor/Tests/__MovementPreview_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets/Editor/Tests", folder.Substring(folder.LastIndexOf('/') + 1));
        GameObject enemy = null;
        EnemyMovementCurvePreview preview = null;
        Scene testScene = SceneManager.GetActiveScene();
        try
        {
            window.previewing = false;
            Assert.That(AssetDatabase.CopyAsset("Assets/Animations/Enemies/EnemyLWalk.anim", folder + "/Walk.anim"), Is.True);
            Assert.That(AssetDatabase.CopyAsset("Assets/Animations/Enemies/AC_EnemyL.controller", folder + "/Enemy.controller"), Is.True);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/Walk.anim");
            EditorCurveBinding[] originalBindings = AnimationUtility.GetCurveBindings(clip);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + "/Enemy.controller");
            foreach (var layer in controller.layers) ReplaceMovementClip(layer.stateMachine, clip);

            enemy = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyL.prefab"), testScene);
            var animator = enemy.GetComponentInChildren<Animator>();
            var receiver = animator.GetComponent<EnemyAnimationEventReceiver>();
            animator.runtimeAnimatorController = controller;
            Vector3 origin = enemy.transform.position;
            bool sceneWasDirty = enemy.scene.isDirty;
            Selection.activeGameObject = animator.gameObject;
            window.Show();
            window.Focus();
            yield return null;
            // The test runner does not dispatch the Animation window's selection GUI message.
            typeof(AnimationWindow).GetMethod("OnSelectionChangeInternal",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(window, new object[] { false });
            window.animationClip = clip;
            window.previewing = true;
            window.frame = 0;
            yield return null;

            preview = EnemyMovementCurvePreview.Start(receiver, window);
            Assert.That(preview, Is.Not.Null,
                $"CanPreview={window.canPreview}, Previewing={window.previewing}, " +
                $"Animated={AnimationMode.IsPropertyAnimated(receiver, "movementDistance")}");
            var binding = AnimationUtility.GetCurveBindings(clip).Single(b =>
                b.type == typeof(EnemyAnimationEventReceiver) && b.propertyName == "movementDistance");
            float direction = enemy.transform.localScale.x < 0f ? -1f : 1f;

            window.frame = 3;
            yield return null;
            Assert.That(preview.Update(), Is.True);
            float expected = AnimationUtility.GetEditorCurve(clip, binding).Evaluate(window.time);
            Assert.That(enemy.transform.position.x, Is.EqualTo(origin.x + expected * direction).Within(0.0001f));
            window.frame = 2;
            yield return null;
            Assert.That(preview.Update(), Is.True);
            float previousValue = AnimationUtility.GetEditorCurve(clip, binding).Evaluate(window.time);
            Assert.That(enemy.transform.position.x, Is.EqualTo(origin.x + previousValue * direction).Within(0.0001f),
                "Scrubbing backwards must use the origin rather than accumulate offsets.");

            float editedValue = previousValue + 0.125f;
            Undo.IncrementCurrentGroup();
            preview.RecordPosition(origin + Vector3.right * (editedValue * direction));
            Undo.FlushUndoRecordObjects();
            yield return null;
            AnimationCurve editedCurve = AnimationUtility.GetEditorCurve(clip, binding);
            Assert.That(editedCurve.Evaluate(window.time), Is.EqualTo(editedValue).Within(0.0001f));
            Assert.That(Enumerable.Range(0, editedCurve.length).All(i =>
                AnimationUtility.GetKeyLeftTangentMode(editedCurve, i) == AnimationUtility.TangentMode.Constant &&
                AnimationUtility.GetKeyRightTangentMode(editedCurve, i) == AnimationUtility.TangentMode.Constant), Is.True);
            Assert.That(AnimationUtility.GetCurveBindings(clip), Is.EquivalentTo(originalBindings),
                "Scene dragging must preserve the existing tracks without adding Transform curves.");
            Assert.That(enemy.scene.isDirty, Is.EqualTo(sceneWasDirty));

            Undo.PerformUndo();
            yield return null;
            Assert.That(preview.Update(), Is.True);
            Assert.That(AnimationUtility.GetEditorCurve(clip, binding).Evaluate(window.time),
                Is.EqualTo(previousValue).Within(0.0001f));
            window.previewing = false;
            yield return null;
            Assert.That(preview.Update(), Is.False);
            preview.Dispose();
            preview = null;
            yield return null;
            Assert.That(enemy.transform.position.x, Is.EqualTo(origin.x).Within(0.0001f),
                "Ending movement preview must restore the root outside the Animator hierarchy.");
            Assert.That(Tools.hidden, Is.EqualTo(toolsWereHidden));
            Assert.That(enemy.scene.isDirty, Is.EqualTo(sceneWasDirty));

            Vector3 scale = enemy.transform.localScale;
            scale.x = -Mathf.Abs(scale.x);
            enemy.transform.localScale = scale;
            window.previewing = true;
            window.frame = 3;
            yield return null;
            preview = EnemyMovementCurvePreview.Start(receiver, window);
            Assert.That(preview, Is.Not.Null);
            expected = AnimationUtility.GetEditorCurve(clip, binding).Evaluate(window.time);
            Assert.That(enemy.transform.position.x, Is.EqualTo(origin.x - expected).Within(0.0001f));
            preview.Dispose();
            preview = null;
            Assert.That(enemy.transform.position.x, Is.EqualTo(origin.x).Within(0.0001f));
        }
        finally
        {
            preview?.Dispose();
            window.previewing = false;
            Selection.objects = originalSelection;
            if (enemy != null) Object.DestroyImmediate(enemy);
            AssetDatabase.DeleteAsset(folder);
            window.animationClip = originalClip;
            window.frame = originalFrame;
            window.previewing = wasPreviewing;
            window.recording = wasRecording;
            window.playing = wasPlaying;
        }
    }

    private static void ReplaceMovementClip(AnimatorStateMachine machine, AnimationClip clip)
    {
        foreach (var child in machine.states)
            if (child.state.behaviours.Any(b => b is EnemyMovementStateBehaviour)) child.state.motion = clip;
        foreach (var child in machine.stateMachines) ReplaceMovementClip(child.stateMachine, clip);
    }
}

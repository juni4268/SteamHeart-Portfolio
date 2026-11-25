using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Boss1HandAnimationBindingTests
{
    private const string ControllerPath = "Assets/Animations/Boss/Boss1/AC_Boss1Hand.controller";

    [Test]
    public void SharedBindingsMatchTheirMotionStateAndIncomingTrigger()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var states = Boss1HandAnimationBindingUtility.GetStates(controller);
        var bound = states.Where(s => s.State.behaviours.OfType<Boss1HandAnimationBinding>().Any()).ToArray();
        Assert.That(bound.SelectMany(s => s.State.behaviours.OfType<Boss1HandAnimationBinding>())
            .Select(b => b.Role), Is.EquivalentTo(Enum.GetValues(typeof(Boss1HandAnimationRole))
                .Cast<Boss1HandAnimationRole>().Where(r => r != Boss1HandAnimationRole.None)));
        foreach (var owner in bound)
        {
            var bindings = owner.State.behaviours.OfType<Boss1HandAnimationBinding>().ToArray();
            Assert.That(bindings, Has.Length.EqualTo(1), owner.Path);
            Assert.That(Boss1HandAnimationBindingUtility.Validate(controller, owner, bindings[0]), Is.Null, owner.Path);
        }
    }

    [UnityTest]
    public IEnumerator RenamedStatesMovedGraphsAndReplacedClipsKeepRolePlaybackAndCleanup()
    {
        yield return new EnterPlayMode();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        string folder = "Assets/Editor/Tests/__HandAnimationBinding_" + Guid.NewGuid().ToString("N");
        GameObject hand = null;
        AssetDatabase.CreateFolder("Assets/Editor/Tests", folder.Substring(folder.LastIndexOf('/') + 1));
        try
        {
            string fixturePath = folder + "/Controller.controller";
            Assert.That(AssetDatabase.CopyAsset(ControllerPath, fixturePath), Is.True);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(fixturePath);
            var states = Boss1HandAnimationBindingUtility.GetStates(controller);
            var prepareState = StateFor(states, Boss1HandAnimationRole.PropellerPrepare);
            var attackState = StateFor(states, Boss1HandAnimationRole.PropellerAttack);
            var returnState = StateFor(states, Boss1HandAnimationRole.PropellerReturn);
            // Clip replacement touches only Motion; role and pattern tracks remain unchanged.
            foreach (var owner in states.Where(s => s.State.behaviours.OfType<Boss1HandAnimationBinding>().Any()))
            {
                string copyPath = folder + "/" + Guid.NewGuid().ToString("N") + ".anim";
                Assert.That(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(owner.State.motion), copyPath), Is.True);
                owner.State.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(copyPath);
                EditorUtility.SetDirty(owner.State);
            }
            var prepare = (AnimationClip)prepareState.motion;
            var attack = (AnimationClip)attackState.motion;
            var returning = (AnimationClip)returnState.motion;
            var binding = prepareState.behaviours.OfType<Boss1HandAnimationBinding>().Single();
            string trigger = binding.TriggerName;
            Assert.That(AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(prepare), "ReplacementClip"), Is.Empty);
            var machine = controller.layers[0].stateMachine.stateMachines
                .Single(m => m.stateMachine.states.Any(s => s.state == prepareState)).stateMachine;
            var transferred = machine.states.Single(s => s.state == prepareState);
            machine.states = machine.states.Where(s => s.state != prepareState).ToArray();
            var moved = controller.layers[0].stateMachine.AddStateMachine("Moved role graph");
            moved.states = new[] { transferred };
            machine.name = "Renamed original graph";
            prepareState.name = "Renamed preparation state";
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(moved);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            Assert.That(binding.TriggerName, Is.EqualTo(trigger));
            Assert.That(Boss1HandAnimationBindingUtility.Validate(controller,
                Boss1HandAnimationBindingUtility.FindOwner(controller, binding), binding), Is.Null);

            AnimationClip idle = (AnimationClip)controller.layers[0].stateMachine.defaultState.motion;
            Sprite idleSprite = AnimationUtility.GetObjectReferenceCurve(idle,
                AnimationUtility.GetObjectReferenceCurveBindings(idle).Single(b => b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite"))
                .Select(k => k.value).OfType<Sprite>().First();
            hand = new GameObject("Role animation test");
            hand.SetActive(false);
            hand.AddComponent<SpriteRenderer>().sprite = idleSprite;
            var animator = hand.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var driver = hand.AddComponent<Boss1HandAnimationController>();
            hand.SetActive(true);
            var track = CreateTrack();
            yield return null;

            Assert.That(driver.PlayPrepare(track), Is.True);
            animator.Update(0f);
            animator.Update(0f);
            animator.Update(prepare.length + 0.1f);
            yield return null;
            Assert.That(driver.IsHolding, Is.True);
            Assert.That(hand.GetComponent<SpriteRenderer>().flipX, Is.True);
            Assert.That(driver.PlayAttack(), Is.True);
            animator.Update(0f);
            animator.Update(attack.length * 2f + 0.1f);
            yield return null;
            Assert.That(driver.IsPlaying, Is.True);
            Assert.That(driver.IsHolding, Is.False);
            Assert.That(driver.PlayReturn(), Is.True);
            for (int i = 0; i < 4 && driver.CurrentPhase != Boss1HandAnimationPhase.Idle; i++)
            {
                animator.Update(returning.length + 0.1f);
                yield return null;
            }
            Assert.That(driver.CurrentPhase, Is.EqualTo(Boss1HandAnimationPhase.Idle));
            Assert.That(animator.GetCurrentAnimatorClipInfo(0).Single().clip, Is.SameAs(idle));
            float idleTime = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f) * idle.length;
            var idleFrames = AnimationUtility.GetObjectReferenceCurve(idle,
                AnimationUtility.GetObjectReferenceCurveBindings(idle)
                    .Single(b => b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite"));
            Sprite expectedSprite = (Sprite)idleFrames.Last(k => k.time <= idleTime).value;
            Assert.That(hand.GetComponent<SpriteRenderer>().sprite, Is.SameAs(expectedSprite));
            Assert.That(hand.GetComponent<SpriteRenderer>().flipX, Is.False);

            Assert.That(driver.PlayPrepare(track), Is.True);
            animator.enabled = false;
            yield return null;
            Assert.That(driver.IsPlaying, Is.False);
            Assert.That(driver.PlayPrepare(track), Is.False);
            animator.enabled = true;
            driver.ForceIdle();
            Set(track, "prepareRole", (Boss1HandAnimationRole)999);
            LogAssert.Expect(LogType.Warning, new Regex("needs a unique role binding"));
            Assert.That(driver.PlayPrepare(track), Is.False);
        }
        finally
        {
            if (hand != null) Object.DestroyImmediate(hand);
            AssetDatabase.DeleteAsset(folder);
        }
        yield return new ExitPlayMode();
    }

    private static AnimatorState StateFor(System.Collections.Generic.List<Boss1HandAnimationBindingUtility.StateChoice> states,
        Boss1HandAnimationRole role) =>
        states.Single(s => s.State.behaviours.OfType<Boss1HandAnimationBinding>().Any(b => b.Role == role)).State;

    private static Boss1HandAnimationTrack CreateTrack()
    {
        var track = new Boss1HandAnimationTrack();
        Set(track, "prepareRole", Boss1HandAnimationRole.PropellerPrepare);
        Set(track, "attackRole", Boss1HandAnimationRole.PropellerAttack);
        Set(track, "returnRole", Boss1HandAnimationRole.PropellerReturn);
        Set(track, "flipX", true);
        return track;
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) { Time.timeScale = 1f; yield return new ExitPlayMode(); }
    }
}

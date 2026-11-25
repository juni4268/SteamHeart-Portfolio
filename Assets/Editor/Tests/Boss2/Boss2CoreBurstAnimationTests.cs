using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class Boss2CoreBurstAnimationTests
{
    [Test]
    public void BodyUsesTheAuthoredCoreburstClipUntilThePoseEnds()
    {
        var root = new GameObject("Core Burst body pose test");
        try
        {
            root.AddComponent<SpriteRenderer>();
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Boss/Boss2/Boss2BodyIdle.controller");
            var pose = root.AddComponent<Boss2CoreBurstPose>();
            animator.Rebind();
            animator.Update(0f);
            pose.Begin();
            Assert.That(pose.Entered, Is.False);
            for (int i = 0; i < 10 && !pose.Entered; i++) animator.Update(1f / 60f);
            Assert.That(pose.Entered, Is.True);
            Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Animations/Boss/Boss2/Boss2BodyCoreburst.anim")));
            for (int i = 0; i < 360; i++) animator.Update(1f / 60f);
            Assert.That(pose.Completed, Is.False);
            pose.End();
            Assert.That(pose.Completed, Is.False, "End request must wait for actual Animator exit.");
            for (int i = 0; i < 10 && !pose.Completed; i++) animator.Update(1f / 60f);
            Assert.That(pose.Completed, Is.True);
            Assert.That(pose.Interrupted, Is.False);
            Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Animations/Boss/Boss2/Boss2BodyIdle.anim")));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void RingPlaysBothSheetsOnceAndHoldsUntilThePoseEnds()
    {
        var root = new GameObject("Core Burst ring pose test");
        try
        {
            var renderer = root.AddComponent<SpriteRenderer>();
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Boss/Boss2/Boss2RingIdle.controller");
            var pose = root.AddComponent<Boss2CoreBurstPose>();
            animator.Rebind();
            animator.Update(0f);
            pose.Begin();
            var frames = new List<Sprite>();
            for (int i = 0; i < 360; i++)
            {
                animator.Update(1f / 60f);
                if (pose.Entered && (frames.Count == 0 || frames[frames.Count - 1] != renderer.sprite))
                    frames.Add(renderer.sprite);
            }
            Assert.That(frames.Count, Is.EqualTo(51));
            Assert.That(AssetDatabase.GetAssetPath(frames.First()), Does.EndWith("Boss2CoreburstRing.png"));
            Assert.That(AssetDatabase.GetAssetPath(frames.Last()), Does.EndWith("Boss2CoreburstRing2.png"));
            Assert.That(pose.Completed, Is.False);
            var last = renderer.sprite;
            for (int i = 0; i < 120; i++) animator.Update(1f / 60f);
            Assert.That(renderer.sprite, Is.SameAs(last));
            pose.End();
            for (int i = 0; i < 10 && !pose.Completed; i++) animator.Update(1f / 60f);
            Assert.That(pose.Completed, Is.True);
            Assert.That(pose.Interrupted, Is.False);
            Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Animations/Boss/Boss2/Boss2RingIdle.anim")));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void CancelledPoseNeverReportsNormalCompletion()
    {
        var root = new GameObject("Core Burst cancelled pose test");
        try
        {
            root.AddComponent<SpriteRenderer>();
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Boss/Boss2/Boss2BodyIdle.controller");
            var pose = root.AddComponent<Boss2CoreBurstPose>();
            animator.Rebind();
            animator.Update(0f);
            pose.Begin();
            for (int i = 0; i < 10; i++) animator.Update(1f / 60f);
            pose.Cancel();
            for (int i = 0; i < 10; i++) animator.Update(1f / 60f);
            Assert.That(pose.Completed, Is.False);
            Assert.That(pose.Interrupted, Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("UpperHandL")]
    [TestCase("UpperHandR")]
    [TestCase("LowerHandL")]
    [TestCase("LowerHandR")]
    public void HandsHoldTheLastFrameAndReturnThroughEveryFrame(string hand)
    {
        var root = new GameObject("Core Burst hand test");
        try
        {
            var renderer = root.AddComponent<SpriteRenderer>();
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Boss/Boss2/Boss2" + hand + ".overrideController");
            var animation = root.AddComponent<Boss2CoreBurstHandAnimation>();
            animator.Rebind();
            animator.Update(0f);
            animation.Begin();
            var forward = new List<Sprite>();
            for (int i = 0; i < 180 && !animation.Prepared; i++)
            {
                animator.Update(1f / 60f);
                if (forward.Count == 0 || forward[forward.Count - 1] != renderer.sprite) forward.Add(renderer.sprite);
            }
            Assert.That(animation.Prepared, Is.True);
            Assert.That(forward.Count, Is.EqualTo(10));
            for (int i = 0; i < 120; i++) animator.Update(1f / 60f);
            Assert.That(renderer.sprite, Is.SameAs(forward[forward.Count - 1]));
            animation.BeginReturn();
            var reverse = new List<Sprite>();
            for (int i = 0; i < 180 && !animation.Completed; i++)
            {
                animator.Update(1f / 60f);
                if (!animation.Completed && (reverse.Count == 0 || reverse[reverse.Count - 1] != renderer.sprite)) reverse.Add(renderer.sprite);
            }
            forward.Reverse();
            CollectionAssert.AreEqual(forward, reverse);
            Assert.That(animation.Completed, Is.True);
            Assert.That(animation.Interrupted, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase("Boss2BodyIdle")]
    [TestCase("Boss2RingIdle")]
    public void DisablingPoseAnimatorDuringReturnReportsInterruption(string controller)
    {
        var root = new GameObject("Core Burst interrupted return test");
        try
        {
            root.AddComponent<SpriteRenderer>();
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Boss/Boss2/" + controller + ".controller");
            var pose = root.AddComponent<Boss2CoreBurstPose>();
            animator.Rebind();
            animator.Update(0f);
            pose.Begin();
            for (int i = 0; i < 10 && !pose.Entered; i++) animator.Update(1f / 60f);
            Assert.That(pose.Entered, Is.True);

            pose.End();
            animator.enabled = false;
            Assert.That(pose.Interrupted, Is.True);
            Assert.That(pose.Completed, Is.False);
            pose.Cancel();
            Assert.That(pose.Active, Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeathReturnStopsWaitingWhenBodyOrRingAnimatorIsDisabled(bool disableRing)
    {
        var bodyRoot = new GameObject("Core Burst death return body test");
        var ringRoot = new GameObject("Core Burst death return ring test");
        try
        {
            Boss2CoreBurstPose CreatePose(GameObject root, string controller)
            {
                root.AddComponent<SpriteRenderer>();
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Animations/Boss/Boss2/" + controller + ".controller");
                var pose = root.AddComponent<Boss2CoreBurstPose>();
                animator.Rebind();
                animator.Update(0f);
                pose.Begin();
                for (int i = 0; i < 10 && !pose.Entered; i++) animator.Update(1f / 60f);
                Assert.That(pose.Entered, Is.True);
                return pose;
            }
            var bodyPose = CreatePose(bodyRoot, "Boss2BodyIdle");
            var ringPose = CreatePose(ringRoot, "Boss2RingIdle");
            var pattern = bodyRoot.AddComponent<Boss2CoreBurstPattern>();
            var fields = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(Boss2CoreBurstPattern).GetField("bodyAnimation", fields).SetValue(pattern, bodyPose);
            var executionType = typeof(Boss2CoreBurstPattern).GetNestedType("Execution",
                System.Reflection.BindingFlags.NonPublic);
            var execution = (Boss2PatternExecution)System.Activator.CreateInstance(executionType,
                pattern, System.Array.Empty<Boss2CoreBurstHandAnimation>(), ringPose);
            var routine = execution.ReturnAfterDeath(null);
            Assert.That(routine.MoveNext(), Is.True);
            (disableRing ? ringRoot : bodyRoot).GetComponent<Animator>().enabled = false;
            Assert.Throws<System.InvalidOperationException>(() => routine.MoveNext());
        }
        finally
        {
            Object.DestroyImmediate(ringRoot);
            Object.DestroyImmediate(bodyRoot);
        }
    }

    [Test]
    public void CorePlaysBothSheetsBeforeNormalCompletion()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss2/Boss2CoreBurstCore.prefab");
        var root = Object.Instantiate(prefab);
        try
        {
            var renderer = root.GetComponent<SpriteRenderer>();
            var animator = root.GetComponent<Animator>();
            var core = root.GetComponent<Boss2CoreBurstVisual>();
            animator.Rebind();
            animator.Update(0f);
            core.Begin();
            var frames = new List<Sprite>();
            for (int i = 0; i < 400 && !core.Completed; i++)
            {
                animator.Update(1f / 60f);
                if (renderer.sprite != null && (frames.Count == 0 || frames[frames.Count - 1] != renderer.sprite))
                    frames.Add(renderer.sprite);
            }
            Assert.That(core.Completed, Is.True);
            Assert.That(core.Interrupted, Is.False);
            Assert.That(frames.Count, Is.EqualTo(49));
            Assert.That(AssetDatabase.GetAssetPath(frames.First()), Does.EndWith("Boss2CoreburstCore1.png"));
            Assert.That(AssetDatabase.GetAssetPath(frames.Last()), Does.EndWith("Boss2CoreburstCore2.png"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void CancelledCoreDoesNotReportCompletion()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Boss/Boss2/Boss2CoreBurstCore.prefab");
        var root = Object.Instantiate(prefab);
        try
        {
            var animator = root.GetComponent<Animator>();
            var core = root.GetComponent<Boss2CoreBurstVisual>();
            animator.Rebind();
            animator.Update(0f);
            core.Begin();
            for (int i = 0; i < 30; i++) animator.Update(1f / 60f);
            core.Cancel();
            for (int i = 0; i < 400; i++) animator.Update(1f / 60f);
            Assert.That(core.Completed, Is.False);
            Assert.That(core.Interrupted, Is.True);
        }
        finally { Object.DestroyImmediate(root); }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class PlayerW2AnimationTests
{
    private const string Animations = "Assets/Animations/Player/";
    private const string Effects = "Assets/Prefabs/CombatEffects/";

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    [Test]
    public void W2OverridesPreserveMatchedMotionTimingAndEffectsFinishTheirClips()
    {
        var w1 = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Animations + "AC_PlayerW1.overrideController");
        var w2 = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Animations + "AC_PlayerW2.overrideController");
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        w1.GetOverrides(pairs);
        int matched = 0;
        foreach (var pair in pairs)
        {
            bool groundAttack = pair.Key.name == "PlayerAttack1" || pair.Key.name == "PlayerAttack2";
            AnimationClip source = pair.Value ?? pair.Key;
            AnimationClip replacement = w2[pair.Key];
            string path = AssetDatabase.GetAssetPath(replacement);
            Assert.That(path, Does.StartWith(groundAttack ? Animations : Animations + "W2/"));
            Assert.That(replacement.frameRate, Is.EqualTo(source.frameRate), path);
            Assert.That(replacement.length, Is.EqualTo(source.length).Within(0.0001f), path);
            Assert.That(replacement.isLooping, Is.EqualTo(source.isLooping), path);
            AnimationEvent[] originalEvents = source.events;
            if (pair.Key.name == "PlayerDash")
                originalEvents = originalEvents.Where(e => e.functionName != nameof(PlayerCombatEffectAnimationEvents.SpawnCombatEffect)).ToArray();
            AnimationEvent[] events = replacement.events;
            Assert.That(events.Length, Is.EqualTo(originalEvents.Length), path);
            for (int i = 0; i < events.Length; i++)
            {
                Assert.That(events[i].time, Is.EqualTo(originalEvents[i].time).Within(0.0001f), path);
                Assert.That(events[i].functionName, Is.EqualTo(originalEvents[i].functionName), path);
                if (originalEvents[i].objectReferenceParameter != null)
                    Assert.That(AssetDatabase.GetAssetPath(events[i].objectReferenceParameter), Does.StartWith(Effects + "PlayerW2"), path);
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(replacement))
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(replacement, binding))
                    Assert.That(key.value.name, Does.StartWith("PlayerW2"), path);
            matched++;
        }
        Assert.That(matched, Is.EqualTo(17));

        foreach (string name in new[] { "PlayerW2Attack1Effect", "PlayerW2Attack2Effect", "PlayerW2JumpAttackEffect",
                     "PlayerW2SkillAEffect", "PlayerW2SkillSEffect", "PlayerJumpEffect" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Effects + name + ".prefab");
            Assert.That(prefab.GetComponent<CombatAnimationEffect>(), Is.Not.Null, name);
            var controller = prefab.GetComponent<Animator>().runtimeAnimatorController;
            Assert.That(controller, Is.TypeOf<AnimatorOverrideController>(), name);
            AnimationClip clip = controller.animationClips.Single();
            Assert.That(clip.isLooping, Is.False, name);
            AnimationEvent finish = clip.events.Single();
            Assert.That(finish.functionName, Is.EqualTo("FinishEffect"), name);
            Assert.That(finish.time, Is.EqualTo(clip.length).Within(0.0001f), name);
        }
    }

    [UnityTest]
    public IEnumerator ActualJumpSpawnsOneStationaryFootEffectAtEveryWeaponStage()
    {
        yield return new EnterPlayMode();
        DisableSceneRoots();
        foreach (int stage in new[] { -1, 0, 1 })
        {
            GameObject instance = CreatePlayer(stage);
            PlayerMovement movement = instance.GetComponent<PlayerMovement>();
            Physics2D.SyncTransforms();
            Bounds bounds = instance.GetComponent<Collider2D>().bounds;
            Set(movement, "isGrounded", true);
            Player player = instance.GetComponent<Player>();
            player.RequestAction(PlayerAction.Jump);
            player.RequestAction(PlayerAction.Jump);

            CombatAnimationEffect effect = Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None).Single();
            Vector3 position = effect.transform.position;
            Assert.That(effect.transform.parent, Is.Null);
            Assert.That(position.x, Is.EqualTo(bounds.center.x).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(bounds.min.y).Within(0.0001f));
            Assert.That(instance.GetComponent<PlayerMotor>().Velocity.y, Is.GreaterThan(0f));
            instance.transform.position += new Vector3(5f, 2f, 0f);
            instance.SetActive(false);
            Assert.That(effect.gameObject.activeInHierarchy, Is.True);
            Assert.That(effect.transform.position, Is.EqualTo(position));
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (effect != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(effect == null, Is.True, "Jump effect must finish independently of the player.");
            Object.Destroy(instance);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator W2DashDoesNotSpawnCombatEffects()
    {
        yield return new EnterPlayMode();
        DisableSceneRoots();
        GameObject instance = CreatePlayer(1);
        instance.GetComponent<PlayerMovement>().enabled = false;
        Animator animator = instance.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.SetBool("isGrounded", true);
        animator.Update(0f);
        var dash = instance.GetComponent<PlayerDash>();
        Invoke(dash, "StartDash");
        animator.Update(0f);
        animator.Update(0.01f);
        Assert.That(dash.IsDashing, Is.True);
        Assert.That(dash.IsInvincible, Is.True);
        float deadline = Time.realtimeSinceStartup + 2f;
        do
        {
            Assert.That(Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None), Is.Empty,
                "W2 dash must not spawn either independent or following combat effects.");
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "W2 dash must still finish without an effect.");
            yield return null;
        } while (dash.IsDashing);
        Assert.That(dash.IsInvincible, Is.False);
        Object.Destroy(instance);
    }

    private static GameObject CreatePlayer(int stage)
    {
        PlayerRuntimeState.ResetRunProgress();
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        instance.GetComponent<WeaponUnlockOnSceneStart>().enabled = false;
        instance.transform.position = new Vector3(1000f, 1000f, 0f);
        instance.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        instance.GetComponent<PlayerMotor>().SetGravityScale(0f);
        if (stage >= 0)
        {
            PlayerWeaponController weapon = instance.GetComponent<PlayerWeaponController>();
            weapon.EquipWeapon();
            weapon.SetWeaponStage(stage);
        }
        return instance;
    }

    private static void DisableSceneRoots()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;
        new GameObject("W2 animation test camera", typeof(Camera)).tag = "MainCamera";
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}

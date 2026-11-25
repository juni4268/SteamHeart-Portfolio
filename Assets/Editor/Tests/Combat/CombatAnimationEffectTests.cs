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

[Category("PlayerRefactor")]
public sealed class CombatAnimationEffectTests
{
    private static IEnumerable<(AnimationClip clip, bool secondAttack, string damageMethod)> AttackMotions()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        var weapon = (WeaponData)new SerializedObject(player.GetComponent<PlayerWeaponController>())
            .FindProperty("weaponData").objectReferenceValue;
        var controllers = new[] { player.GetComponentInChildren<Animator>().runtimeAnimatorController }
            .Concat(weapon.WeaponStages.Select(s => (RuntimeAnimatorController)s.AnimatorOverrideController))
            .Where(c => c != null).Distinct();
        var seen = new HashSet<AnimationClip>();
        foreach (RuntimeAnimatorController runtime in controllers)
        {
            var overrides = runtime as AnimatorOverrideController;
            var controller = (AnimatorController)(overrides != null ? overrides.runtimeAnimatorController : runtime);
            foreach (AnimatorState state in States(controller.layers[0].stateMachine))
            {
                PlayerGroundAttackStateBehaviour attack = state.behaviours.OfType<PlayerGroundAttackStateBehaviour>().SingleOrDefault();
                bool skill = state.behaviours.Any(b => b is PlayerGroundSlamStateBehaviour);
                if (attack == null && !skill) continue;
                var motion = state.motion as AnimationClip;
                Assert.That(motion, Is.Not.Null, "An attack role must have an explicit Motion.");
                AnimationClip clip = overrides != null && overrides[motion] != null ? overrides[motion] : motion;
                if (!seen.Add(clip)) continue;
                bool second = attack != null && new SerializedObject(attack).FindProperty("attackNumber").intValue == 2;
                yield return (clip, second, skill
                    ? nameof(PlayerGroundSlamAnimationEvents.ApplyGroundSlamDamage)
                    : nameof(PlayerAttackAnimationEvents.ApplyGroundAttackDamage));
            }
        }
    }

    private static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (ChildAnimatorState state in machine.states) yield return state.state;
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            foreach (AnimatorState state in States(child.stateMachine)) yield return state;
    }

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void AttackClipsSpawnIndependentEffectsAndKeepDamageEvents()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        Assert.That(player.GetComponentInChildren<Animator>().GetComponent<PlayerCombatEffectAnimationEvents>(), Is.Not.Null);
        var motions = AttackMotions().ToArray();
        Assert.That(motions, Is.Not.Empty);

        foreach (var motion in motions)
        {
            AnimationClip clip = motion.clip;
            string path = AssetDatabase.GetAssetPath(clip);
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            AnimationEvent spawn = events.Single(e => e.functionName == nameof(PlayerCombatEffectAnimationEvents.SpawnCombatEffect));
            AnimationEvent damage = events.Single(e => e.functionName == motion.damageMethod);
            Assert.That(spawn.time, Is.EqualTo(damage.time).Within(0.0001f), path);
            if (motion.secondAttack)
            {
                Assert.That(events[0].functionName, Is.EqualTo("FaceGroundComboDirection"), path);
                Assert.That(events[0].time, Is.Zero, path);
            }
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.path.EndsWith("Effect")), Is.False, path);
            Assert.That(AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(b => b.path.EndsWith("Effect")), Is.False, path);

            GameObject prefab = spawn.objectReferenceParameter as GameObject;
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.GetComponent<CombatAnimationEffect>(), Is.Not.Null, path);
            Assert.That(prefab.GetComponentsInChildren<Collider2D>(true), Is.Empty, path);
            AnimationClip effectClip = prefab.GetComponent<Animator>().runtimeAnimatorController.animationClips.Single();
            AnimationEvent finish = AnimationUtility.GetAnimationEvents(effectClip).Single();
            Assert.That(effectClip.isLooping, Is.False, path);
            Assert.That(finish.functionName, Is.EqualTo("FinishEffect"), path);
            Assert.That(finish.time, Is.EqualTo(effectClip.length).Within(0.0001f), path);
        }
    }

    [UnityTest]
    public IEnumerator SpawnedEffectsFinishAfterOwnerDisable()
    {
        yield return new EnterPlayMode();
        // Only play-mode copies are disabled; unsaved Editor scene changes are preserved.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;

        foreach (var motion in AttackMotions())
        {
            AnimationClip clip = motion.clip;
            string path = AssetDatabase.GetAssetPath(clip);
            AnimationEvent spawn = AnimationUtility.GetAnimationEvents(clip).Single(e => e.functionName == "SpawnCombatEffect");
            var source = new GameObject("Combat effect event test", typeof(SpriteRenderer), typeof(Animator),
                typeof(PlayerCombatEffectAnimationEvents));
            source.transform.position = new Vector3(1000f, 1000f, 0f);
            source.transform.localScale = new Vector3(-1f, 1f, 1f);
            Animator animator = source.GetComponent<Animator>();
            // Invoke the event receiver with the actual authored prefab; the combo test below
            // separately verifies that the Animator dispatches the two attack events.
            source.GetComponent<PlayerCombatEffectAnimationEvents>()
                .SpawnCombatEffect((GameObject)spawn.objectReferenceParameter);

            CombatAnimationEffect effect = Object.FindFirstObjectByType<CombatAnimationEffect>();
            Assert.That(effect, Is.Not.Null, path);
            Transform effectTransform = effect.transform;
            Assert.That(effectTransform.parent, Is.Null, path);
            Assert.That(effectTransform.position, Is.EqualTo(source.transform.position), path);
            Assert.That(effectTransform.localScale.x, Is.EqualTo(-1f), path);
            SpriteRenderer visual = effect.GetComponentInChildren<SpriteRenderer>();
            Assert.That(visual.enabled, Is.True, path);
            Assert.That(visual.sprite, Is.Not.Null, path);

            animator.enabled = false;
            source.transform.position += Vector3.right * 5f;
            source.SetActive(false);
            Assert.That(effect != null && effect.gameObject.activeInHierarchy, Is.True, path);
            Assert.That(effectTransform.position, Is.EqualTo(new Vector3(1000f, 1000f, 0f)), path);

            AnimationClip effectClip = effect.GetComponent<Animator>().runtimeAnimatorController.animationClips.Single();
            // Edit Mode UnityTests advance with frames even after EnterPlayMode.
            float deadline = Time.realtimeSinceStartup + effectClip.length + 1f;
            while (effect != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(effect == null, Is.True, path + " did not finish its independent animation.");
            Object.Destroy(source);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator ComboTurnUpdatesSecondEffectAndPreservesFirstEffectDirection()
    {
        yield return new EnterPlayMode();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
        Time.timeScale = 1f;

        foreach (float startingDirection in new[] { 1f, -1f })
        {
            GameObject source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
            source.GetComponent<WeaponUnlockOnSceneStart>().enabled = false;
            source.GetComponent<Player>().enabled = false;
            PlayerMovement movement = source.GetComponent<PlayerMovement>();
            movement.enabled = false;
            source.GetComponent<PlayerMotor>().SetGravityScale(0f);
            source.transform.position = new Vector3(1000f, 1000f, 0f);
            source.transform.localScale = new Vector3(startingDirection, 1f, 1f);
            typeof(PlayerMovement).GetField("isFacingRight", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(movement, startingDirection > 0f);
            typeof(PlayerMovement).GetField("moveInput", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(movement, new Vector2(-startingDirection, 0f));
            Animator animator = source.GetComponentInChildren<Animator>();
            source.GetComponent<PlayerWeaponController>().EquipWeapon();
            animator.Rebind();
            animator.SetBool("isGrounded", true);
            animator.Update(0f);
            animator.SetTrigger("GroundAttack1");
            animator.Update(0f);
            animator.Update(0.18f);
            CombatAnimationEffect first = Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None)
                .Single();
            animator.SetTrigger("GroundAttack2");
            animator.Update(0.25f);
            animator.Update(0.01f);
            CombatAnimationEffect second = Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None)
                .Single(e => e != first);

            Assert.That(AnimationUtility.GetAnimationEvents(animator.GetCurrentAnimatorClipInfo(0).Single().clip)
                .Any(e => e.functionName == nameof(PlayerAttackAnimationEvents.FaceGroundComboDirection)), Is.True);
            Assert.That(source.transform.TransformVector(Vector3.right).x, Is.EqualTo(-startingDirection));
            Assert.That(first.transform.TransformVector(Vector3.right).x, Is.EqualTo(startingDirection));
            Assert.That(second.transform.TransformVector(Vector3.right).x, Is.EqualTo(-startingDirection));
            Assert.That(first.transform.parent, Is.Null);
            Assert.That(second.transform.parent, Is.Null);
            Object.Destroy(first.gameObject);
            Object.Destroy(second.gameObject);
            Object.Destroy(source);
            yield return null;
        }
    }
}

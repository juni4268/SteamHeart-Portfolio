using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PlayerDamageFeedbackTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static object Get(object target, string field) => target.GetType().GetField(field, PrivateInstance).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

    [UnityTest]
    public IEnumerator DamagePlaysOneImpactAndEarlyAudioWhileBlockedHitsStayQuiet()
    {
        yield return new EnterPlayMode();
        Task task = VerifyRuntime();
        while (!task.IsCompleted) yield return null;
        task.GetAwaiter().GetResult();
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    public static async Task VerifyRuntime()
    {
        Assert.That(Application.isPlaying, Is.True);
        int originalHealth = PlayerRuntimeState.CurrentHP;
        var existing = new HashSet<CombatAnimationEffect>(Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None));
        var playerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        var feedbackRoot = new GameObject("Player damage feedback test");
        feedbackRoot.SetActive(false);
        try
        {
            playerObject.transform.position = new Vector3(1000f, 1000f);
            playerObject.GetComponent<Rigidbody2D>().simulated = false;
            playerObject.GetComponent<PlayerMovement>().enabled = false;
            playerObject.GetComponent<WeaponUnlockOnSceneStart>().enabled = false;
            var health = playerObject.GetComponent<PlayerHealth>();
            Set(health, "currentHP", health.MaxHP);
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Canvas.prefab")
                .GetComponentInChildren<PlayerHitFeedback>(true);
            var feedback = Object.Instantiate(template, feedbackRoot.transform);
            Set(feedback, "playerHealth", health);
            feedbackRoot.SetActive(true);
            await Task.Delay(50);

            Assert.That(health.TryTakeDamage(new DamageInfo(1)), Is.True);
            var impacts = Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None)
                .Where(effect => !existing.Contains(effect)).ToArray();
            Assert.That(impacts.Length, Is.EqualTo(1));
            var impact = impacts[0];
            Assert.That(impact.transform.position, Is.EqualTo(playerObject.GetComponent<Collider2D>().bounds.center));
            Assert.That(impact.transform.localScale.x, Is.EqualTo(0.55f).Within(0.001f));
            Assert.That((float)Get(feedback, "currentFlashAmount"), Is.EqualTo(0.95f).Within(0.001f));

            var audio = playerObject.GetComponent<PlayerAudio>();
            var voice = Get(audio, "reactionVoice");
            var source = (AudioSource)Get(voice, "source");
            Assert.That(source.clip, Is.SameAs(Get(audio, "hurt1")));
            Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(Mathf.RoundToInt(0.1f * source.clip.frequency)));
            object flashSequence = Get(feedback, "characterFlashSequence");
            Set(health, "hitInvincibilityEndsAt", Time.time + 1f);
            Assert.That(health.TryTakeDamage(new DamageInfo(1)), Is.False);
            health.Heal(1);
            Assert.That(Get(feedback, "characterFlashSequence"), Is.SameAs(flashSequence));
            Assert.That(Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None)
                .Count(effect => !existing.Contains(effect)), Is.EqualTo(1));
            Assert.That((int)Get(audio, "hurtIndex"), Is.EqualTo(1), "Blocked hits and healing must not restart the hurt sound.");

            await Task.Delay(350);
            Assert.That((float)Get(feedback, "currentFlashAmount"), Is.Zero.Within(0.001f));
            Assert.That(feedback.GetComponent<CanvasGroup>().alpha, Is.Zero.Within(0.001f));
            float deadline = Time.realtimeSinceStartup + 2f;
            while (impact != null && Time.realtimeSinceStartup < deadline) await Task.Delay(20);
            Assert.That(impact == null, Is.True, "The existing effect animation cleans itself up.");

            Set(health, "hitInvincibilityEndsAt", float.NegativeInfinity);
            Assert.That(health.TryTakeDamage(new DamageInfo(1)), Is.True);
            Assert.That(source.clip, Is.SameAs(Get(audio, "hurt2")));
            Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(Mathf.RoundToInt(0.025f * source.clip.frequency)));
            feedback.enabled = false;
            Assert.That((float)Get(feedback, "currentFlashAmount"), Is.Zero);
            Assert.That(feedback.GetComponent<CanvasGroup>().alpha, Is.Zero);
        }
        finally
        {
            Object.Destroy(feedbackRoot);
            Object.Destroy(playerObject);
            foreach (var effect in Object.FindObjectsByType<CombatAnimationEffect>(FindObjectsSortMode.None))
                if (!existing.Contains(effect)) Object.Destroy(effect.gameObject);
            PlayerRuntimeState.SetHealth(originalHealth);
        }
    }
}

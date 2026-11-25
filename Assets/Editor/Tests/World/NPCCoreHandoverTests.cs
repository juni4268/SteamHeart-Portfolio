using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class NPCCoreHandoverTests
{
    private static T Get<T>(object target, string field) => (T)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    [Test]
    public void OrdinaryNPCInteractionStillInvokesItsEventImmediately()
    {
        var root = new GameObject("Ordinary NPC", typeof(InteractableNPC));
        try
        {
            var npc = root.GetComponent<InteractableNPC>();
            int calls = 0;
            npc.OnInteract.AddListener(() => calls++);
            npc.Interact(null);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(npc.CanInteract, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [UnityTest]
    public IEnumerator UpperClassHandoverWaitsForReturnedCoreBeforeHealingAndUnlocking()
    {
        yield return new EnterPlayMode();
        yield return VerifyUpperClassHandover();
        yield return new ExitPlayMode();
    }

    private static IEnumerator VerifyUpperClassHandover()
    {
        AsyncOperation loading = SceneManager.LoadSceneAsync("Assets/Scenes/UpperClass_Map.unity");
        while (!loading.isDone) yield return null;
        yield return null;
        var npc = Object.FindFirstObjectByType<InteractableNPC>();
        var player = Object.FindFirstObjectByType<Player>();
        Assert.That(npc, Is.Not.Null);
        Assert.That(player, Is.Not.Null);
        Assert.That(player.input, Is.Not.Null);
        Assert.That(player.health, Is.Not.Null);
        Assert.That(npc.OnInteract, Is.Not.Null);
        var portal = npc.OnInteract.GetPersistentTarget(0) as MapPortal;
        var audio = Get<AudioSource>(npc, "handoverAudio");
        Assert.That(audio, Is.Not.Null);
        float speed = Get<float>(npc, "handoverSpeed");
        player.input.Player.Disable();
        player.GetComponent<PlayerMovement>().enabled = false;
        player.GetComponent<Rigidbody2D>().simulated = false;
        player.transform.position = npc.transform.position + Vector3.left * 1.2f;
        Assert.That(portal, Is.Not.Null);
        Assert.That(portal.CanInteract, Is.False);
        Assert.That(audio.clip, Is.SameAs(AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Audio/Ambience/LowerTown1.wav")));

        int completed = 0;
        npc.OnInteract.AddListener(() => completed++);
        Assert.That(npc.CanInteract, Is.True);
        npc.Interact(player);
        GameObject core = Get<GameObject>(npc, "handoverInstance");
        Assert.That(core, Is.Not.Null, "Interaction starts the broken core handover.");
        Vector3 start = core.transform.position;
        Vector3 destination = npc.transform.position + Get<Vector3>(npc, "handoverEndOffset");
        float duration = Vector3.Distance(start, destination) / speed;
        npc.Interact(player);
        Assert.That(Get<GameObject>(npc, "handoverInstance"), Is.SameAs(core));
        float midway = Time.time + duration * 0.3f;
        while (Time.time < midway) yield return null;
        Assert.That(Vector3.Distance(core.transform.position, start), Is.GreaterThan(0.05f));
        Assert.That(core.GetComponentsInChildren<UnityEngine.UI.RawImage>(), Has.Length.EqualTo(3));
        Assert.That(audio.isPlaying, Is.False);
        Assert.That(completed, Is.Zero);
        Assert.That(Object.FindObjectsByType<NPCInteractionPickup>(FindObjectsSortMode.None), Is.Empty);

        npc.enabled = false;
        yield return null;
        Assert.That(core == null, Is.True, "Canceling removes the traveling core.");
        Assert.That(audio.isPlaying, Is.False);
        Assert.That(portal.CanInteract, Is.False);
        npc.enabled = true;
        Assert.That(npc.CanInteract, Is.True, "An interrupted handover can be retried.");

        player.transform.position = npc.transform.position + Vector3.left * 2.4f;
        player.health.Heal(player.health.MaxHP);
        player.health.TakeDamage(1);
        int hpBeforeReturn = player.health.CurrentHP;
        int hpAfterReturn = Mathf.Min(player.health.MaxHP, hpBeforeReturn + Get<int>(npc, "healAmount"));
        Assert.That(hpAfterReturn, Is.GreaterThan(hpBeforeReturn));
        Vector3 secondStart = player.transform.position + Get<Vector3>(npc, "handoverStartOffset");
        float secondDuration = Vector3.Distance(secondStart, destination) / speed;
        float started = Time.time;
        npc.Interact(player);
        float deadline = started + secondDuration + audio.clip.length * 2f + 3f;
        while (Get<GameObject>(npc, "handoverInstance") != null)
        {
            Assert.That(Time.time, Is.LessThan(deadline));
            Assert.That(completed, Is.Zero);
            yield return null;
        }
        Assert.That(Time.time - started, Is.EqualTo(secondDuration).Within(0.1f), "Travel time follows distance and speed.");
        Assert.That(audio.isPlaying, Is.True, "The NPC responds only after the core arrives.");
        Assert.That(portal.CanInteract, Is.False);
        Assert.That(Object.FindObjectsByType<NPCInteractionPickup>(FindObjectsSortMode.None), Is.Empty);
        int plays = 1;
        int previousSample = audio.timeSamples;
        bool sawReturningCore = false;
        bool returnedCoreArrived = false;
        while (completed == 0)
        {
            Assert.That(Time.time, Is.LessThan(deadline));
            var returningCore = Object.FindFirstObjectByType<NPCInteractionPickup>();
            if (returningCore != null && !sawReturningCore)
            {
                sawReturningCore = true;
                returningCore.Collected += () => returnedCoreArrived = true;
                Assert.That(audio.isPlaying, Is.False, "The normal core returns after the response sounds finish.");
                Assert.That(player.health.CurrentHP, Is.EqualTo(hpBeforeReturn));
                Assert.That(portal.CanInteract, Is.False);
                npc.Interact(player);
                Assert.That(Object.FindObjectsByType<NPCInteractionPickup>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            }
            if (!returnedCoreArrived)
            {
                Assert.That(player.health.CurrentHP, Is.EqualTo(hpBeforeReturn), "Healing waits for the core to reach the player.");
                Assert.That(portal.CanInteract, Is.False);
            }
            yield return null;
            if (audio.isPlaying)
            {
                int sample = audio.timeSamples;
                if (sample < previousSample) plays++;
                previousSample = sample;
            }
        }

        Assert.That(plays, Is.EqualTo(2), "Both responses finish before the reward and portal unlock.");
        Assert.That(Time.time - started, Is.GreaterThanOrEqualTo(secondDuration + audio.clip.length * 2f - 0.1f));
        Assert.That(sawReturningCore && returnedCoreArrived, Is.True);
        Assert.That(player.health.CurrentHP, Is.EqualTo(hpAfterReturn));
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(portal.CanInteract, Is.True);
        yield return null;
        Assert.That(Object.FindObjectsByType<NPCInteractionPickup>(FindObjectsSortMode.None), Is.Empty);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(npc.CanInteract, Is.False);
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}

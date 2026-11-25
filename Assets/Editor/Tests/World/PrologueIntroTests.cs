using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class PrologueIntroTests
{
    private const string Title = "Assets/Scenes/Title.unity";
    private const string LowerTown = "Assets/Scenes/LowerTown_Map.unity";
    private const string Respawn = "Assets/Scenes/Respawn_Map.unity";
    private const string PortalDestination = "Assets/Scenes/X_Map.unity";
    private const float PrologueStopTime = 9.5f;

    private static T Get<T>(object target, string field) => (T)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static async Task Until(Func<bool> condition, string message, float timeout = 20f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), message);
            await Awaitable.NextFrameAsync();
        }
    }

    private static async Task Ready(string path)
    {
        await Until(() => SceneManager.GetActiveScene().path == path && !SceneTransitionOverlay.IsTransitioning
            && !VideoCutsceneOverlay.IsPlaying, "Scene ready: " + path);
        Assert.That(GameplayPause.IsActive, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(AudioListener.pause, Is.False);
    }

    private static async Task StartFromTitle()
    {
        Assert.That(SceneTransitionOverlay.TryLoadScene(Title, 0f, 0.05f, Color.black), Is.True);
        await Ready(Title);
        var title = UnityEngine.Object.FindFirstObjectByType<TitleMenu>();
        await Until(() => !Get<MenuPanelTransition>(title, "mainPresentation").IsTransitioning, "Title presentation");
        title.StartGame();
        await Until(() => VideoCutsceneOverlay.IsPlaying, "Prologue starts");
    }

    [Test]
    public void CoreBubbleUsesExistingArtAndClearsWhenHidden()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(LowerTown);
        try
        {
            LowerTownIntro intro = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                intro = root.GetComponentInChildren<LowerTownIntro>(true);
                if (intro != null) break;
            }
            Assert.That(intro, Is.Not.Null);
            var core = Get<RawImage>(intro, "coreIcon");
            var brokenCore = Get<GameObject>(intro, "brokenCoreIcon");
            var glow = Get<CanvasGroup>(intro, "coreGlow");
            var bubble = Get<RectTransform>(intro, "speechBubble");
            Assert.That(core.texture, Is.SameAs(UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Graphics/Characters/Boss2/Boss2DeadCore.png")));
            Assert.That(core.material.shader.name, Is.EqualTo("ProjectK/UIHueShift"));
            Assert.That(core.material.GetFloat("_HueShift"), Is.EqualTo(0.64f).Within(0.001f));
            var shards = brokenCore.GetComponentsInChildren<RawImage>(true);
            Assert.That(shards.Length, Is.EqualTo(3));
            foreach (var shard in shards)
            {
                Assert.That(shard.texture, Is.SameAs(core.texture));
                Assert.That(shard.material, Is.SameAs(core.material));
                Assert.That(core.uvRect.Contains(shard.uvRect.min), Is.True);
                Assert.That(shard.uvRect.xMax, Is.LessThanOrEqualTo(core.uvRect.xMax));
                Assert.That(shard.uvRect.yMax, Is.LessThanOrEqualTo(core.uvRect.yMax));
            }
            Assert.That(bubble.Find("Direction"), Is.Null);
            Assert.That(brokenCore.transform.parent, Is.SameAs(bubble));
            Assert.That(glow.transform.parent, Is.SameAs(bubble));
            Assert.That(bubble.Find("Tail"), Is.Null);
            Assert.That(bubble.Find("Body").GetComponent<Image>().color.a, Is.LessThanOrEqualTo(0.2f));
            var routine = (IEnumerator)typeof(LowerTownIntro)
                .GetMethod("ShowFaultyCore", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(intro, null);
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(Get<Transform>(intro, "speaker"), Is.SameAs(Get<Transform>(intro, "playerHead")));
            Assert.That(bubble.gameObject.activeSelf && core.enabled, Is.True);
            Assert.That(brokenCore.activeSelf, Is.False);
            Assert.That(glow.gameObject.activeSelf, Is.False);
            Assert.That(core.color, Is.EqualTo(Color.white));
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(core.color.r, Is.LessThan(0.5f));
            brokenCore.SetActive(true);
            glow.gameObject.SetActive(true);
            glow.alpha = 1f;
            glow.transform.localScale = Vector3.one * 1.1f;
            typeof(LowerTownIntro).GetMethod("HideSpeech", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(intro, null);
            Assert.That(bubble.gameObject.activeSelf || core.enabled || brokenCore.activeSelf, Is.False);
            Assert.That(core.color, Is.EqualTo(Color.white));
            Assert.That(Get<Text>(intro, "speech").text, Is.Empty);
            Assert.That(glow.gameObject.activeSelf, Is.False);
            Assert.That(glow.alpha, Is.Zero);
            Assert.That(glow.transform.localScale, Is.EqualTo(Vector3.one));
            var direction = Get<CanvasGroup>(intro, "directionHint");
            Assert.That(direction.GetComponentsInChildren<Text>(true), Is.Empty);
            var directionImage = Get<Image>(intro, "directionImage");
            Assert.That(direction.GetComponentsInChildren<Image>(true), Is.EqualTo(new[] { directionImage }));
            Assert.That(directionImage.transform, Is.SameAs(direction.transform));
            Assert.That(directionImage.preserveAspect, Is.True);
            Assert.That(directionImage.raycastTarget, Is.False);
            var directionAnimator = Get<Animator>(intro, "directionAnimator");
            Assert.That(directionAnimator, Is.Not.Null);
            Assert.That(directionAnimator.transform, Is.SameAs(direction.transform));
            Assert.That(directionAnimator.enabled, Is.True);
            if (directionAnimator.runtimeAnimatorController == null)
                Assert.That(directionImage.enabled, Is.False, "An unassigned animation must not show a white rectangle.");
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void WaitingUsesPlayerIdleAndWalkingKeepsItsClip()
    {
        var controller = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
            "Assets/Animations/Player/AC_PlayerPrologue.controller");
        var machine = controller.layers[0].stateMachine;
        var idle = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/PlayerIdle.anim");
        Assert.That(machine.defaultState.motion, Is.SameAs(idle));
        var bindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(idle);
        Assert.That(bindings.Length, Is.EqualTo(1));
        var frames = UnityEditor.AnimationUtility.GetObjectReferenceCurve(idle, bindings[0]);
        Assert.That(frames.Length, Is.EqualTo(9));
        Assert.That(Array.TrueForAll(frames, frame => frame.value != null), Is.True);
        foreach (var frame in frames)
            Assert.That(((Sprite)frame.value).pivot.y, Is.EqualTo(132f).Within(0.001f));
        var walk = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/PlayerWalk.anim");
        Assert.That(Array.Exists(machine.states, state => state.state.motion == walk), Is.True);
    }

    [UnityTest]
    public IEnumerator CoreConversationAndPlayerIdlePlayThroughToTheHub()
    {
        yield return new EnterPlayMode();
        Task<List<string>> checks = VerifyCoreConversationRuntime();
        while (!checks.IsCompleted) yield return null;
        checks.GetAwaiter().GetResult();
        yield return new ExitPlayMode();
    }

    private static Task<List<string>> VerifyCoreConversationRuntime() =>
        VerifyWithoutRecording(VerifyCoreConversation);

    private static async Task<List<string>> VerifyCoreConversation()
    {
        await Until(() => !SceneTransitionOverlay.IsTransitioning && !GameplayPause.IsActive, "Initial transition");
        PlayerRuntimeState.ResetRunProgress();
        LowerTownIntro.RequestPlayback();
        Assert.That(SceneTransitionOverlay.TryLoadScene(LowerTown, 0f, 0.05f, Color.black), Is.True);
        await Ready(LowerTown);
        var results = await ObserveConversationRuntime();

        LowerTownIntro.RequestPlayback();
        Assert.That(SceneTransitionOverlay.TryLoadScene(LowerTown, 0f, 0.05f, Color.black), Is.True);
        await Ready(LowerTown);
        var skipped = UnityEngine.Object.FindFirstObjectByType<LowerTownIntro>();
        await Until(() => Get<CanvasGroup>(skipped, "coreGlow").gameObject.activeInHierarchy &&
            Get<CanvasGroup>(skipped, "coreGlow").alpha > 0.1f, "Skip during the healthy core glow");
        Get<Button>(skipped, "skipButton").onClick.Invoke();
        await Ready(Respawn);
        AssertGameplayRestored();
        results.Add("Skipping during the healthy core glow cancels the dialogue and restores gameplay in the hub.");
        return results;
    }

    private static async Task<List<string>> ObserveConversationRuntime()
    {
        var intro = UnityEngine.Object.FindFirstObjectByType<LowerTownIntro>();
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var animator = Get<Animator>(intro, "playerAnimator");
        var renderer = animator.GetComponent<SpriteRenderer>();
        var core = Get<RawImage>(intro, "coreIcon");
        var brokenCore = Get<GameObject>(intro, "brokenCoreIcon");
        var glow = Get<CanvasGroup>(intro, "coreGlow");
        var bubble = Get<RectTransform>(intro, "speechBubble");
        var direction = Get<CanvasGroup>(intro, "directionHint");
        var speech = Get<Text>(intro, "speech");
        var idle = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/PlayerIdle.anim");
        var idleTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Graphics/Characters/Player/PlayerIdle.png");
        var results = new List<string>();

        Assert.That(intro.IsPlaying, Is.True);
        Assert.That(player.input.Player.enabled, Is.False);
        Assert.That(player.CanMove || player.CanStartAction, Is.False);
        Assert.That(player.GetComponent<PlayerMovement>().enabled, Is.False);
        Assert.That(UnityEngine.Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include).enabled, Is.False);
        foreach (PlayerHealthBar hp in UnityEngine.Object.FindObjectsByType<PlayerHealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Assert.That(hp.gameObject.activeInHierarchy, Is.False);
        foreach (PlayerAbilityHUD ability in UnityEngine.Object.FindObjectsByType<PlayerAbilityHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Assert.That(ability.gameObject.activeInHierarchy, Is.False);
        var merchant = Get<SpriteRenderer>(intro, "merchant");
        Assert.That(merchant.transform.position.x, Is.EqualTo(-4f));
        Assert.That(merchant.flipX, Is.False);
        bool heardFootstep = false;
        bool capturedWalking = false;
        await Until(() =>
        {
            var locomotion = player.transform.Find("Player Sound Locomotion").GetComponent<AudioSource>();
            heardFootstep |= locomotion.isPlaying && locomotion.clip != null && locomotion.clip.name == "PlayerWalk";
            if (!capturedWalking && player.motor.Velocity.x > 0.05f)
            {
                ScreenCapture.CaptureScreenshot("Temp/LowerTownWalking_Play.png");
                capturedWalking = true;
            }
            return core.enabled && Array.Exists(animator.GetCurrentAnimatorClipInfo(0), info => info.clip == idle);
        },
            "Core bubble and PlayerIdle");
        Assert.That(heardFootstep, Is.True, "Walk frames must play the assigned footstep audio.");
        Assert.That(bubble.gameObject.activeInHierarchy && !core.canvasRenderer.cull, Is.True);
        Assert.That(Get<Transform>(intro, "speaker"), Is.SameAs(Get<Transform>(intro, "playerHead")));
        Assert.That(core.texture, Is.SameAs(UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Graphics/Characters/Boss2/Boss2DeadCore.png")));
        Assert.That(renderer.sprite, Is.Not.Null);
        Assert.That(renderer.sprite.texture, Is.SameAs(idleTexture));
        Assert.That(player.motor.Velocity.x, Is.EqualTo(0f).Within(0.001f));
        ScreenCapture.CaptureScreenshot("Temp/LowerTownCoreLit_Play.png");
        bool sawDimCore = false;
        bool sawLightReturn = false;
        await Until(() =>
        {
            sawDimCore |= core.color.r < 0.5f;
            sawLightReturn |= sawDimCore && core.color.r > 0.9f;
            return brokenCore.activeInHierarchy;
        }, "Core flickers and breaks before the question");
        Assert.That(sawDimCore && sawLightReturn, Is.True);
        Assert.That(core.enabled, Is.False);
        Assert.That(brokenCore.activeInHierarchy, Is.True);
        Assert.That(speech.text, Is.Empty);
        Assert.That(glow.gameObject.activeSelf, Is.False);
        foreach (var shard in brokenCore.GetComponentsInChildren<RawImage>())
            Assert.That(shard.texture == core.texture && !shard.canvasRenderer.cull, Is.True);
        ScreenCapture.CaptureScreenshot("Temp/LowerTownCoreBroken_Play.png");
        float brokenAt = Time.time;
        await Until(() => !bubble.gameObject.activeSelf, "Broken core is hidden before the new question");
        Assert.That(Time.time - brokenAt, Is.GreaterThanOrEqualTo(0.9f));
        float hiddenAt = Time.time;
        await Until(() => speech.text == "?", "The player asks for a healthy core separately");
        Assert.That(Time.time - hiddenAt, Is.GreaterThanOrEqualTo(0.28f));
        Assert.That(core.enabled, Is.True);
        Assert.That(core.color, Is.EqualTo(Color.white));
        Assert.That(brokenCore.activeSelf, Is.False);
        Assert.That(glow.gameObject.activeInHierarchy, Is.True);
        await Until(() => glow.alpha > 0.85f, "Healthy core glow becomes visible");
        Assert.That(glow.transform.localScale.x, Is.GreaterThan(0.85f));
        ScreenCapture.CaptureScreenshot("Temp/LowerTownCoreQuestion_Play.png");
        float talkX = player.transform.position.x;
        Assert.That(talkX, Is.EqualTo(Get<Transform>(intro, "conversationPoint").position.x).Within(0.04f));
        var keyboard = Keyboard.current;
        Assert.That(keyboard, Is.Not.Null);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow, Key.C));
        await Awaitable.NextFrameAsync();
        await Awaitable.NextFrameAsync();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        Assert.That(player.transform.position.x, Is.EqualTo(talkX).Within(0.001f));
        results.Add("The broken core is shown first, then a separate question presents the original healthy core with a glow.");

        await Until(() => speech.text == "!", "Merchant reaction");
        Assert.That(merchant.flipX, Is.False, "The reaction must still face the player.");
        Assert.That(direction.gameObject.activeSelf, Is.False);
        Assert.That(core.enabled, Is.False);
        Assert.That(brokenCore.activeSelf, Is.False);
        Assert.That(glow.gameObject.activeSelf, Is.False);
        Assert.That(bubble.gameObject.activeInHierarchy, Is.True);
        Assert.That(Get<Transform>(intro, "speaker"), Is.SameAs(Get<Transform>(intro, "merchantHead")));
        Assert.That(Array.Exists(animator.GetCurrentAnimatorClipInfo(0), info => info.clip == idle), Is.True);
        Assert.That(renderer.sprite, Is.Not.Null);
        Assert.That(renderer.sprite.texture, Is.SameAs(idleTexture));
        float reactionAt = Time.time;
        await Until(() => Time.time >= reactionAt + 0.25f, "Merchant reaction pop");
        ScreenCapture.CaptureScreenshot("Temp/LowerTownMerchantReaction_Play.png");
        await Until(() => !bubble.gameObject.activeSelf, "Merchant finishes the guidance bubble");
        await Until(() => direction.gameObject.activeInHierarchy && direction.alpha > 0.95f, "Direction cue after reaction");
        Assert.That(merchant.flipX, Is.True);
        Assert.That(bubble.gameObject.activeSelf, Is.False);
        Assert.That(player.motor.Velocity.x, Is.EqualTo(0f).Within(0.001f));
        ScreenCapture.CaptureScreenshot("Temp/LowerTownDirectionCue_Play.png");
        await Until(() => !direction.gameObject.activeSelf, "Direction cue finishes before departure");
        Assert.That(Get<Transform>(intro, "cameraAnchor").position.x,
            Is.EqualTo(Get<Transform>(intro, "conversationShot").position.x).Within(0.03f));
        results.Add("The merchant reacts in a bubble, then looks toward the exit before the player leaves.");

        await Until(() => SceneTransitionOverlay.IsTransitioning, "Player exits the merchant area");
        Assert.That(Get<BoxCollider2D>(intro, "merchantArea").bounds.Intersects(player.GetComponent<Collider2D>().bounds), Is.False);
        await Ready(Respawn);
        AssertGameplayRestored();
        results.Add("The shortened conversation and exit complete, then gameplay resumes in the hub.");
        return results;
    }

    [UnityTest]
    public IEnumerator OpeningAndPortalPlayPrologueAcrossTheSceneCut()
    {
        yield return new EnterPlayMode();
        Task<List<string>> checks = VerifyRuntime();
        while (!checks.IsCompleted) yield return null;
        checks.GetAwaiter().GetResult();
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    public static Task<List<string>> VerifyRuntime() => VerifyWithoutRecording(VerifyOpeningRuntime);

    private static async Task<List<string>> VerifyWithoutRecording(Func<Task<List<string>>> verify)
    {
        PlayerProgressData original = PlayerPermanentProgress.CreateSnapshot();
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        FieldInfo saving = typeof(PlayerProgressPersistence).GetField("canSave", flags);
        bool originalSaving = (bool)saving.GetValue(null);
        PropertyInfo autoSkip = typeof(GameSettings).GetProperty(nameof(GameSettings.AutoSkipWatchedCutscenes));
        bool originalAutoSkip = GameSettings.AutoSkipWatchedCutscenes;
        saving.SetValue(null, false);
        autoSkip.SetValue(null, false);
        try { return await verify(); }
        finally
        {
            typeof(PlayerPermanentProgress).GetMethod("Restore", flags).Invoke(null, new object[] { original });
            saving.SetValue(null, originalSaving);
            autoSkip.SetValue(null, originalAutoSkip);
        }
    }

    private static async Task<List<string>> VerifyOpeningRuntime()
    {
        var results = new List<string>();
        await Until(() => !SceneTransitionOverlay.IsTransitioning && !GameplayPause.IsActive, "Initial transition");
        await StartFromTitle();
        var video = UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>();
        await Until(() => video.GetComponent<VideoPlayer>() != null && video.GetComponent<VideoPlayer>().isPlaying,
            "Prepared Prologue");
        var source = video.GetComponent<VideoPlayer>();
        Assert.That(source.clip.frameCount, Is.EqualTo(286ul));
        Assert.That(source.clip.frameRate, Is.EqualTo(30d));
        Assert.That(Get<float>(video, "playbackLimit"), Is.EqualTo(PrologueStopTime));
        Assert.That(AudioListener.pause, Is.False, "Prologue must keep title music running.");
        var music = GameObject.Find("Game Music").GetComponentsInChildren<AudioSource>();
        AudioSource playingMusic = Array.Find(music, x => x.isPlaying && x.clip != null);
        Assert.That(playingMusic, Is.Not.Null);
        int firstSample = playingMusic.timeSamples;
        float listenUntil = Time.realtimeSinceStartup + 0.2f;
        while (Time.realtimeSinceStartup < listenUntil) await Awaitable.NextFrameAsync();
        Assert.That(playingMusic.timeSamples, Is.GreaterThan(firstSample));
        await Until(() => Get<bool>(video, "finishing"), "Prologue playback limit");
        Assert.That(source.time, Is.InRange((double)PrologueStopTime, source.clip.length));
        Assert.That(source.isPlaying, Is.False, "The video freezes before fading out.");
        results.Add("Prologue stops on the final frame before the scene cut while title music advances.");
        await Ready(LowerTown);
        results.AddRange(await ObserveConversationRuntime());
        Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.Prologue), Is.True);
        Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.LowerTownIntro), Is.True);

        await StartFromTitle();
        video = UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>();
        Get<Button>(video, "skipButton").onClick.Invoke();
        await Ready(Respawn);
        AssertGameplayRestored();
        results.Add("Skipping Prologue before preparation goes directly to Respawn.");

        LowerTownIntro.RequestPlayback();
        Assert.That(SceneTransitionOverlay.TryLoadScene(LowerTown, 0f, 0.05f, Color.black), Is.True);
        await Ready(LowerTown);
        var intro = UnityEngine.Object.FindFirstObjectByType<LowerTownIntro>();
        Assert.That(intro.IsPlaying, Is.True);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Escape));
        await Awaitable.NextFrameAsync();
        await Awaitable.NextFrameAsync();
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        await Ready(Respawn);
        AssertGameplayRestored();
        results.Add("Escape during the LowerTown cutscene skips directly to Respawn without opening Pause.");

        var portal = UnityEngine.Object.FindFirstObjectByType<MapPortal>();
        Assert.That(portal.CanInteract, Is.True);
        portal.Interact(UnityEngine.Object.FindFirstObjectByType<Player>());
        video = UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>();
        Assert.That(video, Is.Not.Null);
        await Until(() => video.GetComponent<VideoPlayer>()?.isPlaying == true, "Prepared prologue continuation");
        source = video.GetComponent<VideoPlayer>();
        Assert.That(source.clip.name, Is.EqualTo("Prologue_Continuation"));
        Assert.That(source.clip.frameCount, Is.EqualTo(150ul));
        Assert.That(source.clip.frameRate, Is.EqualTo(30d));
        await Until(() => source.time >= 4.5d, "Prologue continuation plays through the portal");
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Respawn));
        await Ready(PortalDestination);
        results.Add("The Respawn portal plays the rest of the prologue before loading X_Map.");
        return results;
    }

    private static void AssertGameplayRestored()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        Assert.That(player.input.Player.enabled, Is.True);
        Assert.That(player.GetComponent<PlayerMovement>().enabled, Is.True);
        Assert.That(UnityEngine.Object.FindFirstObjectByType<PlayerHealthBar>().gameObject.activeInHierarchy, Is.True);
        Assert.That(UnityEngine.Object.FindFirstObjectByType<PlayerAbilityHUD>().gameObject.activeInHierarchy, Is.True);
        Assert.That(UnityEngine.Object.FindFirstObjectByType<LowerTownIntro>(), Is.Null);
    }
}

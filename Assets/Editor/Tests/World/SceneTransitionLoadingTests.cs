using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;

public sealed class SceneTransitionLoadingTests
{
    private const string Title = "Assets/Scenes/Title.unity";
    private const string Respawn = "Assets/Scenes/Respawn_Map.unity";

    private static T Get<T>(object target, string field) => (T)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void Skip(VideoCutsceneOverlay video) => typeof(VideoCutsceneOverlay)
        .GetMethod("Skip", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(video, null);

    private static async Task Until(Func<bool> condition, string message)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!condition())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), message);
            await Awaitable.NextFrameAsync();
        }
    }

    private static async Task SceneReady(string path)
    {
        await Until(() => SceneManager.GetActiveScene().path == path &&
            !SceneTransitionOverlay.IsTransitioning && !VideoCutsceneOverlay.IsPlaying, "Scene ready: " + path);
        Assert.That(GameplayPause.IsActive, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(AudioListener.pause, Is.False);
    }

    [Test]
    public void LoadingIndicatorHasItsExistingArtAndFontReferences()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<SceneLoadingIndicator>(
            "Assets/Resources/UI/SceneLoadingIndicator.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.IsConfigured, Is.True);
        var image = prefab.GetComponentInChildren<UnityEngine.UI.Image>(true);
        var label = prefab.GetComponentInChildren<UnityEngine.UI.Text>(true);
        Assert.That(image.sprite, Is.Not.Null);
        Assert.That(label.font, Is.Not.Null);
        Assert.That(label.text, Is.EqualTo("불러오는 중..."));
        Assert.That(label.raycastTarget, Is.False);
    }

    [UnityTest]
    public IEnumerator AsyncSceneAndVideoTransitionsKeepTheirLoadingAndPauseLifecycle()
    {
        yield return new EnterPlayMode();
        Task<List<object>> checks = VerifyRuntime(null);
        while (!checks.IsCompleted) yield return null;
        checks.GetAwaiter().GetResult();
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    // Also callable through Unity MCP in an existing Play session without saving the user's open scene.
    public static async Task<List<object>> VerifyRuntime(string screenshotPath)
    {
        Assert.That(Application.isPlaying, Is.True);
        await Until(() => !SceneTransitionOverlay.IsTransitioning && !GameplayPause.IsActive,
            "Initial scene transition");
        PlayerRuntimeState.ResetRunProgress();
        var results = new List<object>();
        var heldCameras = new List<Camera>();
        void HoldFirstFrame(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != Title) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (!camera.enabled) continue;
                heldCameras.Add(camera);
                camera.enabled = false;
            }
        }

        SceneManager.sceneLoaded += HoldFirstFrame;
        try
        {
            Assert.That(SceneTransitionOverlay.TryLoadScene(Title, 0f, 0.05f, Color.black), Is.True);
            var overlay = UnityEngine.Object.FindFirstObjectByType<SceneTransitionOverlay>();
            var indicator = Get<SceneLoadingIndicator>(overlay, "loadingIndicator");
            Assert.That(indicator.gameObject.activeSelf, Is.False, "Short waits must not flash a loading hint.");
            await Until(() => indicator.gameObject.activeSelf, "Delayed loading hint while waiting for the first camera frame");
            Assert.That(GameplayPause.IsActive, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
            var gear = Get<RectTransform>(indicator, "gear");
            Quaternion initial = gear.localRotation;
            float rotateUntil = Time.realtimeSinceStartup + 0.08f;
            while (Time.realtimeSinceStartup < rotateUntil) await Awaitable.NextFrameAsync();
            Assert.That(Quaternion.Angle(initial, gear.localRotation), Is.GreaterThan(0.1f),
                "The loading gear must animate while gameplay time is paused.");
            if (!string.IsNullOrEmpty(screenshotPath))
            {
                ScreenCapture.CaptureScreenshot(screenshotPath);
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
            }
            results.Add(new { check = "Delayed hint, unscaled rotation, and loading pause", passed = true });
        }
        finally
        {
            SceneManager.sceneLoaded -= HoldFirstFrame;
            foreach (Camera camera in heldCameras) if (camera != null) camera.enabled = true;
        }
        await SceneReady(Title);

        var clip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Graphics/Video/Boss1Clear.mp4");
        Assert.That(clip, Is.Not.Null);
        int callbacks = 0;
        AudioSource fadeSource = null;
        float finalVideoVolume = -1f;
        Assert.That(SceneTransitionOverlay.TryLoadSceneAfterVideo(Respawn, clip,
            0.05f, 0.35f, 0.05f, Color.black, completed =>
            {
                Assert.That(completed, Is.True);
                finalVideoVolume = fadeSource != null ? fadeSource.volume : -1f;
                callbacks++;
            }), Is.True);
        var prepared = UnityEngine.Object.FindFirstObjectByType<SceneTransitionOverlay>();
        await Until(() => Get<AsyncOperation>(prepared, "loadingOperation") != null, "Video first frame starts preloading");
        var operation = Get<AsyncOperation>(prepared, "loadingOperation");
        Assert.That(operation.allowSceneActivation, Is.False);
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Title));
        Assert.That(GameplayPause.IsActive, Is.True);
        Assert.That(Get<Canvas>(prepared, "canvas").enabled, Is.False, "Preloading must not cover the video.");
        await Until(() => operation.progress >= 0.9f, "Destination preparation during video playback");
        Assert.That(operation.isDone, Is.False);
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Title));
        Assert.That(UnityEngine.Object.FindFirstObjectByType<Player>(), Is.Null,
            "The target map's player must not initialize during the video.");
        var video = UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>();
        var videoPlayer = Get<VideoPlayer>(video, "videoPlayer");
        fadeSource = videoPlayer.GetTargetAudioSource(0);
        fadeSource.volume = 0.6f;
        double playbackBeforeSkip = videoPlayer.time;
        Skip(video);
        Skip(video);
        Assert.That(videoPlayer.isPlaying, Is.True, "Playback continues while its picture and audio fade.");
        await Until(() => videoPlayer.time > playbackBeforeSkip + 0.03, "Playback advances during the skip fade");
        Assert.That(fadeSource.volume, Is.InRange(0.001f, 0.599f));
        Assert.That(Get<UnityEngine.UI.RawImage>(video, "videoImage").color.g, Is.InRange(0.001f, 0.999f));
        Assert.That(callbacks, Is.Zero, "The destination handoff waits for both fades.");
        await SceneReady(Respawn);
        Assert.That(finalVideoVolume, Is.Zero, "Video audio reaches silence before the destination handoff.");
        Assert.That(callbacks, Is.EqualTo(1));
        Assert.That(operation.isDone, Is.True);
        Assert.That(UnityEngine.Object.FindFirstObjectByType<Player>(), Is.Not.Null);
        results.Add(new { check = "Video preloads without activation; skip reuses that load", passed = true });

        callbacks = 0;
        Assert.That(SceneTransitionOverlay.TryLoadSceneAfterVideo(Title, clip,
            0f, 0.05f, 0.05f, Color.black, _ => callbacks++), Is.True);
        prepared = UnityEngine.Object.FindFirstObjectByType<SceneTransitionOverlay>();
        Assert.That(Get<AsyncOperation>(prepared, "loadingOperation"), Is.Null);
        Skip(UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>());
        await SceneReady(Title);
        Assert.That(callbacks, Is.EqualTo(1));
        results.Add(new { check = "Skipping before video preparation still loads the destination", passed = true });

        bool? interruption = null;
        Assert.That(SceneTransitionOverlay.TryLoadSceneAfterVideo(Respawn, clip,
            0f, 0f, 0.05f, Color.black, completed => interruption = completed), Is.True);
        prepared = UnityEngine.Object.FindFirstObjectByType<SceneTransitionOverlay>();
        await Until(() => Get<AsyncOperation>(prepared, "loadingOperation") != null, "Preload before video interruption");
        UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>().gameObject.SetActive(false);
        await SceneReady(Respawn);
        Assert.That(interruption, Is.False);
        results.Add(new { check = "Interrupted video releases its pause and queued scene load", passed = true });

        int first = 0;
        int second = 0;
        Assert.That(VideoCutsceneOverlay.TryPlay(clip, _ =>
        {
            first++;
            Assert.That(VideoCutsceneOverlay.TryPlay(clip, __ => second++), Is.True);
        }), Is.True);
        Skip(UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>());
        Assert.That(first, Is.EqualTo(1));
        Assert.That(VideoCutsceneOverlay.IsPlaying, Is.True);
        Skip(UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>());
        await Until(() => !VideoCutsceneOverlay.IsPlaying, "Chained videos finish");
        Assert.That(first, Is.EqualTo(1));
        Assert.That(second, Is.EqualTo(1));
        Assert.That(GameplayPause.IsActive, Is.False);
        Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(Respawn));
        results.Add(new { check = "Clear-to-credits callback chain stays within the same scene", passed = true });
        return results;
    }
}

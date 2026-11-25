using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class CutsceneAutoSkipTests
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private const string Title = "Assets/Scenes/Title.unity";
    private const string LowerTown = "Assets/Scenes/LowerTown_Map.unity";
    private const string Respawn = "Assets/Scenes/Respawn_Map.unity";

    private static void Restore(PlayerProgressData progress) => typeof(PlayerPermanentProgress)
        .GetMethod("Restore", PrivateStatic).Invoke(null, new object[] { progress });
    private static void SetAutoSkip(bool value) => typeof(GameSettings)
        .GetProperty(nameof(GameSettings.AutoSkipWatchedCutscenes)).SetValue(null, value);
    private static void Skip(VideoCutsceneOverlay video) => typeof(VideoCutsceneOverlay)
        .GetMethod("Skip", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(video, null);

    [Test]
    public void WatchedRolesRoundTripWithoutResettingOtherProgressOrBreakingOldSaves()
    {
        var original = PlayerPermanentProgress.CreateSnapshot();
        var saving = typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic);
        bool originalSaving = (bool)saving.GetValue(null);
        saving.SetValue(null, false);
        try
        {
            Restore(new PlayerProgressData { scrap = 37, attackPowerBonus = 3 });
            PlayerPermanentProgress.MarkCutsceneWatched(CutsceneId.Boss1Clear);
            PlayerPermanentProgress.MarkCutsceneWatched(CutsceneId.Boss1Clear);
            PlayerPermanentProgress.MarkCutsceneWatched(CutsceneId.None);
            PlayerPermanentProgress.MarkCutsceneWatched((CutsceneId)999);
            var saved = PlayerProgressStore.Deserialize(JsonUtility.ToJson(PlayerPermanentProgress.CreateSnapshot()));
            Assert.That(PlayerProgressStore.IsValid(saved), Is.True);
            Assert.That(saved.watchedCutscenes, Is.EquivalentTo(new[] { CutsceneId.Boss1Clear }));
            Restore(saved);
            Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.Boss1Clear), Is.True);
            Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.Prologue), Is.False);
            Assert.That(PlayerPermanentProgress.Scrap, Is.EqualTo(37));
            Assert.That(PlayerPermanentProgress.AttackPowerBonus, Is.EqualTo(3));
            foreach (int version in new[] { 1, 2 })
            {
                var old = PlayerProgressStore.Deserialize("{\"version\":" + version + ",\"scrap\":12}");
                Assert.That(PlayerProgressStore.IsValid(old), Is.True);
                Restore(old);
                Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.Boss1Clear), Is.False);
            }
            Assert.That(PlayerProgressStore.IsValid(new PlayerProgressData { watchedCutscenes = new[] { (CutsceneId)999 } }), Is.False);
        }
        finally { Restore(original); saving.SetValue(null, originalSaving); }
    }

    [Test]
    public void ExistingSettingsPrefabAndAllStoryVideosHaveTheirBindings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameSettingsPanel.prefab");
        var field = new SerializedObject(settings.GetComponent<GameSettingsPanel>()).FindProperty("autoSkipWatchedCutscenes");
        var toggle = field.objectReferenceValue as Toggle;
        Assert.That(toggle, Is.Not.Null);
        Assert.That(toggle.GetComponentInChildren<Text>().text, Is.EqualTo("본 컷신 자동 스킵"));
        Assert.That(toggle.transform.parent.name, Is.EqualTo("Gameplay"));

        var roles = new List<CutsceneId>();
        foreach (string path in new[] { "X_Map", "Y_Map", "UpperClass_Map", "Boss1_Map", "Boss2_Map" })
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + path + ".unity");
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (var portal in root.GetComponentsInChildren<MapPortal>(true))
                    {
                        var data = new SerializedObject(portal);
                        if (data.FindProperty("transitionVideo").objectReferenceValue != null)
                            roles.Add((CutsceneId)data.FindProperty("transitionCutscene").intValue);
                    }
                    foreach (var video in root.GetComponentsInChildren<VideoCutscene>(true))
                        roles.Add((CutsceneId)new SerializedObject(video).FindProperty("cutsceneId").intValue);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        Assert.That(roles, Is.EquivalentTo(new[] { CutsceneId.Boss1Entrance, CutsceneId.UpperClassEntrance,
            CutsceneId.Boss2Entrance, CutsceneId.Boss1Clear, CutsceneId.Boss2Clear, CutsceneId.EndingCredits }));
    }

    [UnityTest]
    public IEnumerator PlaybackHistorySettingsAndSceneHandoffsKeepTheirCompletionRules()
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

    public static async Task<List<string>> VerifyRuntime()
    {
        Assert.That(Application.isPlaying, Is.True);
        await Ready(SceneManager.GetActiveScene().path);
        var original = PlayerPermanentProgress.CreateSnapshot();
        var saving = typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic);
        bool originalSaving = (bool)saving.GetValue(null);
        bool originalAutoSkip = GameSettings.AutoSkipWatchedCutscenes;
        saving.SetValue(null, false);
        SetAutoSkip(true);
        var results = new List<string>();
        try
        {
            Restore(new PlayerProgressData());
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Graphics/Video/Boss1Clear.mp4");
            int completed = 0;
            Assert.That(VideoCutsceneOverlay.TryPlay(clip, done => { Assert.That(done, Is.True); completed++; },
                playbackLimit: 0.1f, cutsceneId: CutsceneId.Boss1Clear), Is.True);
            Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.Boss1Clear), Is.False);
            await Until(() => !VideoCutsceneOverlay.IsPlaying, "Normal playback reaches its configured end");
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.Boss1Clear), Is.True);
            Assert.That(GameplayPause.IsActive, Is.False);

            bool skippedCallback = false;
            int chained = 0;
            PlayerPermanentProgress.MarkCutsceneWatched(CutsceneId.EndingCredits);
            Assert.That(VideoCutsceneOverlay.TryPlay(clip, done =>
            {
                Assert.That(done, Is.True);
                VideoCutsceneOverlay.TryPlay(clip, next => { Assert.That(next, Is.True); chained++; },
                    cutsceneId: CutsceneId.EndingCredits);
            }, onSkipped: () => skippedCallback = true, cutsceneId: CutsceneId.Boss1Clear), Is.True);
            Assert.That(chained, Is.EqualTo(1));
            Assert.That(skippedCallback, Is.False, "Automatic skips must use the normal story continuation.");
            Assert.That(VideoCutsceneOverlay.IsPlaying, Is.False);
            Assert.That(GameplayPause.IsActive, Is.False);
            results.Add("Normal completion records history; watched clips chain without video or pause.");

            Assert.That(VideoCutsceneOverlay.TryPlay(clip, cutsceneId: CutsceneId.Boss2Clear), Is.True);
            Skip(UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>());
            await Until(() => !VideoCutsceneOverlay.IsPlaying, "Manual skip finishes");
            Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.Boss2Clear), Is.False);

            bool? interrupted = null;
            Assert.That(VideoCutsceneOverlay.TryPlay(clip, done => interrupted = done,
                cutsceneId: CutsceneId.Boss2Clear), Is.True);
            UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>().gameObject.SetActive(false);
            Assert.That(interrupted, Is.False);
            Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.Boss2Clear), Is.False);
            Assert.That(GameplayPause.IsActive, Is.False);
            results.Add("Manual skips and interruptions do not mark unseen clips as watched.");

            completed = 0;
            Assert.That(SceneTransitionOverlay.TryLoadSceneAfterVideo(Respawn, clip, 0f, 0f, 0.03f, Color.black,
                done => { Assert.That(done, Is.True); completed++; }, CutsceneId.Boss1Clear), Is.True);
            await Ready(Respawn);
            Assert.That(completed, Is.EqualTo(1));
            results.Add("Skipping a watched portal video still loads the destination exactly once.");

            PlayerPermanentProgress.MarkCutsceneWatched(CutsceneId.Prologue);
            await StartFromTitle();
            await Until(() => SceneManager.GetActiveScene().path == LowerTown
                && !SceneTransitionOverlay.IsTransitioning, "Unseen in-game opening");
            Assert.That(VideoCutsceneOverlay.IsPlaying, Is.False);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<LowerTownIntro>().IsPlaying, Is.True);
            UnityEngine.Object.FindFirstObjectByType<LowerTownIntro>().Skip();
            await Ready(Respawn);
            Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.LowerTownIntro), Is.False);

            PlayerPermanentProgress.MarkCutsceneWatched(CutsceneId.LowerTownIntro);
            bool enteredLowerTown = false;
            void Observe(Scene scene, LoadSceneMode mode) { if (scene.path == LowerTown) enteredLowerTown = true; }
            SceneManager.sceneLoaded += Observe;
            try { await StartFromTitle(); await Ready(Respawn); }
            finally { SceneManager.sceneLoaded -= Observe; }
            Assert.That(enteredLowerTown, Is.False);
            results.Add("Watched Prologue preserves an unseen intro; both watched go straight to the hub.");

            SetAutoSkip(false);
            Assert.That(VideoCutsceneOverlay.TryPlay(clip, cutsceneId: CutsceneId.Boss1Clear), Is.True);
            Assert.That(VideoCutsceneOverlay.IsPlaying, Is.True);
            Skip(UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>());
            await Until(() => !VideoCutsceneOverlay.IsPlaying, "Replay with automatic skip disabled");
            Assert.That(PlayerPermanentProgress.HasWatchedCutscene(CutsceneId.Boss1Clear), Is.True);
            Assert.That(LowerTownIntro.ShouldAutoSkip, Is.False);
            results.Add("The global switch replays watched clips and the in-game opening without deleting history.");
            return results;
        }
        finally
        {
            var active = UnityEngine.Object.FindFirstObjectByType<VideoCutsceneOverlay>();
            if (active != null) active.gameObject.SetActive(false);
            Restore(original);
            saving.SetValue(null, originalSaving);
            SetAutoSkip(originalAutoSkip);
        }
    }

    private static async Task StartFromTitle()
    {
        Assert.That(SceneTransitionOverlay.TryLoadScene(Title, 0f, 0.03f, Color.black), Is.True);
        await Ready(Title);
        var title = UnityEngine.Object.FindFirstObjectByType<TitleMenu>();
        var presentation = (MenuPanelTransition)typeof(TitleMenu)
            .GetField("mainPresentation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(title);
        await Until(() => !presentation.IsTransitioning, "Title entrance");
        title.StartGame();
    }

    private static async Task Ready(string path)
    {
        await Until(() => SceneManager.GetActiveScene().path == path && !SceneTransitionOverlay.IsTransitioning
            && !VideoCutsceneOverlay.IsPlaying, "Scene ready: " + path);
        Assert.That(GameplayPause.IsActive, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(AudioListener.pause, Is.False);
    }

    private static async Task Until(Func<bool> condition, string message)
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (!condition())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), message);
            await Awaitable.NextFrameAsync();
        }
    }
}

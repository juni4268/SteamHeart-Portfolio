using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class VideoCutsceneOverlay : MonoBehaviour
{
    private const float SkipHintVisibleDuration = 3f;
    private const float SkipHintFadeDuration = 0.2f;

    private static VideoCutsceneOverlay activeOverlay;

    private VideoPlayer videoPlayer;
    [Header("Prefab UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RawImage videoImage;
    [SerializeField] private AspectRatioFitter aspectRatio;
    [SerializeField] private EventSystem cutsceneEventSystem;
    [SerializeField] private Button skipButton;
    [SerializeField] private CanvasGroup skipHintGroup;
    [SerializeField] private Image skipUnderline;
    private Action<bool> onFinished;
    private Action onStarted;
    private Action onSkipped;
    private GameplayPause.Handle pause;
    private bool pauseAudio;
    private bool skipped;
    private float playbackLimit;
    private float fadeInDuration;
    private float fadeOutDuration;
    private float skipHintVisibleUntil;
    private bool finishing;
    private bool quitting;
    private CutsceneId cutsceneId;

    public static bool IsPlaying => activeOverlay != null;

    private void OnEnable()
    {
        LastInputDevice.Changed += RefreshSkipLabel;
        RefreshSkipLabel();
    }

    private void RefreshSkipLabel()
    {
        Text label = skipButton != null ? skipButton.GetComponentInChildren<Text>(true) : null;
        if (label == null) return;
        string key = LastInputDevice.IsGamepad
            ? PlayerKeyBindings.GamepadDisplayName(PlayerKeyBindings.GamepadCancelPath) : "ESC";
        label.text = $"건너뛰기 [{key}]";
    }

    // Skipping uses the normal completion callback unless a separate destination is supplied.
    public static bool TryPlay(VideoClip clip, Action<bool> onFinished = null,
        float fadeInDuration = 0f, float fadeOutDuration = 0f, Action onStarted = null,
        float playbackLimit = 0f, bool pauseAudio = true, Action onSkipped = null,
        CutsceneId cutsceneId = CutsceneId.None)
    {
        if (!Application.isPlaying || clip == null || IsPlaying || SceneTransitionOverlay.IsTransitioning)
            return false;

        if (GameSettings.AutoSkipWatchedCutscenes && PlayerPermanentProgress.HasWatchedCutscene(cutsceneId))
        {
            onFinished?.Invoke(true);
            return true;
        }

        VideoCutsceneOverlay prefab = Resources.Load<VideoCutsceneOverlay>("UI/VideoCutsceneOverlay");
        if (prefab == null || prefab.canvas == null || prefab.videoImage == null ||
            prefab.aspectRatio == null || prefab.cutsceneEventSystem == null || prefab.skipButton == null ||
            prefab.skipHintGroup == null || prefab.skipUnderline == null)
        {
            Debug.LogError("VideoCutsceneOverlay prefab or its UI references are missing.");
            return false;
        }

        VideoCutsceneOverlay overlay = Instantiate(prefab);
        overlay.canvas.enabled = false;
        overlay.cutsceneEventSystem.gameObject.SetActive(false);
        activeOverlay = overlay;
        overlay.onFinished = onFinished;
        overlay.onStarted = onStarted;
        overlay.onSkipped = onSkipped;
        overlay.playbackLimit = Mathf.Max(0f, playbackLimit);
        overlay.pauseAudio = pauseAudio;
        overlay.fadeInDuration = Mathf.Max(0f, fadeInDuration);
        overlay.fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        overlay.cutsceneId = cutsceneId;
        overlay.StartCoroutine(overlay.PlayClip(clip));
        return true;
    }

    private IEnumerator PlayClip(VideoClip clip)
    {
        // Wait for other screens; a temporary hit stop can hand its time scale over.
        while (Time.timeScale <= 0f && !HitStopManager.IsActive) yield return null;

        HitStopManager.PrepareForPause();
        while (!GameplayPause.TryAcquire(this, out pause)) yield return null;
        if (pauseAudio) pause.PauseAudio();
        pause.PauseAllPlayers();
        pause.PauseEventSystems();

        canvas.enabled = true;
        videoImage.enabled = false;
        videoImage.color = fadeInDuration > 0f ? Color.black : Color.white;
        ShowSkipHint();
        skipButton.onClick.AddListener(Skip);
        cutsceneEventSystem.gameObject.SetActive(true);
        cutsceneEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = clip;
        videoPlayer.renderMode = VideoRenderMode.APIOnly;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.controlledAudioTrackCount = clip.audioTrackCount;
        for (ushort track = 0; track < clip.audioTrackCount; track++)
        {
            AudioSource audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
            audioSource.outputAudioMixerGroup = GameAudioSettings.Shared.GetGroup(GameAudioChannel.Effects);
            videoPlayer.EnableAudioTrack(track, true);
            videoPlayer.SetTargetAudioSource(track, audioSource);
        }

        videoPlayer.prepareCompleted += HandlePrepared;
        videoPlayer.loopPointReached += HandleCompleted;
        videoPlayer.errorReceived += HandleError;
        videoPlayer.Prepare();

        float prepareDeadline = Time.realtimeSinceStartup + 15f;
        while (activeOverlay == this && !videoPlayer.isPrepared)
        {
            if (Time.realtimeSinceStartup >= prepareDeadline)
            {
                HandleError(videoPlayer, "Video preparation timed out.");
                yield break;
            }
            yield return null;
        }
    }

    private void Skip()
    {
        if (finishing || activeOverlay != this) return;
        skipped = true;
        BeginFinish();
    }

    private void HandlePrepared(VideoPlayer source)
    {
        if (finishing || activeOverlay != this) return;
        aspectRatio.aspectRatio = source.height > 0 ? (float)source.width / source.height : 1f;
        source.Play();
        ShowSkipHint();
        if (fadeInDuration > 0f) StartCoroutine(FadeVideo(Color.white, fadeInDuration));
    }

    private void Update()
    {
        if (activeOverlay == this && canvas.enabled && !finishing)
        {
            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || PlayerKeyBindings.ControlForPath(PlayerKeyBindings.GamepadCancelPath)?.wasPressedThisFrame == true)
            {
                Skip();
                return;
            }

            UpdateSkipHint();
            if (videoPlayer != null && videoPlayer.isPlaying && playbackLimit > 0f
                && videoPlayer.time >= playbackLimit)
                BeginFinish();
        }

        if (videoPlayer == null || videoImage == null || videoPlayer.texture == null) return;
        videoImage.texture = videoPlayer.texture;
        videoImage.enabled = true;
        if (!finishing && videoPlayer.isPlaying)
        {
            Action started = onStarted;
            onStarted = null;
            started?.Invoke();
        }
    }

    private void ShowSkipHint()
    {
        skipHintVisibleUntil = Time.unscaledTime + SkipHintVisibleDuration;
        skipHintGroup.alpha = 1f;
        skipHintGroup.blocksRaycasts = true;
        skipUnderline.enabled = false;
    }

    private void UpdateSkipHint()
    {
        Mouse mouse = Mouse.current;
        bool hovering = mouse != null && RectTransformUtility.RectangleContainsScreenPoint(
            (RectTransform)skipButton.transform, mouse.position.ReadValue());

        if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0f || hovering))
            skipHintVisibleUntil = Time.unscaledTime + SkipHintVisibleDuration;

        float targetAlpha = Time.unscaledTime < skipHintVisibleUntil ? 1f : 0f;
        skipHintGroup.alpha = Mathf.MoveTowards(skipHintGroup.alpha, targetAlpha,
            Time.unscaledDeltaTime / SkipHintFadeDuration);
        skipHintGroup.blocksRaycasts = skipHintGroup.alpha > 0.01f;
        skipUnderline.enabled = hovering && skipHintGroup.blocksRaycasts;
    }

    private void HandleCompleted(VideoPlayer source) => BeginFinish();

    private void BeginFinish()
    {
        if (finishing || activeOverlay != this) return;
        finishing = true;
        if (videoPlayer != null && !skipped) videoPlayer.Pause();
        StopAllCoroutines();
        if (fadeOutDuration <= 0f) Finish(true);
        else StartCoroutine(FinishWithFade());
    }

    private IEnumerator FinishWithFade()
    {
        yield return FadeVideo(Color.black, fadeOutDuration, fadeAudio: skipped);
        Finish(true);
    }

    private IEnumerator FadeVideo(Color target, float duration, bool fadeAudio = false)
    {
        var audioSources = fadeAudio && videoPlayer != null
            ? new AudioSource[videoPlayer.controlledAudioTrackCount]
            : Array.Empty<AudioSource>();
        var startingVolumes = new float[audioSources.Length];
        for (int i = 0; i < audioSources.Length; i++)
        {
            audioSources[i] = videoPlayer.GetTargetAudioSource((ushort)i);
            if (audioSources[i] != null) startingVolumes[i] = audioSources[i].volume;
        }

        Color start = videoImage.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            videoImage.color = Color.Lerp(start, target, progress);
            for (int i = 0; i < audioSources.Length; i++)
                if (audioSources[i] != null) audioSources[i].volume = startingVolumes[i] * (1f - progress);
            yield return null;
        }
        videoImage.color = target;
        foreach (AudioSource source in audioSources)
            if (source != null) source.volume = 0f;
    }

    private void HandleError(VideoPlayer source, string message)
    {
        if (finishing || activeOverlay != this) return;
        Debug.LogError($"Video cutscene failed: {message}", this);
        Finish(false);
    }

    private void Finish(bool completed)
    {
        if (activeOverlay != this) return;
        if (completed && !skipped) PlayerPermanentProgress.MarkCutsceneWatched(cutsceneId);
        Action<bool> callback = onFinished;
        Action skipCallback = skipped ? onSkipped : null;
        onFinished = null;
        onSkipped = null;
        Release();
        gameObject.SetActive(false);
        Destroy(gameObject);
        if (skipCallback != null) skipCallback.Invoke();
        else callback?.Invoke(completed);
    }

    private void OnDisable()
    {
        LastInputDevice.Changed -= RefreshSkipLabel;
        Action<bool> callback = onFinished;
        onFinished = null;
        onStarted = null;
        onSkipped = null;
        Release();
        if (!quitting) callback?.Invoke(false);
    }

    private void OnApplicationQuit() => quitting = true;

    private void Release()
    {
        if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
        if (cutsceneEventSystem != null) cutsceneEventSystem.gameObject.SetActive(false);

        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= HandlePrepared;
            videoPlayer.loopPointReached -= HandleCompleted;
            videoPlayer.errorReceived -= HandleError;
            videoPlayer.Stop();
        }

        pause?.Dispose();
        pause = null;

        if (activeOverlay == this) activeOverlay = null;
    }
}

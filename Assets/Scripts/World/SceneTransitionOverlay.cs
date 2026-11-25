using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class SceneTransitionOverlay : MonoBehaviour
{
    private const int OverlaySortingOrder = short.MaxValue;
    private const float DefaultFadeInDuration = 0.35f;
    private const float LoadingHintDelay = 0.4f;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private SceneLoadingIndicator loadingIndicator;
    private Tween transitionTween;
    private AsyncOperation loadingOperation;
    private GameplayPause.Handle pause;
    private string destinationScenePath;
    private float fadeInDuration;
    private Scene destinationScene;
    private bool fadeInPending;
    private bool cameraReady;
    private float loadingStartedAt = -1f;
    private bool quitting;
    private bool pauseAudio = true;

    public static bool IsTransitioning { get; private set; }
    private static SceneTransitionOverlay activeOverlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        activeOverlay = null;
        IsTransitioning = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneTransitions()
    {
        SceneManager.sceneLoaded -= HandleSceneEntry;
        SceneManager.sceneLoaded += HandleSceneEntry;
    }

    private static void HandleSceneEntry(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying || mode != LoadSceneMode.Single) return;

        if (activeOverlay == null)
        {
            activeOverlay = CreateOverlay(Color.black);
            if (activeOverlay == null) return;
            activeOverlay.fadeInDuration = DefaultFadeInDuration;
            IsTransitioning = true;
        }

        activeOverlay.PrepareFadeIn(scene);
    }

    public static bool TryLoadScene(
        string scenePath,
        float fadeOutDuration,
        float fadeInDuration,
        Color fadeColor,
        bool pauseAudio = true)
    {
        if (IsTransitioning || activeOverlay != null || !CanLoadScene(scenePath)) return false;

        SceneTransitionOverlay overlay = CreateOverlay(fadeColor);
        if (overlay == null) return false;
        activeOverlay = overlay;
        overlay.pauseAudio = pauseAudio;
        overlay.Begin(scenePath, fadeOutDuration, fadeInDuration);
        return true;
    }

    public static bool TryLoadSceneAfterVideo(string scenePath, VideoClip clip,
        float videoFadeInDuration, float videoFadeOutDuration, float fadeInDuration, Color fadeColor,
        Action<bool> onVideoFinished = null, CutsceneId cutsceneId = CutsceneId.None)
    {
        if (IsTransitioning || activeOverlay != null || VideoCutsceneOverlay.IsPlaying || !CanLoadScene(scenePath))
            return false;

        SceneTransitionOverlay overlay = CreateOverlay(fadeColor);
        if (overlay == null) return false;
        overlay.destinationScenePath = scenePath;
        activeOverlay = overlay;
        bool started = VideoCutsceneOverlay.TryPlay(clip, completed =>
        {
            if (overlay == null) return;
            // The video's pause is released before this handoff. Keep the prepared scene dormant until now.
            overlay.Begin(scenePath, 0f, fadeInDuration);
            onVideoFinished?.Invoke(completed);
        }, videoFadeInDuration, videoFadeOutDuration, overlay.PreloadDestinationScene, cutsceneId: cutsceneId);
        if (started) return true;

        overlay.FinishTransition();
        return false;
    }

    private static bool CanLoadScene(string scenePath)
    {
        if (!string.IsNullOrWhiteSpace(scenePath) && Application.CanStreamedLevelBeLoaded(scenePath)) return true;
        Debug.LogError($"Cannot load scene '{scenePath}'. Add it to Build Settings first.");
        return false;
    }

    private static SceneTransitionOverlay CreateOverlay(Color fadeColor)
    {
        GameObject overlayObject = new GameObject(
            nameof(SceneTransitionOverlay),
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasGroup),
            typeof(SceneTransitionOverlay));

        DontDestroyOnLoad(overlayObject);

        Canvas canvas = overlayObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;
        canvas.enabled = false;
        var scaler = overlayObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480f, 270f);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup canvasGroup = overlayObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;

        GameObject imageObject = new GameObject(
            "Fade",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform imageTransform = imageObject.GetComponent<RectTransform>();
        imageTransform.SetParent(overlayObject.transform, false);
        imageTransform.anchorMin = Vector2.zero;
        imageTransform.anchorMax = Vector2.one;
        imageTransform.offsetMin = Vector2.zero;
        imageTransform.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = fadeColor;
        image.raycastTarget = true;

        SceneTransitionOverlay overlay = overlayObject.GetComponent<SceneTransitionOverlay>();
        var indicatorPrefab = Resources.Load<SceneLoadingIndicator>("UI/SceneLoadingIndicator");
        if (indicatorPrefab == null || !indicatorPrefab.IsConfigured)
        {
            Debug.LogError("SceneLoadingIndicator prefab or its gear reference is missing.");
            Destroy(overlayObject);
            return null;
        }
        overlay.loadingIndicator = Instantiate(indicatorPrefab, overlayObject.transform, false);
        overlay.loadingIndicator.SetVisible(false);
        overlay.canvas = canvas;
        overlay.canvasGroup = canvasGroup;
        return overlay;
    }

    private void Begin(string scenePath, float fadeOut, float fadeIn)
    {
        IsTransitioning = true;
        destinationScenePath = scenePath;
        fadeInDuration = Mathf.Max(0f, fadeIn);
        canvas.enabled = true;

        if (fadeOut <= 0f)
        {
            canvasGroup.alpha = 1f;
            LoadDestinationScene();
            return;
        }

        transitionTween = canvasGroup
            .DOFade(1f, fadeOut)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(LoadDestinationScene);
    }

    private void LoadDestinationScene()
    {
        transitionTween = null;
        StartCoroutine(LoadDestinationScene(activate: true));
    }

    private void PreloadDestinationScene() => StartCoroutine(LoadDestinationScene(activate: false));

    private IEnumerator LoadDestinationScene(bool activate)
    {
        if (loadingOperation == null && PlayerRuntimeState.IsInitialized && PlayerRuntimeState.CurrentHP <= 0)
        {
            FinishTransition();
            yield break;
        }

        if (activate)
        {
            loadingStartedAt = Time.realtimeSinceStartup;
            HitStopManager.PrepareForPause();
            while (!GameplayPause.TryAcquire(this, out pause)) yield return null;
            if (pauseAudio) pause.PauseAudio();
            pause.PauseAllPlayers();
            pause.PauseEventSystems();
        }

        if (!StartLoadingOperation())
        {
            FinishTransition();
            yield break;
        }
        loadingOperation.allowSceneActivation = activate;
    }

    private bool StartLoadingOperation()
    {
        if (loadingOperation != null) return true;
        try
        {
            loadingOperation = SceneManager.LoadSceneAsync(destinationScenePath, LoadSceneMode.Single);
            loadingOperation.allowSceneActivation = false;
            return true;
        }
        catch (ArgumentException error)
        {
            Debug.LogError($"Scene transition failed: {error.Message}", this);
            return false;
        }
    }

    private void PrepareFadeIn(Scene scene)
    {
        transitionTween?.Kill();
        transitionTween = null;
        destinationScene = scene;
        canvas.enabled = true;
        canvasGroup.alpha = 1f;
        fadeInPending = true;
        cameraReady = false;
        RenderPipelineManager.endCameraRendering -= HandleCameraRendered;
        RenderPipelineManager.endCameraRendering += HandleCameraRendered;
    }

    private void HandleCameraRendered(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType != CameraType.Game || camera.targetTexture != null ||
            camera.gameObject.scene != destinationScene) return;

        // Keep this first frame covered; reveal the scene on the next Update.
        cameraReady = true;
        RenderPipelineManager.endCameraRendering -= HandleCameraRendered;
    }

    private void Update()
    {
        if (loadingStartedAt >= 0f && !cameraReady &&
            Time.realtimeSinceStartup - loadingStartedAt >= LoadingHintDelay)
            loadingIndicator.SetVisible(true);
        if (!fadeInPending || !cameraReady) return;
        fadeInPending = false;
        BeginFadeIn();
    }

    private void BeginFadeIn()
    {
        loadingStartedAt = -1f;
        loadingIndicator.SetVisible(false);
        pause?.Dispose();
        pause = null;
        if (fadeInDuration <= 0f)
        {
            FinishTransition();
            return;
        }

        transitionTween = canvasGroup
            .DOFade(0f, fadeInDuration)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(FinishTransition);
    }

    private void FinishTransition()
    {
        transitionTween = null;
        loadingIndicator.SetVisible(false);
        pause?.Dispose();
        pause = null;
        ReleaseTransition();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        RenderPipelineManager.endCameraRendering -= HandleCameraRendered;
        transitionTween?.Kill();
        transitionTween = null;
        pause?.Dispose();
        pause = null;
        // Unity cannot cancel a queued scene load; do not leave its activation barrier blocking other operations.
        if (!quitting && loadingOperation != null && !loadingOperation.isDone)
            loadingOperation.allowSceneActivation = true;
        ReleaseTransition();
    }

    private void OnApplicationQuit() => quitting = true;

    private void ReleaseTransition()
    {
        // A cancelled overlay may be destroyed after a new respawn fade has already started.
        if (activeOverlay != this) return;
        activeOverlay = null;
        IsTransitioning = false;
    }
}

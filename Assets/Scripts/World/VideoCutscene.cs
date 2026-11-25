using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

[DisallowMultipleComponent]
public sealed class VideoCutscene : MonoBehaviour
{
    [SerializeField] private VideoClip clip;
    [SerializeField] private CutsceneId cutsceneId;
    [SerializeField, Min(0f)] private float fadeInDuration;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
    [SerializeField] private SceneReference destinationScene = new SceneReference();
    [SerializeField] private UnityEvent onCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onFailed = new UnityEvent();

    // Connect this method to any Inspector UnityEvent, or call it from gameplay code.
    public void Play()
    {
        if (!isActiveAndEnabled || VideoCutsceneOverlay.IsPlaying || SceneTransitionOverlay.IsTransitioning) return;
        if (clip == null)
        {
            Debug.LogError($"{name} has no cutscene video assigned.", this);
            ContinueWithoutVideo();
            return;
        }

        bool started = destinationScene != null && destinationScene.IsAssigned
            ? SceneTransitionOverlay.TryLoadSceneAfterVideo(destinationScene.Path, clip,
                fadeInDuration, fadeOutDuration, fadeOutDuration, Color.black, NotifyFinished, cutsceneId)
            : VideoCutsceneOverlay.TryPlay(clip, NotifyFinished, fadeInDuration, fadeOutDuration, cutsceneId: cutsceneId);
        if (!started) ContinueWithoutVideo();
    }

    private void ContinueWithoutVideo()
    {
        if (this == null || !isActiveAndEnabled) return;
        // The video presents the transition; decoding failure must not block progression.
        if (destinationScene != null && destinationScene.IsAssigned)
            SceneTransitionOverlay.TryLoadScene(destinationScene.Path, 0f, fadeOutDuration, Color.black);
        NotifyFinished(false);
    }

    private void NotifyFinished(bool completed)
    {
        if (this == null || !isActiveAndEnabled) return;
        if (completed) onCompleted.Invoke();
        else onFailed.Invoke();
    }

#if UNITY_EDITOR
    private void OnValidate() => destinationScene?.Validate();
#endif
}

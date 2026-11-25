using UnityEngine;

// A persistent UI-only player keeps clicks audible through pause and scene transitions.
public sealed class UIAudio : MonoBehaviour
{
    public enum Cue { None, Move, Click, Back }

    private static UIAudio instance;
    private AudioSource moveSource;
    private AudioSource actionSource;
    private Cue pending;
    private float nextMoveTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance != null) return;
        var root = new GameObject("UI Audio");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<UIAudio>();
    }

    private void Awake()
    {
        moveSource = CreateSource();
        actionSource = CreateSource();
    }

    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.outputAudioMixerGroup = GameAudioSettings.Shared.GetGroup(GameAudioChannel.UI);
        return source;
    }

    public static void PlayMove() => Request(Cue.Move);
    public static void PlayClick() => Request(Cue.Click);
    public static void PlayBack() => Request(Cue.Back);

    private static void Request(Cue cue)
    {
        if (!Application.isPlaying) return;
        if (instance == null) Initialize();
        instance.Queue(cue);
    }

    private void Queue(Cue cue)
    {
        // Pointer selection and hover can report one move twice. Back overrides a button click.
        if (cue == Cue.Move && Time.unscaledTime < nextMoveTime) return;
        if (cue > pending) pending = cue;
    }

    private void LateUpdate()
    {
        if (pending == Cue.None) return;
        Cue cue = pending;
        pending = Cue.None;
        AudioClip clip = GameAudioSettings.Shared.GetUIClip(cue);
        if (clip == null) return;
        AudioSource source = cue == Cue.Move ? moveSource : actionSource;
        if (cue != Cue.Move) moveSource.Stop();
        source.Stop();
        source.clip = clip;
        source.Play();
        nextMoveTime = Time.unscaledTime + 0.06f;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}

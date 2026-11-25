using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class GameMusicPlayer : MonoBehaviour
{
    private const float FadeDuration = 1.5f;
    private readonly MusicBank[] banks = new MusicBank[2];
    private readonly float[] bankGains = new float[2];
    private readonly float[] fadeStartGains = new float[2];
    private BossHealth sceneBoss;
    private AudioClip requestedClip;
    private AudioMixerGroup requestedGroup;
    private int currentBank = -1;
    private int incomingBank = -1;
    private double transitionStart;
    private bool transitioning;
    private bool requestPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var root = new GameObject("Game Music");
        DontDestroyOnLoad(root);
        root.AddComponent<GameMusicPlayer>();
    }

    private void Awake()
    {
        banks[0] = new MusicBank(transform, "Music A");
        banks[1] = new MusicBank(transform, "Music B");
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

    private void Start() => SelectSceneMusic(SceneManager.GetActiveScene());

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        SelectSceneMusic(scene);
    }

    private void SelectSceneMusic(Scene scene)
    {
        UnbindBoss();
        AudioSource source = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SceneMusic cue in root.GetComponentsInChildren<SceneMusic>())
            {
                if (!cue.isActiveAndEnabled) continue;
                source = cue.GetComponent<AudioSource>();
                sceneBoss = cue.FadeOutOnDeath;
                break;
            }
            if (source != null) break;
        }

        // An unassigned scene fades to silence instead of inheriting the previous map's BGM.
        if (source != null) source.Stop();
        if (sceneBoss != null) sceneBoss.Died += HandleBossDied;
        requestedClip = source != null && (sceneBoss == null || !sceneBoss.IsDead) ? source.clip : null;
        requestedGroup = source != null ? source.outputAudioMixerGroup : null;
        int target = transitioning ? incomingBank : currentBank;
        requestPending = target < 0 ? requestedClip != null : !banks[target].Matches(requestedClip, requestedGroup);
        if (requestPending && !transitioning) BeginTransition();
    }

    private void BeginTransition()
    {
        requestPending = false;
        incomingBank = requestedClip != null ? (currentBank == 0 ? 1 : 0) : -1;
        transitionStart = AudioSettings.dspTime;
        if (incomingBank >= 0)
        {
            banks[incomingBank].Start(requestedClip, requestedGroup, transitionStart + 0.05);
            bankGains[incomingBank] = 0f;
        }
        for (int i = 0; i < banks.Length; i++) fadeStartGains[i] = bankGains[i];
        transitioning = true;
    }

    private void HandleBossDied()
    {
        requestedClip = null;
        requestedGroup = null;
        requestPending = false;
        incomingBank = -1;
        // Capture both banks so death during an entry fade or crossfade cannot raise the music again.
        for (int i = 0; i < banks.Length; i++) fadeStartGains[i] = bankGains[i];
        transitionStart = AudioSettings.dspTime;
        transitioning = true;
    }

    private void UnbindBoss()
    {
        if (sceneBoss != null) sceneBoss.Died -= HandleBossDied;
        sceneBoss = null;
    }

    private void Update()
    {
        double now = AudioSettings.dspTime;
        if (!transitioning)
        {
            if (currentBank >= 0) banks[currentBank].Tick(now, 1f);
            return;
        }

        float progress = Mathf.Clamp01((float)(now - transitionStart) / FadeDuration);
        for (int i = 0; i < banks.Length; i++)
        {
            bankGains[i] = Mathf.Lerp(fadeStartGains[i], i == incomingBank ? 1f : 0f, progress);
            banks[i].Tick(now, bankGains[i]);
        }
        if (progress < 1f) return;

        for (int i = 0; i < banks.Length; i++)
        {
            if (i == incomingBank) continue;
            banks[i].Stop();
            bankGains[i] = 0f;
        }
        currentBank = incomingBank;
        transitioning = false;
        // Finish the current fade before applying the latest request from rapid scene changes.
        if (requestPending) BeginTransition();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindBoss();
        for (int i = 0; i < banks.Length; i++)
        {
            banks[i]?.Stop();
            bankGains[i] = fadeStartGains[i] = 0f;
        }
        currentBank = incomingBank = -1;
        transitioning = requestPending = false;
    }

    private sealed class MusicBank
    {
        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly double[] starts = new double[2];
        private readonly double[] ends = new double[2];
        private readonly bool[] fadeStarts = new bool[2];
        private AudioClip clip;
        private AudioMixerGroup group;
        private float loopFade;
        private int nextLoop;

        public MusicBank(Transform parent, string name)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                var child = new GameObject(name + " " + (i + 1));
                child.transform.SetParent(parent, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                sources[i] = source;
            }
        }

        public bool Matches(AudioClip targetClip, AudioMixerGroup targetGroup) => clip == targetClip && group == targetGroup;

        public void Start(AudioClip track, AudioMixerGroup output, double start)
        {
            Stop();
            clip = track;
            group = output;
            loopFade = Mathf.Min(FadeDuration, clip.length * 0.5f);
            Schedule(0, start, false);
            Schedule(1, ends[0] - loopFade, true);
            nextLoop = 0;
        }

        private void Schedule(int index, double start, bool fadeIn)
        {
            AudioSource source = sources[index];
            source.Stop();
            source.clip = clip;
            source.outputAudioMixerGroup = group;
            source.volume = 0f;
            starts[index] = start;
            ends[index] = start + clip.samples / (double)clip.frequency;
            fadeStarts[index] = fadeIn;
            source.PlayScheduled(start);
        }

        public void Tick(double now, float gain)
        {
            if (clip == null) return;
            if (now >= ends[nextLoop])
            {
                double start = System.Math.Max(now + 0.05, ends[1 - nextLoop] - loopFade);
                Schedule(nextLoop, start, true);
                nextLoop = 1 - nextLoop;
            }

            for (int i = 0; i < sources.Length; i++)
            {
                float attack = fadeStarts[i] ? Mathf.Clamp01((float)(now - starts[i]) / loopFade) : 1f;
                float release = Mathf.Clamp01((float)(ends[i] - now) / loopFade);
                // Linear overlaps keep the combined playback gain at or below one.
                sources[i].volume = gain * Mathf.Min(attack, release);
            }
        }

        public void Stop()
        {
            foreach (AudioSource source in sources)
            {
                source.Stop();
                source.clip = null;
                source.volume = 0f;
            }
            clip = null;
            group = null;
        }
    }
}

using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public sealed class SceneAmbience : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup outputGroup;
    [SerializeField] private AudioClip loopClip;
    [SerializeField, Min(0.01f)] private float crossfadeDuration = 1.5f;
    [SerializeField] private AudioClip[] intermittentClips = new AudioClip[0];
    [Tooltip("단발 환경음이 끝난 뒤 다음 소리까지 기다릴 시간 범위입니다. 초 단위입니다.")]
    [SerializeField] private Vector2 intervalRange = new Vector2(12f, 24f);

    private readonly double[] loopStarts = new double[2];
    private readonly double[] loopEnds = new double[2];
    private AudioSource[] loopSources;
    private AudioSource intermittentSource;
    private double nextIntermittentTime;
    private double intermittentStart;
    private double intermittentEnd;
    private float loopFade;
    private int nextLoop;
    private int lastIntermittentIndex = -1;

    private void Awake()
    {
        if (loopClip != null)
            loopSources = new[] { CreateSource("Ambience Loop A"), CreateSource("Ambience Loop B") };
        if (intermittentClips.Length > 0)
            intermittentSource = CreateSource("Ambience Intermittent");
    }

    private AudioSource CreateSource(string sourceName)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = outputGroup;
        return source;
    }

    private void OnEnable()
    {
        double now = AudioSettings.dspTime;
        if (loopSources != null)
        {
            loopFade = Mathf.Min(crossfadeDuration, loopClip.length * 0.5f);
            ScheduleLoop(0, now + 0.05);
            ScheduleLoop(1, loopEnds[0] - loopFade);
            nextLoop = 0;
        }
        lastIntermittentIndex = -1;
        intermittentStart = intermittentEnd = now;
        nextIntermittentTime = now + NextInterval();
    }

    private void ScheduleLoop(int index, double start)
    {
        AudioSource source = loopSources[index];
        source.Stop();
        source.clip = loopClip;
        source.volume = 0f;
        loopStarts[index] = start;
        loopEnds[index] = start + loopClip.samples / (double)loopClip.frequency;
        source.PlayScheduled(start);
    }

    private void Update()
    {
        double now = AudioSettings.dspTime;
        if (loopSources != null)
        {
            if (now >= loopEnds[nextLoop])
            {
                ScheduleLoop(nextLoop, loopEnds[1 - nextLoop] - loopFade);
                nextLoop = 1 - nextLoop;
            }
            for (int i = 0; i < loopSources.Length; i++)
            {
                // Complementary linear fades avoid boosting the level at the loop boundary.
                float fadeIn = Mathf.Clamp01((float)(now - loopStarts[i]) / loopFade);
                float fadeOut = Mathf.Clamp01((float)(loopEnds[i] - now) / loopFade);
                loopSources[i].volume = Mathf.Min(fadeIn, fadeOut);
            }
        }

        if (intermittentSource == null) return;
        if (now >= nextIntermittentTime) PlayIntermittent(now);
        float attack = Mathf.Clamp01((float)(now - intermittentStart) / 0.02f);
        float release = Mathf.Clamp01((float)(intermittentEnd - now) / 0.1f);
        intermittentSource.volume = Mathf.Min(attack, release);
    }

    private void PlayIntermittent(double now)
    {
        int count = intermittentClips.Length;
        int index = count > 1 && lastIntermittentIndex >= 0 ? Random.Range(0, count - 1) : Random.Range(0, count);
        if (count > 1 && lastIntermittentIndex >= 0 && index >= lastIntermittentIndex) index++;
        lastIntermittentIndex = index;
        AudioClip clip = intermittentClips[index];
        if (clip == null)
        {
            nextIntermittentTime = now + NextInterval();
            return;
        }

        intermittentSource.Stop();
        intermittentSource.clip = clip;
        intermittentSource.volume = 0f;
        intermittentStart = now;
        intermittentEnd = now + clip.samples / (double)clip.frequency;
        intermittentSource.Play();
        nextIntermittentTime = intermittentEnd + NextInterval();
    }

    private float NextInterval() => Random.Range(intervalRange.x, intervalRange.y);

    private void OnDisable()
    {
        if (loopSources != null)
            foreach (AudioSource source in loopSources) source.Stop();
        if (intermittentSource != null) intermittentSource.Stop();
    }

    private void OnValidate()
    {
        crossfadeDuration = Mathf.Max(0.01f, crossfadeDuration);
        intervalRange.x = Mathf.Max(0.1f, intervalRange.x);
        intervalRange.y = Mathf.Max(intervalRange.x, intervalRange.y);
    }
}

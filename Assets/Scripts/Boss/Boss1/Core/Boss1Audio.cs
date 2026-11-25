using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public sealed class Boss1Audio : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup outputGroup;
    [Tooltip("경고 표시 중 반복할 AudioClip입니다. 반복 경계가 정리된 파일을 연결합니다.")]
    [SerializeField] private AudioClip warning;
    [SerializeField] private AudioClip slam;
    [Tooltip("Slam 파일에서 실제 타격음이 시작되는 위치입니다. 충돌 순간 이 위치부터 재생합니다.")]
    [SerializeField, Min(0f)] private float slamPlaybackOffset = 0.4f;
    [SerializeField] private AudioClip crushImpact;
    [SerializeField] private AudioClip punchImpact;
    [SerializeField] private AudioClip wallImpact;
    [SerializeField] private AudioClip propellerStart;
    [SerializeField] private AudioClip propellerLoop;
    [SerializeField] private AudioClip propellerEnd;
    [SerializeField] private AudioClip debrisLanding;
    [SerializeField] private AudioClip debrisDust;
    [SerializeField] private AudioClip turnOff;
    [SerializeField] private AudioClip turnOn;
    [SerializeField] private AudioClip deadDrop;
    [SerializeField] private AudioClip overdriveChargeStart;
    [SerializeField] private AudioClip overdriveChargeLoop;
    [SerializeField] private AudioClip overdriveRelease;

    private const float TransitionFade = 0.15f;
    private readonly System.Collections.Generic.List<Voice> voices = new System.Collections.Generic.List<Voice>();
    private Boss1Controller controller;
    private Voice warningVoice;
    private Voice[] slamVoices;
    private Voice[] landingVoices;
    private Voice[] dustVoices;
    private Voice[] machineryVoices;
    private Voice propellerStartVoice;
    private Voice propellerLoopVoice;
    private Voice propellerEndVoice;
    private Voice deathVoice;
    private Voice overdriveChargeStartVoice;
    private Voice overdriveChargeLoopVoice;
    private Voice overdriveReleaseVoice;
    private int slamIndex;
    private int landingIndex;
    private int dustIndex;
    private int machineryIndex;
    private double lastLandingTime = double.NegativeInfinity;
    private double lastDustTime = double.NegativeInfinity;
    private bool propellerActive;

    private void Awake()
    {
        controller = GetComponent<Boss1Controller>();
        warningVoice = CreateVoice("Warning");
        slamVoices = new[] { CreateVoice("Slam A"), CreateVoice("Slam B") };
        landingVoices = new[] { CreateVoice("Landing A"), CreateVoice("Landing B") };
        dustVoices = new[] { CreateVoice("Dust A"), CreateVoice("Dust B") };
        machineryVoices = new[] { CreateVoice("Machinery A"), CreateVoice("Machinery B") };
        propellerStartVoice = CreateVoice("Propeller Start");
        propellerLoopVoice = CreateVoice("Propeller Loop");
        propellerEndVoice = CreateVoice("Propeller End");
        deathVoice = CreateVoice("Death");
        overdriveChargeStartVoice = CreateVoice("Overdrive Charge Start");
        overdriveChargeLoopVoice = CreateVoice("Overdrive Charge Loop");
        overdriveReleaseVoice = CreateVoice("Overdrive Release");
    }

    private Voice CreateVoice(string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = outputGroup;
        Voice voice = new Voice(source);
        voices.Add(voice);
        return voice;
    }

    private void OnEnable()
    {
        if (controller == null) return;
        controller.PatternFinished += HandlePatternFinished;
        controller.StateChanged += HandleStateChanged;
    }

    private void Update()
    {
        double now = AudioSettings.dspTime;
        foreach (Voice voice in voices) voice.Tick(now);
    }

    public void PlayWarning() => warningVoice.Play(warning, 0.03f, 0f, loop: true);
    public void StopWarning() => warningVoice.FadeOut(0.1f);

    public void PlaySlam() => PlayImpact(slam, float.PositiveInfinity, slamPlaybackOffset);
    public void PlayCrushImpact() => PlayImpact(crushImpact, 0.5f);
    public void PlayPunchImpact() => PlayImpact(punchImpact, 0.35f);

    public void PlayWallImpact()
    {
        PlayImpact(wallImpact, 0.65f);
        PlayDebrisBreak();
    }

    private void PlayImpact(AudioClip clip, float duration, float playbackOffset = 0f)
    {
        foreach (Voice voice in slamVoices) voice.FadeOut(0.1f);
        slamVoices[slamIndex].Play(clip, 0f, 0.15f,
            maximumDuration: duration, playbackOffset: playbackOffset);
        slamIndex = 1 - slamIndex;
    }

    public void PlayDebrisLanding()
    {
        // One landing cluster, even when several pieces hit in the same physics step.
        double now = AudioSettings.dspTime;
        if (now - lastLandingTime < 0.12) return;
        lastLandingTime = now;
        foreach (Voice voice in landingVoices) voice.FadeOut(0.1f);
        landingVoices[landingIndex].Play(debrisLanding, 0f, 0.15f);
        PlayDust(float.PositiveInfinity);
        landingIndex = 1 - landingIndex;
    }

    public void PlayDebrisBreak() => PlayDust(0.7f);

    private void PlayDust(float duration)
    {
        // Wall arrival and several debris breaks can describe the same impact cluster.
        double now = AudioSettings.dspTime;
        if (now - lastDustTime < 0.12) return;
        lastDustTime = now;
        foreach (Voice voice in dustVoices) voice.FadeOut(0.1f);
        dustVoices[dustIndex].Play(debrisDust, 0.03f, 0.15f, maximumDuration: duration);
        dustIndex = 1 - dustIndex;
    }

    private void PlayMachinery(AudioClip clip)
    {
        StopMachinery();
        machineryVoices[machineryIndex].Play(clip, 0.2f, 0.2f, pitch: 1.25f, volume: 0.7f);
        machineryIndex = 1 - machineryIndex;
    }

    public void PlayTurnOff() => PlayMachinery(turnOff);
    public void PlayTurnOn() => PlayMachinery(turnOn);
    public void StopMachinery()
    {
        foreach (Voice voice in machineryVoices) voice.FadeOut(0.2f);
    }

    public void PlayDeadDrop() => deathVoice.Play(deadDrop, 0.03f, 0.2f);

    public void StartOverdriveCharge()
    {
        overdriveReleaseVoice.FadeOut(0.1f);
        overdriveChargeStartVoice.Play(overdriveChargeStart, 0.05f, TransitionFade);
        float loopDelay = overdriveChargeStart != null
            ? Mathf.Max(0f, overdriveChargeStart.length - TransitionFade)
            : 0f;
        overdriveChargeLoopVoice.Play(overdriveChargeLoop, TransitionFade, 0f, loopDelay, true);
    }

    public void PlayOverdriveRelease()
    {
        overdriveChargeStartVoice.FadeOut(0.08f);
        overdriveChargeLoopVoice.FadeOut(0.08f);
        overdriveReleaseVoice.Play(overdriveRelease, 0.005f, 0.08f);
    }

    public void StopOverdrive()
    {
        overdriveChargeStartVoice.FadeOut(0.15f);
        overdriveChargeLoopVoice.FadeOut(0.15f);
        overdriveReleaseVoice.FadeOut(0.15f);
    }

    public void PlayPropellerStart()
    {
        propellerEndVoice.FadeOut(TransitionFade);
        propellerActive = true;
        propellerStartVoice.Play(propellerStart, 0.1f, TransitionFade);
    }

    public void StartPropeller()
    {
        propellerActive = true;
        propellerStartVoice.FadeOut(TransitionFade);
        propellerLoopVoice.Play(propellerLoop, TransitionFade, 0f, loop: true);
    }

    public void StopPropeller(bool playEnd)
    {
        bool wasActive = propellerActive;
        propellerActive = false;
        propellerStartVoice.FadeOut(0.2f);
        propellerLoopVoice.FadeOut(0.2f);
        if (playEnd && wasActive)
            propellerEndVoice.Play(propellerEnd, 0.1f, 0.2f, maximumDuration: 0.8f);
        else if (!playEnd)
            propellerEndVoice.FadeOut(0.2f);
    }

    private void HandlePatternFinished(Boss1PatternBase pattern, Boss1PatternResult result)
    {
        StopWarning();
        StopPropeller(false);
        StopMachinery();
        StopOverdrive();
        float tail = result == Boss1PatternResult.Completed ? 0.35f : 0.15f;
        foreach (Voice voice in slamVoices) voice.FadeOut(tail);
        foreach (Voice voice in landingVoices) voice.FadeOut(tail);
        foreach (Voice voice in dustVoices) voice.FadeOut(tail);
    }

    private void HandleStateChanged(Boss1State previous, Boss1State next)
    {
        if (next != Boss1State.Dead && next != Boss1State.Disabled) return;
        propellerActive = false;
        foreach (Voice voice in voices) voice.FadeOut(0.15f);
        if (next == Boss1State.Dead) PlayDeadDrop();
    }

    private void OnDisable()
    {
        if (controller != null)
        {
            controller.PatternFinished -= HandlePatternFinished;
            controller.StateChanged -= HandleStateChanged;
        }
        propellerActive = false;
        foreach (Voice voice in voices) voice.Stop();
    }

    // DSP time keeps scheduled playback and fades aligned, independently of gameplay time scale.
    private sealed class Voice
    {
        private readonly AudioSource source;
        private double startTime;
        private double endTime;
        private double stopTime;
        private float fadeIn;
        private float fadeOut;
        private float stopDuration;
        private float stopVolume;
        private float playbackVolume;
        private bool active;

        public Voice(AudioSource audioSource) => source = audioSource;

        public void Play(AudioClip clip, float attack, float release, float delay = 0f,
            bool loop = false, float maximumDuration = float.PositiveInfinity, float playbackOffset = 0f,
            float pitch = 1f, float volume = 1f)
        {
            Stop();
            if (clip == null) return;
            source.clip = clip;
            int offsetSamples = Mathf.Clamp(Mathf.RoundToInt(playbackOffset * clip.frequency), 0, clip.samples - 1);
            source.timeSamples = offsetSamples;
            source.loop = loop;
            source.pitch = pitch;
            playbackVolume = volume;
            source.volume = attack > 0f ? 0f : playbackVolume;
            startTime = AudioSettings.dspTime + delay;
            float remainingDuration = (clip.samples - offsetSamples) / (clip.frequency * source.pitch);
            endTime = loop ? double.PositiveInfinity : startTime + Mathf.Min(remainingDuration, maximumDuration);
            stopTime = double.PositiveInfinity;
            fadeIn = attack;
            fadeOut = release;
            active = true;
            if (delay > 0f) source.PlayScheduled(startTime);
            else source.Play();
        }

        public void FadeOut(float duration)
        {
            if (!active) return;
            double now = AudioSettings.dspTime;
            if (now < startTime || duration <= 0f) { Stop(); return; }
            if (now + duration >= stopTime) return;
            stopVolume = source.volume;
            stopDuration = duration;
            stopTime = now + duration;
        }

        public void Tick(double now)
        {
            if (!active) return;
            if (now >= endTime || now >= stopTime) { Stop(); return; }
            float volume = playbackVolume * (fadeIn > 0f ? Mathf.Clamp01((float)(now - startTime) / fadeIn) : 1f);
            if (fadeOut > 0f) volume = Mathf.Min(volume, playbackVolume * Mathf.Clamp01((float)(endTime - now) / fadeOut));
            if (!double.IsPositiveInfinity(stopTime))
                volume = Mathf.Min(volume, stopVolume * Mathf.Clamp01((float)(stopTime - now) / stopDuration));
            source.volume = volume;
        }

        public void Stop()
        {
            source.Stop();
            source.volume = 0f;
            active = false;
        }
    }
}

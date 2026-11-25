using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public sealed class Boss2Audio : MonoBehaviour
{
    public enum Sound
    {
        RingCharge, RingAttack, CoreCharge, CoreAttack, BlackHole, GravityField,
        TrackingLaser, CannonBeam, Groggy, BarrierOn, DeathCharge, DeathExplosion
    }

    [SerializeField] private AudioMixerGroup outputGroup;
    [SerializeField] private AudioClip ringCharge;
    [SerializeField] private AudioClip ringAttack;
    [SerializeField] private AudioClip coreCharge;
    [SerializeField] private AudioClip coreAttack;
    [SerializeField] private AudioClip blackHole;
    [SerializeField] private AudioClip gravityField;
    [SerializeField] private AudioClip trackingLaser;
    [SerializeField] private AudioClip cannonBeam;
    [SerializeField] private AudioClip groggy;
    [SerializeField] private AudioClip barrierOn;
    [SerializeField] private AudioClip deathExplosion;

    private readonly List<Playback> playing = new List<Playback>();
    private readonly Stack<AudioSource> availableSources = new Stack<AudioSource>();
    private Boss2Controller owner;

    private void Awake() => owner = GetComponent<Boss2Controller>();

    private void OnEnable()
    {
        if (owner != null) owner.StateChanged += HandleStateChanged;
    }

    // Each visual owns its playback handle, including effects that outlive their spawning pattern.
    public Playback Play(Sound sound, bool loop = false, float fadeIn = 0.02f, float fadeOut = 0.15f, float pitch = 1f)
    {
        if (!isActiveAndEnabled) return null;
        AudioClip clip = GetClip(sound);
        if (clip == null) return null;
        AudioSource source;
        if (availableSources.Count > 0) source = availableSources.Pop();
        else
        {
            var child = new GameObject("Boss2 Sound");
            child.transform.SetParent(transform, false);
            source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        source.gameObject.name = sound.ToString();
        source.outputAudioMixerGroup = outputGroup;
        source.pitch = pitch;
        var playback = new Playback(source, clip, loop, fadeIn, fadeOut,
            sound == Sound.CannonBeam, sound == Sound.Groggy ? 0.75f : 1f);
        playing.Add(playback);
        BalanceCannonVolumes();
        return playback;
    }

    private AudioClip GetClip(Sound sound) => sound switch
    {
        Sound.RingCharge => ringCharge,
        Sound.RingAttack => ringAttack,
        Sound.CoreCharge or Sound.DeathCharge => coreCharge,
        Sound.CoreAttack => coreAttack,
        Sound.BlackHole => blackHole,
        Sound.GravityField => gravityField,
        Sound.TrackingLaser => trackingLaser,
        Sound.CannonBeam => cannonBeam,
        Sound.Groggy => groggy,
        Sound.BarrierOn => barrierOn,
        Sound.DeathExplosion => deathExplosion,
        _ => null
    };

    private void Update()
    {
        double now = AudioSettings.dspTime;
        for (int i = playing.Count - 1; i >= 0; i--)
        {
            Playback playback = playing[i];
            if (playback.Tick(now)) continue;
            availableSources.Push(playback.Release());
            playing.RemoveAt(i);
        }
        BalanceCannonVolumes();
    }

    private void BalanceCannonVolumes()
    {
        float totalGain = 0f;
        foreach (Playback playback in playing)
            if (playback.IsCannonBeam) totalGain += playback.EnvelopeGain;

        // Include attack/release envelopes so joining and ending beams do not cause volume jumps.
        float scale = 1f / Mathf.Max(1f, totalGain);
        foreach (Playback playback in playing)
            if (playback.IsCannonBeam) playback.ApplyGain(scale);
    }

    private void HandleStateChanged(Boss2CombatState previous, Boss2CombatState next)
    {
        if (next != Boss2CombatState.Dead && next != Boss2CombatState.Disabled && next != Boss2CombatState.Inactive) return;
        foreach (Playback playback in playing) playback.FadeOut(0.15f);
    }

    private void OnDisable()
    {
        if (owner != null) owner.StateChanged -= HandleStateChanged;
        foreach (Playback playback in playing) availableSources.Push(playback.Release());
        playing.Clear();
    }

    public sealed class Playback
    {
        private AudioSource source;
        private readonly double startTime;
        private readonly double endTime;
        private readonly float attack;
        private readonly float release;
        private readonly float volume;
        private double stopTime = double.PositiveInfinity;
        private float stopDuration;
        private float stopVolume;

        internal bool IsCannonBeam { get; }
        internal float EnvelopeGain { get; private set; }

        internal Playback(AudioSource source, AudioClip clip, bool loop, float attack, float release, bool isCannonBeam, float volume)
        {
            this.source = source;
            this.attack = attack;
            this.release = release;
            this.volume = volume;
            IsCannonBeam = isCannonBeam;
            startTime = AudioSettings.dspTime;
            endTime = loop ? double.PositiveInfinity : startTime + clip.length / source.pitch;
            source.clip = clip;
            source.loop = loop;
            EnvelopeGain = attack > 0f ? 0f : volume;
            source.volume = EnvelopeGain;
            source.Play();
        }

        public void FadeOut(float duration = 0.15f)
        {
            if (source == null) return;
            double now = AudioSettings.dspTime;
            if (now + duration >= stopTime) return;
            stopVolume = EnvelopeGain;
            stopDuration = duration;
            stopTime = now + duration;
        }

        internal bool Tick(double now)
        {
            if (source == null || now >= endTime || now >= stopTime) return false;
            float gain = volume * (attack > 0f ? Mathf.Clamp01((float)(now - startTime) / attack) : 1f);
            if (release > 0f) gain = Mathf.Min(gain, volume * Mathf.Clamp01((float)(endTime - now) / release));
            if (!double.IsPositiveInfinity(stopTime))
                gain = Mathf.Min(gain, stopVolume * Mathf.Clamp01((float)(stopTime - now) / stopDuration));
            EnvelopeGain = gain;
            source.volume = EnvelopeGain;
            return true;
        }

        internal void ApplyGain(float scale)
        {
            if (source != null) source.volume = EnvelopeGain * scale;
        }

        internal AudioSource Release()
        {
            AudioSource result = source;
            source.Stop();
            source.clip = null;
            source.volume = 0f;
            source = null;
            return result;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class PlayerAudio : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup outputGroup;
    [SerializeField] private AudioClip walk;
    [SerializeField] private AudioClip run;
    [SerializeField, Range(0f, 1f)] private float walkVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float runVolume = 0.25f;
    [SerializeField, Min(0.01f)] private float runFootstepDuration = 0.22f;
    [SerializeField] private AudioClip jump;
    [SerializeField] private AudioClip landing;
    [SerializeField] private AudioClip dash;
    [SerializeField] private AudioClip attackSwing1;
    [SerializeField] private AudioClip attackSwing2;
    [SerializeField] private AudioClip attackHit1;
    [SerializeField] private AudioClip attackHit2;
    [FormerlySerializedAs("skillA")]
    [SerializeField] private AudioClip groundSlam;
    [SerializeField] private AudioClip counterSwing;
    [SerializeField] private AudioClip counterHit;
    [SerializeField] private AudioClip parry;
    [SerializeField] private AudioClip hurt1;
    [SerializeField] private AudioClip hurt2;
    [Tooltip("피격음의 강한 소리가 빠르게 들리도록 앞부분을 건너뛸 시간입니다.")]
    [SerializeField, Min(0f)] private float hurt1PlaybackOffset = 0.1f;
    [SerializeField, Min(0f)] private float hurt2PlaybackOffset = 0.025f;
    [SerializeField] private AudioClip death;
    [Tooltip("사망 사운드 파일의 앞부분을 건너뛸 시간입니다. 초 단위로 입력합니다.")]
    [SerializeField, Min(0f)] private float deathPlaybackOffset;

    private enum SwingRole { None, Ground1, Ground2, Air, Counter }
    private readonly List<Voice> voices = new List<Voice>();
    private PlayerHealth health;
    private Player player;
    private PlayerMovement playerMovement;
    private PlayerMotor motor;
    private Voice movementVoice;
    private Voice locomotionVoice;
    private Voice dashVoice;
    private Voice swingVoice;
    private Voice hitVoice;
    private Voice skillVoice;
    private Voice reactionVoice;
    private Voice deathVoice;
    private SwingRole swingRole;
    private int hurtIndex;
    private int lastHitFrame = -1;
    private bool deathPlayed;

    private bool CanPlay => isActiveAndEnabled && health != null && !health.IsDead;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        player = GetComponent<Player>();
        playerMovement = GetComponent<PlayerMovement>();
        motor = GetComponent<PlayerMotor>();
        movementVoice = CreateVoice("Movement");
        locomotionVoice = CreateVoice("Locomotion");
        dashVoice = CreateVoice("Dash");
        swingVoice = CreateVoice("Swing");
        hitVoice = CreateVoice("Hit");
        skillVoice = CreateVoice("Ground Slam");
        reactionVoice = CreateVoice("Reaction");
        deathVoice = CreateVoice("Death");
    }

    private Voice CreateVoice(string name)
    {
        var child = new GameObject("Player Sound " + name);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = outputGroup;
        var voice = new Voice(source);
        voices.Add(voice);
        return voice;
    }

    private void OnEnable()
    {
        if (health == null) return;
        health.Damaged += HandleDamaged;
        health.Died += StopGameplaySounds;
    }

    private void Update()
    {
        double now = AudioSettings.dspTime;
        foreach (Voice voice in voices) voice.Tick(now);
    }

    public void PlayJump() { if (CanPlay) movementVoice.Play(jump); }
    public void PlayLanding() { if (CanPlay) movementVoice.Play(landing); }
    public void PlayRunFootstep(float playbackOffset)
    {
        if (!CanPlay || run == null || player == null || playerMovement == null || motor == null ||
            !playerMovement.IsGrounded || !player.CanMove || playerMovement.IsKnockbackLocked ||
            playerMovement.IsBeingCarried || (player.dash != null && player.dash.IsDashing) ||
            Mathf.Abs(motor.Velocity.x) <= 0.05f) return;

        locomotionVoice.PlaySegment(run, runVolume, playbackOffset, runFootstepDuration);
    }
    public void StopLocomotion() => locomotionVoice?.FadeOut(0.08f);
    public void PlayWalkFootstep(float playbackOffset)
    {
        if (!CanPlay || motor == null || Mathf.Abs(motor.Velocity.x) <= 0.05f) return;
        locomotionVoice.PlaySegment(walk, walkVolume, playbackOffset, 0.3f);
    }
    public void PlayDash() { if (CanPlay) dashVoice.Play(dash); }
    public void StopDash() => dashVoice?.FadeOut(0.08f);

    public void PlayGroundSwing(int number) => PlaySwing(number == 2 ? attackSwing2 : attackSwing1,
        number == 2 ? SwingRole.Ground2 : SwingRole.Ground1);
    public void PlayAirSwing() => PlaySwing(attackSwing1, SwingRole.Air);
    public void PlayCounterSwing() => PlaySwing(counterSwing, SwingRole.Counter);

    private void PlaySwing(AudioClip clip, SwingRole role)
    {
        if (!CanPlay) return;
        swingRole = role;
        swingVoice.Play(clip);
    }

    public void StopGroundSwing(int number) => StopSwing(number == 2 ? SwingRole.Ground2 : SwingRole.Ground1);
    public void StopAirSwing() => StopSwing(SwingRole.Air);
    public void StopCounterSwing() => StopSwing(SwingRole.Counter);

    private void StopSwing(SwingRole role)
    {
        // The outgoing first combo State must not stop the second swing that already entered.
        if (swingRole != role) return;
        StopAttackSwing();
    }

    public void StopAttackSwing()
    {
        swingVoice?.FadeOut(0.05f);
        swingRole = SwingRole.None;
    }

    public void PlayAttackHit(int number) => PlayHit(number == 2 ? attackHit2 : attackHit1);
    public void PlayCounterHit() => PlayHit(counterHit);

    private void PlayHit(AudioClip clip)
    {
        if (!CanPlay || lastHitFrame == Time.frameCount) return;
        lastHitFrame = Time.frameCount;
        hitVoice.Play(clip);
    }

    public void PlayGroundSlam() { if (CanPlay) skillVoice.Play(groundSlam); }
    public void StopGroundSlam() => skillVoice?.FadeOut(0.12f);
    public void PlayParry() { if (CanPlay) reactionVoice.Play(parry); }

    private void HandleDamaged(int amount)
    {
        if (!CanPlay || health.CurrentHP <= 0) return;
        reactionVoice.Play(hurtIndex == 0 ? hurt1 : hurt2,
            playbackOffset: hurtIndex == 0 ? hurt1PlaybackOffset : hurt2PlaybackOffset);
        hurtIndex = 1 - hurtIndex;
    }

    private void StopGameplaySounds()
    {
        foreach (Voice voice in voices) voice.FadeOut(0.08f);
        swingRole = SwingRole.None;
    }

    public void PlayDeath()
    {
        if (!isActiveAndEnabled || health == null || !health.IsDead || deathPlayed) return;
        deathPlayed = true;
        StopGameplaySounds();
        deathVoice.Play(death, 0.02f, deathPlaybackOffset);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= HandleDamaged;
            health.Died -= StopGameplaySounds;
        }
        foreach (Voice voice in voices) voice.Stop();
        swingRole = SwingRole.None;
        deathPlayed = false;
    }

    private sealed class Voice
    {
        private readonly AudioSource source;
        private double startTime;
        private double endTime;
        private double stopTime;
        private float attack;
        private float targetVolume;
        private float stopDuration;
        private float stopVolume;
        private bool active;

        public Voice(AudioSource source) => this.source = source;

        public void Play(AudioClip clip, float fadeIn = 0.005f, float playbackOffset = 0f)
        {
            Stop();
            if (clip == null) return;
            source.clip = clip;
            source.loop = false;
            source.pitch = 1f;
            int offsetSamples = Mathf.Clamp(Mathf.RoundToInt(playbackOffset * clip.frequency), 0, clip.samples - 1);
            source.timeSamples = offsetSamples;
            source.volume = fadeIn > 0f ? 0f : 1f;
            attack = fadeIn;
            targetVolume = 1f;
            startTime = AudioSettings.dspTime;
            endTime = startTime + (clip.samples - offsetSamples) / (double)clip.frequency;
            stopTime = double.PositiveInfinity;
            active = true;
            source.Play();
        }

        public void PlaySegment(AudioClip clip, float volume, float playbackOffset, float duration)
        {
            if (clip == null)
            {
                Stop();
                return;
            }

            Stop();
            source.clip = clip;
            source.loop = false;
            source.pitch = 1f;
            int offsetSamples = Mathf.Clamp(Mathf.RoundToInt(playbackOffset * clip.frequency), 0, clip.samples - 1);
            source.timeSamples = offsetSamples;
            source.volume = 0f;
            targetVolume = volume;
            attack = 0.005f;
            startTime = AudioSettings.dspTime;
            endTime = startTime + Mathf.Min(duration, (clip.samples - offsetSamples) / (float)clip.frequency);
            stopTime = double.PositiveInfinity;
            active = true;
            source.Play();
        }

        public void FadeOut(float duration)
        {
            if (!active) return;
            double now = AudioSettings.dspTime;
            if (now + duration >= stopTime) return;
            stopVolume = targetVolume > 0f ? source.volume / targetVolume : 0f;
            stopDuration = duration;
            stopTime = now + duration;
        }

        public void Tick(double now)
        {
            if (!active) return;
            if (now >= endTime || now >= stopTime) { Stop(); return; }
            float gain = attack > 0f ? Mathf.Clamp01((float)(now - startTime) / attack) : 1f;
            gain = Mathf.Min(gain, Mathf.Clamp01((float)(endTime - now) / 0.06f));
            if (!double.IsPositiveInfinity(stopTime))
                gain = Mathf.Min(gain, stopVolume * Mathf.Clamp01((float)(stopTime - now) / stopDuration));
            source.volume = targetVolume * gain;
        }

        public void Stop()
        {
            source.Stop();
            source.loop = false;
            source.volume = 0f;
            active = false;
        }
    }
}

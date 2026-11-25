using UnityEngine;
using UnityEngine.Audio;

public sealed class GameAudioSettings : ScriptableObject
{
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup music;
    [SerializeField] private AudioMixerGroup effects;
    [SerializeField] private AudioMixerGroup ui;
    [SerializeField] private AudioClip uiMove;
    [SerializeField] private AudioClip uiClick;
    [SerializeField] private AudioClip uiBack;

    private static GameAudioSettings shared;
    public static GameAudioSettings Shared => shared != null ? shared : shared = Resources.Load<GameAudioSettings>("Settings/GameAudioSettings");

    public AudioMixerGroup GetGroup(GameAudioChannel channel) => channel switch
    {
        GameAudioChannel.Music => music,
        GameAudioChannel.Effects => effects,
        GameAudioChannel.UI => ui,
        _ => null
    };

    public void ApplyVolumes()
    {
        AudioListener.volume = 1f;
        mixer.SetFloat("MasterVolume", ToDecibels(GameSettings.MasterVolume));
        mixer.SetFloat("MusicVolume", ToDecibels(GameSettings.MusicVolume));
        mixer.SetFloat("EffectsVolume", ToDecibels(GameSettings.EffectsVolume));
        mixer.SetFloat("UIVolume", ToDecibels(GameSettings.UIVolume));
    }

    public AudioClip GetUIClip(UIAudio.Cue cue) => cue switch
    {
        UIAudio.Cue.Move => uiMove,
        UIAudio.Cue.Click => uiClick,
        UIAudio.Cue.Back => uiBack,
        _ => null
    };

    private static float ToDecibels(float value) => value <= 0f ? -80f : Mathf.Log10(value) * 20f;
}

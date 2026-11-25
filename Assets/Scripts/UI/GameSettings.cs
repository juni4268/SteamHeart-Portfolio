using System;
using UnityEngine;

public enum SettingsCategory { Sound, Display, Gameplay }
public enum GameAudioChannel { Music, Effects, UI }

public static class GameSettings
{
    private const string VolumeKey = "ProjectK.MasterVolume";
    private const string FullscreenKey = "ProjectK.Fullscreen";
    private static bool hasChanges;
    public static event Action Changed;
    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 1f;
    public static float EffectsVolume { get; private set; } = 1f;
    public static float UIVolume { get; private set; } = 1f;
    public static bool ShowInteractionKeys { get; private set; } = true;
    public static bool ShowDamageNumbers { get; private set; } = true;
    public static bool ShowEnemyHealthBars { get; private set; } = true;
    public static bool ShowBossHealthBar { get; private set; } = true;
    public static bool AutoSkipWatchedCutscenes { get; private set; } = true;
    public static bool Fullscreen { get; private set; }
    public static bool VSync { get; private set; }
    public static int ResolutionWidth { get; private set; }
    public static int ResolutionHeight { get; private set; }

    private const string Prefix = "ProjectK.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedSettings()
    {
        hasChanges = false;
        Changed = null;
        MasterVolume = ReadFloat("MasterVolume");
        MusicVolume = ReadFloat("MusicVolume");
        EffectsVolume = ReadFloat("EffectsVolume");
        UIVolume = ReadFloat("UIVolume");
        ShowInteractionKeys = ReadBool("ShowInteractionKeys", true);
        ShowDamageNumbers = ReadBool("ShowDamageNumbers", true);
        ShowEnemyHealthBars = ReadBool("ShowEnemyHealthBars", true);
        ShowBossHealthBar = ReadBool("ShowBossHealthBar", true);
        AutoSkipWatchedCutscenes = ReadBool("AutoSkipWatchedCutscenes", true);
        Fullscreen = ReadBool("Fullscreen", Screen.fullScreen);
        VSync = ReadBool("VSync", QualitySettings.vSyncCount > 0);
        ResolutionWidth = PlayerPrefs.GetInt(Prefix + "ResolutionWidth", Screen.width);
        ResolutionHeight = PlayerPrefs.GetInt(Prefix + "ResolutionHeight", Screen.height);
        if (PlayerPrefs.HasKey(FullscreenKey))
            Screen.fullScreen = Fullscreen;
        if (PlayerPrefs.HasKey(Prefix + "ResolutionWidth") && ResolutionWidth > 0 && ResolutionHeight > 0)
            Screen.SetResolution(ResolutionWidth, ResolutionHeight, Screen.fullScreenMode);
        if (PlayerPrefs.HasKey(Prefix + "VSync")) QualitySettings.vSyncCount = VSync ? 1 : 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static async void InitializeAudio()
    {
        // AudioMixer.SetFloat must run after the initial Start callbacks.
        await Awaitable.NextFrameAsync();
        if (Application.isPlaying) ApplyAudio();
    }

    private static void ApplyAudio() => GameAudioSettings.Shared.ApplyVolumes();

    private static float ReadFloat(string key) => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, 1f));
    private static bool ReadBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;
    private static void NotifyChanged() { hasChanges = true; Changed?.Invoke(); }

    public static void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
        ApplyAudio();
        NotifyChanged();
    }

    public static void SetChannelVolume(GameAudioChannel channel, float value)
    {
        value = Mathf.Clamp01(value);
        switch (channel)
        {
            case GameAudioChannel.Music: MusicVolume = value; break;
            case GameAudioChannel.Effects: EffectsVolume = value; break;
            case GameAudioChannel.UI: UIVolume = value; break;
        }
        PlayerPrefs.SetFloat(Prefix + channel + "Volume", value);
        ApplyAudio();
        NotifyChanged();
    }

    public static void SetFullscreen(bool value)
    {
        Fullscreen = value;
        Screen.fullScreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        NotifyChanged();
    }

    public static void SetResolution(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        ResolutionWidth = width;
        ResolutionHeight = height;
        Screen.SetResolution(width, height, Fullscreen);
        PlayerPrefs.SetInt(Prefix + "ResolutionWidth", width);
        PlayerPrefs.SetInt(Prefix + "ResolutionHeight", height);
        NotifyChanged();
    }

    public static void SetVSync(bool value)
    {
        VSync = value;
        QualitySettings.vSyncCount = value ? 1 : 0;
        WriteBool("VSync", value);
    }

    private static void WriteBool(string key, bool value)
    {
        PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
        NotifyChanged();
    }

    public static void SetInteractionKeys(bool value) { ShowInteractionKeys = value; WriteBool("ShowInteractionKeys", value); }
    public static void SetDamageNumbers(bool value) { ShowDamageNumbers = value; WriteBool("ShowDamageNumbers", value); }
    public static void SetEnemyHealthBars(bool value) { ShowEnemyHealthBars = value; WriteBool("ShowEnemyHealthBars", value); }
    public static void SetBossHealthBar(bool value) { ShowBossHealthBar = value; WriteBool("ShowBossHealthBar", value); }
    public static void SetAutoSkipWatchedCutscenes(bool value) { AutoSkipWatchedCutscenes = value; WriteBool("AutoSkipWatchedCutscenes", value); }

    public static void ResetCategory(SettingsCategory category)
    {
        switch (category)
        {
            case SettingsCategory.Sound:
                SetMasterVolume(1f);
                foreach (GameAudioChannel channel in Enum.GetValues(typeof(GameAudioChannel))) SetChannelVolume(channel, 1f);
                break;
            case SettingsCategory.Display:
                SetFullscreen(true);
                SetResolution(Screen.currentResolution.width, Screen.currentResolution.height);
                SetVSync(true);
                break;
            case SettingsCategory.Gameplay:
                SetInteractionKeys(true); SetDamageNumbers(true); SetEnemyHealthBars(true); SetBossHealthBar(true);
                SetAutoSkipWatchedCutscenes(true);
                break;
        }
    }

    public static void Save()
    {
        if (!hasChanges) return;
        PlayerPrefs.Save();
        hasChanges = false;
    }
}

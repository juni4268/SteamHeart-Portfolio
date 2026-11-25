using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;

public sealed class GameAudioSettingsTests
{
    [UnityTest]
    public IEnumerator StartupDefersMixerChangesUntilTheNextFrame()
    {
        yield return new EnterPlayMode();
        yield return null;

        var mixer = (AudioMixer)new SerializedObject(GameAudioSettings.Shared)
            .FindProperty("mixer").objectReferenceValue;
        PropertyInfo volume = typeof(GameSettings).GetProperty(nameof(GameSettings.MasterVolume));
        float originalVolume = GameSettings.MasterVolume;
        float originalListenerVolume = AudioListener.volume;
        Assert.That(mixer.GetFloat("MasterVolume", out float originalMixerVolume), Is.True);
        try
        {
            // Change only runtime state, leaving the user's saved preferences untouched.
            volume.SetValue(null, 0.25f);
            mixer.SetFloat("MasterVolume", 0f);
            typeof(GameSettings).GetMethod("InitializeAudio", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);
            mixer.GetFloat("MasterVolume", out float beforeNextFrame);
            Assert.That(beforeNextFrame, Is.EqualTo(0f));

            yield return null;
            yield return null;
            mixer.GetFloat("MasterVolume", out float appliedVolume);
            Assert.That(appliedVolume, Is.EqualTo(Mathf.Log10(0.25f) * 20f).Within(0.001f));
        }
        finally
        {
            volume.SetValue(null, originalVolume);
            mixer.SetFloat("MasterVolume", originalMixerVolume);
            AudioListener.volume = originalListenerVolume;
        }

        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}

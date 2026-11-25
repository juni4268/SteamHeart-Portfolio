using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// A single pause owner restores only the state it actually suspended.
public static class GameplayPause
{
    private static Handle activeHandle;
    public static bool IsActive => activeHandle != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => activeHandle = null;

    public static bool TryAcquire(Behaviour owner, out Handle handle)
    {
        handle = null;
        if (owner == null || !owner.isActiveAndEnabled || IsActive || Time.timeScale <= 0f)
            return false;
        handle = new Handle(Time.timeScale);
        activeHandle = handle;
        Time.timeScale = 0f;
        return true;
    }

    public sealed class Handle : IDisposable
    {
        private readonly float previousTimeScale;
        private List<Player> pausedPlayers;
        private List<EventSystem> pausedEventSystems;
        private bool previousAudioPause;
        private bool pausedAudio;
        private bool disposed;

        internal Handle(float previousTimeScale) => this.previousTimeScale = previousTimeScale;
        private bool OwnsPause => !disposed && ReferenceEquals(activeHandle, this);

        public void PausePlayer(Player player)
        {
            if (!OwnsPause || player == null || player.input == null || !player.input.Player.enabled) return;
            pausedPlayers ??= new List<Player>();
            pausedPlayers.Add(player);
            player.input.Player.Disable();
        }

        public void PauseAllPlayers()
        {
            if (!OwnsPause) return;
            foreach (Player player in UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None))
                PausePlayer(player);
        }

        public void PauseAudio()
        {
            if (!OwnsPause || pausedAudio) return;
            previousAudioPause = AudioListener.pause;
            pausedAudio = true;
            AudioListener.pause = true;
        }

        public void PauseEventSystems()
        {
            if (!OwnsPause) return;
            foreach (EventSystem eventSystem in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (!eventSystem.enabled) continue;
                pausedEventSystems ??= new List<EventSystem>();
                pausedEventSystems.Add(eventSystem);
                eventSystem.enabled = false;
            }
        }

        public void Dispose()
        {
            if (!OwnsPause) return;
            disposed = true;
            activeHandle = null;
            // A later nonzero time-scale change belongs to its caller.
            if (Time.timeScale == 0f) Time.timeScale = previousTimeScale;
            if (pausedAudio) AudioListener.pause = previousAudioPause;
            if (pausedPlayers != null)
            {
                foreach (Player player in pausedPlayers)
                {
                    if (player != null && player.isActiveAndEnabled && player.input != null &&
                        player.health != null && !player.health.IsDead)
                        player.input.Player.Enable();
                }
                pausedPlayers.Clear();
            }
            if (pausedEventSystems != null)
            {
                foreach (EventSystem eventSystem in pausedEventSystems)
                    if (eventSystem != null) eventSystem.enabled = true;
                pausedEventSystems.Clear();
            }
        }
    }
}

using UnityEngine;

// Coordinates disk persistence and retries; PlayerProgressStore keeps the existing JSON format.
public static class PlayerProgressPersistence
{
    private const float SaveRetryInterval = 5f;
    private static bool hasPendingSave;
    private static float nextSaveRetryTime;
    private static bool canSave;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        hasPendingSave = false;
        nextSaveRetryTime = 0f;
        canSave = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadProgress()
    {
        PlayerProgressLoadResult result = PlayerProgressStore.Load(out PlayerProgressData progress);
        canSave = result != PlayerProgressLoadResult.Failed;
        if (result == PlayerProgressLoadResult.Loaded)
            PlayerPermanentProgress.Restore(progress);
        else if (!canSave)
            Debug.LogWarning("Automatic progress saving is disabled for this session because the existing save could not be loaded.");
    }

    public static void RetryPendingSave(bool immediately = false)
    {
        if (!hasPendingSave || (!immediately && Time.unscaledTime < nextSaveRetryTime)) return;
        SaveProgress();
    }

    internal static void SaveProgress()
    {
        if (!canSave) return;
        // Retry the latest snapshot, including purchases made after a failed write.
        hasPendingSave = !PlayerProgressStore.Save(PlayerPermanentProgress.CreateSnapshot());
        if (hasPendingSave) nextSaveRetryTime = Time.unscaledTime + SaveRetryInterval;
    }
}

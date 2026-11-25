using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class HitStopManager : MonoBehaviour
{
    private static HitStopManager instance;
    private Behaviour owner;
    private bool active;
    private GameplayPause.Handle pause;
    private float endTime;

    public static bool IsActive => instance != null && instance.active;

    public static void Request(Behaviour requester, float duration)
    {
        if (requester == null || !requester.isActiveAndEnabled || duration <= 0f ||
            IsActive || Time.timeScale <= 0f) return;

        if (instance == null)
            instance = new GameObject(nameof(HitStopManager)).AddComponent<HitStopManager>();
        if (!GameplayPause.TryAcquire(instance, out GameplayPause.Handle handle)) return;
        instance.pause = handle;
        instance.owner = requester;
        instance.endTime = Time.unscaledTime + duration;
        instance.active = true;
    }

    // Existing pause screens take ownership after releasing the temporary hit stop.
    public static float PrepareForPause()
    {
        if (instance != null) instance.Release();
        return Time.timeScale;
    }

    public static void Cancel(Behaviour requester)
    {
        if (instance != null && instance.owner == requester) instance.Release();
    }

    private void OnEnable() => SceneManager.sceneUnloaded += OnSceneUnloaded;

    private void Update()
    {
        if (!active) return;
        if (Time.timeScale != 0f || owner == null || !owner.isActiveAndEnabled ||
            Time.unscaledTime >= endTime) Release();
    }

    private void Release()
    {
        if (!active) return;
        pause?.Dispose();
        pause = null;
        active = false;
        owner = null;
    }

    private void OnSceneUnloaded(Scene scene) => Release();

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Release();
    }

    private void OnDestroy()
    {
        Release();
        if (instance == this) instance = null;
    }
}

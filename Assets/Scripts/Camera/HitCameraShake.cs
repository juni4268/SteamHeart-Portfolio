using UnityEngine;

// Apply after Cinemachine; remove our offset before the next frame's tracking.
[DefaultExecutionOrder(10000)]
public sealed class HitCameraShake : MonoBehaviour
{
    private Behaviour owner;
    private float startTime;
    private float duration;
    private float strength;
    private Vector2 axis;
    private float frequency;
    private float blendDuration;
    private bool fadeOut;
    private bool playing;
    private Vector3 appliedOffset;

    public static void Play(Behaviour requester, float duration, float strength, Vector2 axis,
        float frequency = 25f, float blendDuration = 0f)
        => Play(requester, duration, strength, axis, true, frequency, blendDuration);

    public static void PlaySustained(Behaviour requester, float duration, float strength, Vector2 axis,
        float frequency = 25f, float blendDuration = 0f)
        => Play(requester, duration, strength, axis, false, frequency, blendDuration);

    private static void Play(Behaviour requester, float duration, float strength, Vector2 axis,
        bool fadeOut, float frequency, float blendDuration)
    {
        Camera camera = Camera.main;
        if (camera == null || duration <= 0f || strength <= 0f || axis.sqrMagnitude <= Mathf.Epsilon) return;
        HitCameraShake shake = camera.GetComponent<HitCameraShake>();
        if (shake == null) shake = camera.gameObject.AddComponent<HitCameraShake>();
        if (shake.playing)
        {
            float remaining = Mathf.Clamp01(1f - (Time.unscaledTime - shake.startTime) / shake.duration);
            float currentStrength = shake.strength * shake.axis.magnitude * (shake.fadeOut ? remaining : 1f);
            if (remaining > 0f && shake.owner != null && shake.owner.isActiveAndEnabled
                && strength * axis.magnitude <= currentStrength)
                return;
            // Restore the previous offset before replacing a weaker or expired shake.
            shake.Stop();
        }
        shake.owner = requester;
        shake.startTime = Time.unscaledTime;
        shake.duration = duration;
        shake.strength = strength;
        shake.axis = axis;
        shake.frequency = Mathf.Max(0.01f, frequency);
        shake.blendDuration = Mathf.Max(0f, blendDuration);
        shake.fadeOut = fadeOut;
        shake.playing = true;
    }

    public static void Cancel(Behaviour requester)
    {
        Camera camera = Camera.main;
        HitCameraShake shake = camera != null ? camera.GetComponent<HitCameraShake>() : null;
        if (shake != null && shake.owner == requester) shake.Stop();
    }

    private void Update() => RemoveOffset();

    private void LateUpdate()
    {
        if (!playing) return;
        float elapsed = Time.unscaledTime - startTime;
        if (owner == null || !owner.isActiveAndEnabled || elapsed >= duration)
        {
            Stop();
            return;
        }
        float amplitude = strength * (fadeOut ? 1f - elapsed / duration : 1f);
        if (blendDuration > 0f)
        {
            float blendIn = Mathf.SmoothStep(0f, 1f, elapsed / blendDuration);
            float blendOut = Mathf.SmoothStep(0f, 1f, (duration - elapsed) / blendDuration);
            amplitude *= blendIn * blendOut;
        }
        float phase = elapsed * frequency * Mathf.PI * 2f;
        appliedOffset = new Vector3(Mathf.Cos(phase) * axis.x,
            Mathf.Sin(phase * 0.8f) * axis.y, 0f) * amplitude;
        transform.position += appliedOffset;
    }

    private void RemoveOffset()
    {
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
    }

    private void Stop()
    {
        RemoveOffset();
        playing = false;
        owner = null;
    }

    private void OnDisable() => Stop();
}

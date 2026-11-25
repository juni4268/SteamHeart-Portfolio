using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap))]
public sealed class Boss1FloorShakeEffect : MonoBehaviour
{
    [SerializeField, Min(0f)] private float duration = 0.2f;
    [Tooltip("타일 한 칸을 1로 했을 때 바닥 그림이 상하로 흔들리는 최대 폭입니다.")]
    [SerializeField, Min(0f)] private float amplitude = 0.02f;
    [SerializeField, Min(0.01f)] private float frequency = 18f;

    private Tilemap tilemap;
    private Vector3 restAnchor;
    private float startTime;
    private bool playing;

    private void Awake() => tilemap = GetComponent<Tilemap>();

    public void Play()
    {
        Stop();
        if (!isActiveAndEnabled || duration <= 0f || amplitude <= 0f) return;

        restAnchor = tilemap.tileAnchor;
        startTime = Time.unscaledTime;
        playing = true;
        ApplyOffset(-amplitude);
    }

    private void LateUpdate()
    {
        if (!playing) return;

        // Use the same clock as the impact's camera shake, including during hit stop.
        float elapsed = Time.unscaledTime - startTime;
        float progress = elapsed / duration;
        if (progress >= 1f)
        {
            Stop();
            return;
        }

        float decay = (1f - progress) * (1f - progress);
        ApplyOffset(-Mathf.Cos(elapsed * frequency * Mathf.PI * 2f) * amplitude * decay);
    }

    private void ApplyOffset(float offset)
    {
        // Only move tile artwork; the GameObject and its BoxCollider2D stay fixed.
        tilemap.tileAnchor = restAnchor + Vector3.up * offset;
    }

    public void Stop()
    {
        if (!playing) return;
        tilemap.tileAnchor = restAnchor;
        playing = false;
    }

    private void OnDisable() => Stop();
}

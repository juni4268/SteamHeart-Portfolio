using System;
using UnityEngine;

[Serializable]
public sealed class CameraShakeSettings
{
    [Tooltip("화면 흔들림 지속시간(초)입니다. 0이면 재생하지 않습니다.")]
    [SerializeField, Min(0f)] private float duration;
    [Tooltip("화면 흔들림 강도(월드 단위)입니다. 0이면 재생하지 않습니다.")]
    [SerializeField, Min(0f)] private float strength;
    [Tooltip("X는 가로, Y는 세로 흔들림 비율입니다. 0인 축은 흔들리지 않습니다.")]
    [SerializeField] private Vector2 axis;
    [Tooltip("초당 흔들림 횟수입니다. 낮을수록 천천히 흔들립니다.")]
    [SerializeField, Min(0.01f)] private float frequency = 25f;
    [Tooltip("시작과 끝에서 흔들림 강도를 부드럽게 전환하는 시간(초)입니다. 0이면 즉시 전환합니다.")]
    [SerializeField, Min(0f)] private float blendDuration;

    public CameraShakeSettings(float duration, float strength, Vector2 axis,
        float frequency = 25f, float blendDuration = 0f)
    {
        this.duration = duration;
        this.strength = strength;
        this.axis = axis;
        this.frequency = frequency;
        this.blendDuration = blendDuration;
    }

    public void Play(Behaviour owner) => HitCameraShake.Play(owner, duration, strength, axis, frequency, blendDuration);

    public void PlaySustained(Behaviour owner, float activeDuration)
        => HitCameraShake.PlaySustained(owner, activeDuration, strength, axis, frequency, blendDuration);

    public void Validate()
    {
        duration = Mathf.Max(0f, duration);
        strength = Mathf.Max(0f, strength);
        axis = new Vector2(Mathf.Max(0f, axis.x), Mathf.Max(0f, axis.y));
        frequency = Mathf.Max(0.01f, frequency);
        blendDuration = Mathf.Max(0f, blendDuration);
    }
}

using UnityEngine;

[CreateAssetMenu(fileName = "Boss1CrushConfig", menuName = "Boss/Boss1CrushConfig")]
public sealed class Boss1CrushConfig : ScriptableObject
{
    [Tooltip("양손이 압착 준비 위치로 이동하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float placementDuration = 1f;
    [Tooltip("손이 플레이어의 높이를 추적하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float trackingDuration = 3f;
    [Tooltip("플레이어의 높이를 추적하는 손의 이동 속도입니다.")]
    [SerializeField, Min(0f)] private float trackingSpeed = 2.2f;
    [Tooltip("추적을 멈춘 뒤 압착 공격 경고를 표시하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float warningDuration = 1.4f;
    [Tooltip("경고가 끝난 뒤 압착 직전에 양손을 바깥쪽으로 당기는 거리(유닛)입니다.")]
    [SerializeField, Min(0f)] private float pullbackDistance = 0.2f;
    [Tooltip("바깥쪽으로 당기는 시간(초)입니다. 당김이 끝나면 바로 압착합니다.")]
    [SerializeField, Min(0f)] private float pullbackDuration = 0.1f;
    [Tooltip("양손이 안쪽으로 이동해 압착하는 시간(초)입니다.")]
    [UnityEngine.Serialization.FormerlySerializedAs("compressionDuration")]
    [SerializeField, Min(0f)] private float crushDuration = 0.15f;
    [Tooltip("압착 후 손을 충돌 위치에 유지하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float impactHoldDuration = 1f;
    [Tooltip("손이 기본 위치로 돌아가는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float returnDuration = 1.5f;
    [Tooltip("한 번의 패턴 실행에서 압착 공격을 반복하는 횟수입니다.")]
    [SerializeField, Min(1)] private int repeatCount = 3;
    [Tooltip("압착 공격에 맞았을 때 받는 피해량입니다.")]
    [SerializeField, Min(1)] private int damage = 1;

    [Header("Camera Shake")]
    [Tooltip("양손이 압착 위치에 도착할 때마다 재생하는 가로 화면 흔들림입니다.")]
    [SerializeField] private CameraShakeSettings impactShake =
        new CameraShakeSettings(0.16f, 0.12f, new Vector2(1f, 0.15f));

    public float PlacementDuration => placementDuration;
    public float TrackingDuration => trackingDuration;
    public float TrackingSpeed => trackingSpeed;
    public float WarningDuration => warningDuration;
    public float PullbackDistance => pullbackDistance;
    public float PullbackDuration => pullbackDuration;
    public float CrushDuration => crushDuration;
    public float ImpactHoldDuration => impactHoldDuration;
    public float ReturnDuration => returnDuration;
    public int RepeatCount => repeatCount;
    public int Damage => damage;
    public CameraShakeSettings ImpactShake => impactShake;

    private void OnValidate()
    {
        placementDuration = Mathf.Max(0f, placementDuration);
        trackingDuration = Mathf.Max(0f, trackingDuration);
        trackingSpeed = Mathf.Max(0f, trackingSpeed);
        warningDuration = Mathf.Max(0f, warningDuration);
        pullbackDistance = Mathf.Max(0f, pullbackDistance);
        pullbackDuration = Mathf.Max(0f, pullbackDuration);
        crushDuration = Mathf.Max(0f, crushDuration);
        impactHoldDuration = Mathf.Max(0f, impactHoldDuration);
        returnDuration = Mathf.Max(0f, returnDuration);
        repeatCount = Mathf.Max(1, repeatCount);
        damage = Mathf.Max(1, damage);
        impactShake.Validate();
    }
}

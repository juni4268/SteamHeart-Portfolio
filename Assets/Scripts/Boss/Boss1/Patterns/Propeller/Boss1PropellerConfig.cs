using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Boss1PropellerConfig", menuName = "Boss/Boss1PropellerConfig")]
public sealed class Boss1PropellerConfig : ScriptableObject
{
    [Header("진행 시간")]
    [Tooltip("선택된 손이 흡입 위치로 이동하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float placementDuration = 0.6f;
    [Tooltip("흡입이 시작되기 전 경고를 표시하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float warningDuration = 1.5f;
    [Tooltip("흡입 공격이 지속되는 시간(초)입니다.")]
    [SerializeField, Min(0.01f)] private float activeDuration = 6f;
    [Tooltip("손이 기본 위치로 돌아가는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float returnDuration = 0.5f;

    [Header("흡입")]
    [FormerlySerializedAs("maximumForce")]
    [Tooltip("흡입 중심에서 플레이어를 끌어당기는 최대 힘입니다. 멀어질수록 약해집니다.")]
    [SerializeField, Min(0f)] private float suctionForce = 55f;
    [Tooltip("흡입 힘이 최소 비율까지 줄어드는 거리입니다. 이보다 멀어져도 최소 힘은 유지합니다.")]
    [SerializeField, Min(0.01f)] private float forceFalloffDistance = 8f;
    [Tooltip("멀리 떨어졌을 때 적용하는 최대 흡입 힘 대비 비율입니다. 0.25는 25%입니다.")]
    [SerializeField, Range(0f, 1f)] private float minimumForceRatio = 0.25f;
    [Tooltip("손 쪽 이동 속도가 이 값 이상이면 흡입 힘을 추가하지 않습니다. 0이면 제한하지 않습니다.")]
    [SerializeField, Min(0f)] private float maximumPullSpeed = 15f;

    [Header("피해")]
    [Tooltip("손을 중심으로 플레이어에게 피해를 주는 범위의 반지름입니다.")]
    [SerializeField, Min(0.01f)] private float attackRadius = 2f;
    [Tooltip("공격 범위 안의 플레이어에게 피해를 주는 간격(초)입니다.")]
    [SerializeField, Min(0.01f)] private float damageInterval = 1f;
    [Tooltip("공격 범위 안에서 한 번에 받는 피해량입니다.")]
    [SerializeField, Min(1)] private int damagePerTick = 1;

    [Header("Wind Shape (X: Min, Y: Max)")]
    [Tooltip("바람 생성 반지름 범위입니다. 피해 반지름과 독립적으로 적용됩니다.")]
    [SerializeField] private Vector2 spawnRadiusRange = new Vector2(6f, 7f);
    [Tooltip("외곽에서 중심까지 회전하는 각도 범위(도)입니다. 왼손은 시계 방향입니다.")]
    [SerializeField] private Vector2 rotationAmountRange = new Vector2(220f, 320f);
    [Tooltip("나선 반지름 감소 곡률 범위입니다. 1은 일정한 감소, 클수록 초반에 빠르게 안쪽으로 휘어집니다.")]
    [SerializeField] private Vector2 curvatureRange = new Vector2(0.8f, 1.3f);
    [SerializeField, Min(0.01f)] private float horizontalScale = 1f;
    [SerializeField, Min(0.01f)] private float verticalScale = 1f;
    [SerializeField, Range(16, 128)] private int segmentCount = 32;

    [Header("Wind Stream (X: Min, Y: Max)")]
    [Tooltip("선의 최대 길이 범위(월드 거리)입니다.")]
    [SerializeField] private Vector2 maxLengthRange = new Vector2(2f, 3f);
    [SerializeField] private Vector2 widthRange = new Vector2(0.025f, 0.045f);
    [Tooltip("전체 나선 경로 이동시간 범위(초)입니다. 95% 지점에서 소멸합니다.")]
    [SerializeField] private Vector2 travelDurationRange = new Vector2(1f, 1.4f);
    [Tooltip("공격 중 생성 간격 범위(초)입니다. 경고 중에는 더 드물게 생성합니다.")]
    [SerializeField] private Vector2 spawnIntervalRange = new Vector2(0.12f, 0.18f);
    [SerializeField, Range(0f, 180f)] private float angleJitter = 10f;

    [Header("Wind Visual")]
    [SerializeField] private Color startColor = new Color(1f, 0.45f, 0.08f, 0.35f);
    [SerializeField] private Color endColor = new Color(1f, 0.9f, 0.45f, 0.85f);
    [SerializeField, Range(0f, 1f)] private float warningIntensity = 0.45f;
    [SerializeField, Range(0f, 1f)] private float activeIntensity = 1f;

    [Header("Camera Shake")]
    [Tooltip("흡입 공격 중 유지하는 약한 화면 흔들림입니다. Duration 대신 Active Duration 동안 재생합니다.")]
    [FormerlySerializedAs("suctionStartShake")]
    [SerializeField] private CameraShakeSettings suctionShake =
        new CameraShakeSettings(0.18f, 0.06f, new Vector2(0.5f, 1f), 2f, 0.25f);

    public float PlacementDuration => placementDuration;
    public float WarningDuration => warningDuration;
    public float ActiveDuration => activeDuration;
    public float ReturnDuration => returnDuration;
    public float SuctionForce => suctionForce;
    public float MaximumPullSpeed => maximumPullSpeed;
    public float AttackRadius => attackRadius;
    public float DamageInterval => damageInterval;
    public int DamagePerTick => damagePerTick;
    public Vector2 SpawnRadiusRange => spawnRadiusRange;
    public Vector2 RotationAmountRange => rotationAmountRange;
    public Vector2 CurvatureRange => curvatureRange;
    public float HorizontalScale => horizontalScale;
    public float VerticalScale => verticalScale;
    public int SegmentCount => segmentCount;
    public Vector2 MaxLengthRange => maxLengthRange;
    public Vector2 WidthRange => widthRange;
    public Vector2 TravelDurationRange => travelDurationRange;
    public Vector2 SpawnIntervalRange => spawnIntervalRange;
    public float AngleJitter => angleJitter;
    public Color StartColor => startColor;
    public Color EndColor => endColor;
    public float WarningIntensity => warningIntensity;
    public float ActiveIntensity => activeIntensity;
    public CameraShakeSettings SuctionShake => suctionShake;

    public float GetSuctionForce(float distance)
    {
        float distanceRatio = Mathf.Clamp01(distance / forceFalloffDistance);
        return suctionForce * Mathf.Lerp(1f, minimumForceRatio, distanceRatio);
    }

    private void OnValidate()
    {
        placementDuration = Mathf.Max(0f, placementDuration);
        warningDuration = Mathf.Max(0f, warningDuration);
        activeDuration = Mathf.Max(0.01f, activeDuration);
        returnDuration = Mathf.Max(0f, returnDuration);
        suctionForce = Mathf.Max(0f, suctionForce);
        forceFalloffDistance = Mathf.Max(0.01f, forceFalloffDistance);
        minimumForceRatio = Mathf.Clamp01(minimumForceRatio);
        maximumPullSpeed = Mathf.Max(0f, maximumPullSpeed);
        attackRadius = Mathf.Max(0.01f, attackRadius);
        damageInterval = Mathf.Max(0.01f, damageInterval);
        damagePerTick = Mathf.Max(1, damagePerTick);
        spawnRadiusRange = ValidateRange(spawnRadiusRange, 0.01f);
        rotationAmountRange = ValidateRange(rotationAmountRange, 0f);
        curvatureRange = ValidateRange(curvatureRange, 0.1f);
        maxLengthRange = ValidateRange(maxLengthRange, 0.01f);
        widthRange = ValidateRange(widthRange, 0.001f);
        travelDurationRange = ValidateRange(travelDurationRange, 0.01f);
        spawnIntervalRange = ValidateRange(spawnIntervalRange, 0.01f);
        horizontalScale = Mathf.Max(0.01f, horizontalScale);
        verticalScale = Mathf.Max(0.01f, verticalScale);
        segmentCount = Mathf.Clamp(segmentCount, 16, 128);
        angleJitter = Mathf.Clamp(angleJitter, 0f, 180f);
        warningIntensity = Mathf.Clamp01(warningIntensity);
        activeIntensity = Mathf.Clamp01(activeIntensity);
        suctionShake.Validate();
    }

    private static Vector2 ValidateRange(Vector2 range, float minimum)
    {
        float low = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
        return new Vector2(low, Mathf.Max(low, Mathf.Max(range.x, range.y)));
    }
}

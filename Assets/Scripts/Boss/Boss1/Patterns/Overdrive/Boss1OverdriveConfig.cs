using UnityEngine;

[CreateAssetMenu(fileName = "Boss1OverdriveConfig", menuName = "Boss/Boss1OverdriveConfig")]
public sealed class Boss1OverdriveConfig : ScriptableObject
{
    [Header("첫 번째 손 준비")]
    [Tooltip("선택된 손이 시작 위치로 이동하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float placementDuration = 0.8f;
    [Tooltip("손이 시작 위치에 도착한 뒤 뒤로 당기기 전에 충전하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float attackWaitDuration = 2f;
    [Tooltip("충전 완료 후 당김 기준점까지 이동하는 시간(초)입니다. 도착하면 추가 대기 없이 Punch합니다.")]
    [SerializeField, Min(0f)] private float pullbackDuration = 0.2f;

    [Header("장전 중 고철 위 대응")]
    [Tooltip("양손 준비가 끝나고 장전이 시작된 뒤 고철 위 체류 감지를 시작하기까지의 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float debrisResponseWaitDuration;
    [Tooltip("같은 고철 위에 연속으로 서 있으면 대응 손이 그 고철을 내려찍는 감지시간(초)입니다. 이동·당김·하강은 기존 내려찍기 설정을 사용합니다.")]
    [SerializeField, Min(0f)] private float debrisStandDetectionDuration = 0.3f;

    [Header("첫 번째 손 충전 효과")]
    [Tooltip("손 가장자리와 방전의 색상 배율입니다. 흰색이면 Prefab의 색을 유지하며, 번개는 항상 불투명하게 표시합니다.")]
    [SerializeField, ColorUsage(false)] private Color chargeColor = Color.white;
    [Tooltip("손 가장자리와 짧은 방전의 밝기 배율입니다. 금속 면의 원래 색상은 유지합니다.")]
    [SerializeField, Min(0f)] private float chargeBrightness = 0.85f;
    [Tooltip("충전 진행도에 따른 압력 변화입니다. 가로축과 세로축은 0~1입니다.")]
    [SerializeField] private AnimationCurve chargePressure = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("충전한 주먹을 공격 방향으로 압축하는 비율입니다. 발사 순간 원래 크기로 돌아옵니다.")]
    [SerializeField, Range(0f, 0.2f)] private float chargeCompression = 0.08f;
    [Tooltip("충전 후반 손의 시각적 진동 크기(유닛)입니다.")]
    [SerializeField, Min(0f)] private float chargeShakeStrength = 0.025f;
    [Tooltip("충전 시간의 마지막에 진동을 멈추고 잠긴 자세로 유지하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float chargeLockDuration = 0.1f;
    [Tooltip("발사 후 손 잔상이 사라지는 기준 시간(초)입니다. 테두리와 번개는 Punch가 끝날 때까지 유지합니다.")]
    [SerializeField, Min(0.01f)] private float chargeReleaseDuration = 0.35f;

    [Header("추가 연출")]
    [Tooltip("벽 충돌 기절 중 플레이어 머리 위에서 도는 별 표시입니다.")]
    [SerializeField] private PlayerStunEffect stunEffectPrefab;
    [Tooltip("충돌로 파괴된 잔해 조각이 흩어지고 사라지는 시간(초)입니다.")]
    [SerializeField, Min(0.01f)] private float debrisBreakDuration = 0.55f;

    [Header("잔해 타격")]
    [Tooltip("당김 후 손이 반대편 Punch Exit Point의 X까지 이동하며 잔해를 치는 속도(유닛/초)입니다.")]
    [SerializeField, Min(0.01f)] private float strikeSpeed = 12f;

    [Header("벽 충돌")]
    [Tooltip("잔해에 밀린 플레이어가 벽에 도착하는 순간 한 번 받는 HP 피해량입니다.")]
    [SerializeField, Min(1)] private int wallDamage = 1;
    [Tooltip("벽 충돌 후 플레이어의 이동과 행동을 제한하는 기절 시간(초)입니다.")]
    [SerializeField, Min(0.01f)] private float wallStunDuration = 2f;

    [Header("남은 손 내려찍기")]
    [Tooltip("바닥에 닿는 손 위치에서 준비 위치까지 들어 올리는 높이(유닛)입니다.")]
    [SerializeField, Min(0.01f)] private float slamPreparationHeight = 3f;
    [Tooltip("Prepare 애니메이션과 함께 플레이어를 추적하는 최소 시간(초)입니다. 준비 높이 도착과 애니메이션 완료까지 추적을 유지합니다.")]
    [SerializeField, Min(0f)] private float slamTrackingDuration = 1.65f;
    [Tooltip("조준 중 손이 준비 높이와 플레이어의 가로 위치를 향해 이동하는 속도(유닛/초)입니다.")]
    [SerializeField, Min(0.01f)] private float slamTrackingSpeed = 12f;
    [Tooltip("조준 위치를 고정한 뒤 내려찍기 직전에 손을 위쪽으로 당기는 높이(유닛)입니다.")]
    [SerializeField, Min(0f)] private float slamPullbackHeight = 0.2f;
    [Tooltip("위로 당긴 뒤 추가 대기 없이 내려찍기까지 걸리는 당김 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float slamPullbackDuration = 0.1f;
    [Tooltip("후속 내려찍기와 고철 대응 내려찍기의 일정한 이동속도(유닛/초)입니다. 이동 거리가 길어지면 도착 시간만 늘어납니다.")]
    [SerializeField, Min(0.01f)] private float slamSpeed = 15f;
    [Tooltip("남은 손의 내려찍기에 맞았을 때 받는 별도 HP 피해량입니다.")]
    [SerializeField, Min(1)] private int slamDamage = 1;

    [Header("본체 그로기와 복귀")]
    [Tooltip("내려찍기 후 본체가 지면 가까이 내려오는 시간(초)입니다. 공격 명중 여부와 관계없이 하강합니다.")]
    [SerializeField, Min(0f)] private float groggyDescentDuration = 0.8f;
    [Tooltip("하강한 본체 피격 Collider의 밑면과 바닥 사이의 간격(유닛)입니다. 지상 공격으로 닿을 수 있도록 설정합니다.")]
    [SerializeField, Min(0f)] private float groggyGroundClearance = 0.1f;
    [Tooltip("본체가 내려온 위치에서 그로기를 유지하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float groggyDuration = 5f;
    [Tooltip("그로기가 끝난 뒤 본체와 두 손이 원래 위치로 함께 복귀하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float recoveryDuration = 0.8f;

    [Header("Camera Shake")]
    [Tooltip("손이 고철을 타격하는 순간의 화면 흔들림입니다.")]
    [SerializeField] private CameraShakeSettings punchImpactShake =
        new CameraShakeSettings(0.12f, 0.1f, new Vector2(1f, 0.2f));
    [Tooltip("발사된 고철이 벽에 도착하는 순간의 화면 흔들림입니다.")]
    [SerializeField] private CameraShakeSettings wallImpactShake =
        new CameraShakeSettings(0.22f, 0.16f, new Vector2(1f, 0.35f));
    [Tooltip("반대쪽 손이 바닥을 내려찍는 순간의 화면 흔들림입니다.")]
    [SerializeField] private CameraShakeSettings slamImpactShake =
        new CameraShakeSettings(0.25f, 0.2f, new Vector2(0.25f, 1f));

    public float PlacementDuration => placementDuration;
    public float AttackWaitDuration => attackWaitDuration;
    public float PullbackDuration => pullbackDuration;
    public float DebrisResponseWaitDuration => debrisResponseWaitDuration;
    public float DebrisStandDetectionDuration => debrisStandDetectionDuration;
    public Color ChargeColor => chargeColor;
    public float ChargeBrightness => chargeBrightness;
    public AnimationCurve ChargePressure => chargePressure;
    public float ChargeCompression => chargeCompression;
    public float ChargeShakeStrength => chargeShakeStrength;
    public float ChargeLockDuration => chargeLockDuration;
    public float ChargeReleaseDuration => chargeReleaseDuration;
    public PlayerStunEffect StunEffectPrefab => stunEffectPrefab;
    public float DebrisBreakDuration => debrisBreakDuration;
    public float StrikeSpeed => strikeSpeed;
    public int WallDamage => wallDamage;
    public float WallStunDuration => wallStunDuration;
    public float SlamPreparationHeight => slamPreparationHeight;
    public float SlamTrackingDuration => slamTrackingDuration;
    public float SlamTrackingSpeed => slamTrackingSpeed;
    public float SlamPullbackHeight => slamPullbackHeight;
    public float SlamPullbackDuration => slamPullbackDuration;
    public float SlamSpeed => slamSpeed;
    public int SlamDamage => slamDamage;
    public float GroggyDescentDuration => groggyDescentDuration;
    public float GroggyGroundClearance => groggyGroundClearance;
    public float GroggyDuration => groggyDuration;
    public float RecoveryDuration => recoveryDuration;
    public CameraShakeSettings PunchImpactShake => punchImpactShake;
    public CameraShakeSettings WallImpactShake => wallImpactShake;
    public CameraShakeSettings SlamImpactShake => slamImpactShake;

    private void OnValidate()
    {
        placementDuration = Mathf.Max(0f, placementDuration);
        attackWaitDuration = Mathf.Max(0f, attackWaitDuration);
        pullbackDuration = Mathf.Max(0f, pullbackDuration);
        debrisResponseWaitDuration = Mathf.Max(0f, debrisResponseWaitDuration);
        debrisStandDetectionDuration = Mathf.Max(0f, debrisStandDetectionDuration);
        chargeBrightness = Mathf.Max(0f, chargeBrightness);
        chargeCompression = Mathf.Clamp(chargeCompression, 0f, 0.2f);
        chargeShakeStrength = Mathf.Max(0f, chargeShakeStrength);
        chargeLockDuration = Mathf.Max(0f, chargeLockDuration);
        chargeReleaseDuration = Mathf.Max(0.01f, chargeReleaseDuration);
        debrisBreakDuration = Mathf.Max(0.01f, debrisBreakDuration);
        strikeSpeed = Mathf.Max(0.01f, strikeSpeed);
        wallDamage = Mathf.Max(1, wallDamage);
        wallStunDuration = Mathf.Max(0.01f, wallStunDuration);
        slamPreparationHeight = Mathf.Max(0.01f, slamPreparationHeight);
        slamTrackingDuration = Mathf.Max(0f, slamTrackingDuration);
        slamTrackingSpeed = Mathf.Max(0.01f, slamTrackingSpeed);
        slamPullbackHeight = Mathf.Max(0f, slamPullbackHeight);
        slamPullbackDuration = Mathf.Max(0f, slamPullbackDuration);
        slamSpeed = Mathf.Max(0.01f, slamSpeed);
        slamDamage = Mathf.Max(1, slamDamage);
        groggyDescentDuration = Mathf.Max(0f, groggyDescentDuration);
        groggyGroundClearance = Mathf.Max(0f, groggyGroundClearance);
        groggyDuration = Mathf.Max(0f, groggyDuration);
        recoveryDuration = Mathf.Max(0f, recoveryDuration);
        punchImpactShake.Validate();
        wallImpactShake.Validate();
        slamImpactShake.Validate();
    }
}

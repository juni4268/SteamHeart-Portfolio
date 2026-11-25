using UnityEngine;

[CreateAssetMenu(fileName = "Boss1SlamConfig", menuName = "Boss/Boss1SlamConfig")]
public sealed class Boss1SlamConfig : ScriptableObject
{
    [Tooltip("바닥 내려찍기 경고 영역의 높이입니다.")]
    [SerializeField, Min(0.01f)] private float floorWarningHeight = 0.15f;
    [Tooltip("바닥 충격 피해를 판정할 때 플레이어의 바닥 접촉 여부를 확인하는 거리입니다.")]
    [SerializeField, Min(0f)] private float floorContactDistance = 0.05f;
    [Tooltip("양손이 내려찍기 준비 위치로 이동하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float preparationDuration = 0.5f;
    [Tooltip("준비 위치에서 바닥 경고를 표시하며 대기하는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float preparationWaitDuration = 1.5f;
    [Tooltip("내려찍기 직전에 손을 공격 반대 방향인 위쪽으로 당기는 높이(유닛)입니다.")]
    [SerializeField, Min(0f)] private float pullbackHeight = 0.2f;
    [Tooltip("위로 당긴 뒤 추가 대기 없이 내려찍기까지 걸리는 당김 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float pullbackDuration = 0.1f;
    [Tooltip("양손이 바닥을 내려찍는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float slamDuration = 0.2f;
    [Tooltip("내려찍기 이동 곡선입니다. 가로축은 시간, 세로축은 이동 거리의 비율(0~1)이며, 끝부분이 가파를수록 충돌 직전 가속이 강해집니다.")]
    [SerializeField] private AnimationCurve slamMovementCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.5f, 2.5f));
    [Tooltip("바닥을 내려찍은 뒤 공격 자세를 유지하며 정지하는 시간(초)입니다. 이후 복귀와 잔해 낙하가 시작됩니다.")]
    [SerializeField, Min(0f)] private float impactHoldDuration = 0.5f;
    [Tooltip("내려찍기 후 손이 기본 위치로 돌아가는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float returnDuration = 1f;
    [Header("잔해 낙하")]
    [Tooltip("한 번의 내려찍기 후 낙하하는 잔해 수의 최솟값입니다.")]
    [SerializeField, Min(1)] private int minimumDebrisCount = 3;
    [Tooltip("한 번의 내려찍기 후 낙하하는 잔해 수의 최댓값입니다.")]
    [SerializeField, Min(1)] private int maximumDebrisCount = 4;
    [UnityEngine.Serialization.FormerlySerializedAs("scrapWarningDuration")]
    [Tooltip("모든 잔해의 무작위 낙하 위치에 동시에 경고를 표시하는 시간(초)입니다. 위치는 고정되며, 경고가 끝나면 모든 잔해가 함께 낙하합니다.")]
    [SerializeField, Min(0f)] private float debrisWarningDuration = 0.8f;
    [UnityEngine.Serialization.FormerlySerializedAs("scrapWarningWidth")]
    [Tooltip("잔해 낙하 위치를 표시하는 경고 영역의 너비입니다.")]
    [SerializeField, Min(0.01f)] private float debrisWarningWidth = 0.5f;
    [Tooltip("손의 내려찍기나 바닥 충격에 맞았을 때 받는 피해량입니다.")]
    [SerializeField, Min(1)] private int slamDamage = 1;

    [Header("Camera Shake")]
    [Tooltip("양손이 바닥을 내려찍는 순간 한 번 재생하는 화면 흔들림입니다.")]
    [SerializeField] private CameraShakeSettings impactShake =
        new CameraShakeSettings(0.25f, 0.18f, new Vector2(0.25f, 1f));

    public float FloorWarningHeight => floorWarningHeight;
    public float FloorContactDistance => floorContactDistance;
    public float PreparationDuration => preparationDuration;
    public float PreparationWaitDuration => preparationWaitDuration;
    public float PullbackHeight => pullbackHeight;
    public float PullbackDuration => pullbackDuration;
    public float SlamDuration => slamDuration;
    public AnimationCurve SlamMovementCurve => slamMovementCurve;
    public float ImpactHoldDuration => impactHoldDuration;
    public float ReturnDuration => returnDuration;
    public int MinimumDebrisCount => minimumDebrisCount;
    public int MaximumDebrisCount => maximumDebrisCount;
    public float DebrisWarningDuration => debrisWarningDuration;
    public float DebrisWarningWidth => debrisWarningWidth;
    public int SlamDamage => slamDamage;
    public CameraShakeSettings ImpactShake => impactShake;

    private void OnValidate()
    {
        floorWarningHeight = Mathf.Max(0.01f, floorWarningHeight);
        floorContactDistance = Mathf.Max(0f, floorContactDistance);
        preparationDuration = Mathf.Max(0f, preparationDuration);
        preparationWaitDuration = Mathf.Max(0f, preparationWaitDuration);
        pullbackHeight = Mathf.Max(0f, pullbackHeight);
        pullbackDuration = Mathf.Max(0f, pullbackDuration);
        slamDuration = Mathf.Max(0f, slamDuration);
        impactHoldDuration = Mathf.Max(0f, impactHoldDuration);
        returnDuration = Mathf.Max(0f, returnDuration);
        minimumDebrisCount = Mathf.Max(1, minimumDebrisCount);
        maximumDebrisCount = Mathf.Max(minimumDebrisCount, maximumDebrisCount);
        debrisWarningDuration = Mathf.Max(0f, debrisWarningDuration);
        debrisWarningWidth = Mathf.Max(0.01f, debrisWarningWidth);
        slamDamage = Mathf.Max(1, slamDamage);
        impactShake.Validate();
    }
}

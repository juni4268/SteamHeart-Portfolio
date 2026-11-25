using UnityEngine;

[CreateAssetMenu(fileName = "Boss1DebrisConfig", menuName = "Boss/Boss1DebrisConfig")]
public sealed class Boss1DebrisConfig : ScriptableObject
{
    [Tooltip("잔해가 아래로 떨어지는 속도입니다.")]
    [SerializeField, Min(0.01f)] private float fallSpeed = 4f;
    [Tooltip("잔해에 맞은 플레이어가 수평 방향으로 밀려나는 속도입니다.")]
    [SerializeField, Min(0f)] private float damageKnockbackSpeed = 7f;
    [Tooltip("잔해에 맞은 플레이어가 위로 밀려나는 속도입니다.")]
    [SerializeField, Min(0f)] private float damageKnockbackLift = 1.3f;
    [Tooltip("잔해 피격 시 밀려나는 효과가 지속되는 시간(초)입니다.")]
    [SerializeField, Min(0f)] private float damageKnockbackDuration = 0.1f;
    [UnityEngine.Serialization.FormerlySerializedAs("scrapDamage")]
    [Tooltip("잔해에 맞았을 때 받는 피해량입니다.")]
    [SerializeField, Min(1)] private int debrisDamage = 1;

    [Header("Camera Shake")]
    [Tooltip("잔해가 바닥에 착지하는 순간 재생하는 약한 화면 흔들림입니다.")]
    [SerializeField] private CameraShakeSettings landingShake =
        new CameraShakeSettings(0.1f, 0.035f, new Vector2(0.2f, 1f));

    public float FallSpeed => fallSpeed;
    public float DamageKnockbackSpeed => damageKnockbackSpeed;
    public float DamageKnockbackLift => damageKnockbackLift;
    public float DamageKnockbackDuration => damageKnockbackDuration;
    public int DebrisDamage => debrisDamage;
    public CameraShakeSettings LandingShake => landingShake;

    private void OnValidate()
    {
        fallSpeed = Mathf.Max(0.01f, fallSpeed);
        damageKnockbackSpeed = Mathf.Max(0f, damageKnockbackSpeed);
        damageKnockbackLift = Mathf.Max(0f, damageKnockbackLift);
        damageKnockbackDuration = Mathf.Max(0f, damageKnockbackDuration);
        debrisDamage = Mathf.Max(1, debrisDamage);
        landingShake.Validate();
    }
}

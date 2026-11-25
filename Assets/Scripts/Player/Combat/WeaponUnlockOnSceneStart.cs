using UnityEngine;

[RequireComponent(typeof(PlayerWeaponController))]
public sealed class WeaponUnlockOnSceneStart : MonoBehaviour
{
    [Tooltip("이 Scene 진입 시 보장할 최소 무기 단계 인덱스입니다. 0은 첫 번째 단계입니다.")]
    [SerializeField, Min(0)] private int minimumWeaponStage;
    [Tooltip("Scene 진입 시 무기를 첫 번째 단계로 초기화합니다.")]
    [SerializeField] private bool resetWeaponStageOnStart;

    private void Start()
    {
        PlayerWeaponController weapon = GetComponent<PlayerWeaponController>();
        weapon.EquipWeapon();
        if (resetWeaponStageOnStart)
            weapon.SetWeaponStage(0);
        else if (weapon.CurrentWeaponStageIndex < minimumWeaponStage)
            weapon.SetWeaponStage(minimumWeaponStage);
    }
}

using System;
using UnityEngine;

public class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private WeaponSkinCatalog skinCatalog;

    private Animator animator;
    private RuntimeAnimatorController defaultAnimatorController;

    public WeaponData CurrentWeapon => IsWeaponEquipped ? weaponData : null;
    public bool IsWeaponEquipped => PlayerRuntimeState.HasWeapon;
    public int CurrentWeaponStageIndex => PlayerRuntimeState.WeaponStageIndex;
    public WeaponSkinCatalog SkinCatalog => skinCatalog;
    public WeaponSkinData CurrentSkin => skinCatalog != null
        ? skinCatalog.Find(PlayerPermanentProgress.EquippedWeaponSkin) ?? skinCatalog.DefaultSkin : null;

    public WeaponStageData CurrentWeaponStage
    {
        get
        {
            if (!IsWeaponEquipped || weaponData == null) return null;

            WeaponStageData[] weaponStages = weaponData.WeaponStages;
            if (weaponStages == null ||
                CurrentWeaponStageIndex < 0 ||
                CurrentWeaponStageIndex >= weaponStages.Length)
            {
                return null;
            }

            return weaponStages[CurrentWeaponStageIndex];
        }
    }

    public event Action<WeaponData> OnWeaponChanged;
    public event Action<WeaponStageData> OnWeaponStageChanged;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            defaultAnimatorController = animator.runtimeAnimatorController;
        }

        if (IsWeaponEquipped && weaponData != null)
        {
            ClampSavedStageIndex();
            ApplyCurrentStageAnimatorController();
        }
    }

    private void OnEnable() => PlayerPermanentProgress.WeaponSkinsChanged += ApplyCurrentStageAnimatorController;
    private void OnDisable() => PlayerPermanentProgress.WeaponSkinsChanged -= ApplyCurrentStageAnimatorController;

    public void EquipWeapon()
    {
        if (weaponData == null)
        {
            Debug.LogError($"{nameof(PlayerWeaponController)} needs a WeaponData asset.", this);
            return;
        }

        bool wasEquipped = IsWeaponEquipped;
        PlayerRuntimeState.UnlockWeapon();
        ClampSavedStageIndex();
        ApplyCurrentStageAnimatorController();

        if (!wasEquipped)
        {
            OnWeaponChanged?.Invoke(weaponData);
        }

        OnWeaponStageChanged?.Invoke(CurrentWeaponStage);
    }

    public void SetWeaponStage(int stageIndex)
    {
        if (!IsWeaponEquipped || weaponData == null) return;

        WeaponStageData[] weaponStages = weaponData.WeaponStages;
        if (weaponStages == null ||
            stageIndex < 0 ||
            stageIndex >= weaponStages.Length ||
            weaponStages[stageIndex] == null ||
            CurrentWeaponStageIndex == stageIndex)
        {
            return;
        }

        PlayerRuntimeState.SetWeaponStage(stageIndex);
        ApplyCurrentStageAnimatorController();
        OnWeaponStageChanged?.Invoke(CurrentWeaponStage);
    }

    public void AdvanceWeaponStage()
    {
        SetWeaponStage(CurrentWeaponStageIndex + 1);
    }

    private void ClampSavedStageIndex()
    {
        WeaponStageData[] weaponStages = weaponData.WeaponStages;
        if (weaponStages == null || weaponStages.Length == 0)
        {
            PlayerRuntimeState.SetWeaponStage(0);
            return;
        }

        PlayerRuntimeState.SetWeaponStage(
            Mathf.Clamp(CurrentWeaponStageIndex, 0, weaponStages.Length - 1));
    }

    private void ApplyCurrentStageAnimatorController()
    {
        if (animator == null) return;

        AnimatorOverrideController overrideController =
            CurrentWeaponStage?.AnimatorOverrideController;

        RuntimeAnimatorController targetController =
            overrideController != null
                ? overrideController
                : defaultAnimatorController;

        WeaponSkinData skin = CurrentSkin;
        if (IsWeaponEquipped && PlayerPermanentProgress.OwnsSkin(skin) &&
            skin.IsCompatibleWith(weaponData, CurrentWeaponStage, out _))
            targetController = skin.AnimationController;

        if (animator.runtimeAnimatorController != targetController)
        {
            animator.runtimeAnimatorController = targetController;
        }
    }

    public bool CanUseSkin(WeaponSkinData skin) => skin != null && weaponData != null &&
        weaponData.WeaponStages.Length > 0 &&
        skin.IsCompatibleWith(weaponData, CurrentWeaponStage ?? weaponData.WeaponStages[0], out _);

    public bool TryEquipSkin(WeaponSkinData skin)
    {
        if (!CanUseSkin(skin) || !PlayerPermanentProgress.OwnsSkin(skin)) return false;
        GetComponent<Player>()?.CancelCombatActions();
        return PlayerPermanentProgress.TryEquipSkin(skin);
    }
}

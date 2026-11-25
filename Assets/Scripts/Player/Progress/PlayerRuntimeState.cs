using UnityEngine;

public static class PlayerRuntimeState
{
    public static bool IsInitialized { get; private set; }
    public static int CurrentHP { get; private set; }
    public static bool HasWeapon { get; private set; }
    public static int WeaponStageIndex { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        ResetRunProgress();
    }

    public static void InitializeIfNeeded(int maxHP)
    {
        if (IsInitialized) return;

        CurrentHP = Mathf.Max(maxHP, 0);
        IsInitialized = true;
    }

    public static void SetHealth(int currentHP)
    {
        CurrentHP = Mathf.Max(currentHP, 0);
    }

    public static void UnlockWeapon()
    {
        if (HasWeapon) return;
        HasWeapon = true;
    }

    public static void SetWeaponStage(int stageIndex)
    {
        int next = Mathf.Max(stageIndex, 0);
        if (WeaponStageIndex == next) return;
        WeaponStageIndex = next;
    }

    public static void ResetRunProgress()
    {
        IsInitialized = false;
        CurrentHP = 0;
        HasWeapon = false;
        WeaponStageIndex = 0;
    }
}

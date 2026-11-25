using System;
using System.Collections.Generic;
using UnityEngine;

// Persistent rewards and upgrades. Current HP and weapon state belong to PlayerRuntimeState.
public static class PlayerPermanentProgress
{
    public static event Action<int> ScrapChanged;
    public static event Action<int> AttackPowerBonusChanged;
    public static event Action<int> MaxHealthIncreased;
    public static event Action UpgradesChanged;
    public static event Action WeaponSkinsChanged;
    private static readonly HashSet<string> ownedWeaponSkins = new HashSet<string>();
    private static readonly HashSet<CutsceneId> watchedCutscenes = new HashSet<CutsceneId>();
    public static string EquippedWeaponSkin { get; private set; } = string.Empty;
    public static int Scrap { get; private set; }
    public static int AttackPowerBonus { get; private set; }
    public static int UpgradeMaterials { get; private set; }
    public static int BonusMaxHP { get; private set; }
    public static float HitInvincibilityBonus { get; private set; }
    public static bool HasUpgradeMaterials => UpgradeMaterials >= UpgradeSettings.Shared.MaterialsPerUpgrade;
    public static bool IsAttackMaxed => AttackPowerBonus >= UpgradeSettings.Shared.MaxAttackPowerBonus;
    public static bool IsHealthMaxed => BonusMaxHP >= UpgradeSettings.Shared.MaxHealthBonus;
    public static bool IsHitInvincibilityMaxed => HitInvincibilityBonus >= UpgradeSettings.Shared.MaxHitInvincibilityBonus;
    public static bool CanPurchaseMaterial => !HasUpgradeMaterials && !(IsAttackMaxed && IsHealthMaxed && IsHitInvincibilityMaxed)
        && Scrap >= UpgradeSettings.Shared.MaterialScrapCost;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Scrap = AttackPowerBonus = UpgradeMaterials = BonusMaxHP = 0;
        HitInvincibilityBonus = 0f;
        ScrapChanged = null;
        AttackPowerBonusChanged = null;
        MaxHealthIncreased = null;
        UpgradesChanged = null;
        ownedWeaponSkins.Clear();
        watchedCutscenes.Clear();
        EquippedWeaponSkin = string.Empty;
        WeaponSkinsChanged = null;
    }

    internal static void Restore(PlayerProgressData progress)
    {
        UpgradeSettings settings = UpgradeSettings.Shared;
        Scrap = progress.scrap;
        AttackPowerBonus = Mathf.Clamp(progress.attackPowerBonus, 0, settings.MaxAttackPowerBonus);
        // Version 1 stored completed health upgrades and leftover pieces together.
        BonusMaxHP = progress.version == 1
            ? (int)Math.Min(settings.MaxHealthBonus, (long)(progress.healthPieces / settings.MaterialsPerUpgrade) * settings.MaxHealthIncrease)
            : Mathf.Clamp(progress.bonusMaxHP, 0, settings.MaxHealthBonus);
        UpgradeMaterials = Mathf.Clamp(progress.version == 1
            ? progress.healthPieces % settings.MaterialsPerUpgrade : progress.upgradeMaterials, 0, settings.MaterialsPerUpgrade);
        HitInvincibilityBonus = Mathf.Clamp(progress.hitInvincibilityBonus, 0f, settings.MaxHitInvincibilityBonus);
        ownedWeaponSkins.Clear();
        if (progress.ownedWeaponSkins != null)
            foreach (string id in progress.ownedWeaponSkins) ownedWeaponSkins.Add(id);
        EquippedWeaponSkin = progress.equippedWeaponSkin ?? string.Empty;
        watchedCutscenes.Clear();
        if (progress.watchedCutscenes != null)
            foreach (CutsceneId id in progress.watchedCutscenes) watchedCutscenes.Add(id);
    }

    public static PlayerProgressData CreateSnapshot() => new PlayerProgressData
    {
        scrap = Scrap,
        attackPowerBonus = AttackPowerBonus,
        bonusMaxHP = BonusMaxHP,
        upgradeMaterials = UpgradeMaterials,
        hitInvincibilityBonus = HitInvincibilityBonus,
        ownedWeaponSkins = new List<string>(ownedWeaponSkins).ToArray(),
        equippedWeaponSkin = EquippedWeaponSkin,
        watchedCutscenes = new List<CutsceneId>(watchedCutscenes).ToArray()
    };

    public static bool HasWatchedCutscene(CutsceneId id) => id != CutsceneId.None && watchedCutscenes.Contains(id);

    public static void MarkCutsceneWatched(CutsceneId id)
    {
        if (id == CutsceneId.None || !Enum.IsDefined(typeof(CutsceneId), id) || !watchedCutscenes.Add(id)) return;
        PlayerProgressPersistence.SaveProgress();
    }

    public static void AddScrap(int amount)
    {
        if (amount <= 0) return;
        int added = Mathf.Min(amount, int.MaxValue - Scrap);
        if (added == 0) return;
        Scrap += added;
        PlayerProgressPersistence.SaveProgress();
        ScrapChanged?.Invoke(Scrap);
    }

    public static bool TryPurchaseMaterial()
    {
        if (!CanPurchaseMaterial) return false;
        Scrap -= UpgradeSettings.Shared.MaterialScrapCost;
        UpgradeMaterials++;
        PlayerProgressPersistence.SaveProgress();
        ScrapChanged?.Invoke(Scrap);
        UpgradesChanged?.Invoke();
        return true;
    }

    public static bool TryPurchaseAttackUpgrade()
    {
        if (!HasUpgradeMaterials || IsAttackMaxed) return false;
        UpgradeSettings settings = UpgradeSettings.Shared;
        AttackPowerBonus += Mathf.Min(settings.AttackPowerIncrease, settings.MaxAttackPowerBonus - AttackPowerBonus);
        ConsumeMaterials();
        AttackPowerBonusChanged?.Invoke(AttackPowerBonus);
        return true;
    }

    public static bool TryPurchaseHealthUpgrade()
    {
        if (!HasUpgradeMaterials || IsHealthMaxed) return false;
        UpgradeSettings settings = UpgradeSettings.Shared;
        int increase = Mathf.Min(settings.MaxHealthIncrease, settings.MaxHealthBonus - BonusMaxHP);
        BonusMaxHP += increase;
        ConsumeMaterials();
        MaxHealthIncreased?.Invoke(increase);
        return true;
    }

    public static bool TryPurchaseHitInvincibilityUpgrade()
    {
        if (!HasUpgradeMaterials || IsHitInvincibilityMaxed) return false;
        UpgradeSettings settings = UpgradeSettings.Shared;
        float next = HitInvincibilityBonus + settings.HitInvincibilityIncrease;
        // Snap the last step to the cap rather than requiring an extra purchase for float rounding.
        HitInvincibilityBonus = next >= settings.MaxHitInvincibilityBonus - 0.000001f
            ? settings.MaxHitInvincibilityBonus : next;
        ConsumeMaterials();
        return true;
    }

    private static void ConsumeMaterials()
    {
        UpgradeMaterials = 0;
        PlayerProgressPersistence.SaveProgress();
        UpgradesChanged?.Invoke();
    }

    public static bool OwnsSkin(WeaponSkinData skin) => skin != null &&
        (skin.UnlockedByDefault || ownedWeaponSkins.Contains(skin.SkinId));

    public static bool TryPurchaseSkin(WeaponSkinData skin)
    {
        if (skin == null || string.IsNullOrWhiteSpace(skin.SkinId) || OwnsSkin(skin) || Scrap < skin.ScrapCost) return false;
        Scrap -= skin.ScrapCost;
        ownedWeaponSkins.Add(skin.SkinId);
        PlayerProgressPersistence.SaveProgress();
        ScrapChanged?.Invoke(Scrap);
        WeaponSkinsChanged?.Invoke();
        return true;
    }

    public static bool TryEquipSkin(WeaponSkinData skin)
    {
        if (!OwnsSkin(skin) || string.IsNullOrWhiteSpace(skin.SkinId)) return false;
        if (EquippedWeaponSkin == skin.SkinId) return true;
        EquippedWeaponSkin = skin.SkinId;
        PlayerProgressPersistence.SaveProgress();
        WeaponSkinsChanged?.Invoke();
        return true;
    }
}

using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class UpgradeSettingsTests
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private UpgradeSettings originalSettings;
    private UpgradeSettings settings;
    private PlayerProgressData originalProgress;
    private bool originalCanSave;

    [SetUp]
    public void SetUp()
    {
        originalSettings = UpgradeSettings.Shared;
        originalProgress = PlayerPermanentProgress.CreateSnapshot();
        originalCanSave = (bool)typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic).GetValue(null);
        typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic).SetValue(null, false);
        settings = ScriptableObject.CreateInstance<UpgradeSettings>();
        typeof(UpgradeSettings).GetField("shared", PrivateStatic).SetValue(null, settings);
        Restore(new PlayerProgressData());
    }

    [TearDown]
    public void TearDown()
    {
        typeof(UpgradeSettings).GetField("shared", PrivateStatic).SetValue(null, originalSettings);
        Restore(originalProgress);
        typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic).SetValue(null, originalCanSave);
        Object.DestroyImmediate(settings);
    }

    private static void Restore(PlayerProgressData progress) => typeof(PlayerPermanentProgress)
        .GetMethod("Restore", PrivateStatic).Invoke(null, new object[] { progress });

    private static void FillMaterials()
    {
        for (int i = 0; i < UpgradeSettings.Shared.MaterialsPerUpgrade; i++)
            Assert.That(PlayerPermanentProgress.TryPurchaseMaterial(), Is.True);
    }

    [Test]
    public void MaterialsCostFiveEachStopAtThreeAndDoNotUpgradeAutomatically()
    {
        Restore(new PlayerProgressData { scrap = 100 });
        FillMaterials();
        Assert.That(PlayerPermanentProgress.Scrap, Is.EqualTo(85));
        Assert.That(PlayerPermanentProgress.UpgradeMaterials, Is.EqualTo(3));
        Assert.That(PlayerPermanentProgress.BonusMaxHP, Is.Zero);
        Assert.That(PlayerPermanentProgress.AttackPowerBonus, Is.Zero);
        Assert.That(PlayerPermanentProgress.TryPurchaseMaterial(), Is.False);
        Assert.That(PlayerPermanentProgress.Scrap, Is.EqualTo(85));
    }

    [Test]
    public void IncompleteMaterialsAndInsufficientScrapDoNotChangeProgress()
    {
        Restore(new PlayerProgressData { scrap = 4, upgradeMaterials = 2 });
        Assert.That(PlayerPermanentProgress.TryPurchaseMaterial(), Is.False);
        Assert.That(PlayerPermanentProgress.TryPurchaseAttackUpgrade(), Is.False);
        Assert.That(PlayerPermanentProgress.TryPurchaseHealthUpgrade(), Is.False);
        Assert.That(PlayerPermanentProgress.TryPurchaseHitInvincibilityUpgrade(), Is.False);
        Assert.That(PlayerPermanentProgress.UpgradeMaterials, Is.EqualTo(2));
        Assert.That(PlayerPermanentProgress.Scrap, Is.EqualTo(4));
    }

    [Test]
    public void EachUpgradeConsumesAllMaterialsAndPriceRemainsFixed()
    {
        Restore(new PlayerProgressData { scrap = 100 });
        FillMaterials();
        Assert.That(PlayerPermanentProgress.TryPurchaseAttackUpgrade(), Is.True);
        Assert.That(PlayerPermanentProgress.AttackPowerBonus, Is.EqualTo(1));
        Assert.That(PlayerPermanentProgress.UpgradeMaterials, Is.Zero);
        FillMaterials();
        int gainedHealth = 0;
        System.Action<int> listener = amount => gainedHealth += amount;
        PlayerPermanentProgress.MaxHealthIncreased += listener;
        try { Assert.That(PlayerPermanentProgress.TryPurchaseHealthUpgrade(), Is.True); }
        finally { PlayerPermanentProgress.MaxHealthIncreased -= listener; }
        Assert.That(gainedHealth, Is.EqualTo(1));
        Assert.That(PlayerPermanentProgress.BonusMaxHP, Is.EqualTo(1));
        FillMaterials();
        Assert.That(PlayerPermanentProgress.TryPurchaseHitInvincibilityUpgrade(), Is.True);
        Assert.That(PlayerPermanentProgress.HitInvincibilityBonus, Is.EqualTo(0.1f));
        Assert.That(PlayerPermanentProgress.UpgradeMaterials, Is.Zero);
        Assert.That(PlayerPermanentProgress.Scrap, Is.EqualTo(55));
        Assert.That(settings.MaterialScrapCost, Is.EqualTo(5));
    }

    [Test]
    public void UpgradesStopAtCapsAndRejectedPurchasesKeepMaterials()
    {
        Restore(new PlayerProgressData { attackPowerBonus = 10, bonusMaxHP = 10,
            hitInvincibilityBonus = 1f, upgradeMaterials = 3, scrap = 100 });
        Assert.That(PlayerPermanentProgress.TryPurchaseAttackUpgrade(), Is.False);
        Assert.That(PlayerPermanentProgress.TryPurchaseHealthUpgrade(), Is.False);
        Assert.That(PlayerPermanentProgress.TryPurchaseHitInvincibilityUpgrade(), Is.False);
        Assert.That(PlayerPermanentProgress.UpgradeMaterials, Is.EqualTo(3));
        Assert.That(PlayerPermanentProgress.TryPurchaseMaterial(), Is.False);
        Restore(new PlayerProgressData { attackPowerBonus = 10, bonusMaxHP = 10, hitInvincibilityBonus = 1f, scrap = 100 });
        Assert.That(PlayerPermanentProgress.TryPurchaseMaterial(), Is.False);
    }

    [Test]
    public void TenInvincibilityUpgradesReachExactlyOneSecond()
    {
        Restore(new PlayerProgressData { scrap = 200 });
        for (int i = 0; i < 10; i++)
        {
            FillMaterials();
            Assert.That(PlayerPermanentProgress.TryPurchaseHitInvincibilityUpgrade(), Is.True);
        }
        Assert.That(PlayerPermanentProgress.HitInvincibilityBonus, Is.EqualTo(1f));
        Assert.That(PlayerPermanentProgress.IsHitInvincibilityMaxed, Is.True);
    }

    [Test]
    public void LegacyHealthPiecesBecomeHealthBonusAndRemainingSharedMaterials()
    {
        Restore(new PlayerProgressData { version = 1, scrap = 31, attackPowerBonus = 4, healthPieces = 7 });
        Assert.That(PlayerPermanentProgress.BonusMaxHP, Is.EqualTo(2));
        Assert.That(PlayerPermanentProgress.UpgradeMaterials, Is.EqualTo(1));
        PlayerProgressData saved = PlayerProgressStore.Deserialize(JsonUtility.ToJson(PlayerPermanentProgress.CreateSnapshot()));
        Assert.That(saved.version, Is.EqualTo(2));
        Assert.That(PlayerProgressStore.IsValid(saved), Is.True);
        Restore(saved);
        Assert.That(PlayerPermanentProgress.BonusMaxHP, Is.EqualTo(2));
        Assert.That(PlayerPermanentProgress.UpgradeMaterials, Is.EqualTo(1));
        Assert.That(PlayerPermanentProgress.AttackPowerBonus, Is.EqualTo(4));
    }

    [Test]
    public void HitInvincibilityBonusAndPartialMaterialsRoundTrip()
    {
        Restore(new PlayerProgressData { upgradeMaterials = 2, bonusMaxHP = 7, hitInvincibilityBonus = 0.4f });
        var saved = PlayerProgressStore.Deserialize(JsonUtility.ToJson(PlayerPermanentProgress.CreateSnapshot()));
        Restore(saved);
        Assert.That(PlayerPermanentProgress.UpgradeMaterials, Is.EqualTo(2));
        Assert.That(PlayerPermanentProgress.BonusMaxHP, Is.EqualTo(7));
        Assert.That(PlayerPermanentProgress.HitInvincibilityBonus, Is.EqualTo(0.4f));
        Assert.That(PlayerProgressStore.IsValid(new PlayerProgressData { hitInvincibilityBonus = float.NaN }), Is.False);
    }

    [Test]
    public void DamageStartsProtectionBeforeCallbacksAndIgnoresFollowupHitsUntilExpiry()
    {
        Restore(new PlayerProgressData { hitInvincibilityBonus = 0.1f });
        WithHealth(health =>
        {
            bool nestedHitAccepted = true;
            System.Action<int> callback = _ => nestedHitAccepted = health.TryTakeDamage(new DamageInfo(1));
            health.Damaged += callback;
            Assert.That(health.TryTakeDamage(new DamageInfo(1)), Is.True);
            health.Damaged -= callback;
            Assert.That(nestedHitAccepted, Is.False);
            Assert.That(health.CurrentHP, Is.EqualTo(19));
            Assert.That(health.IsInvincible, Is.True);
            Assert.That(health.TryReceiveHit(), Is.False);
            Assert.That(health.TryTakeDamage(new DamageInfo(1)), Is.False);
            typeof(PlayerHealth).GetField("hitInvincibilityEndsAt", PrivateInstance).SetValue(health, Time.time - 1f);
            Assert.That(health.TryTakeDamage(new DamageInfo(1)), Is.True);
            Assert.That(health.TryTakeDamage(new DamageInfo(1, true)), Is.True);
        });
    }

    [Test]
    public void BaseZeroAllowsConsecutiveHitsAndZeroDamageDoesNotStartProtection()
    {
        WithHealth(health =>
        {
            Assert.That(health.HitInvincibilityDuration, Is.Zero);
            Assert.That(health.TryTakeDamage(new DamageInfo(0)), Is.False);
            Assert.That(health.IsInvincible, Is.False);
            Assert.That(health.TryTakeDamage(new DamageInfo(1)), Is.True);
            Assert.That(health.TryTakeDamage(new DamageInfo(1)), Is.True);
            Assert.That(health.CurrentHP, Is.EqualTo(18));
        });
    }

    private static void WithHealth(System.Action<PlayerHealth> check)
    {
        int originalHP = PlayerRuntimeState.CurrentHP;
        var root = new GameObject("Hit invincibility test");
        root.SetActive(false);
        var data = ScriptableObject.CreateInstance<PlayerData>();
        try
        {
            typeof(PlayerData).GetField("maxHP", PrivateInstance).SetValue(data, 20);
            var player = root.AddComponent<Player>();
            typeof(Player).GetField("data", PrivateInstance).SetValue(player, data);
            var health = root.GetComponent<PlayerHealth>();
            typeof(PlayerHealth).GetField("player", PrivateInstance).SetValue(health, player);
            typeof(PlayerHealth).GetField("currentHP", PrivateInstance).SetValue(health, 20);
            root.SetActive(true);
            check(health);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(data);
            PlayerRuntimeState.SetHealth(originalHP);
        }
    }
}

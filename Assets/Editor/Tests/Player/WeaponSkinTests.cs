using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WeaponSkinTests
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private PlayerProgressData original;
    private bool canSave;
    private WeaponSkinData skin;
    private WeaponSkinCatalog catalog;

    [SetUp]
    public void SetUp()
    {
        original = PlayerPermanentProgress.CreateSnapshot();
        canSave = (bool)typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic).GetValue(null);
        typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic).SetValue(null, false);
        catalog = AssetDatabase.LoadAssetAtPath<WeaponSkinCatalog>("Assets/Data/Weapon/WeaponSkinCatalog.asset");
        skin = Object.Instantiate(catalog.DefaultSkin);
        var data = new SerializedObject(skin);
        data.FindProperty("skinId").stringValue = "test-paid-skin";
        data.FindProperty("scrapCost").intValue = 5;
        data.FindProperty("unlockedByDefault").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        Restore(new PlayerProgressData { scrap = 10, attackPowerBonus = 3, bonusMaxHP = 4,
            hitInvincibilityBonus = 0.2f, upgradeMaterials = 2 });
    }

    [TearDown]
    public void TearDown()
    {
        Restore(original);
        typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic).SetValue(null, canSave);
        Object.DestroyImmediate(skin);
    }

    private static void Restore(PlayerProgressData progress) => typeof(PlayerPermanentProgress)
        .GetMethod("Restore", PrivateStatic).Invoke(null, new object[] { progress });

    [Test]
    public void PurchaseChargesOnceAndDoesNotChangeCombatBonusesOrMaterials()
    {
        Assert.That(PlayerPermanentProgress.TryEquipSkin(skin), Is.False);
        Assert.That(PlayerPermanentProgress.TryPurchaseSkin(skin), Is.True);
        Assert.That(PlayerPermanentProgress.TryPurchaseSkin(skin), Is.False);
        Assert.That(PlayerPermanentProgress.TryEquipSkin(skin), Is.True);
        Assert.That(PlayerPermanentProgress.Scrap, Is.EqualTo(5));
        Assert.That(PlayerPermanentProgress.AttackPowerBonus, Is.EqualTo(3));
        Assert.That(PlayerPermanentProgress.BonusMaxHP, Is.EqualTo(4));
        Assert.That(PlayerPermanentProgress.HitInvincibilityBonus, Is.EqualTo(0.2f));
        Assert.That(PlayerPermanentProgress.UpgradeMaterials, Is.EqualTo(2));
    }

    [Test]
    public void InsufficientScrapDoesNotUnlockOrEquipSkin()
    {
        Restore(new PlayerProgressData { scrap = 4 });
        Assert.That(PlayerPermanentProgress.TryPurchaseSkin(skin), Is.False);
        Assert.That(PlayerPermanentProgress.OwnsSkin(skin), Is.False);
        Assert.That(PlayerPermanentProgress.Scrap, Is.EqualTo(4));
    }

    [Test]
    public void OwnedAndEquippedSkinsRoundTripAndOldSavesRemainValid()
    {
        PlayerPermanentProgress.TryPurchaseSkin(skin);
        PlayerPermanentProgress.TryEquipSkin(skin);
        var saved = PlayerProgressStore.Deserialize(JsonUtility.ToJson(PlayerPermanentProgress.CreateSnapshot()));
        Assert.That(PlayerProgressStore.IsValid(saved), Is.True);
        Restore(saved);
        Assert.That(PlayerPermanentProgress.OwnsSkin(skin), Is.True);
        Assert.That(PlayerPermanentProgress.EquippedWeaponSkin, Is.EqualTo(skin.SkinId));
        var old = PlayerProgressStore.Deserialize("{\"version\":2,\"scrap\":10}");
        Assert.That(PlayerProgressStore.IsValid(old), Is.True);
        Restore(old);
        Assert.That(PlayerPermanentProgress.OwnsSkin(catalog.DefaultSkin), Is.True);
        Assert.That(PlayerPermanentProgress.OwnsSkin(skin), Is.False);
    }

    [Test]
    public void SkinChangingClipDurationIsRejected()
    {
        var originalController = skin.AnimationController;
        var changed = Object.Instantiate(originalController);
        var clip = new AnimationClip();
        clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 2, 1));
        try
        {
            var data = new SerializedObject(skin);
            data.FindProperty("animationController").objectReferenceValue = changed;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(skin.IsCompatibleWith(skin.Weapon, skin.Weapon.WeaponStages[0], out _), Is.True);
            var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
            changed.GetOverrides(pairs);
            changed[pairs[0].Key] = clip;
            Assert.That(skin.IsCompatibleWith(skin.Weapon, skin.Weapon.WeaponStages[0], out _), Is.False);
        }
        finally { Object.DestroyImmediate(changed); Object.DestroyImmediate(clip); }
    }

    [Test]
    public void SkinChangingAnimationEventsIsRejected()
    {
        var changed = Object.Instantiate(skin.AnimationController);
        var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
        changed.GetOverrides(pairs);
        AnimationClip clip = Object.Instantiate(pairs[0].Value ?? pairs[0].Key);
        try
        {
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { functionName = "UnexpectedDamage", time = 0f } });
            changed[pairs[0].Key] = clip;
            var data = new SerializedObject(skin);
            data.FindProperty("animationController").objectReferenceValue = changed;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(skin.IsCompatibleWith(skin.Weapon, skin.Weapon.WeaponStages[0], out _), Is.False);
        }
        finally { Object.DestroyImmediate(changed); Object.DestroyImmediate(clip); }
    }
}

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorkshopUpgradeUITests
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private PlayerProgressData originalProgress;
    private bool originalCanSave;
    private GameObject root;
    private UpgradeShopView view;

    [SetUp]
    public void SetUp()
    {
        originalProgress = PlayerPermanentProgress.CreateSnapshot();
        originalCanSave = (bool)typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic).GetValue(null);
        typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic).SetValue(null, false);
        root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Canvas.prefab");
        view = root.GetComponentInChildren<UpgradeShopView>(true);
    }

    [TearDown]
    public void TearDown()
    {
        Restore(originalProgress);
        typeof(PlayerProgressPersistence).GetField("canSave", PrivateStatic).SetValue(null, originalCanSave);
        if (root != null) PrefabUtility.UnloadPrefabContents(root);
    }

    private static void Restore(PlayerProgressData progress) => typeof(PlayerPermanentProgress)
        .GetMethod("Restore", PrivateStatic).Invoke(null, new object[] { progress });

    [Test]
    public void UpgradePageIsUniqueAndAllViewBindingsBelongToIt()
    {
        Transform card = view.transform.Find("Card");
        Transform[] layouts = card.Cast<Transform>().Where(child => child.name == "UpgradeLayout").ToArray();
        Assert.That(layouts.Length, Is.EqualTo(1), "Duplicate pages must not cover the wired purchase buttons.");
        Transform page = layouts[0];
        Assert.That(view.HasRequiredReferences, Is.True);
        var fields = new SerializedObject(view).GetIterator();
        while (fields.Next(true))
        {
            if (fields.propertyType != SerializedPropertyType.ObjectReference || fields.propertyPath.StartsWith("m_")) continue;
            var target = fields.objectReferenceValue as Component;
            if (target != null)
                Assert.That(target.transform.IsChildOf(page), Is.True, fields.propertyPath + " must use the visible upgrade page.");
        }
        var skins = view.GetComponent<WeaponSkinShop>();
        Assert.That(new SerializedObject(skins).FindProperty("upgradePage").objectReferenceValue, Is.EqualTo(page.gameObject));
        skins.ShowSkins();
        Assert.That(page.gameObject.activeSelf, Is.False, "Switching to skins must hide the whole upgrade UI.");
        skins.ShowUpgrades();
        Assert.That(page.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void MaterialPurchasesEnableSelectedUpgradeConfirmation()
    {
        Restore(new PlayerProgressData { scrap = 20, upgradeMaterials = 1, attackPowerBonus = 6 });
        view.Refresh();
        var data = new SerializedObject(view);
        var slots = data.FindProperty("materialSlots");
        var purchase = (Button)data.FindProperty("materialPurchaseButton").objectReferenceValue;
        var confirm = (Button)data.FindProperty("confirmButton").objectReferenceValue;
        var selectedHeading = (Text)data.FindProperty("selectedHeading").objectReferenceValue;
        Button[] upgrades = new[] { "attackPurchaseLabel", "healthPurchaseLabel", "hitInvincibilityPurchaseLabel" }
            .Select(name => ((Text)data.FindProperty(name).objectReferenceValue).GetComponentInParent<Button>(true)).ToArray();
        Vector2 purchasePosition = ((RectTransform)purchase.transform).anchoredPosition;
        Assert.That(view.HasRequiredReferences, Is.True);
        Assert.That(purchase.gameObject.activeSelf, Is.True);
        Assert.That(purchase.interactable, Is.True);
        Assert.That(upgrades.All(button => button.gameObject.activeSelf && button.interactable), Is.True);
        Assert.That(confirm.gameObject.activeSelf, Is.True);
        Assert.That(confirm.interactable, Is.False, "Incomplete materials must disable confirmation without hiding upgrade choices.");
        view.SelectUpgrade(UpgradeKind.Health);
        Assert.That(view.SelectedUpgrade, Is.EqualTo(UpgradeKind.Health));
        Assert.That(selectedHeading.text, Is.EqualTo("체력"));
        Assert.That(confirm.interactable, Is.False);
        view.SelectUpgrade(UpgradeKind.Attack);
        Assert.That(selectedHeading.text, Is.EqualTo("공격력"));
        Assert.That(((Text)data.FindProperty("materialPurchaseLabel").objectReferenceValue).text, Is.EqualTo("재료 구매"));
        var held = (Image)slots.GetArrayElementAtIndex(0).objectReferenceValue;
        var empty = (Image)slots.GetArrayElementAtIndex(1).objectReferenceValue;
        Assert.That(held.color.a, Is.GreaterThan(empty.color.a));
        var price = (Text)data.FindProperty("materialPriceLabel").objectReferenceValue;
        Assert.That(price.text, Is.EqualTo(UpgradeSettings.Shared.MaterialScrapCost.ToString()));
        Assert.That(price.transform.IsChildOf(purchase.transform), Is.True);
        Assert.That(held.sprite, Is.Not.EqualTo(price.transform.parent.Find("ScrapIcon").GetComponent<Image>().sprite));
        Assert.That(held.raycastTarget, Is.False);
        Assert.That(empty.raycastTarget, Is.False);

        Assert.That(PlayerPermanentProgress.TryPurchaseMaterial(), Is.True);
        view.Refresh();
        Assert.That(PlayerPermanentProgress.Scrap, Is.EqualTo(20 - UpgradeSettings.Shared.MaterialScrapCost));
        Assert.That(confirm.interactable, Is.False);
        Assert.That(((RectTransform)purchase.transform).anchoredPosition,
            Is.EqualTo(purchasePosition));
        Assert.That(PlayerPermanentProgress.TryPurchaseMaterial(), Is.True);
        view.Refresh();
        Assert.That(purchase.gameObject.activeSelf, Is.True);
        Assert.That(purchase.interactable, Is.False);
        Assert.That(upgrades.All(button => button.gameObject.activeSelf), Is.True);
        Assert.That(confirm.interactable, Is.True, "Complete materials must enable the selected upgrade.");
        Assert.That(PlayerPermanentProgress.TryPurchaseAttackUpgrade(), Is.True);
        view.Refresh();
        Assert.That(PlayerPermanentProgress.UpgradeMaterials, Is.Zero);
        Assert.That(purchase.gameObject.activeSelf, Is.True);
        Assert.That(purchase.interactable, Is.True);
        Assert.That(upgrades.All(button => button.gameObject.activeSelf), Is.True);
        Assert.That(confirm.interactable, Is.False, "Consuming the materials must disable confirmation again.");
        Assert.That(((RectTransform)purchase.transform).anchoredPosition,
            Is.EqualTo(purchasePosition));
        var triangle = (UpgradeTriangleFrame)data.FindProperty("triangle").objectReferenceValue;
        Assert.That(new SerializedObject(triangle).FindProperty("progress").vector3Value.x,
            Is.EqualTo(7f / UpgradeSettings.Shared.MaxAttackPowerBonus).Within(0.0001f));
    }

    [Test]
    public void InsufficientScrapDisablesPurchaseButKeepsPriceVisible()
    {
        Restore(new PlayerProgressData { scrap = 0 });
        view.Refresh();
        var data = new SerializedObject(view);
        var purchase = (Button)data.FindProperty("materialPurchaseButton").objectReferenceValue;
        Assert.That(purchase.interactable, Is.False);
        var price = (Text)data.FindProperty("materialPriceLabel").objectReferenceValue;
        Assert.That(price.gameObject.activeSelf, Is.True);
        Assert.That(price.text, Is.EqualTo(UpgradeSettings.Shared.MaterialScrapCost.ToString()));
        Assert.That(purchase.image.color.a, Is.GreaterThan(0));
        Assert.That(purchase.image.sprite, Is.Not.Null);
        var frames = data.FindProperty("materialFrames");
        for (int i = 0; i < frames.arraySize; i++)
            Assert.That(frames.GetArrayElementAtIndex(i).objectReferenceValue, Is.TypeOf<UpgradeMaterialGlow>());
    }

    [Test]
    public void WorkshopHeaderAndFooterKeepFramePadding()
    {
        var card = (RectTransform)view.transform.Find("Card");
        Vector3[] CornersInCard(string name)
        {
            var rect = (RectTransform)card.Find(name);
            var corners = new Vector3[4];
            rect.GetLocalCorners(corners);
            // Prefab preview Canvas scale can be zero before rendering; use child-to-card coordinates.
            var toCard = Matrix4x4.TRS(rect.localPosition, rect.localRotation, rect.localScale);
            return corners.Select(toCard.MultiplyPoint3x4).ToArray();
        }
        foreach (string name in new[] { "TitlePlate", "PageToggle", "Close" })
        {
            foreach (Vector3 point in CornersInCard(name))
            {
                Assert.That(point.x, Is.InRange(card.rect.xMin + 16f, card.rect.xMax - 16f), name);
                Assert.That(point.y, Is.InRange(card.rect.yMin + 16f, card.rect.yMax - 16f), name);
            }
        }
        Assert.That(CornersInCard("TitlePlate").Min(point => point.y),
            Is.GreaterThan(CornersInCard("HeaderSeam").Max(point => point.y)), "The title must clear the header seam.");
        Assert.That(CornersInCard("Close").Max(point => point.y),
            Is.LessThan(CornersInCard("BottomSeam").Min(point => point.y)), "The close button must clear the footer seam.");
        var press = card.Find("Close").GetComponent<UIButtonPressFeedback>();
        float depth = new SerializedObject(press).FindProperty("depth").floatValue;
        Assert.That(CornersInCard("Close").Min(point => point.y) - depth,
            Is.GreaterThanOrEqualTo(card.rect.yMin + 16f), "The pressed button must keep its bottom padding.");
        Assert.That(((RectTransform)card.Find("Title")).anchoredPosition.y,
            Is.EqualTo(((RectTransform)card.Find("TitlePlate")).anchoredPosition.y));
    }

    [Test]
    public void MaterialGlowHasNoOpaqueOuterBoundary()
    {
        var go = new GameObject("MaterialGlowTest", typeof(RectTransform), typeof(UpgradeMaterialGlow));
        try
        {
            var glow = go.GetComponent<UpgradeMaterialGlow>();
            ((RectTransform)go.transform).sizeDelta = new Vector2(100, 100);
            using (var helper = new VertexHelper())
            {
                typeof(UpgradeMaterialGlow).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Invoke(glow, new object[] { helper });
                int boundary = 0;
                for (int i = 0; i < helper.currentVertCount; i++)
                {
                    var vertex = new UIVertex();
                    helper.PopulateUIVertex(ref vertex, i);
                    if (vertex.position.magnitude < 49f) continue;
                    Assert.That(vertex.color.a, Is.Zero);
                    boundary++;
                }
                Assert.That(boundary, Is.GreaterThan(0));
            }
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void RadarConnectsOneEndpointPerNormalizedStat()
    {
        var go = new GameObject("TriangleGeometryTest", typeof(RectTransform), typeof(UpgradeTriangleFrame));
        try
        {
            var triangle = go.GetComponent<UpgradeTriangleFrame>();
            ((RectTransform)go.transform).sizeDelta = new Vector2(100, 100);
            var settings = new SerializedObject(triangle);
            settings.FindProperty("cornerRadius").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            triangle.SetProgress(new Vector3(0.6f, 0.4f, 0.3f));
            using (var helper = new VertexHelper())
            {
                typeof(UpgradeTriangleFrame).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Invoke(triangle, new object[] { helper });
                Color32 fill = new SerializedObject(triangle).FindProperty("fillColor").colorValue;
                var vertices = Enumerable.Range(0, helper.currentVertCount).Select(index =>
                {
                    var vertex = new UIVertex();
                    helper.PopulateUIVertex(ref vertex, index);
                    return vertex;
                }).Where(vertex => vertex.color.Equals(fill)).ToArray();
                Assert.That(vertices.Length, Is.EqualTo(3));
                float radius = 94f / Mathf.Sqrt(3f);
                var center = new Vector3(0f, -radius * 0.25f, 0f);
                Vector3 attack = center + new Vector3(-Mathf.Sqrt(3f) * 0.5f, -0.5f, 0f) * radius;
                Vector3 health = center + new Vector3(Mathf.Sqrt(3f) * 0.5f, -0.5f, 0f) * radius;
                Vector3 invincibility = center + Vector3.up * radius;
                Assert.That(Vector3.Distance(vertices[0].position, Vector3.Lerp(center, attack, 0.6f)), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(vertices[1].position, Vector3.Lerp(center, health, 0.4f)), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(vertices[2].position, Vector3.Lerp(center, invincibility, 0.3f)), Is.LessThan(0.001f));
            }
        }
        finally { Object.DestroyImmediate(go); }
    }
}

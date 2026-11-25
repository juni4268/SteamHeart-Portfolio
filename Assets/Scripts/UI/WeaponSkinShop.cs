using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class WeaponSkinShop : MonoBehaviour
{
    [SerializeField] private WeaponSkinCatalog catalog;
    [SerializeField] private GameObject upgradePage;
    [SerializeField] private GameObject skinPage;
    [UnityEngine.Serialization.FormerlySerializedAs("upgradeTab")]
    [SerializeField] private Button pageButton;
    [SerializeField] private Text pageButtonLabel;
    [SerializeField] private Text titleLabel;
    [SerializeField] private Text selectedName;
    [SerializeField] private WeaponSkinPreview preview;
    [SerializeField] private WeaponSkinShopRow rowTemplate;
    private readonly List<WeaponSkinShopRow> rows = new List<WeaponSkinShopRow>();
    private UpgradeShop shop;
    private WeaponSkinData selected;
    private bool initialized;

    public bool ShowingSkins { get; private set; }
    public bool HasRequiredReferences => catalog != null && catalog.DefaultSkin != null &&
        upgradePage != null && skinPage != null && pageButton != null && pageButtonLabel != null &&
        titleLabel != null && selectedName != null && preview != null && rowTemplate != null;

    private void OnEnable()
    {
        if (!HasRequiredReferences) return;
        if (!initialized)
        {
            pageButton.onClick.AddListener(TogglePage);
            rowTemplate.gameObject.SetActive(false);
            foreach (WeaponSkinData skin in catalog.Skins)
            {
                if (skin == null) continue;
                WeaponSkinShopRow row = Instantiate(rowTemplate, rowTemplate.transform.parent);
                row.Bind(skin, this);
                row.gameObject.SetActive(true);
                rows.Add(row);
            }
            initialized = true;
        }
        PlayerPermanentProgress.ScrapChanged += OnScrapChanged;
        PlayerPermanentProgress.WeaponSkinsChanged += Refresh;
    }

    public void Open(UpgradeShop owner)
    {
        shop = owner;
        ShowUpgrades();
    }

    private void OnDisable()
    {
        PlayerPermanentProgress.ScrapChanged -= OnScrapChanged;
        PlayerPermanentProgress.WeaponSkinsChanged -= Refresh;
    }

    public void ShowUpgrades() => SetPage(false);
    public void ShowSkins() => SetPage(true);
    public void TogglePage() => SetPage(!ShowingSkins);

    private void SetPage(bool skins)
    {
        ShowingSkins = skins;
        upgradePage.SetActive(!skins);
        skinPage.SetActive(skins);
        titleLabel.text = skins ? "무기 스킨" : "제작대";
        pageButtonLabel.text = skins ? "강화" : "무기 스킨";
        if (skins) Select(selected != null ? selected : catalog.Find(PlayerPermanentProgress.EquippedWeaponSkin) ?? catalog.DefaultSkin);
        Refresh();
        shop?.RefreshNavigation();
    }

    public void Select(WeaponSkinData skin)
    {
        selected = skin;
        selectedName.text = skin.DisplayName;
        if (skinPage.activeInHierarchy) preview.Show(catalog.GetIdle(skin));
        Refresh();
    }

    public void PurchaseOrEquip(WeaponSkinData skin)
    {
        if (shop == null || !shop.CanPurchase || shop.WeaponController == null || !shop.WeaponController.CanUseSkin(skin)) return;
        Select(skin);
        if (!PlayerPermanentProgress.OwnsSkin(skin)) PlayerPermanentProgress.TryPurchaseSkin(skin);
        else shop.WeaponController.TryEquipSkin(skin);
        Refresh();
        shop.RefreshNavigation();
    }

    private void OnScrapChanged(int amount) => Refresh();

    private void Refresh()
    {
        WeaponSkinData equipped = catalog.Find(PlayerPermanentProgress.EquippedWeaponSkin) ?? catalog.DefaultSkin;
        foreach (WeaponSkinShopRow row in rows)
            row.Refresh(row.Skin == selected, row.Skin == equipped,
                shop != null && shop.WeaponController != null && shop.WeaponController.CanUseSkin(row.Skin));
    }

    public void AddNavigationButtons(List<Button> buttons)
    {
        buttons.Add(pageButton);
        if (!ShowingSkins) return;
        foreach (WeaponSkinShopRow row in rows)
        {
            buttons.Add(row.SelectButton);
            buttons.Add(row.PurchaseButton);
        }
    }
}

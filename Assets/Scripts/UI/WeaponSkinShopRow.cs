using UnityEngine;
using UnityEngine.UI;

public sealed class WeaponSkinShopRow : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Image icon;
    [SerializeField] private Text nameLabel;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Text purchaseLabel;
    private WeaponSkinShop shop;
    public WeaponSkinData Skin { get; private set; }
    public Button SelectButton => selectButton;
    public Button PurchaseButton => purchaseButton;

    public void Bind(WeaponSkinData skin, WeaponSkinShop owner)
    {
        Skin = skin;
        shop = owner;
        icon.sprite = skin.Icon;
        nameLabel.text = skin.DisplayName;
        selectButton.onClick.AddListener(Select);
        purchaseButton.onClick.AddListener(Purchase);
    }

    public void Refresh(bool selected, bool equipped, bool compatible)
    {
        bool owned = PlayerPermanentProgress.OwnsSkin(Skin);
        purchaseLabel.text = !compatible ? "사용 불가" : equipped ? "장착 중" : owned ? "장착" : $"{Skin.ScrapCost} 고철 · 구매";
        purchaseButton.interactable = compatible && !equipped && (owned || PlayerPermanentProgress.Scrap >= Skin.ScrapCost);
        icon.color = selected ? Color.white : new Color(0.7f, 0.65f, 0.55f);
    }

    private void Select() => shop.Select(Skin);
    private void Purchase() => shop.PurchaseOrEquip(Skin);
}

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponSkinCatalog", menuName = "Project K/Weapon Skin Catalog")]
public sealed class WeaponSkinCatalog : ScriptableObject
{
    [SerializeField] private WeaponSkinData defaultSkin;
    [Tooltip("Explicit Idle motion from the shared Animator graph. The selected skin resolves its replacement automatically.")]
    [SerializeField] private AnimationClip idleMotion;
    [SerializeField] private WeaponSkinData[] skins = Array.Empty<WeaponSkinData>();

    public WeaponSkinData DefaultSkin => defaultSkin;
    public WeaponSkinData[] Skins => skins;
    public WeaponSkinData Find(string id) => Array.Find(skins, skin => skin != null && skin.SkinId == id);
    public AnimationClip GetIdle(WeaponSkinData skin) => skin != null && skin.AnimationController != null && idleMotion != null
        ? skin.AnimationController[idleMotion] : null;
}

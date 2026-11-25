using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Project K/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [SerializeField] private string weaponName;
    [SerializeField] private WeaponStageData[] weaponStages = Array.Empty<WeaponStageData>();

    public string WeaponName => weaponName;
    public WeaponStageData[] WeaponStages => weaponStages;

    private void OnValidate()
    {
        if (weaponStages == null) return;

        foreach (WeaponStageData weaponStage in weaponStages)
        {
            weaponStage?.Validate();
        }
    }
}

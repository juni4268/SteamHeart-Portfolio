using System;
using UnityEngine;

[Serializable]
public class WeaponAttackData
{
    [SerializeField] private string attackName;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField] private AttackPolygon attackPolygon = new AttackPolygon();

    public string AttackName => attackName;
    public int Damage => damage;
    public AttackPolygon AttackPolygon => attackPolygon;

    internal void Validate()
    {
        damage = Mathf.Max(1, damage);
    }
}

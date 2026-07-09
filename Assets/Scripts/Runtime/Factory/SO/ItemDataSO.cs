using System;
using UnityEngine;

public enum ItemType { Ore, Ingot, Component, Fuel, Misc, Weapon, Helmet, Chestplate, Boots }

[CreateAssetMenu(fileName = "NewItem", menuName = "Factory/Item")]
public class ItemDataSO : GameDataSO
{
    [Obsolete("GameDataSO의 id와 displayName로 바뀌었습니다. 이제 name은 Object.name으로 fallback됩니다.")]
    public string name => base.name;
    public ItemType type;
}

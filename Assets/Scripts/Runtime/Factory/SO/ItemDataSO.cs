using System;
using UnityEngine;

public enum ItemType { Ore, Ingot, Component, Fuel, Misc, Weapon, Helmet, Chestplate, Boots }

[CreateAssetMenu(fileName = "NewItem", menuName = "Factory/Item")]
public class ItemDataSO : GameDataSO
{
    [Obsolete("GameDataSO�� id�� displayName�� �ٲ�����ϴ�. ���� name�� Object.name���� fallback�˴ϴ�.")]
    public string Name => base.name;
    public ItemType type;
}

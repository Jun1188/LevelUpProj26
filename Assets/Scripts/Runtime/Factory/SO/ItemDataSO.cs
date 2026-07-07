using UnityEngine;

public enum ItemType { Ore, Ingot, Component, Fuel, Misc, Weapon, Helmet, Chestplate, Boots }

[CreateAssetMenu(fileName = "NewItem", menuName = "Factory/Item")]
public class ItemDataSO : GameDataSO
{
    public ItemType type;
}

using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponItem", menuName = "Factory/WeaponItem")]
public class WeaponItemSO : ItemDataSO
{
    [Header("Weapon Specific")]
    public GunData gunData;
}
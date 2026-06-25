using UnityEngine;

// 무기 종류를 나눌 열거형(Enum)
public enum WeaponType
{
    Pistol,
    SubMachineGun,
    Sniper
}

[CreateAssetMenu(fileName = "GunData", menuName = "ScriptableObjects/GunData", order = 1)]
public class GunData : ScriptableObject
{
    [Header("Weapon Identity")]
    public string gunName = "Pistol";
    public WeaponType weaponType;
    public bool isAutomatic;

    [Header("Gun General Settings")]
    public float damage = 10f;
    public float fireRate = 0.2f;       // 연사 간격 (초) - 낮을수록 빠름 (예: 기관총은 0.08, 저격총은 2.0)
    public float bulletSpeed = 50f;
    public float maxRange = 100f;
    public GameObject bulletPrefab;

    [Header("Ammo & Reload Settings")]
    public int magSize = 30;
    public float reloadTime = 1.5f;

    [Header("Recoil Settings")]
    public float recoilForce = 2f;
}
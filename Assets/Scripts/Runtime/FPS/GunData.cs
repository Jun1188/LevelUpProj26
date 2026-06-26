using UnityEngine;

public enum WeaponType { Pistol, SubMachineGun, Sniper }

[CreateAssetMenu(fileName = "GunData", menuName = "ScriptableObjects/GunData", order = 1)]
public class GunData : ScriptableObject
{
    [Header("Weapon Identity")]
    public string gunName = "Pistol";
    public WeaponType weaponType;
    public bool isAutomatic;             // SMG는 true, 권총/저격은 false

    [Header("Gun General Settings")]
    public float damage = 10f;
    public float fireRate = 0.2f;        // 연사 속도 (낮을수록 빠름)
    public float bulletSpeed = 50f;
    public float maxRange = 100f;
    public GameObject bulletPrefab;

    [Header("Ammo & Reload Settings")]
    public int magSize = 30;
    public float reloadTime = 1.5f;

    [Header("Recoil Settings (반동 제어)")]
    public float verticalRecoil = 3f;     
    public float horizontalRecoil = 2f;   
}
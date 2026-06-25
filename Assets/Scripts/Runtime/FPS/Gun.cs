using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class Gun : MonoBehaviour
{
    public GunData gunData;
    public PlayerController playerController;
    
    private float lastFireTime = 0f;
    private int currentAmmo;
    private bool isReloading = false;

    private IObjectPool<GameObject> bulletPool;

    private void Awake()
    {
        bulletPool = new ObjectPool<GameObject>(
            createFunc: CreateBullet,       
            actionOnGet: OnGetBullet,       
            actionOnRelease: OnReleaseBullet,
            actionOnDestroy: OnDestroyBullet,
            collectionCheck: true,
            defaultCapacity: 20,            
            maxSize: 50                    
        );
    }

    private void Start()
    {
        if (gunData != null) currentAmmo = gunData.magSize;
    }

    public bool Fire()
    {
        if (gunData == null || gunData.bulletPrefab == null) return false;
        if (Time.time < lastFireTime + gunData.fireRate) return false; // 연사력(쿨타임) 검사
        if (isReloading || currentAmmo <= 0) return false;

        // 총알 생성 및 위치 조절
        GameObject bullet = bulletPool.Get();
        bullet.transform.SetParent(this.transform);
        bullet.transform.position = transform.position;
        if (playerController != null && playerController.playerCamera != null)
        {
            bullet.transform.rotation = playerController.playerCamera.rotation;
        }

        // 발사 힘 주기
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero; 
            rb.angularVelocity = Vector3.zero; 
            rb.AddForce(bullet.transform.forward * gunData.bulletSpeed, ForceMode.Impulse);
        }

        currentAmmo--;
        lastFireTime = Time.time; // 마지막 발사 시간 갱신
        Debug.Log($"[{gunData.gunName}] 탕! 탄약: {currentAmmo}/{gunData.magSize}");

        // 카메라 기준 반동 처리
        if (playerController != null && playerController.playerCamera != null)
        {
            Vector3 recoilDir = -playerController.playerCamera.forward;

            recoilDir.y = 0f; 
            recoilDir.Normalize();

            playerController.AddRecoil(recoilDir, gunData.recoilForce);
        }

        return true;
    }

    public void StartReload()
    {
        if (isReloading || currentAmmo == gunData.magSize) return;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(gunData.reloadTime);
        currentAmmo = gunData.magSize;
        isReloading = false;
        Debug.Log($"{gunData.gunName} 재장전 완료!");
    }

    #region 오브젝트 풀 내부 함수들
    private GameObject CreateBullet()
    {
        GameObject bullet = Instantiate(gunData.bulletPrefab);
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        bulletScript?.SetPool(bulletPool);
        return bullet;
    }
    private void OnGetBullet(GameObject bullet) { bullet.SetActive(true); }
    private void OnReleaseBullet(GameObject bullet) { bullet.SetActive(false); }
    private void OnDestroyBullet(GameObject bullet) { Destroy(bullet); }
    #endregion
}
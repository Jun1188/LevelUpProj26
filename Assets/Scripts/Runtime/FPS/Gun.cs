using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class Gun : MonoBehaviour
{
    public GunData gunData;
    public PlayerController playerController;
    public Transform muzzlePoint; 
    
    private float lastFireTime = 0f;
    private int currentAmmo;
    private bool isReloading = false;
    private bool isFiringPressed = false; 

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

    private void Update()
    {
        if (gunData == null) return;

        // 연사 무기(SMG)인 경우 누르고 있으면 자동 사격
        if (isFiringPressed && gunData.isAutomatic && !isReloading)
        {
            Fire();
        }
    }

    public void SetFiringPressed(bool isPressed)
    {
        isFiringPressed = isPressed;
        
        // 단발 무기(권총, 저격총)는 누르는 순간 딱 한 번 실행
        if (isFiringPressed && !gunData.isAutomatic && !isReloading)
        {
            Fire();
        }
    }

    public bool Fire()
    {
        if (gunData == null || gunData.bulletPrefab == null) return false;
        if (Time.time < lastFireTime + gunData.fireRate) return false; 
        if (isReloading || currentAmmo <= 0) 
        {
            if (currentAmmo <= 0 && !isReloading) StartReload();
            return false;
        }

        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position;
        Quaternion spawnRot = muzzlePoint != null ? muzzlePoint.rotation : transform.rotation;

        GameObject bullet = bulletPool.Get();
        bullet.transform.position = spawnPos;
        bullet.transform.rotation = spawnRot;

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.Setup(gunData.bulletSpeed, 3f); 
        }

        currentAmmo--;
        lastFireTime = Time.time; 

        if (playerController != null)
        {
            // 1. 화면 위로 올리는 반동 적용
            playerController.AddCameraRecoil(gunData.verticalRecoil);

            // 2. 몸통 수평(X, Z) 뒤로 밀리는 반동 적용
            Vector3 recoilDir = -playerController.transform.forward; 
            recoilDir.y = 0f; // 수평 유지
            recoilDir.Normalize();
            
            playerController.AddHorizontalBodyRecoil(recoilDir, gunData.horizontalRecoil);
        }

        return true;
    }

    public void StartReload()
    {
        if (isReloading || gunData == null || currentAmmo == gunData.magSize) return;
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
    private void OnGetBullet(GameObject bullet) => bullet.SetActive(true);
    private void OnReleaseBullet(GameObject bullet) => bullet.SetActive(false);
    private void OnDestroyBullet(GameObject bullet) => Destroy(bullet);
    #endregion
}
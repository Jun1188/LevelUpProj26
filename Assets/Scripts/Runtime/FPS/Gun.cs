using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class Gun : MonoBehaviour
{
    #region [1. Variables]
    [Header("References")]
    public GunData gunData;
    public PlayerController playerController;
    public Transform muzzlePoint; 

    [Header("Weapon States")]
    private float lastFireTime = 0f;
    private int currentAmmo;
    private bool isReloading = false;
    private bool isFiringPressed = false; 

    [Header("Object Pooling Backend")]
    private IObjectPool<GameObject> bulletPool;
    private GameObject previousBulletPrefab; // 이전 총알 프리팹 추적용
    #endregion

    #region [2. Unity Lifecycle]
    private void Awake()
    {
        InitializePool();
    }

    private void Start()
    {
        if (gunData != null) currentAmmo = gunData.magSize;
    }

    private void Update()
    {
        if (gunData == null) return;

        // 연사 무기(SMG)인 경우 마우스 좌클릭을 누르고 있으면 자동 사격
        if (isFiringPressed && gunData.isAutomatic && !isReloading)
        {
            Fire();
        }
    }
    #endregion

    #region [3. Public API - Weapon Setup & Control]
    
    // ⭐️ 중복되던 ChangeGunData와 SetupGunData를 하나로 깔끔하게 통합했습니다.
    public void SetupGunData(GunData newGunData)
    {
        if (newGunData == null) return;

        // 1. 기존 풀 내용 초기화 및 데이터 교체
        if (gunData == null || gunData.bulletPrefab != newGunData.bulletPrefab)
        {
            bulletPool?.Clear(); // 풀 내부의 구형 총알 오브젝트 파괴
        }

        this.gunData = newGunData;
        
        // 2. 새 총 상태 초기화 (탄창 완충, 진행 중이던 장전 코루틴 차단)
        currentAmmo = gunData.magSize;
        isReloading = false;
        StopAllCoroutines(); 

        // 3. 총알 프리팹이 변경되었다면 풀 재생성
        if (previousBulletPrefab != gunData.bulletPrefab)
        {
            previousBulletPrefab = gunData.bulletPrefab;
            InitializePool();
        }

        Debug.Log($"[{gunData.gunName}] 장착 완료! 데미지: {gunData.damage}, 탄창: {gunData.magSize}");
    }

    // 무기 장착 해제 기능
    public void ClearGunData()
    {
        gunData = null;
        currentAmmo = 0;
        isFiringPressed = false;
        isReloading = false;
        StopAllCoroutines();
        Debug.Log("[무기 해제] 무기를 해제하여 공격할 수 없습니다.");
    }

    public void SetFiringPressed(bool pressed)
    {
        isFiringPressed = pressed;
    }
    #endregion

    #region [4. Core Mechanics - Fire & Reload]
    public bool Fire()
    {
        if (!gameObject.activeSelf) return false;
        if (gunData == null || gunData.bulletPrefab == null) return false;
        if (Time.time < lastFireTime + gunData.fireRate) return false;
        
        if (isReloading || currentAmmo <= 0)
        {
            if (currentAmmo <= 0 && !isReloading) StartReload();
            return false;
        }

        // 총알 스폰 위치 및 회전값 결정
        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position;
        Quaternion spawnRot = muzzlePoint != null ? muzzlePoint.rotation : transform.rotation;

        // 풀에서 총알 꺼내기
        GameObject bullet = bulletPool.Get();
        bullet.transform.position = spawnPos;
        bullet.transform.rotation = spawnRot;

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.Setup(gunData.bulletSpeed, 3f); // 3초 뒤 자동 반환
        }

        currentAmmo--;
        lastFireTime = Time.time;

        // 플레이어 몸통 및 카메라에 반동 수치 전달
        if (playerController != null)
        {
            Vector3 recoilDir = -playerController.transform.forward; // 바라보는 방향 정반대
            recoilDir.y = 0f; // 수평 유지
            recoilDir.Normalize();
            
            // ⭐️ 해결: PlayerController의 수정된 3개짜리 매개변수 AddRecoil을 정상 호출합니다!
            playerController.AddRecoil(recoilDir, gunData.verticalRecoil, gunData.horizontalRecoil);
        }

        return true;
    }

    public void StartReload()
    {
        if (isReloading || gunData == null || currentAmmo == gunData.magSize || !gameObject.activeSelf) return;
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
    #endregion

    #region [5. Object Pooling Core Functions]
    private void InitializePool()
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
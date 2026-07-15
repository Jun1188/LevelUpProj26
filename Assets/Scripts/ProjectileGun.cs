using UnityEngine;
using UnityEngine.Pool;

public class ProjectileGun : WeaponBase
{
    private IObjectPool<GameObject> bulletPool;

    protected override void Awake()
    {
        base.Awake(); // 부모의 Awake(PlayerController 찾기) 실행
        InitializePool(); // 이 총기만의 전용 오브젝트 풀 생성
    }

    // WeaponBase의 ExecuteFire를 강제로 구현
    protected override void ExecuteFire()
    {
        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position;
        Quaternion spawnRot = muzzlePoint != null ? muzzlePoint.rotation : transform.rotation;

        // 전용 풀에서 총알 스폰
        GameObject bullet = bulletPool.Get();
        bullet.transform.position = spawnPos;
        bullet.transform.rotation = spawnRot;

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.Setup(gunData.bulletSpeed, 3f);
        }
    }

    #region Object Pooling
    private void InitializePool()
    {
        if (gunData.bulletPrefab == null) return;

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
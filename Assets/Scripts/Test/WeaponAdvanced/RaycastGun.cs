using UnityEngine;
using UnityEngine.Pool;

public class RaycastGun : WeaponBase
{

    RaycastHit hitInfo;

    protected override void Awake()
    {
        base.Awake(); // 부모의 Awake(PlayerController 찾기) 실행
 
    }

    protected override void ExecuteFire()
    {
        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position;

        // ⭐️ 탄퍼짐 적용 (정면 방향에 랜덤한 구형 오차를 더함)
        Vector3 fireDirection = (muzzlePoint != null ? muzzlePoint.forward : transform.forward);
        fireDirection += Random.insideUnitSphere * (currentSpread / 100f); // 수치 보정

        Quaternion spawnRot = Quaternion.LookRotation(fireDirection);

        if(Physics.Raycast(spawnPos, fireDirection, out hitInfo, gunData.range , gunData.enemyLayer))
        {
            var E = hitInfo.transform.GetComponent<Entity>();
            if (E) print("Entity Hit! " + E.gameObject.name);
        }
    }



}
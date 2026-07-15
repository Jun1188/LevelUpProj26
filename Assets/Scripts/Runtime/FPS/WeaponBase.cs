using System.Collections;
using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Core References")]
    public GunData gunData;
    public Transform muzzlePoint;
    protected PlayerController playerController;

    [Header("Current States")]
    protected int currentAmmo;
    protected bool isReloading = false;
    protected float lastFireTime = 0f;

    protected virtual void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    protected virtual void Start()
    {
        currentAmmo = gunData.magSize;
    }

    protected virtual void OnEnable()
    {
        // 무기를 스왑해서 꺼낼 때마다 상태 초기화
        isReloading = false;
    }

    protected virtual void OnDisable()
    {
        // 무기를 집어넣을 때 코루틴 안전하게 정지
        StopAllCoroutines();
    }

    // 매니저가 호출하는 사격 시도 함수
    public virtual void TryFire()
    {
        if (isReloading) return;

        // 탄약 부족 처리
        if (currentAmmo <= 0)
        {
            StartReload();
            return;
        }

        // 연사속도 제어
        if (Time.time < lastFireTime + gunData.fireRate) return;

        // 실제 사격 로직 실행 (탄약 차감, 쿨타임 갱신)
        currentAmmo--;
        lastFireTime = Time.time;

        ExecuteFire(); // 자식 클래스에서 구현된 진짜 발사(레이캐스트 or 총알생성) 실행
        ApplyRecoil();
    }

    // 자식 클래스(ProjectileGun 등)에서 반드시 구현해야 하는 발사 로직
    protected abstract void ExecuteFire();

    protected void ApplyRecoil()
    {
        // 카메라 쉐이크
        if (CameraShakeManager.Instance != null)
            CameraShakeManager.Instance.ShakeOnPlayerShoot(3);

        // 플레이어 반동 적용
        if (playerController != null)
        {
            Vector3 recoilDir = -playerController.transform.forward;
            recoilDir.y = 0f;
            recoilDir.Normalize();

            playerController.AddRecoil(recoilDir, gunData.verticalRecoil, gunData.horizontalRecoil);
        }
    }

    public void StartReload()
    {
        if (isReloading || currentAmmo == gunData.magSize || !gameObject.activeSelf) return;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        // 필요 시 애니메이션 트리거 호출
        // anim.SetTrigger("Reload");

        yield return new WaitForSeconds(gunData.reloadTime);

        currentAmmo = gunData.magSize;
        isReloading = false;
        Debug.Log($"{gunData.gunName} 재장전 완료!");
    }
}
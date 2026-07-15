using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapon Ob List")]
    public WeaponBase[] weapons; // 하위에 있는 Gun1, Gun2 등을 모두 드래그 앤 드롭
    private int currentIndex = -1; // -1이면 현재 맨손 상태

    [Header("Input Buffer (선입력)")]
    private float fireBufferWindow = 0.15f; // 0.15초 동안 입력 기억
    private float lastFireInputTime = -1f;


    public ProceduralRecoil recoilHandler;

    public WeaponBase CurrentWeapon
    {
        get
        {
            if (currentIndex >= 0 && currentIndex < weapons.Length)
                return weapons[currentIndex];
            return null;
        }
    }

    private void Start()
    {
        // 시작할 때 모든 무기를 꺼둡니다 (맨손 상태로 시작)
        foreach (var weapon in weapons)
        {
            weapon.gameObject.SetActive(false);
            weapon.weaponManager = this; 
        }
    }

    private void Update()
    {
        if (CurrentWeapon == null) return; // 무기가 없으면 입력 무시

        HandleInput();
    }

    private void HandleInput()
    {
        bool isAutomatic = CurrentWeapon.gunData.isAutomatic;
        if ((isAutomatic && Input.GetMouseButton(0)) || (!isAutomatic && Input.GetMouseButtonDown(0)))
        {
            lastFireInputTime = Time.time; // 마우스를 누른 시간을 갱신
        }

        // 2. 버퍼 시간(0.15초) 내에 있다면 계속 사격 시도
        if (Time.time - lastFireInputTime <= fireBufferWindow)
        {
            if (CurrentWeapon.TryFire())
            {
                lastFireInputTime = -1f; // ⭐️ 발사에 성공하면 버퍼를 비움(소비)
            }
        }

        if (Input.GetKeyDown(KeyCode.R)) CurrentWeapon.StartReload();
    }

    // ⭐️ [핵심] 인벤토리에서 GunData를 넘겨주면 해당 무기를 찾아 장착하는 함수
    public void EquipWeapon(GunData targetData)
    {
        if (targetData == null) return;

        // 매니저가 소지한 무기들을 탐색하며 GunData가 일치하는지 확인
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i].gunData == targetData)
            {
                // 이미 들고 있는 무기라면 무시
                if (currentIndex == i) return;

                SwapTo(i);
                Debug.Log($"[무기 교체 완료] {targetData.gunName} 장착 (공격력: {targetData.damage})");
                return;
            }
        }

        // 루프를 다 돌았는데 못 찾았다면 에러 로그 (프리팹을 자식으로 안 넣은 경우)
        Debug.LogWarning($"[오류] {targetData.gunName} 데이터를 가진 무기 오브젝트가 WeaponHolder 하위에 없습니다!");
    }

    // 실제 무기 오브젝트를 껐다 켜는 내부 로직
    private void SwapTo(int newIndex)
    {
        // 기존 무기 끄기
        if (CurrentWeapon != null)
        {
            CurrentWeapon.gameObject.SetActive(false);
        }

        // 새 무기 켜기
        currentIndex = newIndex;
        CurrentWeapon.gameObject.SetActive(true);
    }

    // (선택) 무기 해제 기능이 필요할 경우
    public void UnequipWeapon()
    {
        if (CurrentWeapon != null)
        {
            CurrentWeapon.gameObject.SetActive(false);
        }
        currentIndex = -1;
        Debug.Log("[무기 해제] 현재 맨손 상태입니다.");
    }
}
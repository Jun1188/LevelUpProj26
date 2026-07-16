using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 무기 장착/교체 + 사격·재장전 입력 — 입력 파이프라인의 Player 리시버.
/// 레거시 Input.GetMouseButton 폴링을 라우팅으로 대체 —
/// 건설 모드(BuildController가 Attack 소비)·팝업(UI 맵) 중에는 신호가 도달하지 않는다.
/// </summary>
public class WeaponManager : MonoBehaviour, IInputReceiver
{
    [Header("Weapon Ob List")]
    public WeaponBase[] weapons; // 하위에 있는 Gun1, Gun2 등을 모두 드래그 앤 드롭
    private int currentIndex = -1; // -1이면 현재 맨손 상태

    private bool isFiringHeld;   // 자동화기 연사용 눌림 상태

    public WeaponBase CurrentWeapon
    {
        get
        {
            if (currentIndex >= 0 && currentIndex < weapons.Length)
                return weapons[currentIndex];
            return null;
        }
    }

    public int Priority => InputPriority.Player;
    public bool IsInputActive => isActiveAndEnabled;

    private void Start()
    {
        // 시작할 때 모든 무기를 꺼둡니다 (맨손 상태로 시작)
        foreach (var weapon in weapons)
        {
            weapon.gameObject.SetActive(false);
        }

        if (InputManager.Instance != null) InputManager.Instance.Register(this);
        else Debug.LogError("[WeaponManager] 씬에 InputManager가 없습니다.", this);
    }

    private void OnDisable()
    {
        isFiringHeld = false;
        if (InputManager.Instance != null) InputManager.Instance.Unregister(this);
    }

    private void Update()
    {
        // 자동화기 연사 — 눌림 상태 동안 매 프레임 시도 (발사 간격은 무기가 관리)
        if (isFiringHeld && CurrentWeapon != null && CurrentWeapon.gunData.isAutomatic)
            CurrentWeapon.TryFire();
    }

    public bool OnInput(in InputEvent e)
    {
        if (CurrentWeapon == null) return false;   // 맨손이면 하류로 통과

        switch (e.Id)
        {
            case InputActionId.Attack:
                if (e.Phase == InputActionPhase.Performed)
                {
                    isFiringHeld = true;
                    if (!CurrentWeapon.gunData.isAutomatic) CurrentWeapon.TryFire();   // 단발은 즉시 1회
                    return true;
                }
                if (e.Phase == InputActionPhase.Canceled)
                {
                    isFiringHeld = false;
                }
                return false;

            case InputActionId.Reload:
                if (e.Phase != InputActionPhase.Performed) return false;
                CurrentWeapon.StartReload();
                return true;
        }
        return false;
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
        isFiringHeld = false;
        Debug.Log("[무기 해제] 현재 맨손 상태입니다.");
    }
}
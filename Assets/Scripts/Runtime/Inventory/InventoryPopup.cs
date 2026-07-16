using UnityEngine;

/// <summary>
/// 인벤토리 패널(inventoryUIPanel)에 부착하는 파이프라인 어댑터.
/// 열기/닫기 로직 자체는 PlayerController가 계속 소유하고, 이 컴포넌트는:
///  - 패널 활성화 시 UI 맵 Push → 사격·건설 등 Gameplay 입력 신호 차단
///  - Cancel(ESC) → PlayerController.CloseInventory() 호출
///    (마우스 캐리지 아이템 드롭, 상자 패널 정리, 커서 복원 포함)
/// </summary>
public class InventoryPopup : UIPopup
{
    [SerializeField] private PlayerController player;

    protected override void OnEnable()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        base.OnEnable();
    }

    public override void Close()
    {
        if (player != null) player.CloseInventory();   // 패널 SetActive(false)까지 수행
        else base.Close();
    }
}
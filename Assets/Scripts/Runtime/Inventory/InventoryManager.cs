using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Global Mouse Carriage (마우스 쥐고 있는 임시 슬롯)")]
    public ItemSocket mouseCarriageSlot;     // 화면 최상단 Canvas에 배치할 마우스 추적용 UI 슬롯
    private ItemStack mouseCarriageItem = null; // 현재 마우스가 쥐고 있는 백엔드 데이터

    [Header("Player References")]
    public PlayerController playerController;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (mouseCarriageSlot != null) mouseCarriageSlot.ClearSlot();

        ForceCheckWeaponEquip();
    }

    private void Update()
    {
        // 마우스 커서가 아이템을 쥐고 있다면 UI가 실시간으로 마우스 좌표를 추적
        if (mouseCarriageItem != null && mouseCarriageItem.item != null && mouseCarriageItem.amount > 0)
        {
            mouseCarriageSlot.gameObject.SetActive(true);
            mouseCarriageSlot.transform.position = Input.mousePosition;
        }
        else
        {
            if (mouseCarriageSlot != null) mouseCarriageSlot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 엔드필드 스타일 핵심: 플레이어 가방이든, 건물 창고 UI든 슬롯을 누르면 무조건 이 중앙 함수로 모입니다.
    /// </summary>
    public void HandleSlotLeftClick(Inventory inventory, int clickedIndex)
    {
        ItemStack clickedBackendSlot = inventory.slots[clickedIndex];

        // A. 마우스에 아무것도 들고 있지 않을 때 -> 클릭한 슬롯 아이템 들어올리기
        if (mouseCarriageItem == null || mouseCarriageItem.item == null)
        {
            if (clickedBackendSlot != null && clickedBackendSlot.item != null)
            {
                mouseCarriageItem = clickedBackendSlot;
                inventory.slots[clickedIndex] = null;
            }
        }
        // B. 마우스에 무언가 아이템을 이미 쥐고 있을 때
        else
        {
            // B-1. 빈 슬롯 클릭 -> 그대로 내려놓기
            if (clickedBackendSlot == null || clickedBackendSlot.item == null)
            {
                inventory.slots[clickedIndex] = mouseCarriageItem;
                mouseCarriageItem = null;
            }
            // B-2. 다른 종류의 아이템 클릭 -> 서로 위치 바꾸기(Swap)
            else if (clickedBackendSlot.item != mouseCarriageItem.item)
            {
                ItemStack temp = clickedBackendSlot;
                inventory.slots[clickedIndex] = mouseCarriageItem;
                mouseCarriageItem = temp;
            }
            // B-3. 같은 종류의 아이템 클릭 -> 개수 합치기(Merge)
            else if (clickedBackendSlot.item == mouseCarriageItem.item)
            {
                int maxStack = mouseCarriageItem.maxStackSize;
                int canAdd = maxStack - clickedBackendSlot.amount;

                if (canAdd > 0)
                {
                    int transferAmount = Mathf.Min(canAdd, mouseCarriageItem.amount);
                    clickedBackendSlot.amount += transferAmount;
                    mouseCarriageItem.amount -= transferAmount;

                    if (mouseCarriageItem.amount <= 0) mouseCarriageItem = null;
                }
            }
        }

        // 마우스 커서 UI 시각적 동기화
        if (mouseCarriageItem != null)
            mouseCarriageSlot.SetItem(mouseCarriageItem.item, mouseCarriageItem.amount);
        else
            mouseCarriageSlot.ClearSlot();

        // 만약 상호작용한 인벤토리가 플레이어 가방이라면 무기 장착 여부를 실시간 체크!
        if (playerController != null && inventory == playerController.playerInventory)
        {
            CheckWeaponEquip(inventory);
        }
        // 화면에 열려있는 모든 인벤토리 UI 새로고침 (가방과 건물 UI가 동시에 켜져있어도 한방에 싱크 완료)
        InventoryUI[] allActiveUIs = FindObjectsByType<InventoryUI>(FindObjectsSortMode.None);
        foreach (InventoryUI ui in allActiveUIs)
        {
            if (ui.gameObject.activeSelf) ui.RefreshAllUI();
        }

    }

    // 플레이어 가방의 0번 슬롯(첫 번째 칸)을 무기 슬롯으로 감시하는 로직
    private void CheckWeaponEquip(Inventory playerInventory)
    {
        if (playerInventory == null || playerInventory.slots == null || playerInventory.slots.Length == 0) return;

        ItemStack firstSlot = playerInventory.slots[0];
        
        // 0번 슬롯에 아이템이 존재하고, 그것이 무기 데이터(WeaponItemSO)인 경우
        if (firstSlot != null && firstSlot.item != null && firstSlot.item is WeaponItemSO weaponItem && firstSlot.amount > 0)
        {
            if (playerController.gun != null)
            {
                // 1. 꺼져있던 총 오브젝트를 활성화
                playerController.gun.gameObject.SetActive(true);
                // 2. 새로운 무기 스크립터블 오브젝트 데이터 주입 및 스왑 연동
                playerController.gun.ChangeGunData(weaponItem.gunData); 
                Debug.Log($"[시스템] 무기 장착 성공: {weaponItem.gunData.gunName}");
            }
        }
        else
        {
            // 0번 슬롯이 비었거나 무기가 아니라면 총 오브젝트 비활성화
            if (playerController.gun != null)
            {
                playerController.gun.gameObject.SetActive(false);
                Debug.Log("[시스템] 무기 해제: 0번 슬롯이 비어있어 무기가 해제되었습니다.");
            }
        }
    }

    public void ForceCheckWeaponEquip()
    {
        if (playerController != null && playerController.playerInventory != null)
        {
            CheckWeaponEquip(playerController.playerInventory);
        }
    }
}
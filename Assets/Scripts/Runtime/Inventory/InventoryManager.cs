using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Global Mouse Carriage (마우스 쥐고 있는 임시 슬롯)")]
    public ItemSocket mouseCarriageSlot;     
    private ItemStack mouseCarriageItem = null; 

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
        CloseScreen();   // 시작 시 인벤 화면 닫힌 상태 보장
    }

    // ════════════════════════════════════════════════════════════════
    // 인벤토리 화면 — 마인크래프트식: 타겟(상자·건물)이 화면을 직접 요청한다.
    // 플레이어는 중개하지 않는다 (구 PlayerController.OpenPlayerInventory/OpenTargetInventory 대체).
    // 패널·UI 참조는 당분간 playerController 필드 경유 — UI 소유권 이관은 UI 담당과 협의 후.
    // ════════════════════════════════════════════════════════════════

    public bool IsScreenOpen { get; private set; }

    // 밤에는 인벤토리 사용 금지 (낮=건설/정비, 밤=전투) — 팀원 추가 기능을 화면 소유자로 이식.
    // TimeManager 없는 씬은 항상 허용.
    private static bool InventoryAllowed =>
        TimeManager.Instance == null || TimeManager.Instance.Cycle.Phase == DayPhase.Day;

    /// <summary>플레이어 가방만 열기 (I키).</summary>
    public void OpenPlayerScreen() => OpenScreen(null);

    /// <summary>컨테이너(상자·건물 보관함)와 함께 열기 — 타겟의 Interact()가 호출.</summary>
    public void OpenContainerScreen(Inventory container) => OpenScreen(container);

    /// <summary>
    /// 심 컨테이너(건물 보관함)를 열기 — 프록시 Inventory에 Bind해서 UI가 같은 객체를 직접 본다.
    /// 별도 동기화 없음: 공장이 넣는 것도, 플레이어가 꺼내는 것도 전부 그 컨테이너에서 일어난다.
    /// </summary>
    public void OpenContainerScreen(ItemContainer container)
    {
        if (container == null || playerController == null || IsScreenOpen) return;

        if (containerProxy == null) containerProxy = gameObject.AddComponent<Inventory>();
        containerProxy.Bind(container);
        boundContainer = container;
        lastSeenVersion = container.Version;
        OpenScreen(containerProxy);
    }

    private Inventory containerProxy;        // 건물 컨테이너를 UI에 꽂기 위한 어댑터 (재사용)
    private ItemContainer boundContainer;    // 열려 있는 동안 공장 측 변경 감지용
    private int lastSeenVersion;

    private void OpenScreen(Inventory container)
    {
        if (playerController == null || IsScreenOpen) return;
        if (!InventoryAllowed)
        {
            Debug.Log("[InventoryManager] 밤에는 인벤토리를 열 수 없습니다.");
            return;
        }
        IsScreenOpen = true;

        playerController.HaltMomentum();   // 열리는 순간 수평 관성 제거

        if (container != null && playerController.chestInventoryUI != null)
        {
            playerController.chestInventoryUI.inventory = container;
            playerController.chestInventoryUI.gameObject.SetActive(true);
            playerController.chestInventoryUI.RefreshAllUI();
        }

        // 패널 활성화 → InventoryPopup.OnEnable (UI 맵 Push + 커서/크로스헤어)
        if (playerController.inventoryUIPanel != null) playerController.inventoryUIPanel.SetActive(true);
        if (playerController.inventoryUI != null) playerController.inventoryUI.RefreshAllUI();
    }

    /// <summary>화면 닫기 — 손에 든 아이템은 월드로 드롭. InventoryPopup(ESC/I/E)이 호출.</summary>
    public void CloseScreen()
    {
        DropMouseCarriageItem();
        boundContainer = null;   // 건물 컨테이너 연결 해제 (Bind는 다음 열기 때 갱신)
        IsScreenOpen = false;

        if (playerController == null) return;
        // 패널 비활성화 → InventoryPopup.OnDisable (UI 맵 Pop + 커서/크로스헤어)
        if (playerController.inventoryUIPanel != null) playerController.inventoryUIPanel.SetActive(false);
        if (playerController.chestInventoryUI != null) playerController.chestInventoryUI.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 건물 보관함이 열려 있는 동안 공장이 내용을 바꾸면(버전 변화) 화면 갱신
        if (boundContainer != null && boundContainer.Version != lastSeenVersion)
        {
            lastSeenVersion = boundContainer.Version;
            if (playerController != null && playerController.chestInventoryUI != null)
                playerController.chestInventoryUI.RefreshAllUI();
        }

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

    public void HandleSlotLeftClick(Inventory inventory, int clickedIndex, InventoryUI uiSource)
    {
        // 1. Shift 키가 눌려있다면 마크식 정밀 퀵 무브 시스템 작동!
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            HandleShiftClick(inventory, clickedIndex);
            return;
        }

        // 2. 드래그 앤 드롭 코드 — 슬롯 조작은 전부 컨테이너 API 경유 (필터·스택 규칙 준수)
        if (mouseCarriageItem == null || mouseCarriageItem.item == null)
        {
            var picked = inventory.TakeAt(clickedIndex);
            if (picked != null && picked.item != null) mouseCarriageItem = picked;
            else if (picked != null) inventory.TryPutAt(clickedIndex, picked); // 빈 껍데기 스택은 되돌림
        }
        else
        {
            if (!IsSlotPlacementAllowed(inventory, clickedIndex, mouseCarriageItem.item)) return;

            ItemStack targetSlot = inventory.GetAt(clickedIndex);
            if (targetSlot == null || targetSlot.item == null)
            {
                if (inventory.TryPutAt(clickedIndex, mouseCarriageItem))
                    mouseCarriageItem = null;
            }
            else if (targetSlot.item == mouseCarriageItem.item)
            {
                int maxCanAdd = targetSlot.maxStackSize - targetSlot.amount;
                int toAdd = Mathf.Min(maxCanAdd, mouseCarriageItem.amount);
                targetSlot.amount += toAdd;
                mouseCarriageItem.amount -= toAdd;
                inventory.Touch();
                if (mouseCarriageItem.amount <= 0) mouseCarriageItem = null;
            }
            else
            {
                if (inventory.TryExchangeAt(clickedIndex, mouseCarriageItem, out var prev))
                    mouseCarriageItem = prev;
            }
        }

        // 🔥 [버그 수정]: 좌클릭이 완전히 끝난 후 마우스 슬롯 비주얼(아이콘/개수)을 실시간 갱신합니다.
        if (mouseCarriageSlot != null)
        {
            if (mouseCarriageItem != null && mouseCarriageItem.item != null && mouseCarriageItem.amount > 0)
            {
                mouseCarriageSlot.SetItem(mouseCarriageItem.item, mouseCarriageItem.amount);
            }
            else
            {
                mouseCarriageSlot.ClearSlot();
            }
        }

        // 전 세계 UI 동시에 새로고침
        RefreshAllGameUIs(playerController.playerInventory);
    }
    public void HandleSlotRightClick(Inventory inventory, int clickedIndex, InventoryUI uiSource)
    {
        ItemStack clickedBackendSlot = inventory.GetAt(clickedIndex);

        // Case 1: 마우스가 비어있을 때 -> 클릭한 슬롯 아이템의 절반을 마우스로 들기
        if (mouseCarriageItem == null || mouseCarriageItem.item == null)
        {
            if (clickedBackendSlot != null && clickedBackendSlot.item != null && clickedBackendSlot.amount > 0)
            {
                // 마인크래프트 방식: 홀수일 때 슬롯에 적게 남고 마우스에 더 많이 들리도록 올림(Ceil) 계산
                int takeAmount = clickedBackendSlot.amount - (clickedBackendSlot.amount / 2);

                mouseCarriageItem = new ItemStack(clickedBackendSlot.item, takeAmount);
                clickedBackendSlot.amount -= takeAmount;
                inventory.Touch();

                if (clickedBackendSlot.amount <= 0)
                    inventory.TakeAt(clickedIndex);
            }
        }
        // Case 2: 마우스에 아이템을 쥐고 있을 때 -> 슬롯에 1개씩 톡톡 내려놓기
        else
        {
            // 장비창(0번 슬롯) 등에 Weapon 조건 제한 체크
            if (!IsSlotPlacementAllowed(inventory, clickedIndex, mouseCarriageItem.item)) return;

            // 슬롯이 완전히 비어있을 때 -> 1개 복사해서 생성
            if (clickedBackendSlot == null || clickedBackendSlot.item == null)
            {
                if (inventory.TryPutAt(clickedIndex, new ItemStack(mouseCarriageItem.item, 1)))
                    mouseCarriageItem.amount--;
            }
            // 슬롯에 같은 아이템이 있고, 최대 스택(64개) 미만일 때 -> 1개 추가
            else if (clickedBackendSlot.item == mouseCarriageItem.item && clickedBackendSlot.amount < clickedBackendSlot.maxStackSize)
            {
                clickedBackendSlot.amount++;
                mouseCarriageItem.amount--;
                inventory.Touch();
            }

            // 마우스에 든 아이템을 다 썼으면 마우스 비우기
            if (mouseCarriageItem.amount <= 0) mouseCarriageItem = null;
        }

        // [UI 및 무기 상태 동기화] (좌클릭 로직과 동일)
        if (mouseCarriageItem != null)
            mouseCarriageSlot.SetItem(mouseCarriageItem.item, mouseCarriageItem.amount);
        else
            mouseCarriageSlot.ClearSlot();

        if (playerController != null && inventory == playerController.playerInventory)
        {
            CheckWeaponEquip(inventory);
        }

        InventoryUI[] allActiveUIs = FindObjectsByType<InventoryUI>(FindObjectsSortMode.None);
        foreach (InventoryUI ui in allActiveUIs)
        {
            if (ui.gameObject.activeSelf) ui.RefreshAllUI();
        }

        // 기존 가방 UI 리프레시 코드 바로 밑에 추가해 줍니다.
        if (HotbarUI.Instance != null)
        {
            HotbarUI.Instance.RefreshHotbar();
        }
    }
    // 아이템을 인벤토리 슬롯에 내려놓을 때 제한하는 규칙도 핫바 범위 전체로 유연하게 열어줍니다.
    private bool IsSlotPlacementAllowed(Inventory inventory, int slotIndex, ItemDataSO item)
    {
        if (item == null) return true;

        // 플레이어 가방 검사 규칙
        if (playerController != null && inventory == playerController.playerInventory)
        {
            // 만약 핫바 영역(0~4번 슬롯)에 장착하는 것이라면 아무 아이템이나 다 들어갈 수 있습니다.
            // 다만 무기가 들어오면 위의 CheckWeaponEquip이 자동으로 알아채서 총을 쥐여줄 것입니다.
            return true; 
        }

        return true; 
    }

    // 슬롯 드롭/클릭 등 기본 인벤토리 조작 시 호출될 때 호환성을 위해 기본값을 플레이어의 현재 핫바로 지정
    public void CheckWeaponEquip(Inventory playerInventory, int activeSlotIndex = -1)
    {
        // weaponManager를 연결하지 않은 씬(무기 없는 테스트 씬 등)에서는 장착 검사 자체를 건너뜀
        if (playerController == null || playerController.weaponManager == null) return;

        // 만약 슬롯 번호가 지정되지 않았다면 플레이어의 현재 활성화된 핫바 인덱스를 가져옴
        if (activeSlotIndex == -1 && HotbarController.Instance != null)
        {
            activeSlotIndex = HotbarController.Instance.CurrentHotbarIndex;
        }

        if (playerInventory.SlotCount > activeSlotIndex && activeSlotIndex >= 0)
        {
            ItemStack activeSlot = playerInventory.GetAt(activeSlotIndex);
            
            // 💡 현재 선택된 핫바 슬롯에 아이템이 있고, 그 아이템이 무기가 맞다면 장착!
            if (activeSlot != null && activeSlot.item is WeaponItemSO weaponItem)
            {
                playerController.weaponManager.EquipWeapon(weaponItem.gunData);
            }
            // 선택된 슬롯이 비어있거나 무기가 아니라면 (예: 광석이나 연료를 들고 있다면) 총을 숨김
            else
            {
                playerController.weaponManager.UnequipWeapon();
            }
        }
    }
    
    // 인벤토리가 닫힐 때 호출되어 손에 들고 있던 아이템을 전방으로 던집니다.
    public void DropMouseCarriageItem()
    {
        if (mouseCarriageItem == null || mouseCarriageItem.item == null || mouseCarriageItem.amount <= 0) return;

        ItemDataSO item = mouseCarriageItem.item;
        int amount = mouseCarriageItem.amount;

        if (playerController == null) return;

        // 플레이어 카메라 정면 1.5m 앞, 약간 위쪽에 스폰 후 전방 투척 (조립은 DroppedItem.Spawn이 담당)
        Vector3 spawnPos = playerController.transform.position + playerController.playerCamera.forward * 1.5f + Vector3.up * 0.5f;
        DroppedItem.Spawn(item, amount, spawnPos, playerController.playerCamera.forward);

        // 마우스 캐리지 백엔드 및 UI 청소
        mouseCarriageItem = null;
        if (mouseCarriageSlot != null) mouseCarriageSlot.ClearSlot();

        // 기존 가방 UI 리프레시 코드 바로 밑에 추가해 줍니다.
        if (HotbarUI.Instance != null)
        {
            HotbarUI.Instance.RefreshHotbar();
        }
    }
    private void HandleShiftClick(Inventory srcInventory, int clickedIndex)
    {
        ItemStack srcSlot = srcInventory.GetAt(clickedIndex);
        if (srcSlot == null || srcSlot.item == null || srcSlot.amount <= 0) return;

        Inventory playerInv = playerController.playerInventory;
        
        // 🔥 [원인 2 해결]: 하드코딩(0~8)을 제거하고, 실제 핫바 크기를 HotbarController에서 유동적으로 가져옵니다.
        int hotbarSize = HotbarController.Instance != null ? HotbarController.Instance.hotbarSlotCount : 5;

        bool isChestOpen = playerController.inventoryUIPanel.activeSelf && playerController.chestInventoryUI.gameObject.activeSelf; 
        Inventory openChestInv = isChestOpen ? playerController.chestInventoryUI.inventory : null;

        if (isChestOpen && openChestInv != null)
        {
            if (srcInventory == openChestInv)
            {
                // 상자에서 클릭함 ➡️ 플레이어의 '진짜 가방 영역(hotbarSize 이후)'으로 이동
                TryMoveItemToRange(playerInv, hotbarSize, playerInv.SlotCount - 1, srcSlot);
            }
            else if (srcInventory == playerInv)
            {
                if (clickedIndex >= hotbarSize)
                {
                    // 진짜 가방 영역에서 클릭함 ➡️ 상자 인벤토리로 이동
                    TryMoveItemToRange(openChestInv, 0, openChestInv.SlotCount - 1, srcSlot);
                }
                else
                {
                    // 핫바 영역에서 Shift 클릭한 것은 상자가 열려있으므로 무시
                    return;
                }
            }
        }
        else
        {
            if (srcInventory == playerInv)
            {
                // 🔥 [원인 2 해결]: 핫바 범위(0 ~ hotbarSize-1)에 따라 영리하게 인덱스를 가방 분기 처리합니다.
                if (clickedIndex >= 0 && clickedIndex < hotbarSize)
                {
                    // 핫바에서 클릭함 ➡️ 가방 영역(hotbarSize ~ 끝)으로 이동
                    TryMoveItemToRange(playerInv, hotbarSize, playerInv.SlotCount - 1, srcSlot);
                }
                else if (clickedIndex >= hotbarSize)
                {
                    // 가방 영역에서 클릭함 ➡️ 핫바 영역(0 ~ hotbarSize-1)으로 이동
                    TryMoveItemToRange(playerInv, 0, hotbarSize - 1, srcSlot);
                }
            }
        }

        srcInventory.Touch();   // 이동 중 인플레이스 amount 수정 통지
        if (srcSlot.amount <= 0)
        {
            srcInventory.TakeAt(clickedIndex);
        }

        RefreshAllGameUIs(playerInv);
    }

    // 마크식 영리한 분배 알고리즘: 지정된 범위 내에 아이템을 겹치고 남은 건 빈칸에 채우는 함수
    private bool TryMoveItemToRange(Inventory targetInv, int startIdx, int endIdx, ItemStack srcSlot)
    {
        if (srcSlot == null || srcSlot.item == null || srcSlot.amount <= 0) return false;

        // 1단계: 기존에 '같은 아이템'이 담긴 슬롯이 있는지 찾아서 스택 합치기 (최대 64개 제한)
        for (int i = startIdx; i <= endIdx; i++)
        {
            if (i >= targetInv.SlotCount) break;

            ItemStack targetSlot = targetInv.GetAt(i);
            if (targetSlot != null && targetSlot.item == srcSlot.item && targetSlot.amount < targetSlot.maxStackSize)
            {
                int maxCanAdd = targetSlot.maxStackSize - targetSlot.amount;
                int toAdd = Mathf.Min(maxCanAdd, srcSlot.amount);

                targetSlot.amount += toAdd;
                srcSlot.amount -= toAdd;
                targetInv.Touch();

                if (srcSlot.amount <= 0) return true; // 다 옮김!
            }
        }

        // 2단계: 그래도 남은 아이템 개수가 있다면 '완전 빈 슬롯'을 찾아서 새로 안착시키기
        for (int i = startIdx; i <= endIdx; i++)
        {
            if (i >= targetInv.SlotCount) break;

            ItemStack targetSlot = targetInv.GetAt(i);
            if (targetSlot == null || targetSlot.item == null)
            {
                int toAdd = Mathf.Min(srcSlot.maxStackSize, srcSlot.amount);
                if (!targetInv.TryPutAt(i, new ItemStack(srcSlot.item, toAdd))) continue; // 규칙 거절 시 다음 칸
                srcSlot.amount -= toAdd;

                if (srcSlot.amount <= 0) return true; // 다 옮김!
            }
        }

        return srcSlot.amount <= 0; // 완전 분배 성공 시 true, 대상 영역이 꽉 차서 남았다면 false
    }

    // 마우스/Shift 클릭 후 모든 UI와 무기를 강제 갱신해주는 통합 헬퍼 함수
    public void RefreshAllGameUIs(Inventory playerInv)
    {
        // 1. 들고 있는 무기 상태 체크
        CheckWeaponEquip(playerInv);

        // 2. 씬에 존재하는 모든 InventoryUI (가방, 상자 등) 새로고침
        InventoryUI[] allActiveUIs = FindObjectsByType<InventoryUI>(FindObjectsSortMode.None);
        foreach (InventoryUI ui in allActiveUIs)
        {
            if (ui.gameObject.activeSelf) ui.RefreshAllUI();
        }
        
        // 3. 상시 핫바 UI 새로고침
        if (HotbarUI.Instance != null)
        {
            HotbarUI.Instance.RefreshHotbar();
        }
    }
}
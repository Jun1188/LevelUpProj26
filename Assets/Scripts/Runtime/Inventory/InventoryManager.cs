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
    }

    private void Update()
    {
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

        // 2. 드래그 앤 드롭 코드
        if (mouseCarriageItem == null || mouseCarriageItem.item == null)
        {
            if (inventory.slots[clickedIndex] != null && inventory.slots[clickedIndex].item != null)
            {
                mouseCarriageItem = inventory.slots[clickedIndex];
                inventory.slots[clickedIndex] = null;
            }
        }
        else
        {
            if (!IsSlotPlacementAllowed(inventory, clickedIndex, mouseCarriageItem.item)) return;

            ItemStack targetSlot = inventory.slots[clickedIndex];
            if (targetSlot == null || targetSlot.item == null)
            {
                inventory.slots[clickedIndex] = mouseCarriageItem;
                mouseCarriageItem = null;
            }
            else if (targetSlot.item == mouseCarriageItem.item)
            {
                int maxCanAdd = targetSlot.maxStackSize - targetSlot.amount;
                int toAdd = Mathf.Min(maxCanAdd, mouseCarriageItem.amount);
                targetSlot.amount += toAdd;
                mouseCarriageItem.amount -= toAdd;
                if (mouseCarriageItem.amount <= 0) mouseCarriageItem = null;
            }
            else
            {
                inventory.slots[clickedIndex] = mouseCarriageItem;
                mouseCarriageItem = targetSlot;
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
        ItemStack clickedBackendSlot = inventory.slots[clickedIndex];

        // Case 1: 마우스가 비어있을 때 -> 클릭한 슬롯 아이템의 절반을 마우스로 들기
        if (mouseCarriageItem == null || mouseCarriageItem.item == null)
        {
            if (clickedBackendSlot != null && clickedBackendSlot.item != null && clickedBackendSlot.amount > 0)
            {
                // 마인크래프트 방식: 홀수일 때 슬롯에 적게 남고 마우스에 더 많이 들리도록 올림(Ceil) 계산
                int takeAmount = clickedBackendSlot.amount - (clickedBackendSlot.amount / 2); 
                
                mouseCarriageItem = new ItemStack(clickedBackendSlot.item, takeAmount);
                clickedBackendSlot.amount -= takeAmount;

                if (clickedBackendSlot.amount <= 0) 
                    inventory.slots[clickedIndex] = null;
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
                inventory.slots[clickedIndex] = new ItemStack(mouseCarriageItem.item, 1);
                mouseCarriageItem.amount--;
            }
            // 슬롯에 같은 아이템이 있고, 최대 스택(64개) 미만일 때 -> 1개 추가
            else if (clickedBackendSlot.item == mouseCarriageItem.item && clickedBackendSlot.amount < clickedBackendSlot.maxStackSize)
            {
                clickedBackendSlot.amount++;
                mouseCarriageItem.amount--;
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
        // 만약 슬롯 번호가 지정되지 않았다면 플레이어의 현재 활성화된 핫바 인덱스를 가져옴
        if (activeSlotIndex == -1 && playerController != null)
        {
            activeSlotIndex = playerController.CurrentHotbarIndex;
        }

        if (playerInventory.slots.Length > activeSlotIndex && activeSlotIndex >= 0)
        {
            ItemStack activeSlot = playerInventory.slots[activeSlotIndex];
            
            // 💡 현재 선택된 핫바 슬롯에 아이템이 있고, 그 아이템이 무기가 맞다면 장착!
            if (activeSlot != null && activeSlot.item is WeaponItemSO weaponItem)
            {
                if (playerController.gun != null)
                {
                    playerController.gun.gameObject.SetActive(true);
                    playerController.gun.SetupGunData(weaponItem.gunData); 
                    Debug.Log($"[무기 교체 완료] {weaponItem.Name} 장착 (공격력: {weaponItem.gunData.damage})");
                }
            }
            // 선택된 슬롯이 비어있거나 무기가 아니라면 (예: 광석이나 연료를 들고 있다면) 총을 숨김
            else
            {
                if (playerController.gun != null)
                {
                    playerController.gun.ClearGunData();
                    playerController.gun.gameObject.SetActive(false); 
                }
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

        // 1. 플레이어 카메라 정면 1.5m 앞, 약간 위쪽을 스폰 위치로 지정
        Vector3 spawnPos = playerController.transform.position + playerController.playerCamera.forward * 1.5f + Vector3.up * 0.5f;

        // 2. 빈 게임 오브젝트를 동적 생성하고 이름 부여
        GameObject dropObj = new($"Dropped_{item.Name}");
        dropObj.transform.position = spawnPos;

        int interactableLayerIndex = LayerMask.NameToLayer("Interactable");
        if (interactableLayerIndex != -1) // 프로젝트에 해당 레이어가 정상 존재한다면
        {
            dropObj.layer = interactableLayerIndex;
        }
        else
        {
            Debug.LogWarning("[레이어 경고] 프로젝트에 'Interactable' 레이어가 존재하지 않습니다. Tags and Layers 설정을 확인해 주세요!");
        }
        
        // 3. 물리(Rigidbody) 및 충돌체(BoxCollider) 추가 (바닥에 튕기고 떨어지게 함)
        Rigidbody rb = dropObj.AddComponent<Rigidbody>();
        BoxCollider col = dropObj.AddComponent<BoxCollider>();
        col.size = new Vector3(0.4f, 0.4f, 0.4f); // 적당한 크기의 히트박스

        // 4. 비주얼(마인크래프트처럼 둥둥 떠서 도는 아이콘 구현) 자식 생성
        GameObject visualObj = new GameObject("Visual");
        visualObj.transform.SetParent(dropObj.transform);
        visualObj.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
        sr.sprite = item.icon; // 아이템 고유 아이콘 맵핑
        visualObj.AddComponent<ItemRotator>(); // 빙글빙글 회전 컴포넌트 추가

        // 5. 상호작용 및 데이터 주입
        DroppedItem droppedScript = dropObj.AddComponent<DroppedItem>();
        droppedScript.Setup(item, amount, sr);

        // 6. 플레이어가 바라보는 정면 방향으로 툭 던지는 물리적 힘(Impulse) 부여
        rb.AddForce(playerController.playerCamera.forward * 3.5f, ForceMode.Impulse);

        // 7. 마우스 캐리지 백엔드 및 UI 청소
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
        ItemStack srcSlot = srcInventory.slots[clickedIndex];
        if (srcSlot == null || srcSlot.item == null || srcSlot.amount <= 0) return;

        Inventory playerInv = playerController.playerInventory;
        
        // 🔥 [원인 2 해결]: 하드코딩(0~8)을 제거하고, 플레이어의 실제 핫바 크기(5)를 유동적으로 가져옵니다.
        int hotbarSize = playerController != null ? playerController.hotbarSlotCount : 5;

        bool isChestOpen = playerController.inventoryUIPanel.activeSelf && playerController.chestInventoryUI.gameObject.activeSelf; 
        Inventory openChestInv = isChestOpen ? playerController.chestInventoryUI.inventory : null;

        if (isChestOpen && openChestInv != null)
        {
            if (srcInventory == openChestInv)
            {
                // 상자에서 클릭함 ➡️ 플레이어의 '진짜 가방 영역(hotbarSize 이후)'으로 이동
                TryMoveItemToRange(playerInv, hotbarSize, playerInv.slotCount - 1, srcSlot);
            }
            else if (srcInventory == playerInv)
            {
                if (clickedIndex >= hotbarSize)
                {
                    // 진짜 가방 영역에서 클릭함 ➡️ 상자 인벤토리로 이동
                    TryMoveItemToRange(openChestInv, 0, openChestInv.slotCount - 1, srcSlot);
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
                    TryMoveItemToRange(playerInv, hotbarSize, playerInv.slotCount - 1, srcSlot);
                }
                else if (clickedIndex >= hotbarSize)
                {
                    // 가방 영역에서 클릭함 ➡️ 핫바 영역(0 ~ hotbarSize-1)으로 이동
                    TryMoveItemToRange(playerInv, 0, hotbarSize - 1, srcSlot);
                }
            }
        }

        if (srcSlot.amount <= 0)
        {
            srcInventory.slots[clickedIndex] = null;
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
            if (i >= targetInv.slots.Length) break;

            ItemStack targetSlot = targetInv.slots[i];
            if (targetSlot != null && targetSlot.item == srcSlot.item && targetSlot.amount < targetSlot.maxStackSize)
            {
                int maxCanAdd = targetSlot.maxStackSize - targetSlot.amount;
                int toAdd = Mathf.Min(maxCanAdd, srcSlot.amount);

                targetSlot.amount += toAdd;
                srcSlot.amount -= toAdd;

                if (srcSlot.amount <= 0) return true; // 다 옮김!
            }
        }

        // 2단계: 그래도 남은 아이템 개수가 있다면 '완전 빈 슬롯'을 찾아서 새로 안착시키기
        for (int i = startIdx; i <= endIdx; i++)
        {
            if (i >= targetInv.slots.Length) break;

            ItemStack targetSlot = targetInv.slots[i];
            if (targetSlot == null || targetSlot.item == null)
            {
                int toAdd = Mathf.Min(srcSlot.maxStackSize, srcSlot.amount);
                targetInv.slots[i] = new ItemStack(srcSlot.item, toAdd);
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
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public Inventory inventory;             // 백엔드 Inventory
    public GameObject itemSocketPrefab;     // 팀원의 ItemSocketPrefab
    public Transform slotGridParent;        // 슬롯 프리팹들이 정렬될 Grid Layout Group 오브젝트

    [Header("Mouse Drag Pointer UI")]
    public ItemSocket mouseCarriageSlot;    //  마우스 커서를 따라다닐 임시 UI 슬롯 오브젝트

    private ItemSocket[] uiSlots;           // 화면에 생성된 UI 슬롯들의 ItemSocket 컴포넌트 배열
    private ItemStack mouseCarriageItem = null; // 현재 마우스가 쥐고 있는 아이템 데이터 정보

    private void Awake()
    {
        if (inventory == null) return; 

        InitUISlots(); 
        RefreshAllUI(); 
    
        if (mouseCarriageSlot != null) mouseCarriageSlot.ClearSlot(); 
        gameObject.SetActive(false); 
    }

    private void Update()
    {
        // 마우스가 무언가 아이템을 쥐고 있다면 UI가 마우스 커서 위치를 실시간 추적
        if (mouseCarriageItem != null && mouseCarriageItem.item != null) 
        {
            mouseCarriageSlot.gameObject.SetActive(true); 
            mouseCarriageSlot.transform.position = Input.mousePosition; 
        }
        else
        {
            if (mouseCarriageSlot != null) mouseCarriageSlot.gameObject.SetActive(false); 
        }
    }

    private void InitUISlots() 
    {
        uiSlots = new ItemSocket[inventory.slotCount]; 

        foreach (Transform child in slotGridParent) { Destroy(child.gameObject); } 

        for (int i = 0; i < inventory.slotCount; i++) 
        {
            GameObject go = Instantiate(itemSocketPrefab, slotGridParent); 
            ItemSocket socket = go.GetComponent<ItemSocket>(); 
            uiSlots[i] = socket; 

            InventorySlotUI slotUI = go.AddComponent<InventorySlotUI>(); 
            slotUI.Init(i, this); 
        }
    }

    public void RefreshAllUI() 
    {
        for (int i = 0; i < inventory.slotCount; i++) 
        {
            ItemStack backendStack = inventory.slots[i]; 
            if (backendStack != null && backendStack.item != null) 
            {
                uiSlots[i].SetItem(backendStack.item, backendStack.amount); 
            }
            else
            {
                uiSlots[i].ClearSlot(); 
            }
        }

        if (mouseCarriageItem != null && mouseCarriageItem.item != null) 
        {
            mouseCarriageSlot.SetItem(mouseCarriageItem.item, mouseCarriageItem.amount); 
        }
        else
        {
            mouseCarriageSlot.ClearSlot(); 
        }
    }

    public void OnSlotLeftClicked(int clickedIndex) 
    {
        ItemStack clickedBackendSlot = inventory.slots[clickedIndex]; 

        if (mouseCarriageItem == null || mouseCarriageItem.item == null) 
        {
            if (clickedBackendSlot != null && clickedBackendSlot.item != null) 
            {
                mouseCarriageItem = clickedBackendSlot; 
                inventory.slots[clickedIndex] = null; 
            }
        }
        else
        {
            if (clickedBackendSlot == null || clickedBackendSlot.item == null) 
            {
                inventory.slots[clickedIndex] = mouseCarriageItem; 
                mouseCarriageItem = null; 
            }
            else if (clickedBackendSlot.item != mouseCarriageItem.item) 
            {
                ItemStack temp = clickedBackendSlot; 
                inventory.slots[clickedIndex] = mouseCarriageItem; 
                mouseCarriageItem = temp; 
            }
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

        RefreshAllUI(); 
    }

    public void OnSlotRightClicked(int clickedIndex)
    {
        Debug.Log($"[인벤토리 매니저] {clickedIndex}번 슬롯 우클릭 됨");
    }
}
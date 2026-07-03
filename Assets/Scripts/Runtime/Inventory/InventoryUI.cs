using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public Inventory inventory;             
    public GameObject itemSocketPrefab;     
    public Transform slotGridParent;        

    [Header("★화면 자동 연동 옵션")]
    public bool isPlayerMainInventory = false; // 체크하면 플레이어 핫바를 제외한 가방 칸만 자동 계산

    // ❌ 무의미한 startSlotIndex, endSlotIndex, useSlotRange 변수 전부 삭제!!
    private ItemSocket[] uiSlots;          

    private void Start()
    {
        if (inventory == null) return;
        InitUISlots();
        RefreshAllUI();
    }

    private void OnEnable()
    {
        if (inventory != null) RefreshAllUI();
    }

    public void InitUISlots()
    {
        foreach (Transform child in slotGridParent) Destroy(child.gameObject);

        // 🔥 [대폭 최적화] 기본값은 인벤토리의 처음(0)부터 끝(slotCount - 1)까지입니다.
        int start = 0;
        int end = inventory.slotCount - 1;

        // 오직 '플레이어 메인 가방 UI'일 때만 자동으로 앞부분(핫바)을 건너뜁니다!
        if (isPlayerMainInventory && InventoryManager.Instance != null && InventoryManager.Instance.playerController != null)
        {
            start = InventoryManager.Instance.playerController.hotbarSlotCount;
        }

        int count = Mathf.Max(0, end - start + 1);
        uiSlots = new ItemSocket[count];

        for (int i = 0; i < count; i++)
        {
            int slotIdx = start + i; 
            GameObject go = Instantiate(itemSocketPrefab, slotGridParent);
            
            InventorySlotUI slotLink = go.GetComponent<InventorySlotUI>();
            if (slotLink == null) slotLink = go.AddComponent<InventorySlotUI>();
            
            slotLink.Init(slotIdx, this);
            uiSlots[i] = go.GetComponent<ItemSocket>();
        }
    }

    public void RefreshAllUI()
    {
        if (inventory == null || uiSlots == null) return;

        // 🔥 새로고침할 때도 동일하게 범위를 자동 계산합니다.
        int start = 0;
        int end = inventory.slotCount - 1;

        if (isPlayerMainInventory && InventoryManager.Instance != null && InventoryManager.Instance.playerController != null)
        {
            start = InventoryManager.Instance.playerController.hotbarSlotCount;
        }

        int count = Mathf.Max(0, end - start + 1);

        if (uiSlots.Length != count)
        {
            InitUISlots();
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int slotIdx = start + i;
            ItemStack itemStack = inventory.slots[slotIdx];
            if (itemStack != null && itemStack.item != null && itemStack.amount > 0)
            {
                uiSlots[i].SetItem(itemStack.item, itemStack.amount);
            }
            else
            {
                uiSlots[i].ClearSlot();
            }
        }
    }

    public void OnSlotLeftClicked(int clickedIndex)
    {
        InventoryManager.Instance.HandleSlotLeftClick(inventory, clickedIndex, this);
    }

    public void OnSlotRightClicked(int clickedIndex)
    {
        InventoryManager.Instance.HandleSlotRightClick(inventory, clickedIndex, this);
    }
}
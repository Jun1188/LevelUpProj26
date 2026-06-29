using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public Inventory inventory;             // 백엔드 데이터 (여기에 플레이어 Inventory를 넣든, 건물 Inventory를 넣든 다 대응됨!)
    public GameObject itemSocketPrefab;     
    public Transform slotGridParent;        

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
        foreach (Transform child in slotGridParent)
        {
            Destroy(child.gameObject);
        }

        uiSlots = new ItemSocket[inventory.slotCount];

        for (int i = 0; i < inventory.slotCount; i++)
        {
            GameObject go = Instantiate(itemSocketPrefab, slotGridParent);
            
            InventorySlotUI slotLink = go.GetComponent<InventorySlotUI>();
            if (slotLink == null) slotLink = go.AddComponent<InventorySlotUI>();
            slotLink.Init(i, this);

            uiSlots[i] = go.GetComponent<ItemSocket>();
        }
    }

    public void RefreshAllUI()
    {
        if (inventory == null || uiSlots == null) return;

        if (uiSlots.Length != inventory.slotCount)
        {
            InitUISlots();
        }

        for (int i = 0; i < inventory.slotCount; i++)
        {
            ItemStack itemStack = inventory.slots[i];
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

    // 슬롯이 좌클릭 되었을 때 독단적으로 처리하지 않고 중앙 타워(InventoryManager)로 던집니다!
    public void OnSlotLeftClicked(int clickedIndex)
    {
        if (inventory == null) return;
        InventoryManager.Instance.HandleSlotLeftClick(inventory, clickedIndex);
    }

    public void OnSlotRightClicked(int clickedIndex)
    {
        // 필요시 마크식 절반 나누기 구현 공간
    }
}
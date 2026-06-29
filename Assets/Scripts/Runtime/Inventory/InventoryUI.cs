using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public Inventory inventory;             
    public GameObject itemSocketPrefab;     
    public Transform slotGridParent;        

    [Header("Slot Range Settings (★새로 추가된 옵션)")]
    public bool useSlotRange = false;       // 특정 구역만 출력할 것인가?
    public int startSlotIndex = 0;          // 출력 시작 슬롯 번호
    public int endSlotIndex = 35;           // 출력 끝 슬롯 번호 (포함)

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

        // 사용할 슬롯 범위 계산
        int start = useSlotRange ? startSlotIndex : 0;
        int end = useSlotRange ? Mathf.Min(endSlotIndex, inventory.slotCount - 1) : inventory.slotCount - 1;
        int count = end - start + 1;

        uiSlots = new ItemSocket[count];

        for (int i = 0; i < count; i++)
        {
            int slotIdx = start + i; // ★핵심: 백엔드 실제 인벤토리 배열의 절대 인덱스 매핑
            GameObject go = Instantiate(itemSocketPrefab, slotGridParent);
            
            InventorySlotUI slotLink = go.GetComponent<InventorySlotUI>();
            if (slotLink == null) slotLink = go.AddComponent<InventorySlotUI>();
            
            // UI 슬롯에게 백엔드의 진짜 슬롯 인덱스를 바인딩해줍니다.
            slotLink.Init(slotIdx, this);

            uiSlots[i] = go.GetComponent<ItemSocket>();
        }
    }

    public void RefreshAllUI()
    {
        if (inventory == null || uiSlots == null) return;

        int start = useSlotRange ? startSlotIndex : 0;
        int end = useSlotRange ? Mathf.Min(endSlotIndex, inventory.slotCount - 1) : inventory.slotCount - 1;
        int count = end - start + 1;

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
using UnityEngine;
using UnityEngine.UI;

public class HotbarUI : MonoBehaviour
{
    public static HotbarUI Instance { get; private set; }

    [Header("References")]
    public Inventory playerInventory;       
    public GameObject itemSocketPrefab;     
    public Transform hotbarGridParent;      
    public InventoryUI mainInventoryUI;     

    [Header("Visual Colors (시각 효과 색상)")]
    public Color defaultSlotColor = new Color(1f, 1f, 1f, 0.3f);       
    public Color equipmentSlotColor = new Color(0.2f, 0.5f, 1f, 0.5f);  
    public Color activeBorderColor = Color.yellow;                      
    public Color defaultBorderColor = new Color(0.1f, 0.1f, 0.1f, 1f);   

    private ItemSocket[] hotbarSlots;
    private Image[] slotBackgrounds;
    private Image[] slotBorders;

    // 🔥 [유동적 크기 자동화]: 플레이어가 가진 설정을 실시간으로 단일 진실 공급원으로 삼습니다.
    public int HotbarSlotCount 
    {
        get
        {
            if (InventoryManager.Instance != null && InventoryManager.Instance.playerController != null)
            {
                return InventoryManager.Instance.playerController.hotbarSlotCount;
            }
            return 5; // 방어용 기본값
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (playerInventory != null)
        {
            InitHotbarSlots();
            RefreshHotbar();
        }
    }

    public void InitHotbarSlots()
    {
        foreach (Transform child in hotbarGridParent) Destroy(child.gameObject);

        // 🔥 플레이어 컨트롤러의 값을 토대로 슬롯 UI 배열을 동적 생성합니다!
        int count = HotbarSlotCount; 
        hotbarSlots = new ItemSocket[count];
        slotBackgrounds = new Image[count];
        slotBorders = new Image[count];

        InventoryUI activeInventoryUI = mainInventoryUI;
        if (activeInventoryUI == null)
        {
            activeInventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        }

        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(itemSocketPrefab, hotbarGridParent);
            
            InventorySlotUI slotLink = go.GetComponent<InventorySlotUI>();
            if (slotLink == null) slotLink = go.AddComponent<InventorySlotUI>();

            // 핫바는 항상 가방의 0번 인덱스부터 차례대로 매핑됩니다.
            if (slotLink != null && activeInventoryUI != null)
            {
                slotLink.Init(i, activeInventoryUI);
            }

            hotbarSlots[i] = go.GetComponent<ItemSocket>();
            slotBackgrounds[i] = go.GetComponent<Image>();

            Transform borderTransform = go.transform.Find("Border") ?? go.transform.Find("Frame");
            if (borderTransform != null) slotBorders[i] = borderTransform.GetComponent<Image>();
        }
    }

    public void RefreshHotbar()
    {
        if (playerInventory == null || hotbarSlots == null) return;

        int activeIndex = 0;
        if (InventoryManager.Instance != null && InventoryManager.Instance.playerController != null)
        {
            activeIndex = InventoryManager.Instance.playerController.CurrentHotbarIndex;
        }

        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (i >= playerInventory.slots.Length) break;

            ItemStack itemStack = playerInventory.slots[i];
            if (itemStack != null && itemStack.item != null && itemStack.amount > 0)
            {
                hotbarSlots[i].SetItem(itemStack.item, itemStack.amount);
            }
            else
            {
                hotbarSlots[i].ClearSlot();
            }

            if (slotBackgrounds[i] != null)
            {
                slotBackgrounds[i].color = (i == 0) ? equipmentSlotColor : defaultSlotColor;
            }

            if (slotBorders[i] != null)
            {
                if (i == activeIndex)
                {
                    slotBorders[i].color = activeBorderColor;
                    hotbarSlots[i].transform.localScale = Vector3.one * 1.1f;
                }
                else
                {
                    slotBorders[i].color = defaultBorderColor;
                    hotbarSlots[i].transform.localScale = Vector3.one * 1.0f;
                }
            }
        }
    }
}
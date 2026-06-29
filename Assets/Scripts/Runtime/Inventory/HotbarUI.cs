using UnityEngine;
using UnityEngine.UI;

public class HotbarUI : MonoBehaviour
{
    public static HotbarUI Instance { get; private set; }

    [Header("References")]
    public Inventory playerInventory;       // 플레이어 백엔드 가방 데이터
    public GameObject itemSocketPrefab;     // 기존에 사용하던 슬롯 프리팹 그대로 재사용!
    public Transform hotbarGridParent;      // 핫바 슬롯들이 배치될 UI 부모
    public InventoryUI mainInventoryUI;     // 🔥 인스펙터에서 직접 드래그 앤 드롭할 수 있도록 변수 개방!

    [Header("Visual Colors (시각 효과 색상)")]
    public Color defaultSlotColor = new Color(1f, 1f, 1f, 0.3f);       
    public Color equipmentSlotColor = new Color(0.2f, 0.5f, 1f, 0.5f);  
    public Color activeBorderColor = Color.yellow;                      
    public Color defaultBorderColor = new Color(0.1f, 0.1f, 0.1f, 1f);   

    private ItemSocket[] hotbarSlots;
    private Image[] slotBackgrounds;
    private Image[] slotBorders;

    // 🔥 [컴파일 에러 해결]: PlayerController에서 참조하는 핫바 개수 프로퍼티 구현
    public int HotbarSlotCount => hotbarSlots != null ? hotbarSlots.Length : 5;

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

        // 🔥 플레이어 컨트롤러에 설정된 동적 핫바 크기(5)를 실시간 반영
        int count = 5;
        if (InventoryManager.Instance != null && InventoryManager.Instance.playerController != null)
        {
            count = InventoryManager.Instance.playerController.hotbarSlotCount;
        }

        hotbarSlots = new ItemSocket[count];
        slotBackgrounds = new Image[count];
        slotBorders = new Image[count];

        // 🔥 [원인 1 해결]: 메인 UI가 비활성화(Hide) 상태여도 강제로 찾아오도록 Include 옵션 추가!
        if (mainInventoryUI == null)
        {
            mainInventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        }

        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(itemSocketPrefab, hotbarGridParent);
            
            // 클릭 이벤트 연동 (이제 mainInventoryUI가 null이 아니므로 완벽히 작동합니다)
            InventorySlotUI slotLink = go.GetComponent<InventorySlotUI>();
            if (slotLink != null && mainInventoryUI != null)
            {
                slotLink.Init(i, mainInventoryUI);
            }

            hotbarSlots[i] = go.GetComponent<ItemSocket>();
            slotBackgrounds[i] = go.GetComponent<Image>();

            Transform borderTransform = go.transform.Find("Border") ?? go.transform.Find("Frame");
            if (borderTransform != null)
            {
                slotBorders[i] = borderTransform.GetComponent<Image>();
            }
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
            else if (slotBackgrounds[i] != null)
            {
                if (i == activeIndex) slotBackgrounds[i].color = Color.white;
            }
        }
    }
}   
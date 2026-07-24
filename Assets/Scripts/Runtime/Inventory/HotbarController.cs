using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 핫바 선택 입력 어댑터 — 입력 파이프라인의 HudWidget 리시버.
/// PlayerController에서 분리: 숫자키(1~9)·휠 스크롤 선택 + Q 드롭.
/// 레거시 Input.GetKeyDown/GetAxisRaw 폴링을 파이프라인 라우팅으로 대체 —
/// 팝업이 열려 Gameplay 맵이 내려가면 신호 자체가 발생하지 않는다.
/// </summary>
public class HotbarController : MonoBehaviour, IInputReceiver
{
    public static HotbarController Instance { get; private set; }

    [Tooltip("핫바 칸 수 (인벤토리 0번부터 hotbarSlotCount-1번까지)")]
    public int hotbarSlotCount = 9;

    [SerializeField] private PlayerController player;   // 인벤토리 백엔드 + 드롭 기준점(카메라)

    private int currentHotbarIndex;
    public int CurrentHotbarIndex => currentHotbarIndex;

    public int Priority => InputPriority.HudWidget;
    public bool IsInputActive => isActiveAndEnabled;

    private void Awake()
    {
        Instance = this;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
    }

    private void Start()
    {
        if (InputManager.Instance != null) InputManager.Instance.Register(this);
        else Debug.LogError("[HotbarController] 씬에 InputManager가 없습니다.", this);
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null) InputManager.Instance.Unregister(this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool OnInput(in InputEvent e)
    {
        if (e.Phase != InputActionPhase.Performed) return false;

        switch (e.Id)
        {
            case InputActionId.Hotbar:
                // 슬롯 번호는 발화한 키의 control 이름에서 읽는다 ("1"~"9", "numpad1"~"numpad9")
                string key = e.Context.control.name;
                if (int.TryParse(key[^1..], out int digit) && digit >= 1 && digit <= hotbarSlotCount)
                {
                    Select(digit - 1);
                    return true;
                }
                return false;

            case InputActionId.HotbarScroll:
                float scroll = e.Read<float>();
                if (scroll == 0f) return false;
                int next = currentHotbarIndex + (scroll > 0 ? -1 : 1);   // 위로 굴리면 이전 슬롯
                if (next < 0) next = hotbarSlotCount - 1;
                if (next >= hotbarSlotCount) next = 0;
                Select(next);
                return true;

            case InputActionId.QuickDrop:
                DropActiveItem();
                return true;
        }
        return false;
    }

    private void Select(int index)
    {
        if (index == currentHotbarIndex) return;
        currentHotbarIndex = index;

        if (HotbarUI.Instance != null)
            HotbarUI.Instance.RefreshHotbar();

        if (InventoryManager.Instance != null && player != null && player.playerInventory != null)
            InventoryManager.Instance.CheckWeaponEquip(player.playerInventory);
    }

    /// <summary>현재 핫바 슬롯의 아이템 1개를 전방으로 던진다 (Q).</summary>
    private void DropActiveItem()
    {
        if (player == null || player.playerInventory == null) return;
        var inventory = player.playerInventory;
        if (inventory.SlotCount <= currentHotbarIndex) return;

        ItemStack slot = inventory.GetAt(currentHotbarIndex);
        if (slot == null || slot.item == null || slot.amount <= 0) return;

        Vector3 spawnPos = player.transform.position + player.playerCamera.forward * 1.5f + Vector3.up * 0.5f;
        DroppedItem.Spawn(slot.item, 1, spawnPos, player.playerCamera.forward);

        slot.amount--;
        inventory.Touch();
        if (slot.amount <= 0) inventory.TakeAt(currentHotbarIndex);

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.RefreshAllGameUIs(inventory);
    }
}
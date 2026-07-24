using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int slotIndex { get; private set; } 
    private InventoryUI inventoryUI;

    public void Init(int index, InventoryUI ui)
    {
        slotIndex = index;
        inventoryUI = ui;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventoryUI == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            inventoryUI.OnSlotLeftClicked(slotIndex);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            inventoryUI.OnSlotRightClicked(slotIndex);
        }
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        RefreshTooltipContext();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ItemTooltipUI.Instance != null)
        {
            ItemTooltipUI.Instance.HideTooltip();
        }
    }

    // 슬롯의 현재 아이템 유무를 판단하여 툴팁을 띄우는 헬퍼 함수
    private void RefreshTooltipContext()
    {
        if (inventoryUI == null || inventoryUI.inventory == null || ItemTooltipUI.Instance == null) return;

        ItemStack currentStack = inventoryUI.inventory.GetAt(slotIndex);
        if (currentStack != null && currentStack.item != null && currentStack.amount > 0)
        {
            // 백엔드에 아이템 데이터가 들어있다면 툴팁 켜기
            ItemTooltipUI.Instance.ShowTooltip(currentStack.item);
        }
        else
        {
            // 빈 슬롯이라면 숨기기
            ItemTooltipUI.Instance.HideTooltip();
        }
    }

    // 인벤토리 창 자체가 닫힐 때 마우스 가출 현상으로 툴팁이 굳어버리는 버그 방지용
    private void OnDisable()
    {
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.HideTooltip();
    }
    
}
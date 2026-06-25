using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    public int slotIndex { get; private set; } // 이 슬롯이 인벤토리의 몇 번째 칸인가?
    private InventoryUI inventoryUI;

    // 초기화 함수
    public void Init(int index, InventoryUI ui)
    {
        slotIndex = index;
        inventoryUI = ui;
    }

    // 이 슬롯 영역이 마우스로 클릭되었을 때 실행되는 유니티 내장 함수
    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventoryUI == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 좌클릭: 아이템 전체 들기 / 내려놓기 / 바꾸기
            inventoryUI.OnSlotLeftClicked(slotIndex);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            // 우클릭: 마크처럼 절반만 나누기 등의 기능 (우선 정의만 해둠)
            inventoryUI.OnSlotRightClicked(slotIndex);
        }
    }
}
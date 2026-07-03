using UnityEngine;

[RequireComponent(typeof(Inventory))] // 이 스크립트를 붙이면 Inventory 컴포넌트도 자동으로 함께 붙습니다.
public class Chest : Interactable
{
    private Inventory chestInventory;

    private void Awake()
    {
        chestInventory = GetComponent<Inventory>();
        promptMessage = "상자 열기"; // 조준점을 갖다 대면 뜰 메시지
    }

    public override void OnInteract(PlayerController player)
    {
        // 플레이어에게 이 상자의 인벤토리를 열어달라고 요청합니다.
        player.OpenTargetInventory(chestInventory);
    }
}
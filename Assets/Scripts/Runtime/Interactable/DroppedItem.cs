using UnityEngine;

public class DroppedItem : Interactable
{
    public ItemDataSO item;
    public int amount;

    public void Setup(ItemDataSO itemData, int count, SpriteRenderer sr)
    {
        item = itemData;
        amount = count;
        
        // 조준했을 때 화면에 뜰 메시지 세팅
        promptMessage = $"{item.name} x{amount} 줍기";
    }

    public override void OnInteract(PlayerController player)
    {
        if (item == null || amount <= 0) return;

        // 플레이어 가방 백엔드에 아이템 주워담기 시도
        bool success = player.playerInventory.AddItem(item, amount);
        if (success)
        {
            Debug.Log($"[루팅 성공] {item.name} {amount}개를 주웠습니다.");
            Destroy(gameObject); // 바닥에 있던 오브젝트 삭제
        }
        else
        {
            Debug.LogWarning("[가방 가득 참] 인벤토리에 빈 공간이 없습니다!");
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        // 1. 부딪힌 오브젝트가 플레이어인지 확인
        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent<PlayerController>(out var player))
            {
                // 2. 플레이어 백엔드 가방에 아이템 데이터 쏙 집어넣기!
                bool isSuccess = player.playerInventory.AddItem(item, amount);
                
                if (isSuccess)
                {
                    // 3. 획득에 성공했다면 화면의 모든 인벤토리/핫바 UI 새로고침!
                    InventoryManager.Instance.RefreshAllGameUIs(player.playerInventory);
                    
                    // 4. 바닥에 있던 아이템 오브젝트는 삭제
                    Destroy(gameObject);
                }
            }
        }
    }
}

// 💡 마인크래프트처럼 아이템이 제자리에서 빙글빙글 돌게 만드는 컴포넌트
public class ItemRotator : MonoBehaviour
{
    void Update()
    {
        transform.Rotate(Vector3.up * 90f * Time.deltaTime, Space.World);
    }
}
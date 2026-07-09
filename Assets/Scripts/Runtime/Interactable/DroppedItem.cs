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

    // [방법 1] 조준 후 직접 상호작용 키를 눌러서 줍기
    public override void OnInteract(PlayerController player)
    {
        if (item == null || amount <= 0) return;

        // 플레이어 가방 백엔드에 아이템 주워담기 시도
        bool success = player.playerInventory.AddItem(item, amount);
        
        if (success)
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.RefreshAllGameUIs(player.playerInventory);
            }
            Destroy(gameObject); // 바닥에 있던 오브젝트 삭제
        }
        else
        {
            Debug.LogWarning("[가방 가득 참] 인벤토리에 빈 공간이 없습니다!");
        }
    }

    // [방법 2] 발로 밟거나 근처 센서 구역에 들어가면 자동으로 줍기 (자석 루팅)
    private void OnTriggerEnter(Collider other)
    {
        // 충돌한 대상이 플레이어인지 태그 검사
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null && player.playerInventory != null)
            {
                // 백엔드 가방에 아이템 데이터 추가 시도
                bool isSuccess = player.playerInventory.AddItem(this.item, this.amount);

                if (isSuccess)
                {
                    if (InventoryManager.Instance != null)
                    {
                        Debug.Log($"[루팅 성공] {item.name} {amount}개를 자동으로 주웠습니다.");
                        // 핫바를 포함한 모든 인벤토리 UI 실시간 새로고침 강제 호출!
                        InventoryManager.Instance.RefreshAllGameUIs(player.playerInventory);
                    }

                    // 바닥 오브젝트 깔끔하게 파괴
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
        transform.Rotate(90f * Time.deltaTime * Vector3.up, Space.World);
    }
}
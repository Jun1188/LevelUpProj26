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

    /// <summary>
    /// 월드 드롭 아이템을 코드로 조립해 스폰한다 — 핫바 Q드롭, 인벤 닫을 때 캐리지 드롭 등 공용.
    /// (프리팹화 전 임시 팩토리. 프리팹으로 전환하면 이 함수 내부만 바꾸면 된다)
    /// </summary>
    public static DroppedItem Spawn(ItemDataSO item, int amount, Vector3 position, Vector3 throwDirection)
    {
        // 1. 루트 오브젝트 + 레이어
        GameObject dropObj = new($"Dropped_{item.name}");
        dropObj.transform.position = position;

        int layer = LayerMask.NameToLayer("Interactable");
        if (layer != -1) dropObj.layer = layer;
        else Debug.LogWarning("[레이어 경고] 'Interactable' 레이어가 없습니다. Tags and Layers 설정 확인!");

        // 2. 물리
        Rigidbody rb = dropObj.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // 3. 콜라이더 2개 — 바닥 충돌용 고체 + 플레이어 획득 감지용 센서
        BoxCollider solidCol = dropObj.AddComponent<BoxCollider>();
        solidCol.size = new Vector3(0.3f, 0.3f, 0.3f);
        solidCol.isTrigger = false;

        BoxCollider triggerCol = dropObj.AddComponent<BoxCollider>();
        triggerCol.size = new Vector3(1.5f, 1.5f, 1.5f);
        triggerCol.isTrigger = true;

        // 4. 비주얼 자식 (둥둥 떠서 도는 아이콘)
        GameObject visualObj = new("Visual");
        visualObj.transform.SetParent(dropObj.transform);
        visualObj.transform.localPosition = Vector3.zero;
        visualObj.layer = dropObj.layer;

        SpriteRenderer sr = visualObj.AddComponent<SpriteRenderer>();
        sr.sprite = item.icon;
        visualObj.AddComponent<ItemRotator>();

        // 5. 데이터 주입 + 전방 투척
        DroppedItem dropped = dropObj.AddComponent<DroppedItem>();
        dropped.Setup(item, amount, sr);
        rb.AddForce(throwDirection * 3.5f, ForceMode.Impulse);
        return dropped;
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
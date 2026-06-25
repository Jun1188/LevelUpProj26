using System;
using UnityEngine;

[Serializable]
public class ItemStack
{
    public ItemDataSO item; // 팀원들이 만든 아이템 원본 데이터
    public int amount;      // 현재 쌓인 개수
    public int maxStackSize = 64; // 마크식 64개 제한

    public ItemStack(ItemDataSO item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }
}

public class Inventory : MonoBehaviour
{
    public int slotCount = 36; // 마크 인벤토리 기본 칸수
    public ItemStack[] slots;  // 슬롯 배열

    private void Awake()
    {
        slots = new ItemStack[slotCount];

        // 🛠️ 임시 치트키: 프로젝트에 있는 아무 ItemDataSO 에셋이나 하나 찾아서 
        // Resources 폴더에 넣거나 인스펙터로 받아온 뒤 테스트 가능해!
        // 아래는 프로젝트 내 "Factory/Item" 메뉴로 만든 아이템 에셋이 존재할 때 작동하는 예시 코드야.
        ItemDataSO testItem = Resources.Load<ItemDataSO>("TestItemName"); 
        if (testItem != null)
        {
            AddItem(testItem, 10); // 시작할 때 10개 획득하게 만들기!
        }
    }

    // 아이템 획득 (건물 상호작용이나 필드 루팅 시 호출됨)
    public bool AddItem(ItemDataSO newItem, int count) 
    {
        // 기존에 같은 아이템이 있으면 겹치기
        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i] != null && slots[i].item == newItem && slots[i].amount < slots[i].maxStackSize)
            {
                int canStack = slots[i].maxStackSize - slots[i].amount;
                int toAdd = Mathf.Min(canStack, count);
                slots[i].amount += toAdd;
                count -= toAdd;
                if (count <= 0) return true;
            }
        }

        //남은 건 빈 슬롯에 새로 넣기
        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i] == null || slots[i].item == null)
            {
                slots[i] = new ItemStack(newItem, count);
                return true;
            }
        }
        return false; // 인벤토리 꽉 참
    }
}
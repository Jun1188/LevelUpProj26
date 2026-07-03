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
        if (slots == null || slots.Length == 0)
        {
            slots = new ItemStack[slotCount];
        }
        else if (slots.Length != slotCount)
        {
            Array.Resize(ref slots, slotCount);
        }

        // 기존 테스트용 코드 (필요 없다면 지우셔도 됩니다)
        ItemDataSO testItem = Resources.Load<ItemDataSO>("TestItemName"); 
        if (testItem != null)
        {
            AddItem(testItem, 10); 
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
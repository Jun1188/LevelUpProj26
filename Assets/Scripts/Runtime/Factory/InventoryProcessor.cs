using UnityEngine;


public class InventoryProcessor : BaseProcessor
{
    [Header("=== Inventory Link ===")]
    public Inventory inputInventory;
    public Inventory outputInventory;

    // 플레이어 손제작은 보통 재료가 있는 만큼만 한 번 만들고 멈추므로 false
    protected override bool IsAutomation => false; 

    protected override bool HasEnoughIngredients()
    {
        if (currentRecipe == null || inputInventory == null) return false;

        foreach (var input in currentRecipe.inputs)
        {
            int required = input.amount;
            int total = 0;
            foreach (var slot in inputInventory.slots)
            {
                if (slot != null && slot.item == input.item) total += slot.amount;
            }
            if (total < required) return false;
        }
        return true;
    }

    protected override void ConsumeIngredients()
    {
        foreach (var input in currentRecipe.inputs)
        {
            int toRemove = input.amount;
            for (int i = 0; i < inputInventory.slots.Length; i++)
            {
                var slot = inputInventory.slots[i];
                if (slot != null && slot.item == input.item)
                {
                    if (slot.amount > toRemove) { slot.amount -= toRemove; break; }
                    else { toRemove -= slot.amount; inputInventory.slots[i] = null; }
                }
            }
        }
    }

    protected override void GiveOutputs()
    {
        foreach (var output in currentRecipe.outputs)
        {
            if (output.item != null) outputInventory.AddItem(output.item, output.amount);
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RefreshAllGameUIs(inputInventory);
            if (inputInventory != outputInventory) InventoryManager.Instance.RefreshAllGameUIs(outputInventory);
        }
    }
}
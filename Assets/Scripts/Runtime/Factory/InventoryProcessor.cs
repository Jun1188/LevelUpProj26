using UnityEngine;


public class InventoryProcessor : BaseProcessor
{
    [Header("=== Inventory Link ===")]
    public Inventory inputInventory;
    public Inventory outputInventory;

    // 플레이어 손제작은 보통 재료가 있는 만큼만 한 번 만들고 멈추므로 false
    protected override bool IsAutomation => false; 

    // 구 slots 배열 순회 → 컨테이너 API로 이식 (Inventory가 ItemContainer 위임으로 재설계됨)

    protected override bool HasEnoughIngredients()
    {
        if (currentRecipe == null || inputInventory == null) return false;

        foreach (var input in currentRecipe.inputs)
            if (inputInventory.Container.CountOf(input.item) < input.amount) return false;
        return true;
    }

    protected override void ConsumeIngredients()
    {
        foreach (var input in currentRecipe.inputs)
            inputInventory.Container.TryConsume(input.item, input.amount);   // HasEnoughIngredients 선검사로 보장
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
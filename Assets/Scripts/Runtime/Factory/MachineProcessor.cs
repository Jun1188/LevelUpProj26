using UnityEngine;

[RequireComponent(typeof(Building))]
public class MachineProcessor : BaseProcessor
{
    private Building _building;

    // 공장 기계는 재료가 공급되는 한 계속 돌아야 하므로 true
    protected override bool IsAutomation => true; 

    private void Start()
    {
        _building = GetComponentInParent<Building>();
    }

    protected override bool HasEnoughIngredients()
    {
        if (_building == null || currentRecipe == null) return false;

        var snapshot = _building.Input.Snapshot(); // 전임자 버퍼 스냅샷 기능 사용[cite: 5]
        foreach (var input in currentRecipe.inputs)
        {
            int required = input.amount;
            int found = 0;
            foreach (var (item, count) in snapshot)
            {
                if (item == input.item) { found = count; break; }
            }
            if (found < required) return false;
        }
        return true;
    }

    protected override void ConsumeIngredients()
    {
        foreach (var input in currentRecipe.inputs)
        {
            _building.Input.TryConsume(input.item, input.amount); // 전임자 버퍼 차감 기능 사용[cite: 5]
        }
        _building.NotifyUpstream(); // 상류 벨트 신호 갱신[cite: 5]
    }

    protected override void GiveOutputs()
    {
        foreach (var output in currentRecipe.outputs)
        {
            if (output.item != null) _building.Output.TryAdd(output.item, output.amount);
        }
        _building.NotifyUpstream(); // 하류 벨트 신호 갱신[cite: 5]
    }
}
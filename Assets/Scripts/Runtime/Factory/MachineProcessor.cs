using UnityEngine;

[RequireComponent(typeof(Entities.Building))]
public class MachineProcessor : BaseProcessor
{
    private Building _building;   // 심 건물 (plain C#) — 엔티티 컴포넌트의 Sim으로 얻는다

    // 공장 기계는 재료가 공급되는 한 계속 돌아야 하므로 true
    protected override bool IsAutomation => true;

    private void Start()
    {
        // global::Building은 컴포넌트가 아니라 GetComponent로 찾을 수 없다 —
        // 씬 접점인 Entities.Building을 거쳐 심 참조를 얻는다
        var entity = GetComponentInParent<Entities.Building>();
        _building = entity != null ? entity.Sim : null;
        if (_building == null)
            Debug.LogWarning("[MachineProcessor] 심 건물(Sim) 연결을 찾지 못했습니다 — 배치된 건물에서만 동작합니다.", this);
    }

    protected override bool HasEnoughIngredients()
    {
        if (_building == null || currentRecipe == null) return false;

        var snapshot = _building.Input.Snapshot(); // 전임자 버퍼 스냅샷 기능 사용
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
            _building.Input.TryConsume(input.item, input.amount); // 전임자 버퍼 차감 기능 사용
        }
        _building.NotifyUpstream(); // 상류 벨트 신호 갱신
    }

    protected override void GiveOutputs()
    {
        foreach (var output in currentRecipe.outputs)
        {
            if (output.item != null) _building.Output.TryAdd(output.item, output.amount);
        }
        _building.NotifyUpstream(); // 하류 벨트 신호 갱신
    }
}
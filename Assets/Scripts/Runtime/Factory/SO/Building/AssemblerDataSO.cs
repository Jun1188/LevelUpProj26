using System.Linq;
using UnityEngine;

/// <summary>조합기. 입력 버퍼에 재료가 모이면 레시피대로 조합해 출력한다.</summary>
[CreateAssetMenu(fileName = "NewAssembler", menuName = "Factory/Assembler")]
public class AssemblerDataSO : BuildingDataSO
{
    [Header("레시피")]
    public RecipeDataSO[] availableRecipes;

    public override IBuildingBehavior CreateBehavior(BuildingInstance instance)
        => new AssemblerBehavior(instance, this);
}

// ─── 행동 ──────────────────────────────────────────────────────

/// <summary>
/// 입력 버퍼에 재료가 모이면 조합 시작 → 완료 후 출력 버퍼로.
/// 조합 완료 시점은 ScheduleWake로 예약한다.
///
/// stall 정책:
///   - 결과물이 출력 버퍼에 들어갈 자리가 없으면 조합을 시작하지 않는다.
///   - 완료 시점에 자리가 없으면 완료를 보류한다 (재료·결과물 유실 없음).
///   - 하류가 아이템을 소비하면 NotifyUpstream으로 깨어나 재개한다.
/// </summary>
public class AssemblerBehavior : IBuildingBehavior
{
    readonly BuildingInstance _b;
    RecipeDataSO _recipe;
    float        _readyAt;   // 조합 완료 예정 시각
    bool         _crafting;

    public AssemblerBehavior(BuildingInstance b, AssemblerDataSO data)
    {
        _b      = b;
        _recipe = data.availableRecipes?.FirstOrDefault();
    }

    public void SetRecipe(RecipeDataSO r) => _recipe = r;

    public void OnAfterPlaced() { }

    public void Tick(float dt)
    {
        if (_recipe == null) return;
        var sim = SimulationSystem.Instance;

        // 1. 출력 배출 시도 — 완료 판정보다 먼저 버퍼를 비워야 stall이 풀린다
        PushOutputs();

        // 2. 조합 완료 판정
        if (_crafting)
        {
            if (sim.Now < _readyAt) return;  // 이른 기상 (재료 도착 등) → 완료 시각에 다시 깨어남
            if (!CanStoreOutputs()) return;  // 출력 버퍼 막힘 → 완료 보류 (stall)

            foreach (var o in _recipe.outputs)
                _b.Inventory.TryAddOutput(o.item, o.amount);
            _crafting = false;
            PushOutputs();
        }

        // 3. 다음 조합 시작 — 재료가 모였고 결과물 들어갈 자리가 있을 때만
        if (!HasIngredients() || !CanStoreOutputs()) return;
        ConsumeIngredients();
        _b.NotifyUpstream();   // 입력 버퍼에 자리 생김 → 막혀 있던 상류 깨움
        _crafting = true;
        _readyAt  = sim.Now + _recipe.craftTime;
        sim.ScheduleWake(_b, _recipe.craftTime);
    }

    void PushOutputs()
    {
        foreach (var (item, count) in _b.Inventory.OutputSnapshot)
            for (int k = 0; k < count && _b.TryPushOutput(item); k++)
                _b.Inventory.TryConsumeOutput(item);
    }

    bool HasIngredients()
    {
        foreach (var i in _recipe.inputs)
            if (_b.Inventory.InputAmount(i.item) < i.amount) return false;
        return true;
    }

    /// <summary>레시피 출력 전량이 출력 버퍼에 들어갈 수 있는가.</summary>
    bool CanStoreOutputs()
    {
        foreach (var o in _recipe.outputs)
            if (_b.Inventory.OutputAmount(o.item) + o.amount > _b.Data.maxOutputBuffer)
                return false;
        return true;
    }

    void ConsumeIngredients()
    {
        foreach (var i in _recipe.inputs)
            _b.Inventory.TryConsumeInput(i.item, i.amount);
    }
}

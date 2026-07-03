using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ================================================================
//  BuildingSimulation.cs
//  건물 시뮬레이션 구동 — Dirty Queue + 벨트 최적화 + 행동 구현
//
//  포함:
//    SimulationSystem      — 변화 있는 건물만 틱 (Dirty Queue)
//    BeltSegment           — 연속 벨트를 하나의 유닛으로 처리
//    BeltSegmentManager    — 세그먼트 생성/병합/분리
//    IBuildingBehavior     — 건물 행동 인터페이스
//    MinerBehavior         — 자원 채굴 (주기적 아이템 생산)
//    BeltBehavior          — 컨베이어 벨트 (세그먼트 위임 or 단독)
//    AssemblerBehavior     — 재료 수집 → 조합 → 출력
//    StorageBehavior       — 대용량 버퍼, 요청 시 출력
//    BuildingBehaviorFactory — 카테고리별 행동 생성
// ================================================================

// ─── 시뮬레이션 시스템 ─────────────────────────────────────────

/// <summary>
/// Dirty Queue 기반 이벤트 주도 시뮬레이션.
///
/// 핵심 아이디어:
///   건물 10,000개 중 100개만 현재 활성 → 100번만 Tick() 호출.
///   아이템 수신, 생산 완료 등 "변화"가 생긴 건물만 큐에 넣는다.
///   큐에 없는 건물은 완전 무시.
///
/// MarkDirty 호출 시점:
///   - 건물이 아이템을 수신했을 때  (TryAddInput 성공)
///   - 건물의 내부 타이머가 돌고 있을 때  (Tick 내에서 재등록)
///   - 건물이 처음 배치됐을 때
/// </summary>
public class SimulationSystem : MonoBehaviour
{
    public static SimulationSystem Instance { get; private set; }

    [Tooltip("초당 틱 수. 10이면 0.1초마다 처리.")]
    [SerializeField] float _tps = 10f;

    readonly Queue<BuildingInstance>   _queue = new();
    readonly HashSet<BuildingInstance> _inQ   = new(); // 중복 등록 방지 O(1)

    float _interval, _timer;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance  = this;
        _interval = 1f / Mathf.Max(0.1f, _tps);
    }

    /// <summary>건물 배치 시 호출. 초기 틱을 예약한다.</summary>
    public void Register(BuildingInstance b) => MarkDirty(b);

    /// <summary>건물 제거 시 호출.</summary>
    public void Unregister(BuildingInstance b) => _inQ.Remove(b);

    /// <summary>
    /// 건물을 다음 틱 처리 대상에 추가. O(1).
    /// HashSet으로 중복 호출을 차단하므로 마음껏 호출해도 안전하다.
    /// </summary>
    public void MarkDirty(BuildingInstance b)
    {
        if (b == null) return;
        if (_inQ.Add(b)) _queue.Enqueue(b);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _interval) return;
        _timer -= _interval;
        RunTick();
    }

    void RunTick()
    {
        // 틱 시작 시점의 큐 크기만큼만 처리.
        // 이번 틱에서 새로 MarkDirty된 건물은 다음 틱에 처리된다.
        int count = _queue.Count;
        for (int i = 0; i < count; i++)
        {
            var b = _queue.Dequeue();
            _inQ.Remove(b);
            if (b == null || !b.gameObject.activeSelf) continue;
            b.Tick(_interval);
        }
    }
}


// ─── 행동 인터페이스 ────────────────────────────────────────────

public interface IBuildingBehavior
{
    /// <summary>SimulationSystem이 매 틱마다 호출.</summary>
    void Tick(float dt);

    /// <summary>
    /// BuildingGraph.OnPlaced() 완료 후 1회 호출.
    /// 이 시점에서는 InputConnections / OutputConnections가 모두 확정되어 있다.
    /// 자원 조회, 레시피 결정 등 연결 기반 초기화에 사용.
    /// </summary>
    void OnAfterPlaced();
}

// ─── 채굴기 행동 ────────────────────────────────────────────────

/// <summary>
/// 주기적으로 아이템을 생산해 출력 포트로 Push.
/// 어떤 아이템을 채굴할지는 OnAfterPlaced에서 외부 주입을 통해 결정.
/// (ResourceGrid 같은 공간 시스템은 이 파일의 관심사가 아님)
/// </summary>
public class MinerBehavior : IBuildingBehavior
{
    readonly BuildingInstance _b;
    ItemDataSO _target;
    float      _timer;

    public MinerBehavior(BuildingInstance b) => _b = b;

    // 외부(ResourceGrid 등)에서 OnAfterPlaced 이후 주입
    public void SetTarget(ItemDataSO item) => _target = item;

    public void OnAfterPlaced()
    {
        // 외부 서비스가 주입되어 있으면 사용
        // 없으면 SetTarget()으로 직접 설정
        if (MiningService.GetItemAt != null)
            _target = MiningService.GetItemAt(_b.Origin);
    }

    public void Tick(float dt)
    {
        if (_target == null) return;

        _timer += dt;
        if (_timer < _b.Data.processingTime)
        {
            SimulationSystem.Instance.MarkDirty(_b); // 타이머 진행 중 → 재등록
            return;
        }
        _timer -= _b.Data.processingTime;

        // 직접 Push 실패 시 자체 출력 버퍼에 보관 (벨트가 꽉 찬 경우)
        if (!_b.TryPushOutput(_target))
            _b.Inventory.TryAddOutput(_target);

        SimulationSystem.Instance.MarkDirty(_b); // 다음 채굴 예약
    }
}

/// <summary>
/// Miner가 채굴 대상을 결정할 때 사용하는 서비스 포인트.
/// ResourceGrid가 있다면 Awake에서 아래 델리게이트를 등록하면 된다.
/// 없으면 MinerBehavior.SetTarget()을 직접 호출.
/// </summary>
public static class MiningService
{
    public static Func<Vector2Int, ItemDataSO> GetItemAt;
}

// ─── 벨트 행동 ──────────────────────────────────────────────────

/// <summary>
/// 컨베이어 벨트.
/// </summary>
public class BeltBehavior : IBuildingBehavior
{
    readonly BuildingInstance _b;
    public BeltBehavior(BuildingInstance b) => _b = b;
    public void OnAfterPlaced() { }

    public void Tick(float dt)
    {
        var seg = BeltSegmentManager.Instance.EnsureSegment(_b);  // 항상 세그먼트 존재

        // 입력 버퍼 아이템을 벨트 위로 (입구가 막혔으면 받아준 만큼만 소비)
        foreach (var (item, count) in _b.Inventory.InputSnapshot)
        {
            int moved = 0;
            while (moved < count && seg.TryAddItem(item)) moved++;
            if (moved > 0) _b.Inventory.TryConsumeInput(item, moved);
        }

        // 대표 벨트(입구 = 마지막 인덱스)가 세그먼트 전체를 1번만 구동
        if (seg.BeltCount > 0 && seg.Belts[^1] == _b)
            seg.Tick(dt);

        // 입구가 막혀 버퍼가 안 비면 다음 틱에 재시도
        if (_b.Inventory.InputSnapshot.Count > 0)
            SimulationSystem.Instance.MarkDirty(_b);
    }
}

// ─── 조합기 행동 ────────────────────────────────────────────────

/// <summary>
/// 입력 버퍼에 재료가 모이면 조합 시작 → 완료 후 출력 버퍼로.
/// 조합 중에는 MarkDirty로 계속 재등록해 타이머를 진행시킨다.
/// 재료가 없으면 Dirty 등록 안 함 → 재료 도착(TryAddInput 성공) 시 재등록됨.
/// </summary>
public class AssemblerBehavior : IBuildingBehavior
{
    readonly BuildingInstance _b;
    RecipeDataSO _recipe;
    float        _timer;
    bool         _crafting;

    public AssemblerBehavior(BuildingInstance b)
    {
        _b      = b;
        _recipe = b.Data.availableRecipes?.FirstOrDefault();
    }

    public void SetRecipe(RecipeDataSO r) => _recipe = r;

    public void OnAfterPlaced() { }

    public void Tick(float dt)
    {
        if (_recipe == null) return;

        if (!_crafting)
        {
            if (!HasIngredients()) return; // 재료 없음 → Dirty 등록 안 함
            ConsumeIngredients();
            _crafting = true;
            _timer    = 0f;
            SimulationSystem.Instance.MarkDirty(_b);
        }
        else
        {
            _timer += dt;
            if (_timer < _recipe.craftTime)
            {
                SimulationSystem.Instance.MarkDirty(_b); // 조합 진행 중
                return;
            }

            // 조합 완료
            foreach (var o in _recipe.outputs)
                _b.Inventory.TryAddOutput(o.item, o.amount);
            _crafting = false;

            // 출력 Push 시도
            foreach (var (item, _) in _b.Inventory.OutputSnapshot)
                if (_b.TryPushOutput(item))
                    _b.Inventory.TryConsumeOutput(item);

            // 재료가 이미 있으면 즉시 다음 조합 예약
            if (HasIngredients())
                SimulationSystem.Instance.MarkDirty(_b);
        }
    }

    bool HasIngredients()
    {
        foreach (var i in _recipe.inputs)
            if (_b.Inventory.InputAmount(i.item) < i.amount) return false;
        return true;
    }

    void ConsumeIngredients()
    {
        foreach (var i in _recipe.inputs)
            _b.Inventory.TryConsumeInput(i.item, i.amount);
    }
}

// ─── 저장소 행동 ────────────────────────────────────────────────

/// <summary>
/// 큰 버퍼를 가진 저장소. 입력을 받고, 요청 시(연결된 건물이 Pull) 출력.
/// 특별한 처리 없이 TryPushOutput만 주기적으로 시도.
/// </summary>
public class StorageBehavior : IBuildingBehavior
{
    readonly BuildingInstance _b;
    public StorageBehavior(BuildingInstance b) => _b = b;
    public void OnAfterPlaced() { }

    public void Tick(float dt)
    {
        if (!_b.Inventory.HasOutput) return;
        foreach (var (item, _) in _b.Inventory.OutputSnapshot)
            if (_b.TryPushOutput(item))
                _b.Inventory.TryConsumeOutput(item);
    }
}

// ─── 행동 팩토리 ────────────────────────────────────────────────

/// <summary>
/// BuildingCategory → IBuildingBehavior 생성.
/// 새 카테고리/행동을 추가할 때 여기에만 케이스를 추가하면 된다.
/// </summary>
public static class BuildingBehaviorFactory
{
    public static IBuildingBehavior Create(BuildingCategory cat, BuildingInstance b) =>
        cat switch
        {
            BuildingCategory.Producer  => new MinerBehavior(b),
            BuildingCategory.Transport => new BeltBehavior(b),
            BuildingCategory.Processor => new AssemblerBehavior(b),
            BuildingCategory.Storage   => new StorageBehavior(b),
            _                          => null
        };
}

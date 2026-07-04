using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ================================================================
//  BuildingSimulation.cs
//  건물 시뮬레이션 구동 — Dirty Queue + 행동 구현
//
//  포함:
//    SimulationSystem      — 변화 있는 건물만 틱 (Dirty Queue)
//    IBuildingBehavior     — 건물 행동 인터페이스
//    MinerBehavior         — 자원 채굴 (주기적 아이템 생산)
//    BeltBehavior          — 컨베이어 벨트 (세그먼트 위임 or 단독)
//    AssemblerBehavior     — 재료 수집 → 조합 → 출력
//    StorageBehavior       — 대용량 버퍼, 요청 시 출력
//    BuildingBehaviorFactory — 카테고리별 행동 생성
//
//  관련 (별도 파일):
//    BeltSegment           — 연속 벨트를 하나의 유닛으로 처리
//    BeltSegmentManager    — 세그먼트 생성/병합/분리
// ================================================================

// ─── 시뮬레이션 시스템 ─────────────────────────────────────────

/// <summary>
/// Dirty Queue + Wake 예약 기반 이벤트 주도 시뮬레이션.
///
/// 핵심 아이디어:
///   건물 10,000개 중 100개만 현재 활성 → 100번만 Tick() 호출.
///   큐에 없는 건물은 완전 무시.
///
/// 건물을 깨우는 두 가지 경로:
///   MarkDirty(b)          — "지금 변화가 생겼다"
///                            (아이템 수신, 상류/하류 상태 변화, 최초 배치)
///   ScheduleWake(b, t)    — "t초 후에 깨워라"
///                            (채굴/조합 타이머의 완료 시점 예약)
///
/// 타이머가 도는 건물이 매 틱 자기를 재등록하는 대신 완료 시점에만
/// 깨어나므로, 큐에는 실제 처리할 일이 있는 건물만 남는다.
/// </summary>
public class SimulationSystem : MonoBehaviour
{
    public static SimulationSystem Instance { get; private set; }

    [Tooltip("초당 틱 수. 10이면 0.1초마다 처리.")]
    [SerializeField] float _tps = 10f;

    [Tooltip("프레임 드랍 후 한 프레임에 몰아서 따라잡을 수 있는 최대 틱 수.")]
    [SerializeField] int _maxCatchUpTicks = 5;

    readonly Queue<BuildingInstance>   _queue = new();
    readonly HashSet<BuildingInstance> _inQ   = new(); // 중복 등록 방지 O(1)

    // wake 예약 — (깨울 시각, 건물) 이진 min-heap. index 0 = 가장 이른 예약.
    readonly List<(float time, BuildingInstance b)> _wake = new();

    float _interval, _timer;

    /// <summary>시뮬레이션 누적 시간(초). 틱마다 _interval씩 증가한다.</summary>
    public float Now { get; private set; }

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

    /// <summary>
    /// delay초 후 건물을 깨운다(= MarkDirty). 타이머 완료 시점 예약용.
    /// 같은 건물을 중복 예약해도 안전하다 — 이른 기상은 각 행동이 Now로 걸러낸다.
    /// </summary>
    public void ScheduleWake(BuildingInstance b, float delay)
    {
        if (b == null) return;
        _wake.Add((Now + delay, b));
        for (int i = _wake.Count - 1; i > 0; )
        {
            int p = (i - 1) / 2;
            if (_wake[p].time <= _wake[i].time) break;
            (_wake[p], _wake[i]) = (_wake[i], _wake[p]);
            i = p;
        }
    }

    void PopWake()
    {
        _wake[0] = _wake[^1];
        _wake.RemoveAt(_wake.Count - 1);
        for (int i = 0; ; )
        {
            int l = 2 * i + 1, r = l + 1, m = i;
            if (l < _wake.Count && _wake[l].time < _wake[m].time) m = l;
            if (r < _wake.Count && _wake[r].time < _wake[m].time) m = r;
            if (m == i) break;
            (_wake[m], _wake[i]) = (_wake[i], _wake[m]);
            i = m;
        }
    }

    void Update()
    {
        _timer += Time.deltaTime;

        // 밀린 틱을 따라잡되, 프레임당 한도를 둬서 저사양에서
        // "틱 몰아치기 → 프레임 더 느려짐 → 더 밀림" 나선을 방지한다.
        int ticks = 0;
        while (_timer >= _interval && ticks < Mathf.Max(1, _maxCatchUpTicks))
        {
            _timer -= _interval;
            RunTick();
            ticks++;
        }

        // 한도를 넘긴 빚은 버린다 (다음 프레임에 처리할 1틱분만 유지).
        if (_timer > _interval) _timer = _interval;
    }

    void RunTick()
    {
        Now += _interval;

        // 예약 시각이 된 건물을 큐로 이동
        while (_wake.Count > 0 && _wake[0].time <= Now)
        {
            MarkDirty(_wake[0].b);
            PopWake();
        }

        // 틱 시작 시점의 큐 크기만큼만 처리.
        // 이번 틱에서 새로 MarkDirty된 건물은 다음 틱에 처리된다.
        int count = _queue.Count;
        for (int i = 0; i < count; i++)
        {
            var b = _queue.Dequeue();
            _inQ.Remove(b);
            if (b == null || b.IsRemoved || !b.gameObject.activeSelf) continue;
            b.Tick(_interval);
        }
    }
}


// ─── 행동 인터페이스 ────────────────────────────────────────────

public interface IBuildingBehavior
{
    /// <summary>
    /// SimulationSystem이 이 건물이 깨어 있는 틱에 호출.
    /// (MarkDirty로 등록됐거나 ScheduleWake 예약 시각이 됐을 때)
    /// </summary>
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
///
/// 채굴은 ScheduleWake로 완료 시점에만 깨어난다.
/// 출력 버퍼가 가득 차면 채굴을 멈추고(stall), 하류가 아이템을 소비해
/// NotifyUpstream으로 깨워줄 때 재개한다 → 아이템 유실 없음.
/// </summary>
public class MinerBehavior : IBuildingBehavior
{
    readonly BuildingInstance _b;
    ItemDataSO _target;
    float      _readyAt = -1f;   // 채굴 완료 예정 시각 (-1 = 예약 없음 = 정지 상태)

    public MinerBehavior(BuildingInstance b) => _b = b;

    // 외부(ResourceGrid 등)에서 OnAfterPlaced 이후 주입
    public void SetTarget(ItemDataSO item)
    {
        _target = item;
        SimulationSystem.Instance?.MarkDirty(_b);
    }

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
        var sim = SimulationSystem.Instance;

        // 1. 밀려 있던 출력 버퍼부터 배출 (하류가 받는 만큼 전부)
        foreach (var (item, count) in _b.Inventory.OutputSnapshot)
            for (int k = 0; k < count && _b.TryPushOutput(item); k++)
                _b.Inventory.TryConsumeOutput(item);

        // 2. 채굴 완료 판정
        if (_readyAt >= 0f && sim.Now >= _readyAt)
        {
            _readyAt = -1f;
            // 예약 시점에 버퍼 여유를 확인했으므로 여기서 유실될 수 없다
            if (!_b.TryPushOutput(_target))
                _b.Inventory.TryAddOutput(_target);
        }

        // 3. 다음 채굴 예약 — 출력 버퍼에 자리가 있을 때만.
        //    자리가 없으면 정지(stall); 하류의 NotifyUpstream이 다시 깨운다.
        if (_readyAt < 0f && _b.Inventory.OutputAmount(_target) < _b.Data.maxOutputBuffer)
        {
            _readyAt = sim.Now + _b.Data.processingTime;
            sim.ScheduleWake(_b, _b.Data.processingTime);
        }
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

        // 입력 버퍼 아이템을 벨트 위로 (입구가 막혔으면 받아준 만큼만 소비).
        // TryAddItem은 세그먼트 입구(pos 0) 삽입 — 생산자로부터 입력을 받는 벨트는
        // 상류 벨트가 없는 벨트뿐이므로(1입력 포트) 항상 자기 세그먼트의 입구다.
        foreach (var (item, count) in _b.Inventory.InputSnapshot)
        {
            int moved = 0;
            while (moved < count && seg.TryAddItem(item)) moved++;
            if (moved > 0)
            {
                _b.Inventory.TryConsumeInput(item, moved);
                _b.NotifyUpstream(); // 입력 버퍼에 자리 생김 → 막혀 있던 상류 깨움
            }
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

// ─── 저장소 행동 ────────────────────────────────────────────────

/// <summary>
/// 큰 버퍼를 가진 저장소.
/// 입력 버퍼로 받은 아이템을 출력 버퍼로 옮긴 뒤, 연결된 하류로 Push 시도.
/// </summary>
public class StorageBehavior : IBuildingBehavior
{
    readonly BuildingInstance _b;
    public StorageBehavior(BuildingInstance b) => _b = b;
    public void OnAfterPlaced() { }

    public void Tick(float dt)
    {
        // 입력 버퍼 → 출력 버퍼 이동 (출력 여유만큼만)
        foreach (var (item, count) in _b.Inventory.InputSnapshot)
        {
            int moved = 0;
            while (moved < count && _b.Inventory.TryAddOutput(item)) moved++;
            if (moved > 0)
            {
                _b.Inventory.TryConsumeInput(item, moved);
                _b.NotifyUpstream(); // 입력 버퍼에 자리 생김 → 막혀 있던 상류 깨움
            }
        }

        foreach (var (item, count) in _b.Inventory.OutputSnapshot)
            for (int k = 0; k < count && _b.TryPushOutput(item); k++)
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

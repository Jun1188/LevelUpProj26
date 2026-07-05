using System;
using UnityEngine;

/// <summary>채굴기. 주기적으로 아이템을 생산해 출력 포트로 내보낸다.</summary>
[CreateAssetMenu(fileName = "NewMiner", menuName = "Factory/Miner")]
public class MinerDataSO : BuildingDataSO
{
    [Header("채굴")]
    [Tooltip("아이템 1개 채굴에 걸리는 시간(초).")]
    public float processingTime = 1f;

    public override IBuildingBehavior CreateBehavior(BuildingInstance instance)
        => new MinerBehavior(instance, this);
}

// ─── 행동 ──────────────────────────────────────────────────────

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
    readonly MinerDataSO      _data;
    ItemDataSO _target;
    float      _readyAt = -1f;   // 채굴 완료 예정 시각 (-1 = 예약 없음 = 정지 상태)

    public MinerBehavior(BuildingInstance b, MinerDataSO data) { _b = b; _data = data; }

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
            _readyAt = sim.Now + _data.processingTime;
            sim.ScheduleWake(_b, _data.processingTime);
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

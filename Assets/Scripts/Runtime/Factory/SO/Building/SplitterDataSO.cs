using UnityEngine;

/// <summary>
/// 분배기. 입력 1개를 여러 출력 연결에 라운드로빈으로 고르게 나눈다.
/// 막힌 출구는 건너뛴다 (Factorio 스타일) — 한쪽이 막혀도 나머지로 계속 흐름.
/// 벨트가 아니므로 세그먼트는 분배기 앞뒤에서 끊긴다 (팀 합의: 분기/합류 = 전용 건물).
/// </summary>
[CreateAssetMenu(fileName = "NewSplitter", menuName = "Factory/Buildings/Splitter")]
public class SplitterDataSO : BuildingDataSO
{
    public override IBuildingBehavior CreateBehavior(Building building)
        => new SplitterBehavior(building);
}

// ─── 행동 ──────────────────────────────────────────────────────

public class SplitterBehavior : IBuildingBehavior
{
    readonly Building _b;
    int _next;   // 라운드로빈 커서 — 다음에 밀어볼 출력 연결 인덱스

    public SplitterBehavior(Building b) => _b = b;
    public void OnAfterPlaced() { }

    public void Tick(float dt)
    {
        // 입력 버퍼의 아이템을 출력 연결에 라운드로빈으로 직접 분배 (출력 버퍼 없음)
        foreach (var (item, count) in _b.Input.Snapshot())
        {
            int moved = 0;
            while (moved < count && TryPushRoundRobin(item)) moved++;
            if (moved > 0)
            {
                _b.Input.TryConsume(item, moved);
                _b.NotifyUpstream(); // 입력 버퍼에 자리 생김 → 막혀 있던 상류 깨움
            }
        }
        // 전부 막혔으면 stall — 하류가 소비하면 NotifyUpstream으로 깨어난다
    }

    /// <summary>_next부터 순서대로 출구를 시도, 성공하면 커서를 다음 출구로.</summary>
    bool TryPushRoundRobin(ItemDataSO item)
    {
        var conns = _b.OutputConnections;
        if (conns.Count == 0) return false;

        for (int i = 0; i < conns.Count; i++)
        {
            var c = conns[(_next + i) % conns.Count];
            if (!c.To.Input.TryAdd(item)) continue;   // 막힌 출구는 건너뜀
            _b.Sim.MarkDirty(c.To);
            _next = (_next + i + 1) % conns.Count;
            return true;
        }
        return false;
    }
}

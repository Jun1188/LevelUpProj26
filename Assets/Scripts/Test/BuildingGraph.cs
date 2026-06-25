using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ================================================================
//  BuildingGraph.cs
//  건물 간 상호작용의 핵심 — 연결 관리
//
//  포함:
//    BuildingInventory  — 건물별 입출력 아이템 버퍼
//    BuildingConnection — 두 건물 간의 단방향 연결
//    BuildingInstance   — 씬에 배치된 건물의 런타임 상태
//    GridRegistry       — 좌표 → BuildingInstance O(1) 조회
//                         (배치 로직은 없음, 다른 파트가 채워 넣는다)
//    BuildingGraph      — 포트 매칭으로 연결 자동 생성/해제
//
//  외부에서 건물을 배치할 때 호출 순서:
//    1. GridRegistry.Add(pos, instance)     ← 점유 셀 전부
//    2. BuildingGraph.Instance.OnPlaced(instance)
//    3. SimulationSystem.Instance.Register(instance)
//
//  제거 시:
//    1. BuildingGraph.Instance.OnRemoved(instance)
//    2. SimulationSystem.Instance.Unregister(instance)
//    3. GridRegistry.Remove(pos)            ← 점유 셀 전부
// ================================================================

// ─── 인벤토리 ───────────────────────────────────────────────────

/// <summary>
/// 건물의 입출력 아이템 버퍼.
/// Push-Pull 방식: 생산자가 다음 건물에 TryAddInput() 호출,
/// 소비자는 TryConsumeInput()으로 꺼낸다.
/// </summary>
public class BuildingInventory
{
    readonly Dictionary<string, (ItemDataSO item, int n)> _in  = new();
    readonly Dictionary<string, (ItemDataSO item, int n)> _out = new();
    public readonly int MaxIn, MaxOut;

    public BuildingInventory(int maxIn, int maxOut) { MaxIn = maxIn; MaxOut = maxOut; }

    // ── 입력 버퍼
    public bool TryAddInput(ItemDataSO it, int n = 1)
    {
        _in.TryGetValue(it.name, out var c);
        if (c.n + n > MaxIn) return false;
        _in[it.name] = (it, c.n + n); return true;
    }
    public bool TryConsumeInput(ItemDataSO it, int n = 1)
    {
        if (!_in.TryGetValue(it.name, out var c) || c.n < n) return false;
        int r = c.n - n; if (r == 0) _in.Remove(it.name); else _in[it.name] = (it, r); return true;
    }
    public int InputAmount(ItemDataSO it) => _in.TryGetValue(it.name, out var v) ? v.n : 0;

    // ── 출력 버퍼
    public bool TryAddOutput(ItemDataSO it, int n = 1)
    {
        _out.TryGetValue(it.name, out var c);
        if (c.n + n > MaxOut) return false;
        _out[it.name] = (it, c.n + n); return true;
    }
    public bool TryConsumeOutput(ItemDataSO it, int n = 1)
    {
        if (!_out.TryGetValue(it.name, out var c) || c.n < n) return false;
        int r = c.n - n; if (r == 0) _out.Remove(it.name); else _out[it.name] = (it, r); return true;
    }
    public int  OutputAmount(ItemDataSO it) => _out.TryGetValue(it.name, out var v) ? v.n : 0;
    public bool HasOutput                   => _out.Count > 0;

    // 순회 중 컬렉션 수정 방지 — 항상 ToList() 스냅샷 사용
    public List<(ItemDataSO item, int n)> OutputSnapshot => _out.Values.ToList();
    public List<(ItemDataSO item, int n)> InputSnapshot => _in.Values.ToList();
}

// ─── 연결 ───────────────────────────────────────────────────────

/// <summary>두 BuildingInstance 간의 단방향 아이템 연결.</summary>
public class BuildingConnection
{
    public BuildingInstance From, To;
    public PortDefinition   FromPort, ToPort;
}

// ─── 건물 런타임 ────────────────────────────────────────────────

/// <summary>
/// 씬에 배치된 건물의 런타임 인스턴스.
/// BuildingDataSO = 설계도 (공유됨),  BuildingInstance = 실물 (각자 독립적 상태).
///
/// 연결 목록(InputConnections, OutputConnections)은 BuildingGraph가 채운다.
/// 행동(IBuildingBehavior)은 Strategy 패턴으로 카테고리별로 분기된다.
/// </summary>




// ─── 건물 그래프 ────────────────────────────────────────────────

/// <summary>
/// 건물 간 연결을 방향 그래프(인접 리스트)로 관리.
///
/// [포트 매칭 알고리즘 — OnPlaced에서 1회 실행]
///
///   새 건물 A의 출력 포트 P:
///     ① P의 그리드 좌표   = A.Origin + P.LocalOffset
///     ② 이웃 셀 좌표      = ①의 좌표 + P.Direction 방향으로 1칸
///     ③ 이웃 건물 B       = GridRegistry.GetAt(②)   — O(1)
///     ④ B의 입력 포트 중
///          Direction == Opposite(P.Direction) 인 포트 → 연결!
///
///   제거 시 해당 건물의 모든 연결을 양방향으로 제거.
///
/// [복잡성]
///   연결 생성: O(p)   p = 포트 수 (건물당 보통 1~4)
///   연결 조회: O(1)   Dictionary TryGetValue
///   연결 제거: O(c)   c = 이 건물에 연결된 수
/// </summary>
public class BuildingGraph : MonoBehaviour
{
    public static BuildingGraph Instance { get; private set; }

    // 인접 리스트 — 나가는/들어오는 연결 분리 저장
    readonly Dictionary<BuildingInstance, List<BuildingConnection>> _out = new();
    readonly Dictionary<BuildingInstance, List<BuildingConnection>> _in  = new();

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    // ── 외부 호출 진입점

    /// <summary>건물이 GridRegistry에 등록된 직후 호출.</summary>
    public void OnPlaced(BuildingInstance b)
    {
        _out[b] = new List<BuildingConnection>();
        _in[b]  = new List<BuildingConnection>();
        FindAndRegister(b);            // 포트 매칭
        b.OnAfterConnected();          // 행동 초기화 (Miner가 자원 조회 등)
    }

    /// <summary>건물 제거 직전 호출. GridRegistry.Remove 전에 호출해야 한다.</summary>
    public void OnRemoved(BuildingInstance b)
    {
        foreach (var c in _out[b])
        {
            _in[c.To].Remove(c);
            c.To.InputConnections.Remove(c);
        }
        foreach (var c in _in[b])
        {
            _out[c.From].Remove(c);
            c.From.OutputConnections.Remove(c);
        }
        _out.Remove(b);
        _in.Remove(b);

        BeltSegmentManager.Instance?.OnBuildingRemoved(b);
    }

    // ── 포트 매칭 (핵심 알고리즘)

    void FindAndRegister(BuildingInstance nb)
    {
        var ports = nb.GetEffectivePorts();
        if (ports == null) return;

        foreach (var port in ports)
        {
            // ① 이 포트의 그리드 좌표
            var portCell    = nb.Origin + port.LocalOffset;
            // ② 이웃 셀
            var neighborCell = portCell + Dir.ToVec(port.Direction);
            var neighbor    = GridRegistry.Instance.GetAt(neighborCell);
            if (neighbor == null) continue;

            var opp        = Dir.Opposite(port.Direction);
            var nPorts     = neighbor.GetEffectivePorts();
            if (nPorts == null) continue;

            foreach (var np in nPorts)
            {
                // 방향 반대 + 입출력 반대여야 연결 가능
                if (np.Direction != opp || np.IsInput == port.IsInput) continue;

                // 연결 방향: 출력 → 입력
                var conn = !port.IsInput
                    ? new BuildingConnection { From = nb, FromPort = port, To = neighbor, ToPort = np }
                    : new BuildingConnection { From = neighbor, FromPort = np, To = nb, ToPort = port };

                // 중복 방지
                if (_out[conn.From].Any(c => c.To == conn.To && c.FromPort == conn.FromPort))
                    break;

                RegisterConn(conn);
                break; // 포트당 연결 1개
            }
        }
    }

    void RegisterConn(BuildingConnection c)
    {
        _out[c.From].Add(c); _in[c.To].Add(c);
        c.From.OutputConnections.Add(c);
        c.To.InputConnections.Add(c);
        BeltSegmentManager.Instance?.OnNewConnection(c);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ================================================================
//  BuildingGraph.cs
//  건물 간 상호작용의 핵심 — 연결 관리
//
//  포함:
//    BuildingConnection — 두 건물 간의 단방향 연결
//    BuildingGraph      — 포트 매칭으로 연결 자동 생성/해제
//
//  관련 (별도 파일):
//    ItemContainer      — 건물 입출력 버퍼 (슬롯 기반)
//    BuildingInstance   — 씬에 배치된 건물의 런타임 상태
//    GridRegistry       — 좌표 → BuildingInstance O(1) 조회 (GridSystem 폴더)
//
//  외부에서 건물을 배치할 때 호출 순서 (PlacementBridge가 담당):
//    1. GridRegistry.Add(pos, instance)     ← 점유 셀 전부
//    2. BuildingGraph.Instance.OnPlaced(instance)
//    3. SimulationSystem.Instance.Register(instance)
//
//  제거 시:
//    1. BuildingGraph.Instance.OnRemoved(instance)
//    2. SimulationSystem.Instance.Unregister(instance)
//    3. GridRegistry.Remove(pos)            ← 점유 셀 전부
// ================================================================

// ─── 연결 ───────────────────────────────────────────────────────

/// <summary>두 BuildingInstance 간의 단방향 아이템 연결.</summary>
public class BuildingConnection
{
    public BuildingInstance From, To;
    public PortDefinition   FromPort, ToPort;
}

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

        // 새 연결 = 상태 변화. 출력이 막혀 정지(stall)해 있던 건물이
        // 새로 생긴 하류로 밀어낼 수 있도록 양단을 깨운다.
        // (이게 없으면 "마이너 먼저 설치 → 버퍼 가득 → 벨트 연결" 시 영구 정지)
        SimulationSystem.Instance?.MarkDirty(c.From);
        SimulationSystem.Instance?.MarkDirty(c.To);
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬에 배치된 건물의 런타임 인스턴스.
/// BuildingDataSO = 설계도 (공유됨),  BuildingInstance = 실물 (각자 독립적 상태).
///
/// 연결 목록(InputConnections, OutputConnections)은 BuildingGraph가 채운다.
/// 행동(IBuildingBehavior)은 Strategy 패턴으로 카테고리별로 분기된다.
/// </summary>
public class BuildingInstance : MonoBehaviour
{
    // 불변 데이터 (Initialize 이후 변경 안 됨)
    public BuildingDataSO Data { get; private set; }
    public Vector2Int Origin { get; private set; }
    public int RotationSteps { get; private set; }

    // 런타임 상태
    public BuildingInventory Inventory { get; private set; }
    public bool IsDirty { get; set; } // SimulationSystem이 관리

    // PlacementBridge.Remove가 설정. Destroy는 프레임 끝에 실행되므로,
    // 그 사이에 시뮬레이션이 이 건물을 되살리지 못하도록 막는 표식.
    public bool IsRemoved { get; set; }

    // 연결 목록 — BuildingGraph가 OnPlaced/OnRemoved 시 수정
    public readonly List<BuildingConnection> InputConnections = new();
    public readonly List<BuildingConnection> OutputConnections = new();

    // 행동 전략
    IBuildingBehavior _behavior;

    /// <summary>
    /// 배치 직후, GridRegistry 등록 전에 호출.
    /// rotationSteps: 배치 시 회전 (0~3, 시계 방향 90° 단위)
    /// </summary>
    public void Initialize(BuildingDataSO data, Vector2Int origin, int rotSteps = 0)
    {
        Data = data;
        Origin = origin;
        RotationSteps = rotSteps;
        Inventory = new BuildingInventory(data.maxInputBuffer, data.maxOutputBuffer);
        _behavior = data.CreateBehavior(this);
    }

    /// <summary>회전이 적용된 실제 포트 목록. BuildingGraph가 이걸 사용한다.</summary>
    public PortDefinition[] GetEffectivePorts() => Data.GetRotatedPorts(RotationSteps);

    /// <summary>BuildingGraph.OnPlaced() 완료 후 호출 — 연결이 확정된 뒤 초기화.</summary>
    public void OnAfterConnected() => _behavior?.OnAfterPlaced();

    /// <summary>SimulationSystem이 틱마다 호출.</summary>
    public void Tick(float dt) => _behavior?.Tick(dt);

    /// <summary>
    /// 출력 버퍼의 아이템을 연결된 다음 건물로 Push.
    /// 성공하면 수신 건물을 Dirty 마킹 → 다음 틱에 처리됨.
    /// </summary>
    public bool TryPushOutput(ItemDataSO item)
    {
        foreach (var c in OutputConnections)
        {
            if (!c.To.Inventory.TryAddInput(item)) continue;
            SimulationSystem.Instance.MarkDirty(c.To);
            return true;
        }
        return false; // 모든 출력 막힘
    }

    /// <summary>
    /// 이 건물의 입력 버퍼에 자리가 생겼음을 상류에 알린다.
    /// 출력이 막혀 정지(stall)해 있던 상류 건물이 다음 틱에 재시도한다.
    /// </summary>
    public void NotifyUpstream()
    {
        foreach (var c in InputConnections)
            SimulationSystem.Instance.MarkDirty(c.From);
    }
}
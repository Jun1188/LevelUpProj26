using UnityEngine;

/// <summary>
/// 심 엔티티(Building)의 씬 표현 — 로직 없음, GameObject와 심을 잇는 다리.
/// PlacementBridge가 생성/파괴하고 FactoryBootstrap이 Building↔View 매핑을 관리한다.
/// (구 BuildingInstance — 시뮬레이션 상태는 전부 plain C# Building으로 이전됨)
/// </summary>
public class BuildingView : MonoBehaviour
{
    public Building Building;
}

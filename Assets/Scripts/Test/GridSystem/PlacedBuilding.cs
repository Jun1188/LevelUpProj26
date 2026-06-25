using UnityEngine;

/// <summary>
/// 그리드에 실제로 설치된 건물 하나의 인스턴스 정보.
/// BuildingData(설계도/원본)와 달리, 설치할 때마다 새로 생성된다.
/// 멀티셀 건물이면 풋프린트가 덮는 모든 칸이 같은 PlacedBuilding 객체를 가리킨다.
/// </summary>
public class PlacedBuilding
{
    public BuildingData data;        // 어떤 종류인지 (원본 참조)
    public Vector2Int originCell;    // 풋프린트의 기준 칸 (왼쪽 아래)
    public int rotation;             // 0,1,2,3 (각 90도)
    public GameObject instance;      // 씬에 생성된 실제 GameObject

    public PlacedBuilding(BuildingData data, Vector2Int originCell,
                          int rotation, GameObject instance)
    {
        this.data = data;
        this.originCell = originCell;
        this.rotation = rotation;
        this.instance = instance;
    }
}

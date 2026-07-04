using UnityEngine;

/// <summary>
/// 건물 배치/제거 시 시스템 호출 순서(GridRegistry → BuildingGraph → SimulationSystem)를
/// 한 곳에 캡슐화한 정적 진입점.
/// </summary>
public static class PlacementBridge
{
    public static BuildingInstance Place(BuildingDataSO so, Vector2Int origin, Vector3 pos = default, int rotSteps = 0)
    {
        GameObject go;
        if (so.prefab != null) go = Object.Instantiate(so.prefab, pos, Quaternion.Euler(0, rotSteps * 90f, 0));
        else go = new GameObject(so.name); // 프리팹 누락 시 크래시 방지용 빈 오브젝트

        var instance = go.GetComponent<BuildingInstance>();
        if (instance == null) instance = go.AddComponent<BuildingInstance>();
        
        instance.Initialize(so, origin, rotSteps);

        var rotSize = so.GetRotatedSize(rotSteps);
        for (int x = 0; x < rotSize.x; x++)
            for (int y = 0; y < rotSize.y; y++)
                GridRegistry.Instance.Add(origin + new Vector2Int(x, y), instance);

        BuildingGraph.Instance.OnPlaced(instance);
        SimulationSystem.Instance.Register(instance);

        return instance;
    }

    public static void Remove(BuildingInstance instance)
    {
        if (instance == null) return;

        // 0. 제거 표식 — 같은 프레임에 dirty 큐에 남아 있어도 Tick되지 않게
        instance.IsRemoved = true;

        // 1. SimulationSystem에서 제거
        SimulationSystem.Instance.Unregister(instance);

        // 2. BuildingGraph에서 연결 정리
        BuildingGraph.Instance.OnRemoved(instance);

        // 3. GridRegistry에서 점유 해제
        var rotSize = instance.Data.GetRotatedSize(instance.RotationSteps);
        for (int x = 0; x < rotSize.x; x++)
            for (int y = 0; y < rotSize.y; y++)
                GridRegistry.Instance.Remove(instance.Origin + new Vector2Int(x, y));

        // 4. 게임 오브젝트 삭제
        Object.Destroy(instance.gameObject);
    }
}
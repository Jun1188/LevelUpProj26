using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;

public static class PlacementBridge // MonoBehaviour 상속 제거
{
    public static BuildingInstance Place(BuildingDataSO so, Vector2Int origin, int rotSteps = 0)
    {
        // ⚠️ 주의: 이전 답변에서 언급한 크래시 방지를 위해 임시 프리팹 생성 로직 추가
        GameObject go;
        Debug.Log(so.prefab);
        if (so.prefab != null) go = Object.Instantiate(so.prefab, GridToWorld(origin), Quaternion.Euler(0, rotSteps * 90f, 0));
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

    static Vector3 GridToWorld(Vector2Int g) => new(g.x + 0.5f, 0f, g.y + 0.5f);
}
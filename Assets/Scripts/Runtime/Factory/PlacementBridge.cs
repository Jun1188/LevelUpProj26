using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;

public static class PlacementBridge // MonoBehaviour 상속 제거
{
    public static BuildingInstance Place(BuildingDataSO so, Vector2Int origin, Vector3 pos = default, int rotSteps = 0)
    {
        // ⚠️ 주의: 이전 답변에서 언급한 크래시 방지를 위해 임시 프리팹 생성 로직 추가
        GameObject go;
        Debug.Log(so.prefab);
        if (so.prefab != null) go = Object.Instantiate(so.prefab, pos, Quaternion.Euler(0, rotSteps * 90f, 0));
        else go = new GameObject(so.name); // 프리팹 누락 시 크래시 방지용 빈 오브젝트

        var instance = go.GetComponent<BuildingInstance>();
        if (instance == null) instance = go.AddComponent<BuildingInstance>();

        // 몬스터가 공격할 수 있도록 피격 컴포넌트 부착
        if (go.GetComponent<BuildingDamageable>() == null) go.AddComponent<BuildingDamageable>();

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
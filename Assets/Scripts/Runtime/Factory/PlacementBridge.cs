using UnityEngine;

/// <summary>
/// 배치/제거의 Unity 쪽 진입점 — 심 배치(FactorySim)와 뷰 생성(BuildingView)을 묶는다.
/// 심만 필요하면(테스트 등) FactorySim.Place/Remove를 직접 호출하면 된다.
/// </summary>
public static class PlacementBridge
{
    /// <param name="portOverride">인스턴스별 포트 형상 (벨트 커브 등). null이면 SO 포트 사용.</param>
    /// <param name="prefabOverride">인스턴스별 프리팹 (벨트 커브 메시 등). null이면 SO 프리팹 사용.</param>
    public static Building Place(BuildingDataSO so, Vector2Int origin, Vector3 pos = default, int rotSteps = 0,
        PortDefinition[] portOverride = null, GameObject prefabOverride = null)
    {
        var boot = FactoryBootstrap.Instance;
        var b = boot.Sim.Place(so, origin, rotSteps, portOverride);

        // 뷰 생성
        var prefab = prefabOverride != null ? prefabOverride : so.prefab;
        GameObject go = prefab != null
            ? Object.Instantiate(prefab, pos, Quaternion.Euler(0, rotSteps * 90f, 0))
            : new GameObject(so.name);   // 프리팹 누락 시 빈 오브젝트

        var view = go.GetComponent<BuildingView>();
        if (view == null) view = go.AddComponent<BuildingView>();
        view.Building = b;
        boot.RegisterView(b, view);

        return b;
    }

    public static void Remove(Building b)
    {
        if (b == null || b.IsRemoved) return;
        var boot = FactoryBootstrap.Instance;

        boot.Sim.Remove(b);

        var view = boot.GetView(b);
        boot.UnregisterView(b);
        if (view != null) Object.Destroy(view.gameObject);
    }
}

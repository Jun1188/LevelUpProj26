using UnityEngine;
using System.Collections;
using System.Collections.Generic;
// ─── 그리드 레지스트리 ──────────────────────────────────────────

/// <summary>
/// 좌표 → BuildingInstance O(1) 조회.
/// 배치 로직은 없다. 외부에서 Add/Remove만 호출하면 된다.
///
/// BuildingGraph의 포트 매칭에서만 사용된다.
/// </summary>
public class GridRegistry : MonoBehaviour
{
    public static GridRegistry Instance { get; private set; }

    readonly Dictionary<Vector2Int, BuildingInstance> _grid = new();

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    /// <summary>건물이 점유하는 모든 셀을 등록. 멀티타일 건물은 셀 수만큼 호출.</summary>
    public void Add(Vector2Int cell, BuildingInstance b) => _grid[cell] = b;
    public void Remove(Vector2Int cell) => _grid.Remove(cell);
    public BuildingInstance GetAt(Vector2Int cell) => _grid.TryGetValue(cell, out var b) ? b : null;
    public bool IsOccupied(Vector2Int cell) => _grid.ContainsKey(cell);
}
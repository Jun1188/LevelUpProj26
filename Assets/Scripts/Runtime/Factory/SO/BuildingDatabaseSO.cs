using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프로젝트의 모든 건물 SO 레지스트리 — 수동 연결 금지.
/// 에디터 스캐너(Editor/BuildingDatabaseScanner)가 BuildingDataSO 에셋을 만들거나
/// 지울 때마다 자동으로 이 목록을 갱신한다 (카테고리 → 표시명 순 정렬).
///
/// 소비자는 이 에셋 하나만 참조하면 된다:
///   - PlacementSystem: 배치 후보 목록 (미연결 시 Resources 폴백)
///   - BuildMenuPopup: 카테고리별 버튼 자동 생성
/// </summary>
[CreateAssetMenu(fileName = "BuildingDatabase", menuName = "Factory/Building Database")]
public class BuildingDatabaseSO : ScriptableObject
{
    [Tooltip("자동 수집됨 — 직접 편집하지 말 것 (Tools/Factory/Rebuild Building Database로 재수집)")]
    public BuildingDataSO[] buildings;

    /// <summary>Resources의 기본 데이터베이스. 씬 연결 없이도 어디서든 접근 가능.</summary>
    public static BuildingDatabaseSO LoadDefault()
        => Resources.Load<BuildingDatabaseSO>("BuildingDatabase");

    /// <summary>카테고리별 그룹 (enum 선언 순서 = 메뉴 표시 순서). 빈 카테고리는 생략.</summary>
    public IEnumerable<(BuildingCategory category, List<BuildingDataSO> items)> GroupedByCategory()
    {
        if (buildings == null) yield break;

        var groups = new Dictionary<BuildingCategory, List<BuildingDataSO>>();
        foreach (var b in buildings)
        {
            if (b == null) continue;
            if (!groups.TryGetValue(b.category, out var list))
                groups[b.category] = list = new List<BuildingDataSO>();
            list.Add(b);
        }

        foreach (BuildingCategory cat in System.Enum.GetValues(typeof(BuildingCategory)))
            if (groups.TryGetValue(cat, out var list))
                yield return (cat, list);
    }
}

using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BuildingDatabaseSO 자동 수집기 — "SO를 만들면 메뉴에 저절로 나타난다"의 에디터 쪽 절반.
/// 건물 SO가 임포트/삭제/이동될 때마다 프로젝트의 모든 데이터베이스를 재수집한다.
/// 정렬: 카테고리(enum 순서) → displayName. 수동 연결·정리 작업 불필요.
/// </summary>
public class BuildingDatabaseScanner : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        // 건물 SO나 데이터베이스가 변한 경우에만 재수집 (에셋 저장 루프 방지는 Rebuild 내부의 변경 검사)
        bool relevant = imported.Concat(deleted).Concat(moved)
            .Any(p => p.EndsWith(".asset") &&
                      (AssetDatabase.GetMainAssetTypeAtPath(p) == null ||   // 삭제된 에셋은 타입 조회 불가
                       typeof(BuildingDataSO).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(p)) ||
                       AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(BuildingDatabaseSO)));
        if (relevant) RebuildAll();
    }

    [MenuItem("Tools/Factory/Rebuild Building Database")]
    public static void RebuildAll()
    {
        var all = AssetDatabase.FindAssets("t:BuildingDataSO")
            .Select(g => AssetDatabase.LoadAssetAtPath<BuildingDataSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(b => b != null)
            .OrderBy(b => (int)b.category)
            .ThenBy(b => b.displayName, System.StringComparer.Ordinal)
            .ToArray();

        foreach (var guid in AssetDatabase.FindAssets("t:BuildingDatabaseSO"))
        {
            var db = AssetDatabase.LoadAssetAtPath<BuildingDatabaseSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (db == null || (db.buildings != null && db.buildings.SequenceEqual(all))) continue;

            db.buildings = all;
            EditorUtility.SetDirty(db);
            Debug.Log($"[BuildingDatabase] '{db.name}' 재수집 — 건물 {all.Length}종", db);
        }
    }
}

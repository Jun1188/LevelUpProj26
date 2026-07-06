using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 모든 데이터 SO(아이템/건물/레시피 등)의 공통 베이스.
/// 정체성·표시 메타데이터만 갖는다 — 도메인 데이터는 서브클래스에.
///
/// 식별 원칙:
///   런타임 구분 = SO 참조 비교 (에셋 = 유일 객체, id 불필요)
///   세이브/조회 = Id ("폴더 경로::displayName" 자동 생성, 수동 지정 가능)
///   표시        = displayName / description / icon
/// </summary>
public abstract class GameDataSO : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브/조회용 안정 ID. 비워두면 에셋 파일명이 자동 입력된다.\n" +
             "규칙: 세이브 데이터가 존재하는 id는 절대 변경 금지. 프로젝트 전체에서 유일해야 함.")]
    [SerializeField] string id;

    [Header("표시")]
    [FormerlySerializedAs("name")]           // 기존 에셋의 'name' 필드 값을 그대로 이전
    public string displayName;
    [TextArea]
    public string description;
    public Sprite icon;

    public string Id => id;

    protected virtual void OnValidate()
    {
#if UNITY_EDITOR
        // displayName이 채워진 뒤에만 id를 생성한다 — 새 에셋이 "NewItem" 상태로
        // id가 굳어버리는 것을 방지. 한번 생성된 id는 폴더 이동/개명해도 유지된다(세이브 안정성).
        if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(displayName))
            id = GenerateDefaultId();

        WarnOnDuplicateId();
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// 기본 id = "Data 폴더 기준 상대 폴더 경로::displayName".
    /// 예: Assets/Data/Item/IronOre.asset (displayName "IronOre") → "Item::IronOre"
    ///     Assets/Data/Item/Ores/… 처럼 중첩되면 "Item/Ores::…"
    /// Data 폴더 밖의 에셋은 displayName만 사용.
    /// </summary>
    string GenerateDefaultId()
    {
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (!string.IsNullOrEmpty(assetPath))
        {
            var parts   = assetPath.Split('/');
            int dataIdx = Array.IndexOf(parts, "Data");
            int folderCount = parts.Length - dataIdx - 2;   // Data 다음부터 파일명 전까지
            if (dataIdx >= 0 && folderCount > 0)
                return $"{string.Join("/", parts, dataIdx + 1, folderCount)}::{displayName}";
        }
        return displayName;
    }

    void WarnOnDuplicateId()
    {
        if (string.IsNullOrEmpty(id)) return;
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:GameDataSO"))
        {
            var path  = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var other = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataSO>(path);
            if (other != null && other != this && other.id == id)
            {
                Debug.LogError($"[GameDataSO] id 중복: '{id}' — {name} 와 {other.name}", this);
                return;
            }
        }
    }
#endif
}

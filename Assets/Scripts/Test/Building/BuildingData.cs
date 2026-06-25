using UnityEngine;

/// <summary>
/// 건물의 "설계도/원본" 데이터. 종류별로 에셋 하나씩 만든다.
/// 메뉴: Assets > Create > Factory > Building
/// </summary>
[CreateAssetMenu(menuName = "Factory/Building")]
public class BuildingData : ScriptableObject
{
    public string id;
    public GameObject prefab;

    [Tooltip("풋프린트 크기")]
    public Vector2Int size = Vector2Int.one;

    [Tooltip("피벗 보정용 높이 오프셋")]
    public float yOffset = 0f;
}

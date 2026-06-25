using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ================================================================
//  BuildingData.cs
//  ScriptableObject 정의 + 포트 시스템 지원 타입
//
//  이 파일만 있으면 Inspector에서 건물/아이템/레시피를 전부 정의할 수 있다.
//  건물 배치, 적, 게임 로직은 이 파일과 무관하다.
// ================================================================

// ─── 기본 열거형 ────────────────────────────────────────────────

public enum Direction        { North, East, South, West }
public enum BuildingCategory { Producer, Transport, Processor, Storage, Utility }
public enum ItemType         { Ore, Ingot, Component, Fuel, Misc }

// ─── 방향 헬퍼 ─────────────────────────────────────────────────

public static class Dir
{
    static readonly Vector2Int[] _v = { new(0,1), new(1,0), new(0,-1), new(-1,0) };

    public static Vector2Int ToVec(Direction d) => _v[(int)d];
    public static Direction   Opposite(Direction d) => (Direction)(((int)d + 2) % 4);

    // 시계 방향으로 steps만큼 회전 (건물 회전 지원용)
    public static Direction RotateCW(Direction d, int steps = 1) =>
        (Direction)(((int)d + steps + 4) % 4);

    // Vector2Int 오프셋 시계 방향 90° 회전: (x,y) → (y,−x)
    public static Vector2Int RotateOffsetCW(Vector2Int v) => new(v.y, -v.x);
    public static Vector2Int RotateOffset(Vector2Int v, int cwSteps)
    {
        for (int i = 0; i < ((cwSteps % 4) + 4) % 4; i++) v = RotateOffsetCW(v);
        return v;
    }
}

// ─── 포트 정의 ──────────────────────────────────────────────────

/// <summary>
/// 건물의 입출력 연결점.
/// BuildingDataSO.ports[] 배열에 Inspector로 설정.
///
/// 예 — Miner (1×1, 오른쪽 출력):
///   ports[0]: IsInput=false, LocalOffset=(0,0), Direction=East
///
/// 예 — Belt (1×1, 왼쪽 입력→오른쪽 출력):
///   ports[0]: IsInput=true,  LocalOffset=(0,0), Direction=West
///   ports[1]: IsInput=false, LocalOffset=(0,0), Direction=East
///
/// 예 — Assembler 2×1 (왼쪽 두 입력, 오른쪽 출력):
///   ports[0]: IsInput=true,  LocalOffset=(0,0), Direction=West
///   ports[1]: IsInput=true,  LocalOffset=(0,1), Direction=West
///   ports[2]: IsInput=false, LocalOffset=(1,0), Direction=East
/// </summary>
[Serializable]
public class PortDefinition
{
    public Vector2Int LocalOffset;    // 건물 Origin 기준 상대 그리드 좌표
    public Direction  Direction;      // 포트가 향하는 방향 (아이템 흐름 방향)
    public bool       IsInput;        // true = 수신 포트,  false = 배출 포트
    public ItemType[] AcceptedTypes;  // null 또는 빈 배열 = 모든 타입 허용
}

// ─── ScriptableObjects ──────────────────────────────────────────

/// <summary>
/// 건물 종류를 정의하는 ScriptableObject.
/// 씬에 배치된 건물 100개가 같은 SO 1개를 공유한다 (메모리 효율).
/// </summary>
[CreateAssetMenu(fileName = "NewBuilding", menuName = "Factory/Building")]
public class BuildingDataSO : ScriptableObject
{
    [Header("식별")]
    public new string    name;
    public string        description;
    public Sprite        icon;
    public GameObject    prefab;

    [Header("그리드 크기")]
    public Vector2Int size = Vector2Int.one; // 타일 단위 (1×1, 2×1 등)

    [Header("카테고리")]
    public BuildingCategory category;

    [Header("포트 — 건물 간 연결의 핵심")]
    public PortDefinition[] ports;

    [Header("레시피 / 처리 시간")]
    public RecipeDataSO[]   availableRecipes;
    public float            processingTime = 1f;

    [Header("버퍼 크기")]
    public int              maxInputBuffer  = 10;
    public int              maxOutputBuffer = 10;

    // ── 회전 지원 (배치 시 사용, 상호작용 로직과 무관)
    public Vector2Int GetRotatedSize(int cwSteps) =>
        cwSteps % 2 == 0 ? size : new Vector2Int(size.y, size.x);

    public PortDefinition[] GetRotatedPorts(int cwSteps)
    {
        if (ports == null || cwSteps == 0) return ports;
        return ports.Select(p => new PortDefinition
        {
            IsInput       = p.IsInput,
            AcceptedTypes = p.AcceptedTypes,
            Direction     = Dir.RotateCW(p.Direction, cwSteps),
            LocalOffset   = Dir.RotateOffset(p.LocalOffset, cwSteps),
        }).ToArray();
    }
}


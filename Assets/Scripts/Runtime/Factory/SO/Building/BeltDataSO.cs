using UnityEngine;

/// <summary>
/// 벨트의 모양 — 배치 시점에 결정되는 인스턴스 상태 (SO가 아님).
/// 출력 방향은 RotationSteps가 정하고, 모양은 입력이 어느 쪽에서 오는지를 정한다.
/// </summary>
public enum BeltShape { Straight, CurveL, CurveR }

/// <summary>
/// 컨베이어 벨트. 연속된 같은 종류의 벨트는 BeltSegment로 묶여 처리된다.
/// 직선/커브는 별도 SO가 아니라 배치 시 결정되는 모양(BeltShape) —
/// 포트는 BuildPorts()로 계산해 Building.PortOverride에 주입한다.
/// </summary>
[CreateAssetMenu(fileName = "NewBelt", menuName = "Factory/Buildings/Belt")]
public class BeltDataSO : BuildingDataSO
{
    [Header("운반")]
    [Tooltip("아이템 이동 속도 (타일/초).")]
    public float speedTilesPerSec = 2f;

    [Header("커브 프리팹 (기본 prefab = 직선)")]
    public GameObject curveLPrefab;
    public GameObject curveRPrefab;

    public GameObject PrefabFor(BeltShape shape) => shape switch
    {
        BeltShape.CurveL => curveLPrefab,
        BeltShape.CurveR => curveRPrefab,
        _                => prefab,
    };

    public override IBuildingBehavior CreateBehavior(Building building)
        => new BeltBehavior(building);

    // ── 모양별 포트 계산 (모양 3 × 회전 4 = 12조합 캐시)

    /// <summary>모양과 출력 방향으로 입력 방향을 구한다. 이동 중인 아이템 기준 L=좌회전, R=우회전.</summary>
    public static Direction InputDirFor(BeltShape shape, Direction outDir) => shape switch
    {
        BeltShape.CurveL => Dir.RotateCW(outDir, 3),
        BeltShape.CurveR => Dir.RotateCW(outDir, 1),
        _                => Dir.Opposite(outDir),
    };

    static readonly PortDefinition[][] _portCache = new PortDefinition[12][];

    /// <summary>모양+회전에 맞는 포트 쌍. rotSteps=0일 때 출력은 East.</summary>
    public static PortDefinition[] BuildPorts(BeltShape shape, int rotSteps)
    {
        int steps = (rotSteps % 4 + 4) % 4;
        int key   = (int)shape * 4 + steps;
        if (_portCache[key] != null) return _portCache[key];

        var outDir = Dir.RotateCW(Direction.East, steps);
        return _portCache[key] = new[]
        {
            new PortDefinition { IsInput = true,  Direction = InputDirFor(shape, outDir), LocalOffset = Vector2Int.zero },
            new PortDefinition { IsInput = false, Direction = outDir,                     LocalOffset = Vector2Int.zero },
        };
    }
}

// ─── 행동 ──────────────────────────────────────────────────────

/// <summary>
/// 컨베이어 벨트. 실제 아이템 이동은 BeltSegment가 담당하고,
/// 이 행동은 입력 버퍼를 세그먼트에 올리는 것과 세그먼트 구동 대표 역할만 한다.
/// </summary>
public class BeltBehavior : IBuildingBehavior
{
    readonly Building _b;
    public BeltBehavior(Building b) => _b = b;
    public void OnAfterPlaced() { }

    public void Tick(float dt)
    {
        var seg = _b.Sim.Belts.EnsureSegment(_b);  // 항상 세그먼트 존재

        // 입력 버퍼 아이템을 벨트 위로 (입구가 막혔으면 받아준 만큼만 소비).
        // TryAddItem은 세그먼트 입구(pos 0) 삽입 — 생산자로부터 입력을 받는 벨트는
        // 상류 벨트가 없는 벨트뿐이므로(1입력 포트) 항상 자기 세그먼트의 입구다.
        foreach (var (item, count) in _b.Input.Snapshot())
        {
            int moved = 0;
            while (moved < count && seg.TryAddItem(item)) moved++;
            if (moved > 0)
            {
                _b.Input.TryConsume(item, moved);
                _b.NotifyUpstream(); // 입력 버퍼에 자리 생김 → 막혀 있던 상류 깨움
            }
        }

        // 대표 벨트(입구 = 마지막 인덱스)가 세그먼트 전체를 1번만 구동
        if (seg.BeltCount > 0 && seg.Belts[^1] == _b)
            seg.Tick(dt);

        // 입구가 막혀 버퍼가 안 비면 다음 틱에 재시도
        if (_b.Input.HasAny)
            _b.Sim.MarkDirty(_b);
    }
}

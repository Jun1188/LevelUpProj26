using System.Collections.Generic;
using UnityEngine;

// ─── 벨트 세그먼트 매니저 ──────────────────────────────────────

/// <summary>
/// 벨트 연결/해제 이벤트를 받아 BeltSegment를 생성·병합·분리한다.
/// BuildingGraph.RegisterConn() → OnNewConnection() 순으로 호출된다.
/// </summary>
public class BeltSegmentManager : MonoBehaviour
{
    public static BeltSegmentManager Instance { get; private set; }

    readonly Dictionary<BuildingInstance, BeltSegment> _map = new();
    readonly List<BeltSegment> _segs = new();

    public IReadOnlyList<BeltSegment> Segments => _segs;

    void Awake() { if (Instance != null) { Destroy(gameObject); return; } Instance = this; }

    /// <summary>새 벨트-벨트 연결 등록 시 세그먼트 병합 또는 생성.</summary>
    public void OnNewConnection(BuildingConnection c)
    {
        bool fromBelt = c.From.Data.category == BuildingCategory.Transport;
        bool toBelt = c.To.Data.category == BuildingCategory.Transport;
        if (!fromBelt || !toBelt) return;

        bool hf = _map.TryGetValue(c.From, out var sf);
        bool ht = _map.TryGetValue(c.To, out var st);

        if (!hf && !ht)
        {
            var seg = new BeltSegment();
            seg.Belts.Add(c.From); seg.Belts.Add(c.To);
            _map[c.From] = _map[c.To] = seg;
            _segs.Add(seg);
        }
        else if (hf && !ht) { sf.Belts.Add(c.To); _map[c.To] = sf; }
        else if (!hf) { st.Belts.Insert(0, c.From); _map[c.From] = st; }
        else if (sf != st)
        {
            // 두 세그먼트 병합 — sf 뒤에 st를 붙인다
            foreach (var b in st.Belts) { sf.Belts.Add(b); _map[b] = sf; }
            _segs.Remove(st);
        }
    }

    /// <summary>
    /// 벨트 제거 시 세그먼트 분리.
    /// 단순 구현: 제거된 벨트가 속한 세그먼트를 해체하고
    /// 남은 벨트들은 다음 OnNewConnection 호출 시 재구성된다.
    /// </summary>
    public void OnBuildingRemoved(BuildingInstance b)
    {
        if (!_map.TryGetValue(b, out var seg)) return;
        _segs.Remove(seg);
        foreach (var belt in seg.Belts) _map.Remove(belt);
    }

    public BeltSegment GetSegment(BuildingInstance b) =>
        _map.TryGetValue(b, out var s) ? s : null;
}

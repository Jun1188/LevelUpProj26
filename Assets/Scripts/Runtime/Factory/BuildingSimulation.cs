using System.Collections.Generic;
using UnityEngine;

// ================================================================
//  BuildingSimulation.cs
//  건물 시뮬레이션 구동 — Dirty Queue + Wake 예약
//
//  포함:
//    SimulationSystem — 변화 있는 건물만 틱
//
//  관련 (별도 파일):
//    IBuildingBehavior     — BuildingDataSO.cs (행동 인터페이스)
//    각 행동 구현          — 대응하는 XxxDataSO.cs 파일에 SO와 함께 정의
//    BeltSegment(Manager)  — 벨트 최적화
// ================================================================

/// <summary>
/// Dirty Queue + Wake 예약 기반 이벤트 주도 시뮬레이션.
///
/// 핵심 아이디어:
///   건물 10,000개 중 100개만 현재 활성 → 100번만 Tick() 호출.
///   큐에 없는 건물은 완전 무시.
///
/// 건물을 깨우는 두 가지 경로:
///   MarkDirty(b)          — "지금 변화가 생겼다"
///                            (아이템 수신, 상류/하류 상태 변화, 최초 배치)
///   ScheduleWake(b, t)    — "t초 후에 깨워라"
///                            (채굴/조합 타이머의 완료 시점 예약)
///
/// 타이머가 도는 건물이 매 틱 자기를 재등록하는 대신 완료 시점에만
/// 깨어나므로, 큐에는 실제 처리할 일이 있는 건물만 남는다.
/// </summary>
public class SimulationSystem : MonoBehaviour
{
    public static SimulationSystem Instance { get; private set; }

    [Tooltip("초당 틱 수. 10이면 0.1초마다 처리.")]
    [SerializeField] float _tps = 10f;

    [Tooltip("프레임 드랍 후 한 프레임에 몰아서 따라잡을 수 있는 최대 틱 수.")]
    [SerializeField] int _maxCatchUpTicks = 5;

    readonly Queue<BuildingInstance>   _queue = new();
    readonly HashSet<BuildingInstance> _inQ   = new(); // 중복 등록 방지 O(1)

    // wake 예약 — (깨울 시각, 건물) 이진 min-heap. index 0 = 가장 이른 예약.
    readonly List<(float time, BuildingInstance b)> _wake = new();

    float _interval, _timer;

    /// <summary>시뮬레이션 누적 시간(초). 틱마다 _interval씩 증가한다.</summary>
    public float Now { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance  = this;
        _interval = 1f / Mathf.Max(0.1f, _tps);
    }

    /// <summary>건물 배치 시 호출. 초기 틱을 예약한다.</summary>
    public void Register(BuildingInstance b) => MarkDirty(b);

    /// <summary>건물 제거 시 호출.</summary>
    public void Unregister(BuildingInstance b) => _inQ.Remove(b);

    /// <summary>
    /// 건물을 다음 틱 처리 대상에 추가. O(1).
    /// HashSet으로 중복 호출을 차단하므로 마음껏 호출해도 안전하다.
    /// </summary>
    public void MarkDirty(BuildingInstance b)
    {
        if (b == null) return;
        if (_inQ.Add(b)) _queue.Enqueue(b);
    }

    /// <summary>
    /// delay초 후 건물을 깨운다(= MarkDirty). 타이머 완료 시점 예약용.
    /// 같은 건물을 중복 예약해도 안전하다 — 이른 기상은 각 행동이 Now로 걸러낸다.
    /// </summary>
    public void ScheduleWake(BuildingInstance b, float delay)
    {
        if (b == null) return;
        _wake.Add((Now + delay, b));
        for (int i = _wake.Count - 1; i > 0; )
        {
            int p = (i - 1) / 2;
            if (_wake[p].time <= _wake[i].time) break;
            (_wake[p], _wake[i]) = (_wake[i], _wake[p]);
            i = p;
        }
    }

    void PopWake()
    {
        _wake[0] = _wake[^1];
        _wake.RemoveAt(_wake.Count - 1);
        for (int i = 0; ; )
        {
            int l = 2 * i + 1, r = l + 1, m = i;
            if (l < _wake.Count && _wake[l].time < _wake[m].time) m = l;
            if (r < _wake.Count && _wake[r].time < _wake[m].time) m = r;
            if (m == i) break;
            (_wake[m], _wake[i]) = (_wake[i], _wake[m]);
            i = m;
        }
    }

    void Update()
    {
        _timer += Time.deltaTime;

        // 밀린 틱을 따라잡되, 프레임당 한도를 둬서 저사양에서
        // "틱 몰아치기 → 프레임 더 느려짐 → 더 밀림" 나선을 방지한다.
        int ticks = 0;
        while (_timer >= _interval && ticks < Mathf.Max(1, _maxCatchUpTicks))
        {
            _timer -= _interval;
            RunTick();
            ticks++;
        }

        // 한도를 넘긴 빚은 버린다 (다음 프레임에 처리할 1틱분만 유지).
        if (_timer > _interval) _timer = _interval;
    }

    void RunTick()
    {
        Now += _interval;

        // 예약 시각이 된 건물을 큐로 이동
        while (_wake.Count > 0 && _wake[0].time <= Now)
        {
            MarkDirty(_wake[0].b);
            PopWake();
        }

        // 틱 시작 시점의 큐 크기만큼만 처리.
        // 이번 틱에서 새로 MarkDirty된 건물은 다음 틱에 처리된다.
        int count = _queue.Count;
        for (int i = 0; i < count; i++)
        {
            var b = _queue.Dequeue();
            _inQ.Remove(b);
            if (b == null || b.IsRemoved || !b.gameObject.activeSelf) continue;
            b.Tick(_interval);
        }
    }
}

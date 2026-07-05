using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Factory 시스템 특성화 테스트 하네스.
/// 재설계 전 "지금의 올바른 동작"을 박제해, 내부를 갈아엎어도 회귀를 잡아낸다.
///
/// 사용법: 빈 씬에 빈 GameObject를 만들고 이 컴포넌트를 붙인 뒤 플레이.
///         6개 시나리오가 자동 실행되고 좌상단/콘솔에 PASS/FAIL이 표시된다.
///         (FactoryBootstrap이 있는 씬에서는 싱글톤이 충돌하므로 실행 금지)
///
/// NUnit(Test Runner)이 아닌 씬 하네스인 이유:
///   테스트 asmdef는 Assembly-CSharp을 참조할 수 없는데, Runtime 코드가
///   Test 폴더 코드를 역참조 중이라(PlayerController→Entity, InventoryUI→ItemSocket)
///   어셈블리 분리가 현재 불가능. 심/뷰 분리 후 EditMode NUnit으로 이전 예정.
/// </summary>
public class FactoryScenarioTests : MonoBehaviour
{
    [Tooltip("테스트 가속용 Time.timeScale. 틱 따라잡기 상한(5틱/프레임)을 넘지 않는 10 권장.")]
    [SerializeField] float _timeScale = 10f;

    readonly List<(string name, bool pass, string detail)> _results = new();
    readonly List<string> _fails = new();               // 실행 중인 시나리오의 실패 메시지
    readonly List<ScriptableObject> _createdSOs = new();

    GameObject _systems;
    ItemDataSO _ore, _ingot;

    // ─── 실행 루프 ──────────────────────────────────────────────

    IEnumerator Start()
    {
        if (SimulationSystem.Instance != null)
        {
            Debug.LogError("[FactoryScenarioTests] 씬에 이미 Factory 시스템이 있습니다. 빈 씬에서 실행하세요.");
            yield break;
        }

        Time.timeScale = _timeScale;
        _ore   = MakeItem("TestOre",   ItemType.Ore);
        _ingot = MakeItem("TestIngot", ItemType.Ingot);

        yield return Run("1. 기본 체인 운반",              S1_BasicChain);
        yield return Run("2. 설치 순서 무관 (stall 데드락)", S2_OrderIndependence);
        yield return Run("3. 막힌 체인 무유실·정지",        S3_StallNoLoss);
        yield return Run("4. 중간 철거 분할·복구",          S4_DemolishSplit);
        yield return Run("5. 회전 배치 연결",              S5_RotatedChain);
        yield return Run("6. 어셈블러 조합 체인",           S6_AssemblerChain);

        Time.timeScale = 1f;
        foreach (var so in _createdSOs) DestroyImmediate(so);
        _createdSOs.Clear();

        int passed = 0;
        foreach (var r in _results) if (r.pass) passed++;
        Debug.Log($"[FactoryScenarioTests] 완료: {passed}/{_results.Count} 통과");
        foreach (var r in _results)
            if (!r.pass) Debug.LogError($"[FAIL] {r.name}\n{r.detail}");
    }

    /// <summary>시나리오 1개를 격리 실행. 예외도 실패로 기록하고 다음으로 넘어간다.</summary>
    IEnumerator Run(string name, Func<IEnumerator> scenario)
    {
        Setup();
        _fails.Clear();

        var body = scenario();
        string exception = null;
        while (true)
        {
            object cur;
            try
            {
                if (!body.MoveNext()) break;
                cur = body.Current;
            }
            catch (Exception e)
            {
                exception = e.ToString();
                break;
            }
            yield return cur;
        }

        if (exception != null) _fails.Add("예외 발생:\n" + exception);
        _results.Add((name, _fails.Count == 0, string.Join("\n", _fails)));
        Teardown();
    }

    void Setup()
    {
        _systems = new GameObject("TestSystems");
        _systems.AddComponent<GridRegistry>();
        _systems.AddComponent<BuildingGraph>();
        _systems.AddComponent<SimulationSystem>();
        _systems.AddComponent<BeltSegmentManager>();
        MiningService.GetItemAt = _ => _ore;
    }

    void Teardown()
    {
        MiningService.GetItemAt = null;
        foreach (var b in FindObjectsByType<BuildingInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            DestroyImmediate(b.gameObject);
        DestroyImmediate(_systems); // 싱글톤 파괴 → 다음 Setup에서 새로 생성됨
    }

    // ─── 시나리오 ──────────────────────────────────────────────

    /// <summary>마이너→벨트×2→저장소: 아이템이 끝까지 운반된다.</summary>
    IEnumerator S1_BasicChain()
    {
        Place(Miner(), 0, 0);
        Place(Belt(), 1, 0);
        Place(Belt(), 2, 0);
        var store = Place(Storage(), 3, 0);

        yield return WaitSim(4f);
        Expect(StoredCount(store, _ore) >= 1,
            $"저장소에 아이템이 도착해야 함 (실제: {StoredCount(store, _ore)}개)");
    }

    /// <summary>마이너를 먼저 설치해 stall시킨 뒤 벨트를 연결해도 흐른다. (데드락 회귀 테스트)</summary>
    IEnumerator S2_OrderIndependence()
    {
        var miner = Place(Miner(outBuf: 2), 0, 0);

        yield return WaitSim(1.5f); // 버퍼(2)가 차고 stall될 시간
        int stalled = miner.Inventory.OutputAmount(_ore);
        Expect(stalled == 2, $"출력이 막히면 버퍼 상한(2)에서 생산이 멈춰야 함 (실제: {stalled}개)");

        Place(Belt(), 1, 0);
        var store = Place(Storage(), 2, 0);

        yield return WaitSim(3f);
        Expect(StoredCount(store, _ore) >= 1,
            $"벨트 연결 후 stall이 풀려 아이템이 흘러야 함 (실제 저장소: {StoredCount(store, _ore)}개)");
    }

    /// <summary>출구 없는 체인: 가득 차면 생산이 멈추고, 총량이 더 늘지도 사라지지도 않는다.</summary>
    IEnumerator S3_StallNoLoss()
    {
        var miner = Place(Miner(outBuf: 2), 0, 0);
        var belt  = Place(Belt(), 1, 0);

        yield return WaitSim(6f); // 모든 버퍼·벨트가 가득 찰 시간
        int total1 = SystemTotal(miner, belt);

        yield return WaitSim(2f);
        int total2 = SystemTotal(miner, belt);

        Expect(total1 == total2, $"가득 찬 뒤에는 총량이 변하면 안 됨 (증발/과잉생산): {total1} → {total2}");
        Expect(total2 <= 2 + 10 + 2, $"총량이 버퍼 상한을 넘으면 안 됨 (실제: {total2})");
    }

    /// <summary>벨트 중간 철거 → 세그먼트 2분할, 재설치 → 흐름 복구.</summary>
    IEnumerator S4_DemolishSplit()
    {
        var miner = Place(Miner(), 0, 0);
        var b1 = Place(Belt(), 1, 0);
        var b2 = Place(Belt(), 2, 0);
        var b3 = Place(Belt(), 3, 0);
        var store = Place(Storage(), 4, 0);

        yield return WaitSim(2f);
        PlacementBridge.Remove(b2);
        yield return null; // Destroy 반영 프레임

        var s1 = BeltSegmentManager.Instance.GetSegment(b1);
        var s3 = BeltSegmentManager.Instance.GetSegment(b3);
        Expect(s1 != null && s3 != null && s1 != s3, "철거 후 상류/하류가 별도 세그먼트로 나뉘어야 함");

        int before = StoredCount(store, _ore);
        Place(Belt(), 2, 0);
        yield return WaitSim(4f);
        Expect(StoredCount(store, _ore) > before,
            $"재설치 후 흐름이 복구돼야 함 (저장소: {before} → {StoredCount(store, _ore)}개)");
    }

    /// <summary>회전 배치(남향 체인)에서도 포트가 연결된다.</summary>
    IEnumerator S5_RotatedChain()
    {
        Place(Miner(), 0, 0, rot: 1);            // 출력 East → South
        Place(Belt(), 0, -1, rot: 1);            // 입력 West→North, 출력 East→South
        var store = Place(Storage(), 0, -2, rot: 1);

        yield return WaitSim(4f);
        Expect(StoredCount(store, _ore) >= 1,
            $"회전된 체인에서도 아이템이 도착해야 함 (실제: {StoredCount(store, _ore)}개)");
    }

    /// <summary>마이너→벨트→어셈블러(2광석=1주괴)→벨트→저장소.</summary>
    IEnumerator S6_AssemblerChain()
    {
        var recipe = MakeRecipe(_ore, 2, _ingot, 1, craftTime: 0.3f);

        Place(Miner(), 0, 0);
        Place(Belt(), 1, 0);
        Place(Assembler(recipe), 2, 0);
        Place(Belt(), 3, 0);
        var store = Place(Storage(), 4, 0);

        yield return WaitSim(6f);
        Expect(StoredCount(store, _ingot) >= 1,
            $"조합된 주괴가 저장소에 도착해야 함 (실제: {StoredCount(store, _ingot)}개)");
    }

    // ─── 검증/대기 헬퍼 ─────────────────────────────────────────

    void Expect(bool condition, string message)
    {
        if (!condition) _fails.Add(message);
    }

    /// <summary>시뮬레이션 시간 기준 대기. 벽시계 30초를 넘기면 중단(무한 대기 방지).</summary>
    IEnumerator WaitSim(float simSeconds)
    {
        float target = SimulationSystem.Instance.Now + simSeconds;
        float deadline = Time.realtimeSinceStartup + 30f;
        while (SimulationSystem.Instance.Now < target)
        {
            if (Time.realtimeSinceStartup > deadline)
                throw new TimeoutException($"시뮬레이션이 진행되지 않음 (Now={SimulationSystem.Instance.Now})");
            yield return null;
        }
    }

    static int StoredCount(BuildingInstance store, ItemDataSO item)
        => store.Inventory.InputAmount(item) + store.Inventory.OutputAmount(item);

    /// <summary>막힌 체인 검증용: 마이너 출력 + 벨트 입력 버퍼 + 벨트 위 아이템 총합.</summary>
    int SystemTotal(BuildingInstance miner, BuildingInstance belt)
    {
        int total = miner.Inventory.OutputAmount(_ore) + belt.Inventory.InputAmount(_ore);
        var seg = BeltSegmentManager.Instance.GetSegment(belt);
        if (seg != null) total += seg.Items.Count;
        return total;
    }

    // ─── 배치/SO 생성 헬퍼 (테스트 자체 완결 — Data 폴더 에셋에 의존하지 않음) ──

    static BuildingInstance Place(BuildingDataSO so, int x, int y, int rot = 0)
        => PlacementBridge.Place(so, new Vector2Int(x, y), default, rot);

    BuildingDataSO Miner(float ptime = 0.2f, int outBuf = 5)
    {
        var so = MakeBuilding<MinerDataSO>("TestMiner",
            new[] { Port(false, Direction.East) }, outBuf: outBuf);
        so.processingTime = ptime;
        return so;
    }

    BuildingDataSO Belt() =>
        MakeBuilding<BeltDataSO>("TestBelt",
            new[] { Port(true, Direction.West), Port(false, Direction.East) });

    BuildingDataSO Storage() =>
        MakeBuilding<StorageDataSO>("TestStorage",
            new[] { Port(true, Direction.West) }, inBuf: 50, outBuf: 50);

    BuildingDataSO Assembler(RecipeDataSO recipe)
    {
        var so = MakeBuilding<AssemblerDataSO>("TestAssembler",
            new[] { Port(true, Direction.West), Port(false, Direction.East) });
        so.availableRecipes = new[] { recipe };
        return so;
    }

    static PortDefinition Port(bool isInput, Direction dir) =>
        new() { IsInput = isInput, Direction = dir, LocalOffset = Vector2Int.zero };

    T MakeBuilding<T>(string name, PortDefinition[] ports, int inBuf = 10, int outBuf = 5)
        where T : BuildingDataSO
    {
        var so = ScriptableObject.CreateInstance<T>();
        so.name            = name;
        so.size            = Vector2Int.one;
        so.ports           = ports;
        so.maxInputBuffer  = inBuf;
        so.maxOutputBuffer = outBuf;
        _createdSOs.Add(so);
        return so;
    }

    ItemDataSO MakeItem(string name, ItemType type)
    {
        var so = ScriptableObject.CreateInstance<ItemDataSO>();
        so.name = name;
        so.type = type;
        _createdSOs.Add(so);
        return so;
    }

    RecipeDataSO MakeRecipe(ItemDataSO input, int inAmount, ItemDataSO output, int outAmount, float craftTime)
    {
        var so = ScriptableObject.CreateInstance<RecipeDataSO>();
        so.name      = "TestRecipe";
        so.inputs    = new[] { new RecipeDataSO.Slot { item = input,  amount = inAmount } };
        so.outputs   = new[] { new RecipeDataSO.Slot { item = output, amount = outAmount } };
        so.craftTime = craftTime;
        _createdSOs.Add(so);
        return so;
    }

    // ─── 결과 표시 ─────────────────────────────────────────────

    void OnGUI()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Factory 특성화 테스트  ({_results.Count}/6)");
        foreach (var (name, pass, detail) in _results)
        {
            sb.AppendLine($"{(pass ? "PASS" : "FAIL")}  {name}");
            if (!pass) sb.AppendLine($"      {detail.Replace("\n", "\n      ")}");
        }
        GUI.TextArea(new Rect(20, 20, 520, 300), sb.ToString());
    }
}

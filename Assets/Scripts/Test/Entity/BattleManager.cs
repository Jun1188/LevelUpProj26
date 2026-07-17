using UnityEngine;

// 전투 총괄 매니저 — 전투에 필요한 구성요소를 한곳에서 멤버로 통합한다.
//   Grid    : 길찾기 그리드 (GridManager, 씬 컴포넌트 참조)
//   FlowField : 플로우필드 구동 (FlowFieldManager, 씬 컴포넌트 참조)
//   Spawner : 몬스터 군집 생명주기 (MonsterSpawnManager, 순수 C# — 여기서 소유/Tick 구동)
// 낮/밤 전환(TimeManager)에 맞춰 스폰을 켜고 끄며, 아침에는 군집을 일괄 소멸시킨다.
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("Battle Members")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private FlowFieldManager flowFieldManager;
    [SerializeField] private MonsterSpawnManager spawnManager = new MonsterSpawnManager();

    public GridManager Grid => gridManager;
    public FlowFieldManager FlowField => flowFieldManager;
    public MonsterSpawnManager Spawner => spawnManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 인스펙터에서 비워두면 씬에서 자동 해결
        if (gridManager == null)
            gridManager = GridManager.Instance != null ? GridManager.Instance : FindFirstObjectByType<GridManager>();
        if (flowFieldManager == null)
            flowFieldManager = FlowFieldManager.Instance != null ? FlowFieldManager.Instance : FindFirstObjectByType<FlowFieldManager>();
    }

    private void Start()
    {
        spawnManager.Initialize(gridManager, transform);

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.Cycle.NightStarted += OnNightStarted;
            TimeManager.Instance.Cycle.DayStarted += OnDayStarted;
            spawnManager.SetSpawningEnabled(TimeManager.Instance.Phase == DayPhase.Night);
        }
        else
        {
            // 주야 매니저가 없는 테스트 씬에서는 항상 스폰
            spawnManager.SetSpawningEnabled(true);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.Cycle.NightStarted -= OnNightStarted;
            TimeManager.Instance.Cycle.DayStarted -= OnDayStarted;
        }
    }

    private void Update()
    {
        spawnManager.Tick();
    }

    private void OnNightStarted(int day)
    {
        spawnManager.SetSpawningEnabled(true);
    }

    private void OnDayStarted(int day)
    {
        // 아침 — 스폰 중단 + 살아남은 군집 일괄 소멸
        spawnManager.SetSpawningEnabled(false);
        spawnManager.DespawnAll();
    }
}

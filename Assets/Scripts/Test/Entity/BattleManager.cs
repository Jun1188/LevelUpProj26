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

    // 코어 파괴로 게임이 끝났는지 여부. UI/연출은 GameOver 이벤트를 구독하면 된다.
    public bool IsGameOver { get; private set; }
    public event System.Action GameOver;

    private void Start()
    {
        EnsurePlayerEntity();
        Entities.Building.CoreDestroyed += OnCoreDestroyed;

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
        Entities.Building.CoreDestroyed -= OnCoreDestroyed;
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.Cycle.NightStarted -= OnNightStarted;
            TimeManager.Instance.Cycle.DayStarted -= OnDayStarted;
        }
    }

    // ── 외부 시스템 통합 ──
    // MainScene 등 기존 씬의 플레이어(PlayerController)에는 Player 엔티티가 없다.
    // 씬 에셋을 수정하지 않고 런타임에 Player 컴포넌트를 자동 부착해 몬스터 감지/피격을 연결한다.
    private void EnsurePlayerEntity()
    {
        var controller = FindFirstObjectByType<PlayerController>();
        if (controller == null) return;
        if (controller.GetComponent<Player>() != null) return;

        var player = controller.gameObject.AddComponent<Player>();
        // FPS 플레이어는 카메라/UI가 하위에 있어 Destroy 대신 비활성화로 사망 처리
        player.SetDeathBehavior(destroy: false, delay: 2f);
        Debug.Log("[BattleManager] PlayerController에 Player 엔티티를 런타임 부착했습니다.");
    }

    private void OnCoreDestroyed(Entities.Building core)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        spawnManager.SetSpawningEnabled(false);
        Debug.Log("====== 💀 게임오버 — 코어가 파괴되었습니다! ======");
        GameOver?.Invoke();
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

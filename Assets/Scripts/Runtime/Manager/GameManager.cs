using UnityEngine;

public class GameManager : MonoBehaviour
{
    // 정적 싱글톤 인스턴스
    public static GameManager Instance { get; private set; }

    public enum DayPhase { Day, Night }

    [Header("Time Settings (초 단위)")]
    public float dayDuration = 60f;   // 낮 유지 시간 (예: 60초)
    public float nightDuration = 40f; // 밤 유지 시간 (예: 40초)
    
    [Header("Current State (디버그 확인용)")]
    public int currentDayCount = 1;   // 현재 생존 일수
    public DayPhase currentPhase = DayPhase.Day; // 현재 상태 (낮/밤)
    private float phaseTimer = 0f;     // 현재 페이즈 진행 타이머

    private void Awake()
    {
        // === 1. DontDestroyOnLoad 기반의 안전한 싱글톤 구현 ===
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않음
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        phaseTimer = dayDuration; // 낮 시간부터 시작
        currentPhase = DayPhase.Day;
        Debug.Log($"[게임 시작] {currentDayCount}일차 낮이 시작되었습니다. (건축 가능)");
    }

    private void Update()
    {
        UpdateDayNightCycle();
    }

    // === 2. 낮과 밤의 시간 관리 루프 ===
    private void UpdateDayNightCycle()
    {
        phaseTimer -= Time.deltaTime;

        if (phaseTimer <= 0f)
        {
            if (currentPhase == DayPhase.Day)
            {
                // 낮이 끝나면 ➡️ 밤으로 변경
                currentPhase = DayPhase.Night;
                phaseTimer = nightDuration;
                Debug.Log($"[⚠️ 경고] 밤이 되었습니다! 전투를 준비하세요.");
                // TODO: 여기에 밤이 되었을 때 적 스폰을 시작하는 코드 등을 연동할 수 있습니다.
            }
            else
            {
                // 밤이 끝나면 ➡️ 다음날 아침(낮)으로 변경
                currentDayCount++;
                currentPhase = DayPhase.Day;
                phaseTimer = dayDuration;
                
                Debug.Log($"[☀️ 알림] 아침이 밝았습니다! {currentDayCount}일차 무사 생존 완료.");

                // === 3. 기획 사항: 밤에서 아침이 될 때 세이브 함수 호출 ===
                SaveGame();
            }
        }
    }

    // === 4. 세이브/로드 함수 훅 (뼈대 구현) ===
    public void SaveGame()
    {
        Debug.Log($"====== 💾 [{currentDayCount - 1}일차 완료] 데이터 자동 저장 중... ======");
        
        // TODO: 나중에 여기에 진짜로 인벤토리 데이터와 플레이어 위치 등을 JSON/PlayerPrefs로 저장하는 로직 작성
        // 예: SaveSystem.Save(playerInventory);
    }

    public void LoadGame()
    {
        Debug.Log("====== 📂 세이브 데이터 불러오는 중... ======");
        
        // TODO: 나중에 파일에서 데이터를 읽어와 복구하는 로직 작성
        // 예: SaveSystem.Load();
    }

    // 타 스크립트에서 현재 건축 가능한 타이밍(낮)인지 체크할 수 있는 간편 함수
    public bool IsBuildingAllowed()
    {
        return currentPhase == DayPhase.Day;
    }
}
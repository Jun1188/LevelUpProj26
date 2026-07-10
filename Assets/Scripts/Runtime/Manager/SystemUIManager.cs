using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class SystemUIManager : MonoBehaviour
{
    public static SystemUIManager Instance { get; private set; }

    [Header("=== ESC Pause Menu UI ===")]
    public GameObject pausePanel;         
    private bool isPaused = false;

    [Header("=== Day / Night HUD ===")]
    public TextMeshProUGUI dayText;       // "Day 1" 표시용 텍스트
    public Image timeIconImage;           // 낮/밤 아이콘 이미지
    public Sprite morningSprite;          // 낮 아이콘 (해)
    public Sprite nightSprite;            // 밤 아이콘 (달)

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (pausePanel != null) pausePanel.SetActive(isPaused);
        
        // 🎮 게임 시작 시 GameManager 데이터 기반으로 UI 첫 세팅
        UpdateHUD();
    }


    // ====================================================================
    // ⏰ [유기적 연동] GameManager의 데이터를 가져와 UI를 새로고침하는 함수
    // ====================================================================
    public void UpdateHUD()
    {
        // TimeManager가 씬에 없으면 에러 방지를 위해 리턴
        if (TimeManager.Instance == null) return;

        bool isNight = TimeManager.Instance.Phase == DayPhase.Night;

        // 1. 날짜 텍스트 반영
        if (dayText != null) dayText.text = $"Day {TimeManager.Instance.DayNumber}";

        // 2. 낮/밤 아이콘 반영
        if (timeIconImage != null)
        {
            timeIconImage.sprite = isNight ? nightSprite : morningSprite;
        }
    }

    // ====================================================================
    // ⏸️ ESC 일시정지 메뉴 토글
    // ====================================================================
    public void TogglePauseMenu()
    {
        isPaused = !isPaused;
        pausePanel.SetActive(isPaused);

        if (isPaused)
        {
            Time.timeScale = 0f; 
            Cursor.lockState = CursorLockMode.None; 
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f; 
            Cursor.lockState = CursorLockMode.Locked; 
            Cursor.visible = false;
        }
    }

    // ====================================================================
    // 💾 세이브 / 로드 (버튼 연동용)
    // ====================================================================
    public void OnClickSave()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.SaveGame();

        Debug.Log($"[시스템] System에 의한 GameManager 호출 / 데이터(Day {TimeManager.Instance.DayNumber}) 저장 완료!");
    }

    public void OnClickLoad()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.LoadGame();

        Debug.Log($"[시스템] System에 의한 GameManager 호출 / 데이터(Day {TimeManager.Instance.DayNumber}) 불러오기 완료!");
    }
}
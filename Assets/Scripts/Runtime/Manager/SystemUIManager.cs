using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;


/// <summary>
/// 시스템 UI (일시정지 메뉴, 낮/밤 HUD).
/// 입력 파이프라인의 Fallback 리시버 — 아무도 소비하지 않은 Cancel(ESC)이
/// 여기까지 내려오면 "열려 있는 창이 없다"는 뜻이므로 일시정지를 연다.
/// (닫기는 pausePanel의 PausePopup이 상위 우선순위에서 소비)
/// </summary>
public class SystemUIManager : MonoBehaviour, IInputReceiver
{
    public static SystemUIManager Instance { get; private set; }

    [Header("=== ESC Pause Menu UI ===")]
    public GameObject pausePanel;         // PausePopup 컴포넌트 부착 필요 (timeScale·커서 담당)

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
        if (pausePanel != null) pausePanel.SetActive(false);

        // 🎮 게임 시작 시 GameManager 데이터 기반으로 UI 첫 세팅
        UpdateHUD();

        if (InputManager.Instance != null) InputManager.Instance.Register(this);
        else Debug.LogError("[SystemUIManager] 씬에 InputManager가 없습니다.", this);
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null) InputManager.Instance.Unregister(this);
    }

    // ====================================================================
    // 🎯 입력 파이프라인 — ESC 최종 처리 (일시정지 열기)
    // ====================================================================
    public int Priority => InputPriority.Fallback;
    public bool IsInputActive => isActiveAndEnabled;

    public bool OnInput(in InputEvent e)
    {
        if (e.Phase != InputActionPhase.Performed || e.Id != InputActionId.Cancel) return false;
        TogglePauseMenu();
        return true;
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
    // ⏸️ 일시정지 메뉴 토글 (Resume 버튼 등 UI에서도 호출)
    //    timeScale·커서는 PausePopup(OnEnable/OnDisable)이 담당 —
    //    어떤 경로로 열리든 부수 상태가 일관되게 처리된다.
    // ====================================================================
    public void TogglePauseMenu()
    {
        if (pausePanel == null) return;
        pausePanel.SetActive(!pausePanel.activeSelf);
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
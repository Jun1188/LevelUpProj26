using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 건설 입력 어댑터 — 입력 파이프라인의 BuildTool 리시버.
/// (설계: Input/input-pipeline-architecture.md §8)
///
/// 입력 해석만 담당하고, 실제 배치/철거/프리뷰는 전부 PlacementSystem에 위임한다.
/// PlacementSystem은 파이프라인을 모르므로 UI 버튼·테스트 코드가 같은 API를 직접 호출해도 된다.
/// </summary>
public class BuildController : MonoBehaviour, IInputReceiver
{
    [SerializeField] private PlacementSystem placement;

    public int Priority => InputPriority.BuildTool;   // UI보다 아래, 플레이어보다 위
    public bool IsInputActive => isActiveAndEnabled && placement != null;

    // InputAction 콜백 안에서 IsPointerOverGameObject를 호출하면 Unity가 경고를 찍는다
    // (콜백은 어차피 직전 프레임의 UI 상태를 보게 됨) → Update에서 프레임당 1회 캐싱
    private bool pointerOverUI;

    void Awake()
    {
        if (placement == null) placement = FindFirstObjectByType<PlacementSystem>();
    }

    void Start()
    {
        // Awake 시점에는 InputManager가 아직 없을 수 있어 Start에서 등록
        if (InputManager.Instance != null) InputManager.Instance.Register(this);
        else Debug.LogError("[BuildController] 씬에 InputManager가 없습니다.", this);
    }

    void OnDisable()
    {
        if (InputManager.Instance != null) InputManager.Instance.Unregister(this);
    }

    void Update()
    {
        pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    public bool OnInput(in InputEvent e)
    {
        if (e.Phase != InputActionPhase.Performed) return false;

        // 모드 토글은 대기 상태에서도 받는다
        switch (e.Id)
        {
            case InputActionId.ToggleBuild:    placement.ToggleBuildMode();    return true;
            case InputActionId.ToggleDemolish: placement.ToggleDemolishMode(); return true;
        }

        if (placement.Mode == PlacementSystem.BuildMode.None) return false;   // 이하는 모드 활성 중에만

        switch (e.Id)
        {
            case InputActionId.Cancel:
                placement.ExitMode();
                return true;

            case InputActionId.Rotate:
                return placement.RotatePreview();     // 배치 모드가 아니면 하류로 통과

            case InputActionId.CycleShape:
                return placement.CycleBeltShape();    // 벨트가 아니면 하류로 통과

            case InputActionId.Attack:
                if (pointerOverUI) return true;       // UI 위 클릭은 삼키기만
                placement.ConfirmAtAim();
                return true;   // 모드 중 좌클릭은 항상 소비 — 사격으로 새지 않게

            case InputActionId.Reload:
                return true;   // R키가 Rotate와 겹침 — 모드 중 재장전으로 새지 않게 소비

            case InputActionId.ToggleInventory:
                return true;   // Global 맵이라 모드 중에도 발화 — 건설/인벤 모드 배타 유지 (나가려면 ESC)
        }
        return false;
    }
}
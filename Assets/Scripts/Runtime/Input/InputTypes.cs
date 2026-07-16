using UnityEngine.InputSystem;

// ================================================================
//  InputTypes.cs — 입력 파이프라인 공통 타입
//  설계: input-pipeline-architecture.md 참조
// ================================================================

/// <summary>
/// 액션 문자열 직접 사용 금지 — 오타가 런타임까지 살아남는다.
/// InputActionAsset의 액션 이름과 enum 이름은 반드시 일치시킬 것 (BindAll이 이름으로 매핑).
/// </summary>
public enum InputActionId
{
    None,
    // Gameplay 맵
    Move, Look, Jump, Attack, Interact,
    Rotate, CycleShape, ToggleBuild, ToggleDemolish,
    Cancel,               // Gameplay와 UI 맵 양쪽에 같은 이름으로 존재 (활성 맵의 것이 발화)
    // UI 맵
    Submit, Navigate,
    // Global 맵 (맵 스택과 무관하게 항상 활성)
    ToggleInventory,
}

/// <summary>
/// 라우팅되는 입력 이벤트.
/// ⚠️ OnInput 스코프 밖으로 내보내지 말 것 (큐잉/필드 보관 금지) —
///    CallbackContext는 콜백 동안만 유효하다. 값이 필요하면 즉시 Read해서 복사.
/// </summary>
public readonly struct InputEvent
{
    public readonly InputActionId Id;
    public readonly InputActionPhase Phase;              // Started / Performed / Canceled
    public readonly InputAction.CallbackContext Context; // 값 읽기용

    public InputEvent(InputActionId id, in InputAction.CallbackContext ctx)
    {
        Id = id;
        Phase = ctx.phase;
        Context = ctx;
    }

    public T Read<T>() where T : struct => Context.ReadValue<T>();
}

/// <summary>입력을 받고자 하는 모든 객체가 구현한다.</summary>
public interface IInputReceiver
{
    /// <summary>높을수록 먼저 수신. InputPriority 상수 사용.</summary>
    int Priority { get; }

    /// <summary>false면 라우팅에서 건너뜀. 등록/해제 반복 대신 이 플래그로 제어할 것.</summary>
    bool IsInputActive { get; }

    /// <summary>true 반환 시 입력을 소비(Consume) — 하위 우선순위로 전달되지 않는다.</summary>
    bool OnInput(in InputEvent e);
}

/// <summary>
/// 우선순위는 반드시 이 상수를 통해서만 지정한다. 매직 넘버 금지.
/// 새 계층 추가 시 기존 값 사이의 간격을 사용하고 이 표를 갱신할 것.
/// </summary>
public static class InputPriority
{
    public const int SystemModal = 10000; // 종료 확인창, 로딩 오버레이
    public const int PopupBase   = 5000;  // + 열린 순서(depth)
    public const int HudWidget   = 1000;  // 툴바, 퀵바
    public const int BuildTool   = 500;   // 건설 모드 배치/회전
    public const int Player      = 0;     // 플레이어 조작
    public const int Fallback    = -100;  // 아무도 안 받은 입력의 최종 처리 (ESC → 일시정지 열기)
}
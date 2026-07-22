/// <summary>
/// 플레이어 E키 상호작용 계약 — 마인크래프트식 "동사 하나, 행동은 타겟이 결정".
///
/// 발견은 물리 레이캐스트: 콜라이더가 있는 GO의 부모 어딘가에 이 인터페이스가 있으면 대상.
/// 프롬프트와 실행은 반드시 같은 조준 판정을 공유한다 — PlayerInteractionManager.Current 참조.
///
/// 구현 경로 2가지:
///  - 단독 오브젝트(상자·드롭 아이템): Interactable 베이스 상속
///  - 엔티티(건물 등, 상속이 차 있는 경우): 이 인터페이스 직접 구현
/// </summary>
public interface IInteractable
{
    /// <summary>조준 시 표시할 문구 ("상자 열기"). null/빈 문자열 = 지금은 상호작용 불가 — 프롬프트도 숨겨진다.</summary>
    string Prompt { get; }

    void Interact(PlayerController player);
}

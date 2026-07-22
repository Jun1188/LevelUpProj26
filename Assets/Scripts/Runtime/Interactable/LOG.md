# Interactable(상호작용) 작업 로그

> 최신 항목이 아래. 설계 원칙: 마인크래프트식 — 동사 하나(E), 행동은 타겟이 결정, 화면은 시스템이 관리.

---

## 2026-07-22 — 상호작용 재설계 ①: IInteractable 계약 + 조준 판정 단일화

- **IInteractable 인터페이스 신설** — 발견 계약. 구현 경로 2가지:
  단독 오브젝트는 `Interactable` 베이스(기존 Chest/DroppedItem 무수정), 상속이 차 있는
  엔티티(Entities.Building 등)는 직접 구현
- **조준 판정 단일화**: PlayerInteractionManager가 "조준 중인 IInteractable"을
  `Current`로 캐시(프레임당 1회) + 프롬프트 표시. E키 실행(PlayerController.TryInteract)도
  같은 Current 사용 — 표시/실행이 각자 레이캐스트하며 어긋나던 구조(마스크 불일치 포함) 해소
- `Prompt`가 null/빈 문자열이면 "지금은 상호작용 불가" — 프롬프트도 안 뜨고 E도 무시
- PlayerInteractionManager를 Scripts 루트 → Runtime/Interactable/로 이동 (guid 유지, 프리팹 참조 안전)

### 남은 단계
- ② InventoryScreen 도입 — 타겟이 화면을 직접 요청. PlayerController의
  OpenPlayerInventory/OpenTargetInventory 제거 (플레이어가 UI를 중개하지 않게)
- ③ IInteractiveBehavior(행동 opt-in, IBuildingBehavior와 같은 파일 컨벤션) +
  Entities.Building의 IInteractable 브리지 + Storage 컨테이너 열기 (ItemContainer↔UI 브리지)

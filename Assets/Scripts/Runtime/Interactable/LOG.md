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

---

## 2026-07-22 — 상호작용 재설계 ②: 타겟이 화면을 요청 (OpenInventory 해체)

- **인벤 화면 소유권을 InventoryManager로**: `OpenPlayerScreen()`(I키) /
  `OpenContainerScreen(Inventory)`(상자·건물이 호출) / `CloseScreen()`(팝업이 호출)
- **PlayerController에서 인벤 오케스트레이션 전부 제거** — OpenPlayerInventory/
  OpenTargetInventory/CloseInventory/isInventoryOpen 삭제. 남은 건 `HaltMomentum()`(아바타 상태)뿐.
  플레이어는 이제 화면을 중개하지 않는다 (마인크래프트식)
- Chest는 `InventoryManager.OpenContainerScreen(자기 컨테이너)` 직접 호출 — player 인자 사용 안 함
- 패널·UI 참조는 당분간 playerController 필드 경유 (UI 소유권 이관은 UI 담당과 협의 후)

### 남은 단계
- ③ IInteractiveBehavior(행동 opt-in, IBuildingBehavior와 같은 파일 컨벤션) +
  Entities.Building의 IInteractable 브리지 + Storage 컨테이너 열기 (ItemContainer↔UI 브리지)

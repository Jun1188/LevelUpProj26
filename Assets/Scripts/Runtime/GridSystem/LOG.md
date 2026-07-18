# GridSystem 작업 로그

> 최신 항목이 아래. 입력 관련 이력(BuildController 등)은 Input/LOG.md 참조.

---

## 2026-07-14 — PlacementSystem 실사용 리팩토링

- 레거시 `Input.*` → New Input System 폴링, 이후 07-15 파이프라인 도입 때
  BuildController(입력 해석)와 분리 — 상세: Input/LOG.md

---

## 2026-07-18 — 철거 조준을 건물 몸체 우선으로

- 기존: 바닥(groundMask)만 레이캐스트 → 건물을 조준하면 레이가 통과해 뒤쪽 칸이 잡힘
- 변경: `TryGetAimedBuilding()` 2단 판정 — ① 건물 콜라이더 직접 히트(BuildingView로 판별)
  ② 실패 시 바닥 칸 폴백 (벨트처럼 낮아 몸체 조준이 어려운 건물 대비)
- **공용 쿼리로 공개** — 이후 기계 UI(E 상호작용) 등 "조준한 건물 찾기"는 전부 이 헬퍼로.
  상호작용 계열 현황: Interactable(E, 상자·아이템) / 철거(모드+좌클릭) / 전투(Entities.Building HP)
  — 프롬프트(PlayerInteractionManager)와 실행(TryInteract)의 레이캐스트 중복·마스크 불일치는 팀 협의 대기
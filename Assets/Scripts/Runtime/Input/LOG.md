# Input 파이프라인 작업 로그

> 설계는 input-pipeline-architecture.md, 구현 변경 이력은 여기. 최신 항목이 아래.

---

## 2026-07-14 — PlacementSystem 레거시 입력 탈피 (파이프라인 전 단계)

- 레거시 `Input.*` → `Keyboard.current`/`Mouse.current` 폴링 (New Input System)
- 테스트 OnGUI 제거, B/X 핫키 + UI 연동 API 표면 정리
- FPS 겸용 조준(커서 잠금 시 화면 중앙), UI 위 클릭 가드
- 이 시점에는 아직 파이프라인 없음 — 직접 폴링

---

## 2026-07-15 — 통합 입력 파이프라인 1단계 (설계 문서 §1~§4, §8)

- **설계 리뷰에서 잡은 구멍 4개를 반영해 구현** (상세: 설계 문서 §11 구현 노트)
  1. 공유 에셋 직접 구독 → 런타임 사본 구독 (잔존 람다로 인한 유령 디스패치 차단)
  2. 이름 기반 PopMap → 토큰 기반 (비 LIFO 닫힘에도 스택 무결)
  3. Cancel 액션이 UI 맵에만 있어 건설 취소가 불가능하던 구멍 → Gameplay 맵에도 배치
  4. `InputEvent` 스코프 밖 보관 금지 명문화 (CallbackContext 수명)
- **구현물**:
  - `InputTypes.cs` — InputActionId / InputEvent / IInputReceiver / InputPriority
  - `InputManager.cs` — 액션 맵 스택(①) + 우선순위 라우팅(②). dirty-sort, 스냅샷 순회
  - `GameInput.inputactions` — Gameplay(ToggleBuild/ToggleDemolish/Rotate/CycleShape/Attack/Cancel) + Global(빈 맵). **PlayerControls는 무접촉** (병합 충돌 방지)
- **첫 리시버 이관**: BuildController(입력 해석, BuildTool 우선순위) + PlacementSystem(순수 배치 로직 + 조작 API) 분리 — 사용자 결정. 배치 UI가 붙을 때 같은 API를 호출하면 됨
- **연속/이산 분담 확정**: 조준 레이·호버 하이라이트는 Update 폴링(§7-1), 이산 입력만 라우팅
- **버그 수정**: `IsPointerOverGameObject`를 입력 콜백에서 호출하면 Unity 경고 + 직전 프레임 상태 반환 → BuildController.Update에서 프레임당 1회 캐싱

### 씬 설정 (필수)
- 빈 GO에 `InputManager` + `GameInput.inputactions` 연결 (baseMap = "Gameplay")
- PlacementSystem 있는 씬에 `BuildController` 추가 (placement 참조 자동 탐색)

### 과도기 상태
- 플레이어(`PlayerControls` + PlayerInput Send Messages)는 아직 파이프라인 밖 —
  건설 중 좌클릭이 플레이어 사격으로도 갈 수 있음 (④단계에서 구조적으로 해소)

### 남은 단계
- ③ UIPopup 베이스(depth 우선순위) + 인벤토리 이관 — UI 담당 협의
- ④ PlayerController FSM 이관 + PlayerInput 컴포넌트 제거, Move/Look은 라우팅 제외(폴링) — FPS 담당 협의
- 이후 PlayerControls.inputactions의 액션들을 GameInput으로 통합, 에셋 단일화
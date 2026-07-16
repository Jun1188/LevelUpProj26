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

---

## 2026-07-16 — 파이프라인 2단계 (설계 문서 §5): UIPopup + 인벤토리·일시정지 이관

- **조사에서 발견한 기존 버그 3건**:
  1. `OnInventory`의 Phase 가드가 주석 처리 → 키 누름/뗌 각각 토글 (이중 토글) → 가드 복원
  2. ESC가 `isInventoryOpen`을 무시 → 인벤 위에 일시정지가 겹치고 커서 상태 꼬임
  3. 커서 소유권이 PlayerController(`ToggleCursorAndHUD`)와 SystemUIManager(`TogglePauseMenu`) 두 곳에 분산
- **구현**:
  - `UIPopup`(Input/) — §5 베이스: depth 우선순위(나중에 연 창이 위), UI 맵 토큰 Push/Pop,
    Cancel → 최상단만 닫힘, 모달은 미처리 입력도 삼킴
  - `InventoryPopup`(Inventory/) — inventoryUIPanel에 부착. 열기/닫기 로직은 PlayerController가
    계속 소유, Close만 `CloseInventory()`(캐리지 드롭·상자 정리 포함)로 위임
  - `PausePopup`(Manager/) — pausePanel에 부착. **timeScale·커서를 Enter/Exit에 집중** —
    어떤 경로로 열리든 부수 상태 일관. SystemUIManager.TogglePauseMenu는 SetActive 토글로 단순화
  - **ESC 소유권을 파이프라인으로 단일화**: 열기 = `InputPriority.Fallback(-100)` 리시버
    (SystemUIManager — 아무도 소비하지 않은 Cancel = 열린 창 없음), 닫기 = 각 팝업.
    PlayerController의 `OnTogglePauseMenu` 삭제
  - GameInput에 **UI 맵**(Cancel=ESC) 추가 — 팝업 열림 중 Gameplay 맵 차단으로
    "인벤 열고 B키 건설 진입" 같은 신규 충돌도 해소
- **ESC 흐름 정리**: 건설 모드(BuildController 소비→모드 종료) > 팝업(최상단 닫기) > 아무도 없음(일시정지 열기)

### 씬 설정 (필수)
- inventoryUIPanel에 `InventoryPopup` 부착 (player 참조 자동 탐색)
- pausePanel에 `PausePopup` 부착
- (기존) InputManager + GameInput.inputactions, BuildController

### 과도기 상태 (④에서 해소)
- 플레이어 이동/사격은 여전히 PlayerControls(별개 에셋) — 인벤은 isInventoryOpen 가드로 차단되지만,
  일시정지 중 카메라 회전은 여전히 가능 (기존과 동일)
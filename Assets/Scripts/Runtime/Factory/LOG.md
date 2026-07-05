# Factory 시스템 작업 로그

> Claude Code와 진행한 구조 검토·수정 기록. 최신 항목이 아래.

---

## 2026-07-05 — 코드 정리 (로직 변경 없음)

| 파일 | 내용 |
|---|---|
| `PlacementBridge.cs` | 미사용 using(`com.IvanMurzak.ReflectorNet.Utils`), 디버그 `Debug.Log`, 대화 잔여 주석 제거. 클래스 역할 `<summary>` 추가 |
| `BuildingGraph.cs` | 파일 분리 이후 실제 내용과 어긋난 헤더 주석 수정. 떠돌던 `BuildingInstance` 설명 주석을 `BuildingInstance.cs`로 이전 |
| `BuildingSimulation.cs` | 헤더의 "포함" 목록에서 분리된 `BeltSegment`/`BeltSegmentManager`를 "관련 (별도 파일)"로 이동 |
| `PlacementSystem.cs` (GridSystem) | 미사용 `using VInspector` 제거, 중복 `RotatedSize()` 삭제 → `BuildingDataSO.GetRotatedSize()` 사용, 무의미한 `[SerializeField] GridSystem grid` 제거, 미사용 지역 변수 정리 |
| `SO/IIdentifier.cs` | 빈 인터페이스 + 참조 0건 확인 후 삭제 (.meta 포함) |

---

## 2026-07-05 — 구조 검토 및 팀 결정

데이터 구조/알고리즘 장기 리스크 검토 후, 항목별 문답으로 결정.

### 확정 (구현 완료 — 아래 섹션 참조)
1. **멀티타일 회전 버그** → 회전 기능 재설계 (4방향 포트 사전 계산·캐싱)
2. **아이템 증발** → 생산 정지(stall) 정책 채택
3. **Dirty Queue 무력화** → wake-up 예약(min-heap) 도입
4. **벨트 분기/합류** → 합류기·분배기는 전용 건물(비 Transport)로 취급, 세그먼트는 선형 유지 (팀 기존 합의)

### 보류 (추후 별도 논의)
- **틱 유실**: `SimulationSystem.Update`가 프레임당 최대 1틱만 처리 → 저사양에서 시뮬레이션이 실시간보다 느려짐. 더 고민하기로 함
- **인벤토리 문자열 키**: `BuildingInventory`가 `item.name`(string)으로 아이템 구분 + `new string name` 필드가 `Object.name`을 가림. 큰 문제로 인식, 별도 논의 예정
- **선언만 된 규칙들**: `PortDefinition.AcceptedTypes` 미검사, `maxInputBuffer`가 총량이 아닌 "아이템 종류당 상한"으로 동작. 별도 논의 예정

---

## 2026-07-05 — 확정 사항 구현

### ① 회전 재설계 — `SO/BuildingDataSO.cs`
- **버그**: 기존 `RotateOffset`은 원점 기준 수학 회전 `(x,y)→(y,−x)`만 수행 → 2×1 이상 건물을 회전하면 포트가 풋프린트 밖 셀에 붙음 (1×1만 쓰던 동안엔 미발현)
- **수정**: 재앵커링 공식 `(x,y)→(y, w−1−x)`을 쓰는 `Dir.RotateCellCW` 도입. `GetRotatedPorts`가 4방향 포트 배열을 최초 1회 계산해 캐싱(`BuildPortRotations`), `OnValidate`에서 캐시 무효화
- **주의**: 같은 SO·같은 회전의 건물들이 동일 `PortDefinition` 인스턴스를 공유하게 됨. 런타임에 포트를 수정하려면 복사 후 수정할 것

### ② stall 정책 (아이템 유실 방지)
- `BuildingInstance.NotifyUpstream()` 추가 — 입력 버퍼에 자리가 생기면 상류를 깨우는 역방향 이벤트. stall이 데드락 없이 돌기 위한 핵심
- **Miner**: 출력 버퍼에 자리 있을 때만 채굴 예약. 밀린 출력 버퍼를 매 Tick 먼저 배출 (기존엔 버퍼에 넣은 아이템을 다시 꺼내는 코드가 없어 영구 잔류 + 유실)
- **Assembler**: 결과물 들어갈 자리 확인 후 조합 시작, 완료 시점에 막혀 있으면 완료 보류 (기존엔 `TryAddOutput` 반환값을 버려 재료 소모 후 결과물 증발)
- **Storage**: 입력→출력 버퍼 이동 단계 추가 (기존엔 이 단계가 없어 저장소가 아무것도 내보내지 못했음 — 검토 중 발견한 추가 버그)

### ③ wake-up 스케줄러 — `BuildingSimulation.cs`
- `SimulationSystem`에 시뮬레이션 시계 `Now`(초, 틱마다 증가)와 `ScheduleWake(b, delay)`(이진 min-heap) 추가
- 타이머 건물(마이너·어셈블러)이 매 틱 `MarkDirty` 재등록하는 대신 **완료 시점에만** 깨어남
- 벨트 세그먼트도 소속 벨트 전부가 아닌 **대표(입구, `Belts[^1]`) 벨트 1개만** 재등록 — 세그먼트당 매 틱 1건
- 행동 타이머는 dt 누적에서 절대 시각(`_readyAt`) 비교로 변경

### ④ 벨트 세그먼트 안전장치 — `BeltSegmentManager.cs`
- `OnNewConnection`에 1:1 병합 가드 추가: `From` 출력 연결 또는 `To` 입력 연결이 2개 이상이면 병합 안 함 → "세그먼트는 선형"이라는 팀 합의를 코드로 강제
- 병합/분할 시 새 세그먼트의 대표 벨트만 `MarkDirty` (기존: 전체 벨트)

---

## 2026-07-05 — 철거 시 MissingReferenceException 수정

- **증상**: 건물 철거 후 `FactoryTest.cs` gizmo(구 197행)에서 `seg.Belts[0].transform` 접근 시 예외
- **근본 원인**: `Object.Destroy`는 프레임 끝에 실행됨. 철거된 벨트가 그 프레임의 dirty 큐에 남아 있으면 `RunTick`이 정상 벨트로 보고 Tick → `EnsureSegment`가 죽기 직전 벨트로 **유령 세그먼트**를 등록 → 다음 프레임부터 파괴된 transform 접근
- **수정**:
  1. `BuildingInstance.IsRemoved` 플래그 추가
  2. `PlacementBridge.Remove()` 첫 단계에서 `IsRemoved = true`
  3. `SimulationSystem.RunTick`이 `IsRemoved` 건물을 스킵
  4. `FactoryTest.cs` gizmo에 파괴된 벨트 방어 가드 (철거 직후 1프레임 과도기용)

---

## 2026-07-05 — stall 데드락 수정 (마이너 먼저 설치 시 영구 정지)

- **증상**: 마이너 → 벨트 순서로 설치하면 아이템이 마이너 버퍼에 갇힘. 벨트 → 마이너 순서는 정상
- **원인**: stall 정책의 커버리지 구멍. 마이너가 출력할 곳 없이 버퍼를 채우면 wake 예약을 멈추는데(stall), 이후 벨트를 연결해도 깨우는 이벤트가 없음 — `NotifyUpstream`은 하류가 아이템을 *소비*할 때만 발동하므로, 아무것도 받은 적 없는 새 벨트는 마이너를 영원히 깨우지 않음 (데드락)
- **수정**: `BuildingGraph.RegisterConn`에서 새 연결의 양단(From/To)을 `MarkDirty` — "연결 토폴로지 변화"도 깨우기 이벤트로 승격. stall 규칙: **정지한 건물은 (a) 하류의 소비, (b) 새 연결 생성, 둘 중 하나로 반드시 깨어난다**
- **부수 개선**: 마이너/어셈블러/저장소의 출력 배출 루프를 "아이템 타입당 1개/틱"에서 "하류가 받는 만큼 전부"로 변경 → 정지 해제 후 밀린 버퍼가 빠르게 빠짐

---

## 2026-07-05 — 틱 유실 수정 (보류 항목 처리)

- **문제**: `SimulationSystem.Update`가 프레임당 최대 1틱만 처리 → 프레임 시간이 틱 간격(0.1초)을 넘으면 시뮬레이션이 실시간보다 느려지고 `_timer` 빚이 무한 누적
- **수정**: 고정 틱 루프 + 따라잡기 상한
  - 밀린 틱을 `while`로 몰아서 처리 (프레임 드랍 후 실시간 복귀)
  - 프레임당 최대 `_maxCatchUpTicks`(기본 5, 인스펙터 조절)까지만 — 저사양에서 "틱 몰아치기 → 프레임 더 느려짐" 나선 방지
  - 한도 초과분 빚은 버림 — 지속 저사양에서는 시뮬레이션이 느려지는 걸 허용하되 무한 누적 차단
- **의미**: TPS 10 기준 10 FPS까지는 정확히 실시간 유지, 2 FPS까지는 따라잡기로 커버(50 TPS 순간 처리), 그 이하는 슬로모션

---

## 2026-07-05 — 특성화 테스트 하네스 추가 (재설계 준비)

- **목적**: BuildingSimulation/Behavior/SO 전면 재설계 전에 "현재의 올바른 동작"을 박제 → 재설계 중 회귀 감지
- **위치**: `Test/Factory/FactoryScenarioTests.cs` — 빈 씬에 빈 GameObject + 컴포넌트 부착 후 플레이하면 6개 시나리오 자동 실행, PASS/FAIL 표시
- **시나리오**: ① 기본 체인 운반 ② 설치 순서 무관(stall 데드락 회귀) ③ 막힌 체인 무유실·정지 ④ 중간 철거 분할·복구 ⑤ 회전 배치 연결 ⑥ 어셈블러 조합 체인
- **NUnit이 아닌 이유**: 테스트 asmdef는 Assembly-CSharp을 참조할 수 없는데, Runtime↔Test 코드가 상호 참조 중(`PlayerController→Entity`, `InventoryUI→ItemSocket`)이라 어셈블리 분리 불가. 심/뷰 분리 후 EditMode NUnit으로 이전 예정
- **부수 수정**: 틱 유실 커밋에서 `_maxCatchUpTicks` 필드 선언 누락 발견 → 추가 (컴파일 에러였음)

---

## 2026-07-05 — 재설계 1단계: SO 상속 분리 (god-SO 해체)

- **문제**: `BuildingDataSO` 하나에 모든 건물 종류의 필드가 뭉쳐 있고(`processingTime`은 마이너만, `availableRecipes`는 어셈블러만 사용), `category` enum → `BuildingBehaviorFactory` switch로 행동 분기 (새 건물마다 enum·switch·SO 전부 수정 필요 — OCP 위반)
- **변경**:
  - `BuildingDataSO`를 abstract 베이스로: 공통 필드(name/size/ports/버퍼) + `abstract CreateBehavior(instance)` + 표시용 `abstract Category`
  - 서브클래스 4종 신설 — **각 파일에 SO와 행동을 함께 배치** (팀 컨벤션):
    - `MinerDataSO.cs` — SO(processingTime) + `MinerBehavior` + `MiningService`
    - `BeltDataSO.cs` — SO(speedTilesPerSec 신설) + `BeltBehavior`
    - `AssemblerDataSO.cs` — SO(availableRecipes) + `AssemblerBehavior`
    - `StorageDataSO.cs` — SO + `StorageBehavior`
  - `IBuildingBehavior`는 `BuildingDataSO.cs`로 이동
  - `BuildingBehaviorFactory`·serialized `category` 필드 삭제. `BuildingInstance.Initialize`는 `data.CreateBehavior(this)` 호출
  - `BeltSegmentManager`의 Transport 검사 → `is BeltDataSO`. 벨트 속도는 하드코딩(2f) 대신 `BeltDataSO.speedTilesPerSec`에서 읽음
  - `BuildingSimulation.cs`는 `SimulationSystem`만 남음
  - 기존 `.asset` 5개의 `m_Script` GUID를 대응 서브클래스로 리포인트 (MinorSO→Miner, Belt/커브×2→Belt, Assembler→Assembler). 기존 필드값(processingTime, availableRecipes) 그대로 보존됨
- **효과**: 새 건물 종류 추가 = `XxxDataSO.cs` 파일 1개(SO+행동)만 추가하면 끝, 기존 코드 무수정
- **주의**: 에셋 생성 메뉴가 Factory/Building → Factory/Miner·Belt·Assembler·Storage로 분화됨. Storage 에셋은 아직 없으니 필요 시 새로 생성
- **특성화 테스트 6/6 통과 확인** — 재설계 후 동작 보존 검증됨

---

## 2026-07-05 — BuildingCategory 삭제

- 로직 분기가 전부 타입 기반(`CreateBehavior`, `is BeltDataSO`)으로 대체돼 표시용 1곳만 남았기에 enum과 `Category` 프로퍼티 삭제. FactoryTest는 `Data.GetType().Name` 표시
- **남은 TODO (팀 결정)**: 속도가 다른 벨트는 세그먼트 병합 금지 — 현재는 병합 시 상류 세그먼트 속도로 통일되어 설치 순서 의존적. 고속 벨트 티어 추가 전에 필수. 가드 한 줄이 `BeltSegmentManager.OnNewConnection`에 TODO 주석으로 준비돼 있음

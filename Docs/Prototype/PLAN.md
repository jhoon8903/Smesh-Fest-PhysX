# 제작 계획

현재 단계: **W-003-PHYSX-LIFECYCLE-001 사용자 Play Mode 1차 실패 확인 → 활성화 순서 수정·컴파일 완료 → 재실행 대기**. 채용 공고의 PhysX 요구에 맞춰 첫 물리는 Unity PhysX로 정했고, 현재 런타임 위치·회전·선속도·각속도의 권위는 Rigidbody다. Model은 대여 상태와 세대만 유지한다. Ball 초기 발사와 Ball/Obstacle 풀 반환·재대여 물리 초기화까지 연결했으며, 클릭 입력·Cannon·HP/파괴·결과/재도전은 다음 작은 단위다.

## 현재 순서 — 사용자 정정 반영

[DECISION:user / R-006 / 2026-09-08] 전체 아키텍처를 먼저 만든 뒤 개별 플레이 기능을 진행한다. Daniel이 어떻게 만들려고 하는지 듣는 단계가 먼저다.

1. 수신한 [확정 아키텍처](ARCHITECTURE.md)를 읽고 이어간다. 기준 채택 여부를 다시 묻지 않는다.
2. AI는 그 설명을 바탕으로 확정된 것과 미결정을 정리하고 필요한 질문·검토를 한다.
3. 전체 아키텍처와 제작 순서, Daniel/AI 소유 범위를 함께 정하고 해당 범위부터 만든다.
4. 아키텍처를 마련한 뒤 A안의 작은 플레이 기능을 제작한다.

현재 O-003은 10개 기준 답변을 받았다. 다음 검토는 이 기준의 구체적인 책임·연결·수명 계약이며, 전체 구조나 패턴 선택을 다시 인터뷰하지 않는다. 문서 정리 완료가 아키텍처 구현 완료를 뜻하지 않는다.

## 보존할 결정과 철회한 제안

- [DECISION:user] R-011을 반영한 후속 첫 플레이 목표 ‘Block 영역 클릭으로 Ball 1개 발사·충돌 및 Cannon 방향 회전’, Daniel 씬·카메라·배치·조작감 / AI 코드·검증·기록 분담은 선택을 받았다. 아키텍처 제작의 선행 순서와 역할은 별도로 정한다.
- [CORRECTION] AI가 제안한 발사 코드 선행 제작과 포인터 이벤트 입력 방식은 확정 구현 계획이 아니다. 시작 이벤트는 실제 보조 제작 배정·코드 변경 전에 기록됐으며 정정 이벤트로 종료한다. 실제 구현 시간·성과로 계산하지 않는다.
- [FACT] 기존 GameFlow, UpdateLoop, Object, Observer, Pool, Screen, Ball/Obstacle 등의 사용자 코드가 진행 중이다. 초기 조사 결과를 현재 완성된 구조나 AI 소유 코드로 취급하지 않는다.
- [DECISION:user / R-010] 하이어라키·코드 폴더 준비 후 아키텍처 작업 진행을 요청했다.
- [DECISION:agent] 첫 공통 기반의 독립 단위로 Observable 등록·발행·해제를 제작했다. AI 코드 소유 범위는 `Assets/Scripts/Framework/Observer/Observable.cs`와 `Assets/Editor/Validation/ObservableValidation.cs`다. 검증 메뉴는 명시적으로 실행할 때만 순수 C# 검사를 수행한다. 초기 검사 본문은 `Docs/Prototype/evidence/checks/ObservableChecks.cs.txt`에 스냅샷으로 보관하고, 재실행 원본은 Editor 파일로 둔다. 다른 코드·씬·Prefab·Material·기존 Importer·패키지는 이 단위에서 변경하지 않았다.

## 작업 상태

| 작업 ID | 내용·담당 | 현재 상태 |
|---|---|---|
| SF-START-001 | Main: 시작 요청·설정·Git·저장 씬 조사 | 초기 조사 완료; 사용자 병행 변경 계속됨 |
| SF-START-001-CODE | Terra Medium: 기존 코드 읽기 전용 조사 | 완료, 추가 제작 배정 없음 |
| SF-DOC-001 | Main: 요청·결정·검증·인계 문서 | 최신 정정 반영 |
| W-000 | 확정 기준으로 전체 아키텍처 정제 → 제작 | 10개 기준 수신·원본 기록 완료, 세부 계약 정제 중 |
| W-000-READ-002 | Terra Medium: 최신 골격 읽기 전용 조사 | 완료, 실행 연결은 아직 골격 |
| W-000-DOC-002 | Main: 아키텍처 원본·재개 연결 | 문서화 완료 |
| W-000-READ-003 | Terra Medium: 사용자 최신 GameFlow·Loop 조사 | 완료, 현재 연결과 Inspector 값은 Main이 별도 확인 |
| W-000-OBSERVER-001 | Terra Medium 구현 / Main 통합·Unity 검사 | Observable 구현·동작 10개·고정 구독 Publish 할당 검사 통과 |
| W-000-OBSERVER-REVIEW-001 | Terra Medium: 이벤트 변경·예외 경로 읽기 전용 리뷰 | 계약 범위에서 재현 가능한 결함 발견 없음; 실행 증거는 Main 검사 |
| W-000-CORE-001 | Main: GameFlow·Loop 연결의 세부 계약 | R-013으로 해결; W-000-DI-PAUSE-001에서 연결 완료 |
| W-000-LOOP-001 | Terra Medium 구현 / Main 검토·Editor 실행 | ILoopEvents·LoopDispatcher 동작/할당 검사 통과; 외부 delta 전달 기반만 구현 |
| W-000-DI-PAUSE-001 | Main 통합 / Terra Medium 제작 / Sol High 수명 검토 | 현재 씬 DI 연결·GameClock Editor 검사·Play Mode 25개 검사 통과; High Player 미실행 |
| W-000-POOL-CONTRACT-001 | Main 계약·검증안 / Terra Medium 기존 코드·API 조사 | R-015~R-017 답변 반영 완료; O-006~O-008 해결 |
| W-000-POOL-001 | Main 통합·기록 / Terra Medium 초안 / Sol High 수명 검토·검증 보완 | R-018 인터페이스 구조·씬 DI 연결 완료; Pool Play Mode 66개 및 DI/Pause 25개 통과; High Player 미실행 |
| W-000-MVC-REFERENCE-001 | Main: Core·Observer·View와 적용 기준 / Terra Medium: Pool·DI·실제 호출 비교 | R-019 참고 비교·간소화 기준 문서화. 코드 변경·Unity 실행 없음 |
| W-000-MVC-001 | Terra Medium: Model/View 제작 / Main: 통합·검사·기록 / Sol High: 수명 읽기 검토 | 공통 연결·Play Mode 58개 검사·두 반복 구간 0바이트 확인. 실제 Ball 연결·High Player 미실행 |
| W-000-BALL-MVC-001 | Main: 계약·통합·기록 / Terra Medium: 수명 조사·검사 코드 / Sol High: 수명·파괴 순서 검토 / Daniel: Prefab·PoolConfig·씬 값 | 완료. Play Mode 19개 검사, 씬·Ball/Cube Prefab·PoolConfig 저장 파일 보존. 물리/입력 제외 |
| W-000-OBSTACLE-MVC-001 | Main: 계약·통합·실행·기록 / Luna Low: 수명 조사 / Terra Medium: 검사 코드 / Sol High: 독립 검토 / Daniel: 자산·씬 값 | 완료. Play Mode 25개 검사. 씬·SO는 동일하며 작업 중 외부에서 Ball/Cube Prefab에 추가된 Rigidbody는 보존. 물리/HP/파괴 제외 |
| W-001-AI | 발사 기능 구현의 사전 시작 기록 | 구현 전 철회; 코드 수정 없음 |
| W-001 | Block 영역 클릭 발사·충돌 + Cannon 방향 회전 | 아키텍처 마련 후 진행; 별도 조준 단계 없음 |
| W-002 | 파괴·결과·재도전 | 후속 |
| W-003-PHYSX-LIFECYCLE-001 | Main: 통합·컴파일·기록 / Luna Low: 수명 조사 / Terra Medium: 격리 검사 / Sol High: 수명·물리 독립 검토 / Daniel: Rigidbody·Collider·씬 값과 Play Mode 실행 | 1차 실행은 assertion 10에서 실패해 충돌 시뮬레이션 전 중단. 비활성 rent callback 뒤 활성화되는 순서에 맞춰 Ball Sleep/Obstacle Wake를 `OnEnable`에서 재적용하고 진단을 분리했다. 수정본 컴파일 완료, 재실행 대기 |
| W-003 | Unity PhysX 기준 구현과 이후 직접 구현 물리 비교 | 첫 PhysX 수직 슬라이스 진행 중. 직접 구현 비교 조건과 물리 권위 문서는 대표 플레이 확인 뒤 별도 작성 |

## W-001 — 아키텍처 이후 함께 볼 플레이 결과

[DECISION:user / R-011] 조작의 단일 원본은 [DESIGN의 확정 조작](DESIGN.md#확정-조작--클릭이-조준과-발사를-겸한다)이다. Block이 있는 영역을 클릭하면 해당 위치로 공을 발사하고 Cannon은 Ball 진행 방향으로 회전한다. 별도 조준 모드나 추가 발사 입력은 없다.

Daniel은 현재 씬·카메라·Block/Ball/Cannon 배치와 조작감을 맡고, AI는 확정 아키텍처를 통한 입력 전달·목표 처리·발사·방향 회전의 역할 분리와 검증·기록을 맡는다. 실제 코드와 세부 계약은 아키텍처 마련 뒤 제작한다.

함께 확인할 것은 클릭한 목표 위치로 공이 발사되는지, Cannon 방향이 Ball 진행 방향과 맞는지, 첫 충돌이 발생하는지다. 발사 속도·궤적과 회전 연출 값은 그 단위에서 조정한다. 지금은 요구·검증 계획을 반영한 상태이며 기능 구현·Play Mode 확인은 미실행이다.

## 모델 운영

Main은 실제 실행 메타데이터에서 Astra·Ultra를 확인했다. 초기 코드 조사, 최신 조사·검토, 이번 작은 제작에 Terra·Medium을 사용했다. 보조는 명시 배정과 fork_turns: none으로 시작했고 기존 보조를 재사용했다. 단순 확인 Luna·Low, 합의된 작은 제작 Terra·Medium, 복잡한 검토 Sol·High 정책을 유지한다. 보조 최대 2개, 재위임 없음. 실제 사용량·비용은 미제공이다.

GameFlow·UpdateLoop·Pool과 최소 Model–View 연결, Ball/Obstacle 객체별 MVC 수명과 첫 PhysX 수명 연결을 마련했다. 다음 행동은 수정된 격리 PhysX Probe를 Daniel이 Play Mode에서 재확인한 뒤, 실제 클릭 → 목표점 → Ball 대여/발사와 Cannon 회전을 연결하는 것이다. DI·동적 생성·리소스 로딩을 설계할 때 A-10의 High Stripping 보존 관리와 Player 검증을 함께 반영한다. 확정된 10개 기준은 반복 질문하지 않는다.

## W-000-CORE-001 — 첫 기반 단위의 당시 계획

목표: 기존 GameFlow·UpdateLoop를 기동·중단·시간 이벤트로 연결하기 전에, 구독 수명이 안전한 Observer 기반을 구현·검증한다. 사용자 요청으로 아키텍처 제작을 진행하며 전체 기반 완성이나 A안 플레이 완성을 의미하지 않는다.

| 담당 | 실제 범위 |
|---|---|
| Daniel | 만들어 둔 하이어라키·카메라·씬 값 유지 및 추가 조정. 기존 DI 도구/코드와 시간 적용 범위 설명. |
| AI | 사용자 작업 최신 상태 조사, Observer 단일 파일 구현, Unity 컴파일·순수 이벤트 실행 확인, 기록 갱신. |
| 함께 볼 결과 | 중복·발행 중 등록/해제·예외 복구가 정한 순서대로 동작하고, 이후 Loop·Pool에서 재사용할 수 있는가. |

### 현재 관측과 보존

WorldObjects에 GameFlow/UpdateLoop가 이미 붙어 있다. GameObjects 아래 Ground, Table, Blocks(하위 24개), Ball, Cannon이 있다. 씬의 배율 설정은 minScale=0, maxScale=2이며 코드 기본값보다 이 사용자 값을 우선한다. GameFlow의 기존 로그 초기화와 UpdateLoop의 배율 제한을 보존한다. [하이어라키 근거](evidence/raw/architecture-resume-hierarchy.json), [기준 스냅샷](evidence/raw/architecture-resume-baseline.json).

### 새로 확인할 세부 계약

- O-004 / R-013 답변 수신: VContainer 설치 완료. 현재 manifest/lock과 설치 소스에서 1.19.0을 확인했다. 동일 질문을 반복하지 않는다.
- O-005 / R-013 답변 수신: UI는 별도 시간 관리자를 두지 않고 게임 시간과 분리한다. UI가 열려 있는 동안 게임은 Pause다. 게임 시간은 Unity timeScale에 연결하며 고정 물리 timestep은 보존한다.

### Observer 동작 계약

`Observable<T>`는 순수 C# 인스턴스이며 전역 싱글톤이 아니다. Subscribe/Unsubscribe/Publish/Clear/Count를 제공한다. 동일 delegate 중복 구독은 무시하고 등록 순서로 알린다. 발행 중 해제는 아직 호출되지 않은 대상에 즉시 반영하고, 추가·재구독은 다음 발행부터 적용한다. 재귀 발행은 거절한다. 리스너 예외는 호출자에게 전파하며 내부 발행 상태는 복구한다. 매 Publish에서 배열 복사·LINQ·새 객체를 만들지 않는다. 구독/용량 확장과 초기화는 별도 할당 구간이다.

동기 호출용이며 멀티스레드 동시 접근을 지원하지 않는다. 리스너 예외가 나면 해당 발행의 나머지 호출은 중단된다. 이 계약을 이후 Loop·Pool에서 사용할 때도 보존한다.

### 이번 확인 결과

Unity 6000.3.10f1 Editor에서 컴파일 오류 0건, 명시적 검증 메뉴의 동작 검사 10개 통과. 구독자 1개를 미리 등록하고 100회 워밍업 후 Publish 1,000회의 현재 스레드 관리 할당량은 0바이트였다. 초기 구독·용량 확장·실제 게임/Player의 할당량은 이 수치에 포함되지 않는다. [실행 결과](evidence/raw/observable-validation.json), [기존 파일 보존 확인](evidence/raw/observer-preservation-check.json).

이 Observer 단위 종료 당시 GameFlow·Loop·DI·시간 적용과 High Player는 미실행이었다. 현재 DI·시간 연결 결과는 아래 W-000-DI-PAUSE-001을 따른다.


## W-000-LOOP-001 — Loop 실행 상태와 구독 전달

[DECISION:user / R-012] 조작 정정 이후 “진행하자”로 아키텍처 작업 계속 요청. [DECISION:agent] O-004·O-005 답변 전에는 특정 DI 패키지나 Unity 전역 시간 정책을 채택하지 않고, 시간을 외부에서 받는 Loop 실행 기반을 먼저 만든다.

- Daniel: 현재 하이어라키·Cannon 골격·Inspector 값 유지/추가 작업, DI 도구와 시간 적용의 세부 설명.
- AI: `Assets/Scripts/Framework/Loop/ILoopEvents.cs`, `LoopDispatcher.cs`, `Assets/Editor/Validation/LoopValidation.cs` 및 문서·증거 기록. 기존 GameFlow·UpdateLoop·Observable과 게임 코드·씬은 이 독립 단위의 편집 대상에서 제외한다.
- 함께 볼 결과: 시작 전/중단 후에는 전달하지 않고, 재시작에 중복이 없으며, Update·Fixed·Late가 전달받은 delta를 해당 구독자에 보낸다. Dispose 후 구독 참조가 정리되고 정상 반복 전달 경로의 할당을 확인한다.

구현 계약: `ILoopEvents`는 `UpdateTick`, `FixedTick`, `LateTick` 구독/해제만 노출한다. `LoopDispatcher`는 StartLoop·StopLoop, TickUpdate·TickFixed·TickLate, Dispose를 담당하는 순수 C# 객체다. 입력 delta를 변형 없이 전달하며 Unity Time을 읽거나 쓰지 않는다. 중단은 이후 전달부터 적용되고 진행 중인 현재 발행은 완료한다. Dispose는 구독을 즉시 정리한다. 재귀 phase 전달은 거절하고 예외 후에도 내부 상태를 복구한다. 기존 Observable의 구독 변경 순서를 재사용한다. Unity 프레임의 실제 시각 선택과 GameFlow 주입/기동 연결은 답변 후 별도 단계로 연결한다.


### W-000-LOOP-001 확인 결과

- `ILoopEvents`·`LoopDispatcher`·Editor 검증 메뉴 구현 완료. Main 검토에서 중단 상태의 no-op을 재귀 검사보다 먼저 적용하도록 보완하고, 세 phase의 중단·Dispose·중복 구독·재귀 예외 후 복구 검사도 추가했다.
- Unity 6000.3.10f1 컴파일 후 Console 오류 0건, `Tools/Smesh Fest/Validation/Loop`의 순수 C# 검사 통과. 시작 전/중단 후 전달 억제, phase 분리와 delta 보존, 재시작·중복 억제, Stop/Dispose 중간 영향, 구독 변경, 예외·재귀 복구, delta 경계를 확인했다.
- 구독자 각 1개, 100 cycle 워밍업 뒤 1,000 cycle(매 cycle Update·Fixed·Late, 총 3,000 Tick)의 현재 스레드 관리 할당량은 0바이트. 전체 게임·물리·Player의 성능이나 Zero Alloc을 뜻하지 않는다. [실행 결과](evidence/raw/loop-validation.json).
- 재개 시점의 기존 105개 파일 동일, 누락/변경 0개. 신규 파일은 소유한 C# 3개와 Unity가 생성한 .meta 3개다. [보존 결과](evidence/raw/loop-preservation-check.json).

이 Loop 단위 종료 당시 후속은 O-004·O-005 답변을 받아 GameFlow 주입과 실제 Unity 프레임을 연결하는 것이었다. 현재 DI·시간 연결 결과는 아래 W-000-DI-PAUSE-001을 따른다. Pool·MVC/MVP·SO·Addressables, High Player, 클릭 발사와 Cannon 회전은 여전히 후속이며 전체 아키텍처 완료로 기록하지 않는다.


## W-000-DI-PAUSE-001 — VContainer와 UI Pause 연결

[DECISION:user / R-013] VContainer 사용과 UI 열림 동안 게임 Pause를 확정했다. [DECISION:agent] UI별 요청 소유자를 구분해 중복 요청을 억제하고 마지막 요청이 해제될 때 보관한 요청 배속으로 돌아간다. 중단 상태에서 UI를 닫아도 게임을 강제로 시작하지 않는다. UI 자체의 새 Clock/UpdateLoop는 만들지 않는다.

Daniel은 화면/씬 배치·UI 구성을 계속 맡는다. AI는 순수 GameClock·IGamePause, Unity 시간 적용 어댑터, 기존 GameFlow/UpdateLoop의 주입·프레임 연결, GameLifetimeScope, UI 열림/닫힘을 Pause 수명으로 연결하는 작은 ScreenPauseScope, 검증·기록을 맡는다. 기존 WorldObjects에 scope 연결만 추가하고 기존 오브젝트/수동값은 보존한다. 활성 표시만 있는 기존 HUD 전체를 UI 열림으로 해석하지 않으며, 실제 여닫는 화면 루트에 ScreenPauseScope를 적용한다. UI 생성·MVP 전체 구현은 후속 단위다.

검증 목표: 실제 VContainer 구성으로 게임 시작, 2개 UI의 중첩 열기/닫기와 중복/비활성화/파괴 정리, 요청 배속 복원, Update/Fixed/Late 및 물리의 정지/재개, scope 정리 시 원래 Unity 시간 복원. High 보존은 명시적 생성 factory와 필요한 주입 메서드 Preserve로 관리하고 실행 근거 수준을 구분한다. 기존 씬 원본은 evidence/backups/di-pause-20260908에 보관했다.

### 확인 결과와 다음 단위

- WorldObjects에 GameLifetimeScope를 추가하고 uiRoot에 기존 UI(HUD)를 연결했다. 진행 중 scope가 전역 namespace의 빈 골격으로 바뀐 상태를 재확인하고 이 형태에 등록 내용만 채웠다.
- Unity 컴파일 오류 0건. GameClock Editor 검사 통과, 사전 생성 owner/handler와 100 cycle 워밍업 후 1,000 cycle Pause/Resume/배율 변경의 관리 할당량 0바이트. 실제 전체 게임 할당량과 구분한다.
- 현재 씬 Play Mode에서 VContainer로 시작한 상태의 25개 assertion 통과: 3phase 전달, Loop/Flow 재활성화, 중첩 UI, 마지막 닫힘의 배속 복원, 동기 닫힘·비활성화·파괴 정리, 물리/scaled Tween/scaled 파티클 정지·재개, container DisposeCore 후 원래 timeScale=1 복원. [Play Mode 결과](evidence/raw/di-pause-playmode.json).
- 111개 기준 파일 중 108개 동일. 기존 GameFlow.cs·UpdateLoop.cs·Game.unity만 변경됐다. 씬은 WorldObjects의 scope와 UI 참조 추가뿐이며 기존 컴포넌트·수동값은 동일하다. Play 종료 뒤 dirty=false, root 3개. [보존 결과](evidence/raw/di-pause-preservation-check.json).
- Sol·High 검토에서 UpdateLoop 단독 비활성화 후 영구 정지 문제를 발견해 전달기만 중단하도록 수정하고 실제 회귀 검사로 확인했다. 주입 메서드 Preserve와 명시적 생성 factory를 적용했다. **High Player·IL2CPP/AOT는 아직 검증하지 않았다.**

당시 다음 작은 목표 제안은 기존 Pool/Factory 골격에서 객체 한 개의 대여 → 반환 → 재대여 수명을 정하는 것이다. Daniel은 기존 코드의 의도와 재사용 시 유지할 데이터를 설명하고, AI는 Tween·파티클·비동기 작업·구독의 정리 책임을 검토해 계약과 검증안을 만든다. 그 계약을 바탕으로 구현하며 아키텍처를 건너뛰어 클릭 발사 기능부터 만들지 않는다. 지금 UI 화면 전체를 새로 만들 필요는 없다. 이후 실제 여닫는 화면 루트에 ScreenPauseScope를 붙이고 scope에서 주입한 상태로 사용하면 된다.

## W-000-POOL-CONTRACT-001 → W-000-POOL-001 — Pool 기반 완료

R-014 진행 요청과 R-015·R-016 구현 계획을 받아 객체 한 개의 안전한 대여·반환·재대여를 함께 확인할 결과로 잡았다. 이번 계약의 원본은 [ARCHITECTURE의 Pool/Factory 계약](ARCHITECTURE.md#poolfactory-수명-계약--r-015r-016)이다.

- Daniel: 원래 생각한 공의 생성·초기화·반환 흐름, MVC 재사용 단위, 재도전/스테이지/씬 종료의 풀·자산 유지 범위를 설명한다. 씬·Prefab 배치와 수동값은 계속 Daniel 소유다.
- AI: 현재 빈 골격과 실제 패키지 API를 확인하고 정리 책임·순서·실패 조건·검증안을 준비했다. 답변을 반영해 기존 골격에 필요한 역할만 구현하고 실행 증거를 남긴다.
- 함께 확인할 것: 같은 테스트 객체를 두 번 사용해도 이전 사용의 물리·Tween·파티클·콜백이 남지 않고, 중복 반환/재진입이 잘못된 재대여로 이어지지 않는지.

O-006·O-007 답변과 PoolConfig의 반환 시간 추가를 반영했다. 생성·초기화·반환의 확정 원본은 ARCHITECTURE R-015·R-016이다. O-008도 R-017로 해결했다. 비활성 재고만 정리하며 MinPool을 남기는 Factory를 기존 GameLifetimeScope에 등록하고 기존 GameObjects/PoolContainer에 컴포넌트를 붙여 scope 참조로 연결한다.

AI 소유 코드: Framework/Pool의 PoolConfig·PoolContainer·PoolFactory·Pool·IPool·IPoolable·PoolLifecycleRunner·PoolLease·PoolSpawnArgs·IPoolLifecycle, R-018대로 독립 ObView와 BallView의 IPoolable 구현, 명시적 검증 메뉴와 테스트 객체. 실제 게임별 Model·Controller/효과 파츠 구현, Addressables 핸들 연결, Ball 발사·Cannon 회전은 후속이다. 검증은 임시 객체를 사용하며 씬 변경은 기존 GameObjects/PoolContainer의 컴포넌트 추가와 WorldObjects scope 참조 연결뿐이다. 기존 씬/Prefab/효과 수동값은 보존한다.

함께 볼 검증: 사전 생성과 최대 수, 동일 객체 재사용, 오래된/중복 반환 거절, DI·초기화 후 활성화, 구독·Tween·파티클·취소의 소유자 정리, 실패 격리·재진입·최종 폐기, 설정 시간/MinPool 양쪽 정책과 Pause 중 미사용 정리. 반복 할당은 효과 없는 준비된 풀의 제한된 구간을 따로 측정한다.

결과: [Pool Play Mode 66개](evidence/raw/pool-runtime-validation.json), [DI/UI Pause 회귀 25개](evidence/raw/di-pause-playmode.json) 통과. 빈 IPoolable의 100회 워밍업 후 1,000회 반복 대여·반환은 관리 할당 0바이트다. 조건·재작업·보존 근거는 REVIEW에 기록했다. High Player·IL2CPP/AOT와 실제 게임별 상태·플레이 검증은 미실행이다.

다음 작은 단위는 Ball MVC의 Model 상태·Controller 구독을 이 수명 계약에 연결하는 것이다. Daniel은 실제 Prefab/배치와 사용할 PoolConfig 값, AI는 합의할 MVC 조립·초기화·해제 코드와 검증을 맡는다. 현재 GameObjects/PoolContainer의 Configs는 비어 있다. Create → Smesh Fest → Pool Config에서 설정을 만들고, IPoolable을 구현한 View가 붙은 Prefab을 지정한 뒤 이 목록에 등록하는 방식이다. 발사·Cannon 회전은 전체 아키텍처 연결 후 진행한다.

## W-000-MVC-REFERENCE-001 → W-000-MVC-001 — 참고를 간소화해 연결

R-019에 따라 위의 Ball별 연결 전에 공통 Model–View 통지·해제 수명을 최소 객체로 확인한다. 참고의 모듈 프레임워크를 이식하는 작업이 아니다. 책임과 재사용 판단의 단일 원본은 [ARCHITECTURE](ARCHITECTURE.md)의 R-019 절이다.

| 분담 | 이번 참고 검토와 다음 연결 단위 |
|---|---|
| Daniel | 참고 프레임워크와 간소화 방향 제공. 씬·Prefab·배치·설정값과 기존 코드 의도를 계속 소유한다. 이 비교를 위해 하이어라키를 다시 만들 필요는 없다. |
| AI | 참고의 모델 통지/바인딩과 현재 미연결 상태를 비교·기록했다. 다음 코드 범위는 Framework/Object의 필요한 Model/View 연결, 객체별 구독 해제 책임, 최소 검증 코드다. 기존 Observable·Loop·Pool을 재사용한다. |
| 함께 확인할 결과 | Model 변경이 View 갱신으로 이어지고, 교체·비활성화·반환 뒤 옛 구독이 남지 않으며 재연결 때 중복되지 않는가. |

검증 계획: 비풀링 View의 독립 사용, 최초 연결 시 상태 표시, 같은 Model 재연결, Model 교체, 비활성/재활성, 풀 반환/재대여와 객체별 Controller 구독 정리를 확인한다. 재사용 여부·첫 게임 상태 필드·Controller 공통 API는 실제 연결에 필요한 만큼 정한다. 제네릭 형태나 추가 인터페이스를 미리 필수로 고정하지 않는다. 안정된 반복 통지 구간의 할당은 측정 조건과 함께 기록한다. High Player와 실제 Ball 물리는 별도 검증이다.

W-000-MVC-REFERENCE-001에서는 문서와 조사 증거만 갱신했다. 이후 R-020으로 아래 W-000-MVC-001을 실제 제작했다.

### W-000-MVC-001 완료와 다음 연결

AI 소유 변경은 기존 ObModel.cs, 신규 ObViewOfT.cs와 .meta, MvcRuntimeProbe.cs·MvcValidation.cs와 .meta다. 독립 ObView·기존 Observer/Loop/Pool·GameLifetimeScope·Ball 코드는 변경하지 않았다. 기존 씬·Prefab·설정도 보존했다.

Unity 6000.3.10f1 컴파일 및 Play Mode 58개 검사 통과. 최초/같은 Model 연결, 교체, 비활성/disabled·재활성, 해제/실패·재진입, 주입 → Controller의 Loop 구독 → Model 통지 → View 갱신, 반환/재대여/Factory 종료를 임시 객체로 확인했다. 묶음 재사용과 Model/Controller 재생성은 둘 다 확인했다. 준비된 반복 통지 1,000회와 Unbind/Bind 1,000회는 각각 관리 할당 0바이트이며 실제 게임·렌더·전체 풀 할당 측정이 아니다.

다음은 실제 Ball별 MVC 조립·상태 초기화 계약을 연결하는 단위다. Daniel은 사용할 Prefab·설정·배치와 상태 유지 의도를 맡고, AI는 기존 수명 훅에 연결할 객체별 Model/Controller와 검증을 맡는다. 공통 ObController API를 추측으로 채우거나 모든 타입의 묶음 재사용을 강제하지 않는다. 월드/MVP·SO/Addressables 전체 기반을 정리한 뒤 클릭 발사·Cannon 회전으로 진행한다.

## W-000-BALL-MVC-001 — Ball 묶음 재사용과 초기화

[DECISION:user / R-021] Ball은 풀 인스턴스마다 View·Model·Controller를 한 번 조립해 유지한다. 매 대여마다 새 논리 객체를 만들지 않고, 반환 뒤 첫 사용의 상태·Loop/Model 구독·비동기 작업이 남지 않도록 객체별 초기화 책임을 둔다.

[DECISION:user / R-022] 첫 BallModel의 상태는 `대여 중 여부`와 `사용 세대`처럼 물리 구현과 무관한 수명 정보로 제한한다. Rigidbody 위치·속도·충돌 결과를 지금 권위 상태로 정하지 않는다. Ball MVC를 마치면 다음 컨텍스트에서 Obstacle MVC를 구현하고, 그 뒤 W-003 물리 구현·비교 조건을 진행한다.

| 분담 | 이번 단위 |
|---|---|
| Daniel | 최신 Ball/Cube Prefab·PoolConfig·Game 씬 연결과 Inspector 값을 소유한다. AI 검증을 위해 씬을 재생성하거나 값을 되돌릴 필요가 없다. |
| AI | `InGame/Ball`의 Model·View·Controller 조립과 풀 수명 초기화, 격리 검증, 기록을 맡는다. 발사 입력·Cannon·실제 Physics 비교와 사용자 자산 편집은 제외한다. |
| 함께 확인할 결과 | 같은 Ball을 반환 후 다시 대여했을 때 같은 Model/Controller가 유지되고, 이전 사용 세대는 무효이며 Model 관찰은 한 번만 연결되는가. 실제 Loop/물리 구독은 물리 구현 단위 전까지 만들지 않는다. |

구현 결과: `BallView.OnPoolCreated`가 묶음을 한 번 만들고, 대여에서 Model 세대·View Bind·Controller lease를 연결한다. 반환은 Controller → View → Model 순서로 정리한다. 풀 종료뿐 아니라 씬 계층이 먼저 파괴되는 경우도 Unity `OnDestroy`에서 즉시 같은 정리를 수행한다. 정상 Controller 반환과 PoolLease 반환, 재대여 동일성, 오래된 세대 거절, 활성 풀 종료, 계층 선파괴를 임시 객체로 검사해 19개 assertion을 통과했다. 실제 Ball Prefab·씬·PoolConfig와 물리/입력은 건드리지 않았다.

Ball 단위 종료 당시의 다음 컨텍스트 시작점: `InGame/Obstacle` 골격과 Daniel의 Cube Prefab/Cube PoolConfig/Game PoolContainer를 다시 읽는다. 당시 저장 근거로 Cube Prefab에는 MeshRenderer·BoxCollider만 있고 Cube PoolConfig의 Prefab은 비어 있으며 Game 목록은 Ball 하나였다. Obstacle의 대여 상태·세대를 Ball과 동일하게 둘 수 있는지 확인하되, 공통 베이스로 성급히 추출하지 않는다. 이 작업은 아래 W-000-OBSTACLE-MVC-001에서 완료했고 다음은 W-003 물리 권위와 비교 조건이다.

## W-000-OBSTACLE-MVC-001 — Obstacle 묶음 재사용과 초기화

Ball에서 확인한 객체별 원칙을 Obstacle에 필요한 만큼만 적용했다. 공통 베이스를 새로 추출하지 않았고, `ObstacleModel`에는 물리와 무관한 `IsRented`·`RentalEpoch`만 두었다. `ObstacleView`가 생성 시 Model/Controller를 한 번 조립하고, 대여·반환·풀 종료·Unity 계층 선파괴에서 같은 묶음을 안전하게 재사용·정리한다. `ObstacleController.TryReturn(epoch)`는 현재 Model 세대와 PoolLease가 모두 유효할 때만 반환한다.

격리된 임시 ObstacleView와 PoolFactory로 prewarm, 정상 Controller/lease 반환, 같은 묶음 재대여, 오래된 세대 거절, 활성 풀 종료, 계층 선파괴, 생성 후 미대여 파괴를 확인했다. Sol High 검토 뒤 유효하지 않은 lease로 대여 준비가 중간 실패할 때의 롤백과 `OnDestroy` 오류 로그 감지를 추가해 Unity 6000.3.10f1 Play Mode **25개 assertion**을 통과했다. [실행 결과](evidence/raw/obstacle-mvc-runtime-validation.json).

AI는 Scene·Prefab·PoolConfig를 편집하지 않았다. 검사 전후 Game 씬과 Ball/Cube PoolConfig는 동일하다. 작업 도중 AI/보조의 소유 범위 밖에서 Ball/Cube Prefab에 Rigidbody가 추가된 저장 변경을 발견했고, 출처를 추정하거나 되돌리지 않고 최신 사용자 소유 상태로 보존했다. [보존 기록](evidence/raw/obstacle-mvc-preservation-check.json).

다음 단위는 실제 위치·속도·충돌·Rigidbody 권위와 Unity Physics/직접 구현 Physics의 동일 비교 조건이다. 현재 Obstacle MVC의 대여 상태는 실제 HP·파괴·충돌·렌더 동작을 뜻하지 않는다. Cube Prefab의 ObstacleView 연결, Cube PoolConfig Prefab/목록 연결, Inspector·배치·게임 감각은 Daniel 소유이며 이번 격리 검사에서 자동 완성하지 않았다.

## W-003-PHYSX-LIFECYCLE-001 — 첫 PhysX 발사·충돌 수명

[DECISION:user / R-023 / 2026-09-09 KST] 채용 공고가 PhysX를 명시하므로 첫 플레이 물리는 Unity PhysX로 만들고, 물리 권위와 직접 구현 물리 비교 방법은 이후 별도 문서로 남긴다. 현재 단위에서는 Rigidbody가 위치·회전·선속도·각속도의 유일한 런타임 권위다. Model에 같은 값을 복사하지 않는다.

| 분담 | 이번 단위 |
|---|---|
| Daniel | Ball·Obstacle에 연결한 Rigidbody/Collider, Game 씬 배치와 Inspector 물리값, 최종 조작감을 소유한다. 마지막 Play Mode 메뉴 실행으로 실제 충돌 결과를 확인한다. |
| AI | Ball 1회 초기속도 명령, Ball/Obstacle 대여·반환·폐기 물리 초기화, 오래된 세대/lease 거절, 동적 Rigidbody 계약, 격리 PhysX 충돌 Probe와 문서·증거를 맡는다. |
| 함께 확인할 결과 | 저장 씬을 바꾸지 않는 임시 PhysicsScene에서 Ball이 한 번 발사되어 Obstacle과 충돌·이동하고, 반환/재대여/활성 풀 종료 뒤 속도와 이전 세대가 남지 않는가. |

구현 범위는 `BallController.TryLaunch(epoch, velocity)`와 두 Controller의 Rigidbody 초기화, 같은 GameObject의 Rigidbody/Collider 요구, 파괴된 Unity 객체의 오래된 PoolLease 즉시 거절이다. kinematic Rigidbody를 코드가 임의로 dynamic으로 바꾸지 않고 초기화 단계에서 명확히 거절한다. 질량·중력·제약·충돌 검출·보간·감쇠 등 Inspector 값은 읽기만 하고 보존한다.

검증 원본은 `Tools/Smesh Fest/Validation/PhysX Lifecycle Runtime`이다. 별도 `LocalPhysicsMode.Physics3D` 씬과 임시 소스/PoolConfig만 만들며 저장 Scene·Prefab·SO를 편집하지 않는다. Unity 6000.3.10f1의 최초 일반 컴파일과 14개 변경 스크립트 정적 진단은 오류·경고 0건이었다. Daniel의 1차 Play Mode 실행은 **assertion 10, 시뮬레이션 0회**에서 Ball Sleep/Obstacle Wake 복합 검사 실패로 중단됐다. 따라서 접촉·변위·반환 수명은 아직 통과가 아니다. [1차 실패 근거](evidence/raw/physx-lifecycle-runtime-failed-20260909T061136Z.json).

원인은 Pool의 `OnPoolRent`가 비활성 clone에서 실행되고 그 뒤 `SetActive(true)`가 호출되는 순서인데, 검증과 생산 코드가 비활성 Rigidbody에서 설정한 Sleep/Wake가 활성화 뒤에도 그대로 관측된다고 전제한 것이다. BallView/ObstacleView의 `OnEnable`이 현재 대여 세대를 확인한 뒤 각각 발사 전 Sleep과 충돌 대기 Wake를 재적용하도록 수정했다. Probe는 두 상태와 속도 초기화를 각각 검사하고, 임시 소스의 Rigidbody Inspector 기준을 Factory 초기화 **전**에 저장해 첫 대여부터 비교한다. 수정된 5개 스크립트의 Unity 정적 진단은 warning 0, error 0이며 프로젝트 compiler error도 0이다. **수정본 Play Mode 재실행 전이므로 성공으로 승격하지 않는다.** [수정 컴파일 근거](evidence/raw/physx-activation-fix-editor-compile.json) · [보존 근거](evidence/raw/physx-lifecycle-preservation-check.json).

현재 Game 씬의 24개 ObstacleView는 씬 배치 객체라 Pool의 `OnPoolCreated`를 자동으로 거치지 않는다. 따라서 Rigidbody 자체의 PhysX 반응은 가능하지만 Obstacle MVC/대여 수명은 아직 게임 플레이에 연결되지 않았다. Cube PoolConfig의 prefab과 Game PoolContainer 등록도 비어 있다. 격리 Probe 통과 뒤 W-001에서 **현재 씬 Blocks를 고정 스테이지 배치로 쓸지, Cube PoolConfig로 생성할지** 실제 게임 흐름에 맞춰 연결한다. 이 선택 전에는 사용자 씬을 재구성하지 않는다.

다음 구현 순서는 (1) Daniel의 수정된 격리 Probe 재확인, (2) 클릭 위치를 월드 목표점으로 변환, (3) Ball Pool 대여와 `TryLaunch`, (4) 같은 발사 방향으로 Cannon 회전, (5) 첫 실제 충돌 확인이다. HP/파괴/결과/재도전과 직접 구현 물리 비교는 그 다음이다. 대표 플레이가 확인되면 별도 `Docs/Prototype/PHYSICS_AUTHORITY.md`에 PhysX 권위 경계, 수명 전환, 직접 구현 방식의 대안 권위, 같은 입력·fixed step·초기조건·측정 항목을 정리한다.

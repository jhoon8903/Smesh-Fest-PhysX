# 확인·검증 기록

> **2026-09-09 최신 계약:** targetable Obstacle 직접 Raycast, Muzzle 발사, Cannon world-Yaw-only, Straight의 같은 GameObject `ObstacleView` 직접 충돌 **또는** `world Z > Config 경계` 중 먼저 발생한 중력 전환, Curve의 발사 즉시 중력, Config 기반 CCD와 물리 Layer Matrix를 사용한다. tag·name·parent 검색 fallback과 legacy 경로는 남기지 않는다.

초기 조사일: 2026-09-08. 각 절은 해당 시점의 기록이다. 과거 PhysX 수명 Probe 42개 통과는 유지되지만 최신 CCD/Yaw/충돌 또는 Z 경계 계약은 Probe 코드만 갱신됐고 아직 Play Mode 재실행 전이다. 최신 Unity 스크립트 진단 4개는 warning/error 0, Console error 0이다. 기존 실행 수치는 범위가 다르므로 합산하지 않으며 High 설정 빌드도 미실행이다.

| 검증 ID / 관련 요구·작업 | 실제 확인 방법·담당 | 관측 결과 | 상태·한계 |
|---|---|---|---|
| V-001 / R-001, SF-START-001 | Main: Git 상태·HEAD·경로 | 기존 프로젝트, 브랜치 `SciptSkeleton`, HEAD `f63c05e`. IDE 파일 수정과 다수 미추적 Scripts 존재. | 통과: 현재 디스크 상태 조사. 사용자 기여량·완료율은 추정하지 않음. |
| V-002 / R-001, SF-START-001 | Main: ProjectVersion·manifest·렌더 설정·저장 Game 씬 | Unity 6000.3.10f1, URP 17.3.0, Luna 없음. Game 씬에 카메라·조명·Volume·EventSystem, 빈 GameObjects/UI(HUD) 영역. | 일부 확인: 직렬화 파일 읽기. Editor 미저장 상태·실제 렌더 미확인. |
| V-003 / R-005, SF-START-001 | Main: 현재 작업의 실행 메타데이터와 배정 도구 | Main `gpt-6-astra`·`ultra` 실제 기록 확인. 보조 `gpt-5.6-terra`·`medium`, `fork_turns: none` 명시 호출 수락·결과 반환. | 통과: Main 설정 및 보조 명시 배정. 보조의 별도 실행 모델 확인값·토큰·비용은 미제공. Docs 템플릿은 비활성 그대로이며 프로젝트 `.codex/config.toml` 없음. |
| V-004 / R-002, SF-START-001-CODE | 보조: Assets/Scripts의 실제 C# 읽기 | GameFlow는 MonoBehaviour와 빈 Awake. 나머지 Ball/Obstacle·Object·Screen·Pool·Navigator·UpdateLoop는 빈 선언. A안 기능과 물리 비교 코드 없음. | 통과: 코드 조사. Unity 컴파일/실행 미검증. |
| V-101 / W-001 | Unity 현재 소스 컴파일과 스크립트 진단 | Unity 6000.3.10f1 compiler error 0, 관련 11개 스크립트 warning 0·error 0. 첫 compile의 `PointerEventData.eventSystem` 접근 오류를 별도 EventSystem 참조로 수정 | 통과: 정적 범위. Play Mode 아님 |
| V-102 / W-001 | Unity Play Mode에서 클릭·발사·정렬·충돌 | 1차 Probe는 assertion 5에서 fixture 가정 실패, simulatedSteps 0. test-only 수정·컴파일 완료 | 수정본 재실행과 실제 Game 확인 대기 |
| V-103 / W-003 | 물리 비교 측정 | PhysX 수명 수정본 42개·15 step·contact 1회·Obstacle 이동 0.6153807 통과. 직접 구현 비교 조건은 대표 플레이 뒤 별도 문서화 | PhysX 격리 수명 통과; 실제 플레이와 직접 구현 비교는 미완료 |
| V-104 / W-001-FLIGHT-PHYSICS-003 | Unity 컴파일·스크립트 진단·Layer Matrix 조회 | Ball/Obstacle Config 적용, Ball `ContinuousDynamic`, Obstacle `Continuous`, Cannon Yaw-only, Straight 직접 Obstacle 충돌·Z=0 유지·Z>0 경계 회귀를 코드와 disposable Probe에 연결. Obstacle 8·Ball 9·Ground 10, 필요한 네 gameplay 충돌만 활성 | 최신 스크립트 4개 warning/error 0, Console error 0. Click/PhysX Probe와 실제 Game Play Mode는 Daniel 확인 대기 |
| V-105 / Editor 복구 | 재시작 뒤 씬·컴파일·Project Settings 재조회 | Game 씬 dirty=false·루트 4개, Console error 0, Obstacle/Ball/Ground 레이어와 Layer Matrix 유지, 임시 `__Codex` Editor 스크립트 없음 | 복구 확인. Play Mode 검증과는 별도 |

## W-001-FLIGHT-PHYSICS-003 — CCD·Yaw-only·world-Z 중력

- 코드 권위: Ball/Obstacle SO가 mass·damping·interpolation·collision mode를 적용한다. `BallController`는 Straight의 중력 대기 상태를 대여 epoch와 함께 소유하고, 같은 GameObject의 `ObstacleView` 직접 충돌 또는 중앙 FixedTick의 엄격한 `position.z > GravityActivationWorldZ` 중 먼저 발생한 조건에서 한 번 전환한다.
- Cannon: yawRoot의 초기 회전을 기준으로 수평 방향의 world Y 회전만 적용하며 child local rotation은 수정하지 않는다.
- 최적화: Ball/Obstacle/Ground를 분리하고 Ball↔Obstacle, Ball↔Ground, Obstacle↔Ground, Obstacle↔Obstacle만 gameplay 충돌로 유지했다. Ball↔Ball·Ground↔Ground와 gameplay↔Default/UI 등은 비활성이다.
- 검증 상태: 최신 Unity 스크립트 진단 4개는 warning/error 0, Console error 0이다. 최신 Probe는 얇은 Obstacle CCD, Config 적용, 직접 Obstacle 충돌 전환, Z=0 유지, Z>0 전환, stale epoch, Curve 즉시 중력과 authored Cannon child 회전 보존을 검사하지만 아직 Play Mode 결과는 없다.
- 작업 중 AI가 Physics 설정 저장 확인을 위해 만든 임시 Editor 정리 코드에서 내부 `PhysicsManager` 타입을 잘못 판별해 로드된 Editor 객체를 과도하게 해제했고 Unity가 Fatal Error로 종료됐다. Daniel이 Editor를 다시 실행한 뒤 위 V-105 상태를 확인했다. 임시 스크립트는 제거했으며 같은 내부 설정 객체 로드·정리 경로는 다시 사용하지 않는다.

## 코드 근거

- `Assets/Scripts/Framework/Flow/GameFlow.cs`: 6행 MonoBehaviour, 8–11행 빈 Awake.
- `Assets/Scripts/InGame/Ball/BallController.cs`, `Obstacle/ObstacleController.cs`: 3행부터 빈 클래스. Model·View도 골격.
- `Assets/Scripts/Framework/Loop/UpdateLoop.cs`: 3행부터 빈 클래스. 등록/콜백/Update/FixedUpdate 구현 없음.
- `Assets/Scripts/Framework/Pool/IPool.cs`: 이름과 달리 현재 class 선언. 첫 단위의 수정 대상으로 자동 채택하지 않음.

사용자 병행 변경: 첫 조사 이후 `UpdateLoop.cs`가 추가되어 보조가 이 파일만 보완 확인했다. AI 변경이나 재작업으로 계산하지 않는다.

## 문서·보존 확인

- V-005 / R-004, SF-DOC-001: 기록기 init 및 이벤트 누적·보고서 자동 생성 실행 성공. 문서 링크·인계 manifest 및 최종 증거 검사는 완료 시 원자료로 연결한다.
- V-006 / R-001, R-003: 초기 스크립트·씬·Packages·ProjectSettings 77개 파일의 해시 기록. 후속 비교는 변화 유무만 보여주며 작성자를 해시만으로 판정하지 않는다. AI 쓰기는 Docs로 제한했다.

최초 실행 가능 빌드, 성능 향상, 시간/토큰 절감률은 측정하지 않았다. 이전 스킬 문서에 있는 기록기 테스트 11개 통과는 과거 기록이며 이번 Unity 검증으로 계산하지 않는다.

## 진행 순서 정정

2026-09-08 사용자 지시로 현재 단계를 전체 아키텍처·구현 계획 공유로 바로잡았다. W-001-AI 시작 이벤트는 실제 보조 제작 배정과 코드 변경 전에 남긴 사전 기록이며, 구현 성과가 아니다. 정정 이벤트로 종료한다. 실행한 보조는 읽기 전용 조사 1개뿐이며 완료 상태다.

최신 Git 확인에서 Assets/Scenes/Game.unity도 수정 상태다. AI는 씬 수정·저장 도구를 실행하지 않았으며 이 변경을 되돌리거나 재생성하지 않는다. 초기 코드·씬 조사는 그 시점의 결과로 한정한다. Unity 인스턴스 선택·상태 조회만 수행했고 게임 실행·컴파일 요청은 하지 않았다.

V-005 최종 문서 확인: 로컬 링크 23개와 context-manifest 경로 확인 통과, 해당 문서 Git 제외 없음. [실행 결과](evidence/raw/document-check-architecture-correction.json). 게임 검증과 구분한다.

## W-000-READ-002 — 아키텍처 기준 수신 뒤 최신 골격 확인

2026-09-08 Terra·Medium 보조가 Assets/Scripts를 읽기 전용으로 확인했다. GameFlow.cs:6은 MonoBehaviour, 8–11행 Awake는 비어 있다. UpdateLoop.cs:5는 MonoBehaviour, 7–10행 StartGameLoop는 비어 있다. 초기 조사 뒤 사용자 작업으로 Loop 선언이 바뀌었으며 초기 기록을 덮어쓰지 않는다.

Object MVC, Screen MVP, Pool/PoolFactory/Observable, Ball/Obstacle은 현재 선언 골격이고 클래스 간 실행 연결이 없다. 사용자의 아키텍처 기준이 미정이라는 뜻이 아니라 구현이 진행 중인 상태다. 별도 코드 구현·컴파일·Play Mode는 하지 않았다.

Main은 현재 manifest/lock에서 UniTask를 확인했고 Addressables 항목은 없음을 확인했다. Assets/Plugins/Demigiant/DOTween/DOTween.dll은 존재한다. 패키지 설치·설정 변경은 하지 않았다. Addressables 사용은 이미 사용자 기준으로 확정됐다.

V-007 / R-007, W-000-DOC-002: 8개 기준 A-01~A-08, 로컬 링크 42개, 인계 manifest·필수 원본 로드 경로와 Git 제외 여부 확인 통과. [문서 검사 결과](evidence/raw/architecture-baseline-document-check.json). 코드·씬·패키지는 AI가 수정하지 않았다.

V-008 / R-008, W-000-DI-001: DI를 A-09로 추가하고 현재 기준 9개, 재개 문서의 기준 수·원본 연결, context-manifest 경로를 확인했다. 특정 DI 라이브러리나 스코프는 확정하지 않았다. 게임 코드·씬·패키지 변경 없이 문서만 갱신했다.

V-009 / R-009, W-000-STRIPPING-001: High Stripping을 A-10으로 추가하고 10개 기준·문서 연결·인계 manifest를 확인했다. Unity 6000.3 공식 코드 보존/설정 문서에서 Preserve·link.xml과 High 검증 지침을 확인했다. ProjectSettings 파일에는 Android managedStrippingLevel: 3, scriptingBackend: 1이 기록되어 있다. 이는 읽은 직렬화 값이며 다른 플랫폼·빌드 프로필의 유효 설정까지 검증한 것은 아니다. Assets 아래 사용자 link.xml 검색 결과는 없었다. 패키지 생성 보존 파일·라이브러리 내부 어노테이션은 전수 조사하지 않았다. 코드·씬·설정·패키지 변경, 보존 파일 작성, High Player 빌드 실행은 하지 않았다.


## W-000-OBSERVER-001 — 첫 기반 구현·Editor 실행

- V-010 / R-010, W-000-READ-003: 최신 코드 조사 Terra·Medium, 현재 하이어라키·컴포넌트 조회 Main. 사용자 GameFlow는 로그 레벨 초기화, UpdateLoop는 배율 Clamp와 빈 StartLoop를 갖는다. WorldObjects에 두 컴포넌트가 붙어 있고 씬 minScale=0, maxScale=2다. GameObjects 아래 Ground·Table·Blocks(하위 24개)·Ball·Cannon, 별도 UI(HUD)가 있다. Ball에는 SphereCollider가 있으며 Rigidbody는 아직 없었다. [조회 원자료](evidence/raw/architecture-resume-hierarchy.json). 사용자 변경을 AI 성과로 계산하지 않는다.
- V-011 / W-000-OBSERVER-001: Terra·Medium이 Observable과 검사 본문을 구현했다. Main 검토에서 매번 수행하던 압축 순회를 삭제가 생긴 경우에만 수행하도록 줄였고, 할당 측정의 MethodInfo.Invoke를 측정 전 생성한 Func<long> 호출로 바꿨다. 수정 후 Unity 6000.3.10f1 정상 컴파일, Console 오류 0건을 확인했다. 새 Editor 검증 파일만 대상으로 import했고 씬 저장·전체 재임포트·Play Mode는 실행하지 않았다.
- 검증 도구의 임시 어셈블리 컴파일은 `Compiler failed to produce the assembly. Output: ''`로 실패했다. 이 시도에서 동작 검사는 실행되지 않았다. 일반 Unity 컴파일로 등록한 명시적 Editor 메뉴로 전환해 검사를 완료했다. [실행 경로 원자료](evidence/raw/observable-execution-path.json).
- Editor 메뉴 `Tools/Smesh Fest/Validation/Observable`에서 10개 동작 검사가 통과했다: 등록 순서, 중복/null, 자기 해제, 뒤 순서 대상 해제, 발행 중 추가, 해제 후 재구독, Clear 뒤 추가, 리스너 예외 복구, 재귀 발행 거절·복구, 반복 구독/해제 Count. 실행 원본은 [ObservableValidation.cs](../../Assets/Editor/Validation/ObservableValidation.cs), 초기 본문 스냅샷은 [ObservableChecks.cs.txt](evidence/checks/ObservableChecks.cs.txt)다.
- 할당 측정: Observable<int>와 구독자 1개를 측정 전에 생성, 100회 워밍업 후 동기 Publish 1,000회를 `GC.GetAllocatedBytesForCurrentThread`로 측정해 **0바이트**. Unity Editor 현재 스레드의 고정 구독 조건이다. 구독/용량 증가·다른 payload/리스너·전체 게임 Zero Alloc·성능 향상률·High Player/AOT의 증거는 아니다. [실행 결과](evidence/raw/observable-validation.json).
- V-012 / 보존: 재개 직전 기준 93개 중 92개 동일, 예상한 Observable.cs 1개만 변경, 누락·예상 밖 변경 0개. Game.unity, 기존 스크립트/.meta, 기준에 포함된 수정 Material, Packages와 ProjectSettings가 보존됐다. 새 Editor 검증 파일과 Unity가 만든 해당 폴더/스크립트 .meta를 추가했다. 검증 전후 활성 Game 씬 경로와 dirty=false가 동일했다. [보존 결과](evidence/raw/observer-preservation-check.json). 이 해시는 해당 기준 파일의 동일성을 확인하며 전체 Editor 상태를 증명하지는 않는다.
- W-000-OBSERVER-REVIEW-001: 기존 Terra·Medium 보조의 읽기 전용 검토에서 현재 계약 범위의 재현 가능한 결함을 발견하지 못했다. 별도 실행은 Main 검사의 근거를 따른다. 보조 실제 사용량은 미제공이다.

남은 작업: O-004 DI 도구/기존 구현과 O-005 물리·게임 연출·UI의 시간 적용 답변 후 GameFlow·Loop를 연결한다. 전체 아키텍처, 실제 Loop 프레임 이벤트, Pool·DI·Addressables, 플레이 기능 및 대상 High Player 검증은 미완료다. 이 Editor 메뉴 결과를 NUnit EditMode/PlayMode 테스트 통과로 기록하지 않는다.


## W-001-INPUT-DOC-001 — 클릭 발사·Cannon 회전 요구 반영

2026-09-08 / R-011: 사용자가 별도 조준 단계 없이 Block이 있는 영역을 선택하면 해당 위치로 Ball을 발사하고 Cannon이 Ball 진행 방향으로 회전한다고 확정했다. DESIGN을 조작 원본으로 갱신하고 BRIEF·PLAN·README·HANDOFF의 현재 목표를 동기화했다. 과거 이벤트의 조준·발사 표현은 당시 기록으로 유지한다.

Luna·Low 보조의 읽기 전용 문구 확인에서 현재 재개 문서 4곳의 별도 조준 오해 가능성을 확인해 반영했다. 특정 입력 API·발사 궤적·회전 연출을 새로 확정하지 않았으며 기존 아키텍처 선행 순서는 유지한다. 이 작업은 문서 정정이다. 게임 코드·씬·Inspector·패키지 수정과 Unity 실행·컴파일·플레이 검증을 수행하지 않았다.


## W-000-LOOP-001 — 외부 delta를 받는 Loop 기반

2026-09-08 / R-012. Terra·Medium 읽기 전용 조사(W-000-READ-004)에서 사용자 Cannon Controller/Model/View/Head 골격과 현재 MVC/MVP/Pool의 미연결 상태를 확인했다. DI 구현은 Assets/Scripts 조사에서 발견하지 못했다. 사용자 Observable 스타일 변경과 Cannon 추가를 보존하고 신규 ILoopEvents·LoopDispatcher 및 Editor 검증 메뉴만 작성했다.

Main 검토 1회에서 중단된 Tick을 재귀 검사보다 먼저 무시하도록 경계를 수정했고, 중단/폐기 세 phase·중복 구독·호출자에게 전파된 재귀 예외 후 복구 검사를 보완했다. 신규 C# 3개만 대상으로 import했다. 씬 저장·전체 재임포트·게임 실행은 하지 않았다.

검증: Unity 6000.3.10f1의 정상 컴파일 완료, Console 오류 0건. 명시적 Editor 메뉴 `Tools/Smesh Fest/Validation/Loop`에서 순수 C# 인스턴스로 다음을 실행해 통과했다.

- 기동 전 전달 없음, 반복 기동·중단과 재시작에 중복 없음, 세 phase 분리와 원래 delta 전달.
- 같은 delegate 중복 등록 억제, Stop 중 현재 발행 완료 및 후속/중첩 Tick 무시.
- Dispose 중 나머지 호출 중단, 반복 Dispose·Stop·세 phase Tick의 안전한 무시, 폐기 후 모든 phase 구독 추가와 재시작 거절, 해제 안전.
- 발행 중 해제/재구독의 다음 발행 적용, 리스너 예외와 phase 간 재귀 예외의 전파·이후 복구.
- 0 delta 허용, 실행 중 음수·NaN·무한대 거절.

할당: phase별 구독자 1개를 사전 등록, 100 cycle 워밍업, 1,000 cycle의 Update/Fixed/Late 총 3,000 Tick에서 현재 스레드 관리 할당 **0바이트**. 초기 생성/구독·실제 Unity 프레임·물리·전체 게임·Player/AOT의 수치가 아니다. [실행 결과](evidence/raw/loop-validation.json), [Editor 확인 원자료](evidence/raw/loop-editor-check.json), [재실행 코드](../../Assets/Editor/Validation/LoopValidation.cs).

보존: 이번 시작 기준 105개 모두 동일, 누락·변경 0개. 새 C# 3개와 해당 .meta 3개만 추가했다. 활성 Game 씬 경로·dirty=false와 root 수가 검증 전후 동일했다. [기준](evidence/raw/loop-baseline.json), [하이어라키](evidence/raw/loop-hierarchy-baseline.json), [최종 비교](evidence/raw/loop-preservation-check.json).

이 결과는 Editor 메뉴 실행 검사이며 NUnit·Play Mode·High Player 통과가 아니다. 기존 GameFlow·UpdateLoop 코드는 보존했고 실제 주입/프레임 연결·TimeScale/물리/UI 시간 적용은 O-004·O-005 답변을 기다린다. 전체 아키텍처 완료나 클릭 발사 구현으로 계산하지 않는다.

## W-000-DI-PAUSE-001 — VContainer·게임 시간·UI Pause 실행

2026-09-08 / R-013. Daniel이 설치한 VContainer 1.19.0을 manifest/lock과 실제 PackageCache 소스에서 확인했다. 등록 callback 순서, 기존 컴포넌트 주입, scoped IDisposable 역순 정리를 Terra·Medium 조사와 Sol·High 검토로 확인한 뒤 현재 씬에서 실행했다. UI의 별도 Clock은 만들지 않았다.

### 구현·보완

Main은 GameFlow·UpdateLoop 주입/Unity phase, UnityGameTime, GameLifetimeScope와 ScreenPauseScope를 통합했다. Terra·Medium은 GameClock/IGamePause와 Editor·Play Mode 검사 코드를 작성했다. 진행 중 전역 GameLifetimeScope 골격으로 바뀐 최신 소스를 확인해 namespace를 되돌리지 않고 등록만 채웠다.

Sol·High 검토에서 UpdateLoop.enabled=false→true가 GameClock의 실행 의도를 지워 영구 정지하는 결함을 찾았다. Main이 OnDisable은 dispatcher만 중단하도록 바꾸고 실제 회귀 검사를 추가했다. Main은 StateChanged 중 동기적으로 화면을 닫을 때 Pause 소유권이 남지 않도록 소유 표시를 Pause 호출 전에 설정했다.

컴파일 단계에서 검증 코드의 Framework.DI namespace 불일치(CS0234), VContainer.Unity 확장 using 누락(CS1061), Framework.Object와 UnityEngine.Object 이름 충돌(CS0234)을 수정했다. UnityMCP의 domain reload 포트 재연결 경고도 관찰됐다. 수정 후 메뉴 등록과 Console 오류 0건을 확인했다. 첫 Play 시도는 결과 파일이 생기기 전에 종료해 완료 근거로 쓰지 않았다. 재실행에서는 결과 JSON의 success=true를 확인한 뒤 종료했다.

### 실제 검사 결과

- [첫 GameClock Editor 검사](evidence/raw/game-clock-validation-first.json): 2026-09-08T13:41:41Z, Unity 6000.3.10f1, 일반 C# 객체를 명시적 메뉴에서 실행. 중첩·중복·참조 owner, Stop 중 UI 닫힘, Pause 중 배율 변경, 잘못된 배율, 예외/폐기 계약 통과. 사전 생성 owner/handler, 100 cycle 워밍업 뒤 1,000 cycle Pause/Resume/배율 변경 관리 할당량 **0바이트**. 씬/dirty 변화 없음. 초기 생성·최초 용량 증가·UI 생성·전체 게임 GC의 측정은 아니다.
- [첫 Play Mode 검사](evidence/raw/di-pause-playmode-first.json): 현재 Game.unity의 GameFlow가 실제 주입으로 시작한 상태에서 25개 assertion 통과. 주입을 우회해 직접 StartLoop를 호출하지 않았다. Update 131회·Fixed 26회·Late 130회, 경과 209프레임. 이 실행의 관측치이며 FPS/성능 벤치마크가 아니다.
- UpdateLoop 비활성화/재활성화, GameFlow 중단/재시작, 2개 화면 중 하나만 닫으면 Pause 유지, 마지막 닫힘 후 요청 배속 1.5/2 복원, 이미 활성인 UI에 주입, 비활성화·재활성화·파괴, StateChanged 안의 동기 UI 닫힘을 확인했다.
- 테스트용 Rigidbody·scaled DOTween·scaled ParticleSystem과 3phase Tick이 Pause 동안 진행하지 않고 재개 후 진행했다. 사용자 Ball에는 Rigidbody를 붙이지 않았고 사용자 객체로 발사/물리 성능을 측정하지 않았다.
- 마지막에 Scope.DisposeCore로 컨테이너만 정리해 원래 Unity timeScale=1 복원과 이후 전달 억제를 확인했다. WorldObjects를 파괴하는 LifetimeScope.Dispose는 호출하지 않았다. 검사용 UI/물리/효과 객체는 임시 객체이며 사용자 씬 자산에 저장되지 않았다.
- [최종 Editor 상태](evidence/raw/di-pause-editor-final.json): Console 오류 0건, Play 종료 후 Game 씬 dirty=false, root 3개. NUnit Test Runner 결과가 아니라 명시적 Editor 메뉴 및 실제 Play Mode coroutine의 검사 결과다. Daniel의 수동 조작감 확인은 아직 없다.

### 보존과 확인 한계

기준 111개 중 108개 동일, 누락 0개. 기존 파일 변경은 GameFlow.cs·UpdateLoop.cs·Game.unity뿐이다. WorldObjects에 GameLifetimeScope와 uiRoot=UI(HUD) 참조를 추가해 저장했다. 직전 씬 복구본과 비교해 모든 기존 컴포넌트 블록·Transform·Collider·Material 참조·씬 minScale=0/maxScale=2가 그대로임을 확인했다. [보존 결과](evidence/raw/di-pause-preservation-check.json), [씬의 정확한 변경](evidence/raw/di-pause-scene-change.diff), [복구본 정보](evidence/backups/di-pause-20260908/manifest.json).

전체 아키텍처와 MVC/MVP/Pool/Factory/SO/Addressables 연결은 미완성이다. 현재 비어 있는 상시 HUD에는 ScreenPauseScope를 자동 부착하지 않았으며 실제 여닫는 화면에서 주입·활성 수명으로 사용한다. UI 화면 디자인과 게임 발사 기능을 만든 결과가 아니다.

High 소스 조치는 주입 메서드 Preserve와 순수 서비스의 명시적 생성 factory다. **실제 High Player, Android IL2CPP/AOT, 기기 검증은 미실행**이다. 이 Mac Editor 설치에서 확인한 Mac Player variation은 Mono이며 실제 대상의 High 검증을 대신하지 않는다.

배정은 Main Astra·Ultra, 기존 조사/제작 보조 Terra·Medium, 복잡한 수명 검토 Sol·High다. 보조 최대 2개, 재위임 없음. 검증 코드의 컴파일 보완과 재실행이 포함됐으며 실제 토큰·비용은 미제공(null)이다. start/finish 관측시간에는 보완·검토·연결 대기와 문서 작업이 포함된다.

문서 확인: 로컬 링크 89개, context-manifest 경로 39개, 확정 기준 A-01~A-10과 O-004·O-005 해결 표기를 확인했다. [문서 검사](evidence/raw/di-pause-document-check.json). 저장소 전체 diff --check는 Unity 직렬화 YAML의 빈 값 뒤 공백(Game.unity, 기존 PackageManagerSettings.asset)을 보고했으므로 전체 통과로 기록하지 않는다. 사용자/Unity 직렬화 파일을 공백 정리 목적으로 다시 쓰지 않았다.

### 병행 편집 보존 후 최신 코드 재검증

첫 실행 이후 GameFlow·UpdateLoop의 표현 정리, GameClock의 배율 비교를 Mathf.Approximately로 변경한 병행 편집을 확인했다. 그대로 유지했고 첫 결과를 별도 스냅샷에 보존한 다음 최신 코드로 재검증했다. GameClock은 이제 Unity Mathf에 의존하는 일반 C# 객체이며 Unity 비의존 클래스로 표현하지 않는다.

2026-09-08T13:55:02Z [최신 GameClock 검사](evidence/raw/game-clock-validation.json) 통과·반복 관리 할당 0바이트. 13:55:18Z에 시작한 [최신 Play Mode 검사](evidence/raw/di-pause-playmode.json)도 25개 assertion 통과, Update 90/Fixed 26/Late 89, 경과 152프레임, timeScale 1→1이다. Unity 창에 포커스를 준 뒤 결과 파일 생성을 확인했고 Play를 종료했다. 수동 게임 플레이 평가를 한 것은 아니다.

이 재검증 때문에 동일 검사를 반복했다. [병행 변경 기록](evidence/raw/di-pause-concurrent-edit.json)과 최종 소스·씬 상태는 인계 이벤트에 함께 남긴다. 최초 보존 해시는 당시 스냅샷으로 유지한다.

최종 스냅샷에서는 기준 111개 중 104개 동일, GameFlow·UpdateLoop·Game.unity와 병행 편집된 LoopDispatcher가 변경 상태다. Navigator/ScreenNavi.cs와 .meta, Navigator.meta의 병행 삭제/이동을 관찰했으며 AI는 되돌리지 않았다. LoopDispatcher 표현 정리를 보존한 뒤 Editor Loop 검사를 재실행해 통과했다. [최신 코드 확인](evidence/raw/di-pause-post-edit-validation.json).

## W-000-POOL-001 — 독립 ObView와 선택적 IPoolable

2026-09-09 KST(실행 로그는 UTC). R-015~R-017의 생성·초기화·설정 시간·비활성 MinPool 유지 결정과 R-018의 상속 정정을 반영했다. `ObView : MonoBehaviour`, `BallView : ObView, IPoolable`이다. 모든 ObView를 Poolable 기반에 묶었던 AI 구현은 철회했고 공통 처리는 Pool 소유의 PoolLifecycleRunner로 옮겼다. IPoolable을 구현하지 않은 일반 ObView도 사용할 수 있다.

PoolConfig(SO)·PoolContainer·Factory·사용 번호를 가진 PoolLease를 구현했다. 원본의 활성값을 바꾸지 않고 복제 → DI → 초기화 → 활성화하며, 비활성 재고의 설정 시간이 지나면 MinPool만 남긴다. 실제 GameLifetimeScope에 Factory를 등록하고 기존 GameObjects/PoolContainer의 컴포넌트와 참조만 연결했다. 현재 Configs는 빈 목록이고 실제 Ball Prefab·MVC 상태·발사 동작은 아직 연결하지 않았다.

### 실제 실행 결과

- Unity 6000.3.10f1 Play Mode: **66개 assertion 통과**, Console 오류 0건. 2026-09-08T15:01:55Z [최종 Pool 결과](evidence/raw/pool-runtime-validation.json). 일반 ObView의 독립성, DI/초기화 후 OnEnable, 사전 생성·최대 수·동일 인스턴스 재사용, 오래된/중복/default/타 풀 lease의 분리, 초기화/반환/폐기 실패 격리, 재진입을 확인했다.
- 실제 Tween·ParticleSystem·취소 토큰·구독의 정리를 확인했다. 반환 직전 살아 있는 입자 수를 확인하고 반환 뒤 0을 검사했다. 씬 계층이 먼저 파괴된 다음 Factory를 폐기해도 카운트와 lease가 정리됐다. 테스트는 임시 객체로 실행하며 사용자 Scene/Prefab을 재생성하지 않는다.
- 설정 시간 직전/경계, 0초 자동 정리 끄기, 활성 3개·비활성 3개·MinPool 2에서 비활성 1개만 정리, UI Pause에 해당하는 timeScale=0 중 실제 UniTask 유지보수와 MinPool 유지를 확인했다.
- 100회 워밍업 뒤 **빈 IPoolable의 반복 대여·반환 1,000회: 관리 할당 0바이트**. 효과·취소 토큰·자식 파츠·객체 생성·측정 구간 안 assertion은 제외했다. 전체 게임 Zero Alloc이나 기존 대비 성능 개선을 뜻하지 않는다.
- PoolFactory를 추가한 현재 씬에서 기존 DI·UI Pause 검사도 **25개 통과**했다. 2026-09-08T15:02:23Z, timeScale 1→1, [회귀 결과](evidence/raw/di-pause-playmode.json). 검사 중 테스트용 서비스 종료는 DisposeCore이며 사용자 WorldObjects를 파괴하는 LifetimeScope.Dispose는 호출하지 않았다.
- Play 종료 뒤 Game 씬 dirty=false·루트 3개·Console 오류 0건. UnityMCP 재연결 포트 경고 2건은 별도로 남았다. [Editor 종료 상태](evidence/raw/pool-editor-final.json).

### 보완과 보존

생성/대여 콜백 안의 Dispose로 재고가 되살아나는 문제, 반환 후 같은 대여가 다시 활성화되는 문제, Trim 폐기 중 재진입의 인덱스 문제, 사용 중 Dispose에서 Return 정리가 누락되는 문제를 보완했다. 대여 콜백 안의 Return은 lease를 먼저 무효화하고 콜백이 끝난 뒤 정리한다. 그 콜백에서 Return 이후 만든 상태도 정리되며 뒤의 Rent 파츠는 실행하지 않는다. Dispose는 현재 Rent 호출 묶음이 끝난 뒤 Return → Destroy를 수행한다. 같은 풀의 재귀 대여/사전 생성/정리는 수명 콜백 중 차단한다.

첫 하네스는 정리 완료 플래그만 확인하고 실패·재진입 검사가 부족해 Terra Medium에서 Sol High로 한 번 상향했다. Preserve 이름 충돌·Pool 네임스페이스 충돌·조건부 대여의 미할당 지역변수를 고친 뒤 실제 실행했다. 초기 [63개 통과 기록](evidence/raw/pool-runtime-first-pass.json)을 보존하고, 입자 생성 전제·계층 선파괴 경계를 추가하여 66개로 재검증했다. 이전 [DI·Pause 기록](evidence/raw/di-pause-before-pool-integration.json)도 보존했다. 사용량·비용 수치는 제공되지 않아 추정하지 않는다.

시작 기준 129개 파일 중 121개 동일, 담당 기존 파일 6개 변경, Poolable.cs/.meta 2개는 PoolLifecycleRunner로 이동했다(메타 GUID 유지). 미설명 변경·누락은 없다. 씬 기존 190개 블록에서 PoolContainer 컴포넌트 목록·scope 참조만 바뀌었고 새 컴포넌트 블록 1개 외 기존 객체/Transform/Material/Collider 값은 동일하다. [보존 검사](evidence/raw/pool-preservation-check.json), [정확한 씬 변경](evidence/raw/pool-scene-change.diff). 복구본은 evidence/backups/pool-20260908에 있다.

현재 검증은 일반 컴파일과 Editor Play Mode다. **High Player·Android IL2CPP/AOT·기기는 미실행**이다. PoolFactory는 명시적 생성 factory로 등록하고 테스트 DI 주입 메서드는 Preserve 처리했지만 Player 보존 완료를 주장하지 않는다. Addressables 핸들/자산 해제, 실제 Ball MVC·물리 상태 초기화와 플레이 기능은 후속 단위다.

## W-000-MVC-REFERENCE-001 — 참고 비교와 단계적 간소화

2026-09-09 KST / R-019. Daniel이 ProjectTemplate MVC를 참고로 제시하고 동일 복제 대신 현재 프로젝트에 간소화해 붙여나가도록 방향을 정했다. Main은 Core·Observer·BaseView와 CellModel/CellView·BoardBuilder 연결을 읽었고, 기존 Terra·Medium 보조는 Pool·DI와 LivesView의 실제 Get/Return 사용을 비교했다. 참고 파일은 읽기 전용이다.

- 참고 `BaseView.cs:9`는 PoolableView 상속이며 `Model` setter와 BindModel/UnbindModel이 관찰 연결을 담당한다. CellModel은 변경 뒤 Raise하고 BoardBuilder는 View.Model에 해당 Model을 넣는다. 현재 ObModel·ObController는 비어 있고 ObView에 해당 연결이 없음을 확인했다. 기존 Observable<T> 검증과 Pool 66개 통과가 MVC 연결 완료를 뜻하지 않는다는 점을 재개 문서에 명시했다.
- 참고 ModuleContainer의 SO 모듈·단계별 비동기 초기화·모듈별 DI 등록과 현재의 씬 GameLifetimeScope를 구분했다. 기존 명시적 조립과 R-018의 독립 ObView·선택적 IPoolable을 유지하며 Model 통지·View 연결부터 적용하는 설계안을 ARCHITECTURE R-019와 PLAN에 기록했다. 객체별 Model/Controller 재사용 정책을 전역 하나로 고정하지 않았다.
- 간소화 기준·다음 작은 결과·분담·검증 계획을 기존 문서에 갱신했다. **이 단위는 소스 조사와 문서 변경만 수행했으며, MVC 코드 수정·Unity 컴파일·Play Mode·Player 실행은 하지 않았다.** 참고 프로젝트의 실행 성공이나 High 호환성도 검증하지 않았다.

보존 기준: 현재 프로젝트 152개 파일(기존 수정 파일 포함), 참고 MVC와 대표 사용 파일 34개를 기록했다. 비교 후 참고 34개는 모두 동일하다. 현재 프로젝트의 기준 중 변경은 이번 대상인 Docs/README.md 하나이며 나머지 151개는 동일하다. 기존 사용자 씬·코드·Material·설정과 새 Assets/Project 폴더 파일을 덮어쓰지 않았다. 이는 저장된 파일 기준이며 미저장 Editor 상태를 확인한 결과는 아니다. [조사 기준](evidence/raw/mvc-reference-baseline.json), [보존 결과](evidence/raw/mvc-reference-preservation-check.json).

Luna·Low의 읽기 전용 문서 일치 검토 후 과거 Pool 목표의 표현을 당시 기록으로 명확히 했다. 문서 6개에서 R-019 연결·기준 10개·MVC 미구현 표기, 로컬 링크 109개와 인계 manifest 67개 경로를 확인했고 누락·뒤 공백은 없었다. [문서 확인 결과](evidence/raw/mvc-reference-document-check.json). 누적 기록 파일 검사는 미변경 90개·과거 기록 이후 변경 85개·누락 0개였다. 현재 갱신 문서는 새 종료 이벤트에서 최신 해시로 연결하며 과거 증거는 유지한다. 다음 작업은 PLAN의 W-000-MVC-001이며 이번 비교를 코드 구현 성과로 계산하지 않는다.

## W-000-MVC-001 — 최소 Model–View 연결과 풀 수명 검사

2026-09-09 KST / R-019·R-020. Terra·Medium이 ObModel 변경 통지와 ObView<TModel>의 활성 관찰 수명을 작성했다. Main은 임시 MVC/Pool/DI 검증, 통합·컴파일 보완과 문서를 맡았고 Sol·High는 예외·재진입·구독 수명을 읽기 전용으로 검토했다. Sol은 현재 계약에서 추가 재현 결함을 찾지 못했다. 사용량·비용은 미제공이다.

### 구현과 검토에서 보완한 경계

ObModel은 기존 Observable<ObModel>을 private 전달기로 사용한다. 모델이 필요한 View만 새 ObView<TModel>을 상속하며 기존 ObView의 독립성을 유지한다. Bind/Unbind, 활성 상태의 관찰·최초/변경 RefreshView, 추가 이벤트 구독을 위한 OnModelBound/OnModelUnbound가 공통 책임이다. 비활성 Bind는 참조만 보관하며 Model/Controller의 reset·Dispose·재생성을 강제하지 않는다.

Main이 같은 모델을 연결 훅 안에서 해제하고 다시 연결할 때 이전 호출도 최초 갱신을 수행할 수 있는 경계를 발견했다. 관찰 세대를 구분해 중복 최초 갱신과 이전 연결의 실패 정리가 새 연결을 해제하는 문제를 보완했고 재현 검사를 추가했다. C# 컴파일에서 public/private IsObserving 이름 충돌(CS0102), 검증 코드의 Framework.Object/UnityEngine.Object 해석 충돌(CS0234)을 수정한 뒤 일반 Unity 컴파일을 통과했다.

### 실제 실행과 제한

Unity 6000.3.10f1, 명시적 `Tools/Smesh Fest/Validation/MVC Runtime` 메뉴로 만든 임시 객체에서 **58개 assertion 통과**. [실행 결과](evidence/raw/mvc-runtime-validation.json)의 recordedAtUtc `2026-09-08T15:56:48.3065190Z`는 검사 시작 시각이며 결과 파일은 정리 후 두 프레임을 지난 다음 기록했다. NUnit Test Runner나 Player 결과가 아니다.

- 비풀링 ObView 독립 사용, 비활성 Bind, 최초 현재 상태 표시, 동일 Model 중복 Bind 억제, 변경 통지와 추가 모델 이벤트, Model 교체 후 옛 통지 차단.
- GameObject 비활성/재활성, 컴포넌트 disabled/재활성에서 구독·추가 이벤트 해제 및 최신 상태 표시. Unbind 반복·null 거절·파괴 전 비활성화에서 구독 해제.
- 연결/최초 Refresh/해제 훅 예외 후 정리·재연결. 통지 중 교체/해제, 연결 훅의 동일 모델 재연결·disable/enable, 이전 훅의 실패가 새 관찰을 지우지 않는 경계.
- 별도 VContainer와 실제 PoolFactory가 비활성 복제물에 주입한 뒤 생성·대여. 주입된 ILoopEvents → 테스트 Controller → Model 변경 → View 갱신, 반환 뒤 모델/Loop 통지 차단, 재대여 중복 방지, 오래된 lease 거절, 대여 중 Factory Dispose의 구독 정리.
- MVC 묶음을 유지하는 종류와 Model/Controller를 새로 만드는 종류를 각각 확인했다. 이 두 정책을 공통부가 덮어쓰지 않는다.

할당: 모델·View·추가 모델 이벤트의 delegate를 준비하고 100회 워밍업한 뒤, 동기 상태 변경 통지 1,000회와 Unbind/Bind 1,000회를 각각 현재 스레드에서 측정해 **각각 0바이트**. 측정 구간 안의 로그·assertion, 객체 생성, 풀 대여/반환, 실제 게임 표현·렌더는 제외했다. 전체 게임 Zero Alloc이나 지연 개선률의 증거가 아니다.

Play 전환 직후 MCP 연결이 끊겨 포트 6402를 재선택한 뒤 메뉴를 실행했다. Editor가 비활성이라 정리 후 프레임 진행이 지연되어 Unity 창을 활성화해 결과 기록을 완료했다. 첫 AppleEvent 활성화 시도는 시간 초과했으며 이후 OS 앱 열기로 창을 활성화했다. Game 시간 설정이나 runInBackground를 바꾸지 않았다. 종료 후 Play=false·컴파일 대기 없음, Console 오류 0건이며 MCP 포트 재연결 경고 3건은 남았다. [실행 전](evidence/raw/mvc-editor-before.json), [종료 상태](evidence/raw/mvc-editor-final.json).

### 보존과 다음 범위

시작 기준 140개 파일 중 139개 동일, ObModel.cs만 예상 변경, 누락·미설명 변경 0개. 신규 ObViewOfT.cs·MvcRuntimeProbe.cs·MvcValidation.cs와 각 .meta를 추가했다. 저장 Game 씬과 기존 스크립트·Prefab·Material·설정은 그대로이며 Play 전후 dirty=false·루트 4개다. 이전 기록의 루트 3개로 되돌리지 않았다. [기준](evidence/raw/mvc-baseline.json), [보존 결과](evidence/raw/mvc-preservation-check.json).

실제 BallModel/BallController/BallView 연결·물리·발사·Cannon 회전, 화면별 MVP·SO/Addressables 핸들 정책과 **High Player·IL2CPP/AOT·기기 검증은 미실행**이다. 임시 ProbeController를 실제 게임 Controller 완료로 기록하지 않는다. 이번에 변경하지 않은 DI/Pause·Pool 기존 전체 검사를 다시 수행한 것은 아니며, 실제 Factory를 사용하는 이번 MVC 통합 경로만 새로 검증했다.

현재 Git 브랜치 MVC·HEAD 21567e5와 HANDOFF의 추적 상태를 확인했다. 이전 인계의 SciptSkeleton·미추적 설명을 현재 상태로 정정했다. 시작 baseline의 Git 상태에 이미 UI/Clear.meta·UI/Failed.meta 삭제가 있었으므로 기존 변경으로 보존했다. 이번 작업에서는 커밋·브랜치 전환·푸시를 수행하지 않았다.

문서 6개의 로컬 링크·인계 manifest 75개 경로와 공통 연결/실제 Ball 미완료 구분을 확인했다. [문서 검사](evidence/raw/mvc-document-check.json). 변경 대상의 diff 공백 검사도 통과했다.

## W-000-BALL-MVC-001 — Ball별 묶음 재사용

2026-09-09 KST / R-021·R-022. Daniel은 Ball을 풀 인스턴스별 View·Model·Controller 묶음으로 유지하고, 첫 Model은 물리 비종속 대여 상태·사용 세대만 갖도록 결정했다. 이 컨텍스트에서 Ball MVC를 끝내고 다음 컨텍스트에서 Obstacle MVC, 그 다음 물리 구현으로 진행한다.

Main은 최신 Ball/Cube Prefab·PoolConfig·Game 씬을 읽어 사용자 소유 상태를 기준으로 고정하고 BallModel/BallController/BallView를 통합했다. Terra·Medium은 기존 Pool/ObView 수명을 읽고 임시 검사 코드를 작성했다. Sol·High의 독립 검토에서 계층 선파괴 즉시 정리와 Controller 정상 반환 검사가 빠진 점을 발견했고 Main이 보완했다. 재검사 전 첫 시도는 12개 assertion이었으며, 보완 후 최종 결과는 19개다. 같은 Sol·High 재검토에서 남은 수명·재진입·폐기 결함은 발견되지 않았다. 보조 사용량·비용은 제공되지 않았다.

### 구현 계약

- BallModel은 `IsRented`, 0이 아닌 `RentalEpoch`, 현재 세대 확인만 소유한다. 위치·속도·충돌 결과와 Rigidbody 상태는 없다.
- BallView는 `OnPoolCreated`에서 Model/Controller를 한 번 만들고, 대여 때 Model 시작 → 비활성 Bind → Controller에 현재 PoolLease/세대를 연결한다. 반환 때 Controller → View 관찰 → Model 순서로 정리한다.
- BallController의 `TryReturn(epoch)`는 Controller·Model·PoolLease가 모두 현재 대여일 때만 반환한다. 반환 뒤 lease와 세대를 버려 오래된 완료/반환이 새 대여를 건드리지 않게 한다.
- Pool `OnPoolDestroy`와 Unity `OnDestroy`가 같은 idempotent 묶음 정리를 사용한다. 따라서 씬 계층이 Factory보다 먼저 파괴돼도 캐시된 Controller·Model은 즉시 비활성 상태다.
- 실제 입력·Loop·물리 구독과 시각 갱신은 권위 계약이 없는 상태에서 가짜 구현하지 않았다.

### 실제 검사와 보완 이력

Unity 6000.3.10f1 일반 스크립트 컴파일 후 `Tools/Smesh Fest/Validation/Ball MVC Runtime`을 Play Mode에서 실행했다. 최종 [결과](evidence/raw/ball-mvc-runtime-validation.json)는 `2026-09-08T17:05:38.0294770Z`, success=true, **19 assertions**다.

- 임시 BallView source와 두 개의 임시 PoolConfig만 사용해 prewarm → 대여 → Controller 정상 반환 → 같은 View/Model/Controller 재대여 → 이전 lease/세대 거절 → PoolLease 정상 반환을 확인했다.
- 활성 객체가 남은 Pool Dispose에서 Model·Controller·View 관찰이 모두 정리되는지 확인했다.
- 별도 활성 Ball의 GameObject를 먼저 파괴해 Unity `OnDestroy` 직후 Model·Controller·Observer가 정리되고, 나중 Pool Dispose가 lease를 무효화하며 중복 정리해도 안전한지 확인했다.
- 첫 컴파일에서 검증 메뉴의 `Object`가 `Framework.Object` namespace로 해석된 CS0118 한 건을 확인해 `UnityEngine.Object`로 명시한 뒤 재컴파일했다. 최종 실행 후 Console 오류는 0건이다.
- Play Mode 전환 뒤 Unity가 백그라운드에서 프레임을 진행하지 않아 창만 활성화했다. `runInBackground`, 시간 설정, 씬 값은 바꾸지 않았다. stdio bridge는 domain reload 때 포트를 바꿨지만 검사 결과와 종료 상태는 파일·Console·씬 조회로 다시 확인했다. [Editor 종료 기록](evidence/raw/ball-mvc-editor-final.json).

### 사용자 자산 보존과 남은 범위

검사 전후 Game 씬, Ball/Cube Prefab, Ball/Cube PoolConfig의 SHA256이 각각 동일하다. 저장된 Game 씬은 dirty=false·루트 4개였고 자동 저장·씬/Prefab 재생성은 하지 않았다. [보존 결과](evidence/raw/ball-mvc-preservation-check.json).

Ball 단위 종료 당시 Daniel이 만든 Ball Prefab은 MeshRenderer·SphereCollider·BallView, Ball PoolConfig는 Prefab 연결·Min 3·Max 7·200초이며 Game PoolContainer에 등록돼 있었다. Cube Prefab은 MeshRenderer·BoxCollider만 있고 Cube PoolConfig의 Prefab은 비어 있으며 목록에 등록되지 않았다. 이는 당시 다음 작업의 시작 상태이지 오류 판정이나 AI 수정 결과가 아니다.

Ball 단위 종료 당시 실제 Ball 발사·이동·충돌·Cannon 회전, Obstacle MVC, Unity Physics/직접 구현 Physics, 화면 MVP, SO/Addressables, High 설정 빌드·IL2CPP/AOT·기기 실행은 미실행이었다. 이 중 Obstacle MVC는 아래 후속 단위에서 완료했다. Ball의 19개는 객체 수명 격리 검사이며 실제 게임 플레이 검증이 아니다. 기존 공통 MVC 58개·Pool 66개·DI/UI Pause 25개 전체를 다시 실행한 결과도 아니다. 브랜치 전환·커밋·푸시는 하지 않았다.

## W-000-OBSTACLE-MVC-001 — Obstacle별 묶음 재사용

2026-09-09 KST. Main은 `InGame/Obstacle`의 빈 골격을 물리 비종속 대여 수명으로 연결했다. Luna Low는 Ball/Pool 계약과 최소 검사 범위를 읽었고, Terra Medium은 임시 검사 두 파일만 작성했다. Sol High의 읽기 전용 검토에서 생산 코드 결함은 찾지 못했지만 대여 준비 중 실패 롤백과 Unity 파괴 오류 로그가 검사에 잡히지 않는 두 사각지대를 발견했다. Main이 검사를 보완하고 다시 실행했다.

### 구현·실행 결과

- `ObstacleModel`: `IsRented`, 0이 아닌 `RentalEpoch`, 현재 세대 확인. HP·파괴·위치·속도·충돌 상태는 없음.
- `ObstacleController`: 현재 Model 세대와 PoolLease를 함께 확인하는 `TryReturn`. 반환·폐기 시 lease/세대 해제.
- `ObstacleView`: 생성 때 Model/Controller 한 번 조립, 대여 때 Model → Bind → Controller, 반환·풀 종료·Unity 파괴 때 Controller → Unbind → Model 순서의 idempotent 정리.
- 5개 스크립트 Unity 정적 검사 결과 오류·경고 0건. `Tools/Smesh Fest/Validation/Obstacle MVC Runtime`의 최종 [결과](evidence/raw/obstacle-mvc-runtime-validation.json)는 `2026-09-08T18:03:37.9402350Z`, success=true, **25 assertions**다.
- prewarm, Controller/PoolLease 정상 반환, 같은 View/Model/Controller 재대여, epoch 전진과 오래된 lease/Controller 거절, 활성 Pool Dispose, 계층 선파괴, 생성 후 미대여 파괴를 확인했다. 보완 뒤에는 default lease의 준비 실패가 Model·Controller·View를 완전히 되돌리는지와 세 파괴 경로가 Error/Exception/Assert 로그를 내지 않는지도 검사했다.
- Play Mode를 종료했고 Game 씬은 dirty=false·루트 4개, 컴파일/도메인 reload 대기 없음, 종료 후 Console 항목 0개였다. [Editor 종료 기록](evidence/raw/obstacle-mvc-editor-final.json).

### 보존과 한계

AI 검증은 임시 GameObject·메모리상 PoolConfig만 사용했고 Scene·Prefab·PoolConfig를 저장하지 않았다. Game.unity와 Ball/Cube PoolConfig는 시작·종료 해시가 같다. 작업 도중 Ball/Cube Prefab에 Rigidbody가 추가된 외부 저장 변경이 나타났으며 Main과 보조의 파일 소유 범위 밖이었다. 저자를 추정하거나 되돌리지 않고 사용자 소유 최신 상태로 보존했다. [보존 기록](evidence/raw/obstacle-mvc-preservation-check.json).

이 25개는 Obstacle의 대여/반환 수명 격리 검사다. Cube Prefab의 ObstacleView 연결, Cube PoolConfig와 Game 목록 연결, 실제 Rigidbody 초기화, HP·파괴·충돌·렌더·입력·게임 플레이는 확인하지 않았다. Ball 19개와 공통 MVC/Pool/DI 검사를 이번 최종 실행에서 함께 재실행한 것도 아니다. High 설정 Player 빌드·Android IL2CPP/AOT·기기 실행은 계속 미실행이다. 커밋·푸시·브랜치 전환도 하지 않았다.

## W-003-PHYSX-LIFECYCLE-001 — Rigidbody 권위와 격리 충돌 Probe

2026-09-09 KST / R-023. Daniel이 Ball과 Obstacle에 Rigidbody/Collider를 연결했고, 채용 공고의 PhysX 요구를 따라 첫 구현을 Unity PhysX로 진행하도록 결정했다. Main은 최신 씬·Prefab·PoolConfig와 코드를 읽고 사용자 Inspector 값 보존 기준을 잡았다. Luna Low는 Pool callback/활성화 순서를, Terra Medium은 격리 검증 파일을, Sol High는 물리·풀 파괴 수명을 읽기 전용으로 검토했다. ControlBox/MenuPresenter는 이 프로젝트 코드로 사용하지 않았다.

### 구현과 정적 확인

- BallController는 현재 Model epoch와 PoolLease가 모두 유효하고 아직 발사하지 않은 경우에만 finite·0이 아닌 초기 `linearVelocity`를 한 번 적용한다. 각속도를 지우고 WakeUp하며, 오래된/중복/kinematic 명령은 거절한다.
- Ball은 대여와 반환/폐기에서 선속도·각속도를 지우고 Sleep한다. Obstacle은 대여 때 지우고 WakeUp, 반환/폐기 때 지우고 Sleep한다. 위치·회전·속도는 Model에 복제하지 않아 Rigidbody가 유일한 런타임 물리 권위다.
- BallView/ObstacleView는 같은 루트의 Collider와 dynamic Rigidbody를 요구한다. 빠져 있거나 kinematic이면 묶음 생성 전에 실패하며, 코드는 `AddComponent` 또는 Inspector의 mass/gravity/constraints/collision detection/interpolation/damping 변경으로 숨기지 않는다.
- PoolLease는 Unity 파괴 객체를 즉시 무효로 본다. Sol High 1차 검토가 계층 선파괴 뒤 lease가 raw CLR 참조로 유효하던 결함을 찾았고, 2차 검토가 OnPoolRent/OnPoolReturn callback 중 파괴된 객체가 성공 또는 inactive로 확정될 수 있는 전이 사각지대를 찾았다. Main은 조회와 생성/대여/반환/return-pending 완료 경계를 보완하고 Pool Runtime 회귀를 추가했다. 최종 재검토에서는 합의 범위의 추가 correctness 결함을 찾지 못했다.
- PhysX Probe는 별도 `LocalPhysicsMode.Physics3D` 씬에서 임시 Ball/Obstacle과 메모리 PoolConfig를 만든다. kinematic source 거절, 초기 Sleep/Wake, 유효·무효 발사, `PhysicsScene.Simulate`의 `OnCollisionEnter`, Obstacle 변위, 정상 반환, 같은 body 재대여, 오래된 lease/epoch, 활성 Factory Dispose, 속도/Sleep과 Inspector 속성 보존을 검사한다. setup 실패·개별 cleanup·증거 쓰기 실패에도 임시 scene 정리를 시도한다.

Unity 6000.3.10f1에서 최종 일반 컴파일이 완료됐고 compiler error 0개다. 변경 스크립트 14개의 정적 진단도 warnings 0, errors 0이다. 컴파일 보완 이력은 누락된 Probe assertion helper/definite assignment, `UnityEngine.Object` namespace 명시이며 최종본에는 모두 반영됐다. Console의 경고 3개는 MCP bridge 포트 reload 재시도와 6401 fallback뿐이다. Game 씬은 Play Mode가 아닌 상태로 dirty=false·루트 4개다. [컴파일 원자료](evidence/raw/physx-lifecycle-editor-compile.json).

### 사용자 Play Mode 1차 실패와 활성화 순서 수정

Daniel이 2026-09-09T06:11:36Z에 실행한 첫 Probe는 assertion 10에서 `Rent did not leave the Ball asleep until launch or wake the reset Obstacle.`로 실패했다. `simulatedSteps=0`, 접촉과 변위도 0이므로 이는 충돌 실패가 아니라 **충돌 시뮬레이션 전에 발견된 대여 활성화 계약 실패**다. 당시 하나의 복합식이어서 Ball Sleep과 Obstacle Wake 중 어느 항이 실패했는지는 결과만으로 분리할 수 없다. [보존한 1차 실패](evidence/raw/physx-lifecycle-runtime-failed-20260909T061136Z.json).

코드 순서는 비활성 clone에서 `OnPoolRent` → Controller의 속도 초기화와 Sleep/Wake → `SetActive(true)`였다. 비활성 Rigidbody에 내린 Sleep/Wake 의도가 활성화 뒤에도 그대로 유지된다고 전제하지 않도록, BallView/ObstacleView의 `OnEnable`에서 현재 PoolLease·epoch를 확인한 뒤 Ball은 발사 전 Sleep, Obstacle은 Wake를 재적용했다. 씬 배치형 View처럼 Controller가 아직 없는 경우에는 아무 작업도 하지 않는다. Inspector 값은 변경하지 않는다.

검증기도 Ball Sleep, Ball 속도, Obstacle Wake, Obstacle 속도를 별도 assertion으로 나눴다. Sol High 재검토가 Inspector 보존 기준을 첫 대여 뒤에 잡던 사각지대를 발견해, 임시 원본 Rigidbody 값을 Factory 초기화 전에 저장하고 첫 대여 직후부터 비교하도록 보완했다. 최종 재검토에서 이 수정 범위의 추가 correctness 문제는 발견되지 않았다. 수정된 5개 스크립트 정적 진단 warning 0·error 0, 프로젝트 compiler error 0을 확인했다. 현재 Console의 reload 경고는 MCP 연결에만 해당하며 정리 후 항목 0개다. [수정 컴파일 근거](evidence/raw/physx-activation-fix-editor-compile.json).

### 수정본 사용자 실행 통과와 보존 범위

시작·종료 시 Game.unity, Ball/Cube Prefab, Ball/Cube PoolConfig SO의 SHA256은 각각 동일하다. 기존 Git dirty인 Game.unity는 Daniel이 23개 명시 블록에 Rigidbody/ObstacleView를 추가한 저장 변경이며 AI는 되돌리거나 다시 저장하지 않았다. live Unity 해석 결과는 ObstacleView 24개, Rigidbody 25개로 prefab-backed Obstacle와 Ball을 포함한다. [보존 원자료](evidence/raw/physx-lifecycle-preservation-check.json) · [시작 기준](evidence/raw/physx-lifecycle-baseline.json).

Daniel이 활성화 순서 수정본을 Play Mode에서 다시 실행했다. [불변 통과 스냅샷](evidence/raw/physx-lifecycle-runtime-passed-20260909T064144Z.json)은 success=true, **42 assertions, 15 simulated steps, collision contact 1회, Obstacle displacement 0.6153807**, initialBallSleeping=true, initialObstacleSleeping=false를 기록한다. 이로써 임시 local PhysicsScene의 대여·발사·접촉·반환·재대여·폐기 수명은 통과로 승격한다.

이 통과는 실제 Game Prefab의 질량·중력·배치·조작감, 현재 Game 씬 클릭 입력, HP/파괴/결과의 증거가 아니다. 새 Pool callback 파괴 회귀도 아직 재실행하지 않았다. 기존 Pool 66개·Ball MVC 19개·Obstacle MVC 25개 결과는 변경 전의 과거 근거다. High 설정 Player 빌드·IL2CPP/AOT·기기 실행도 미실행이다.

현재 Game 씬의 ObstacleView는 씬 배치 컴포넌트라 Pool의 `OnPoolCreated`가 자동 호출되지 않는다. native Rigidbody는 PhysX에 참여할 수 있지만 MVC Controller/Model은 아직 게임 흐름에서 초기화되지 않는다. 이후 별도 저장 변경으로 Cube PoolConfig의 prefab 참조는 연결됐지만 Game PoolContainer 목록은 아직 Ball만 가진다. 이 상태를 오류로 자동 수정하거나 씬을 풀 생성 구조로 바꾸지 않았다. W-001은 아래와 같이 현재 Blocks를 첫 고정 충돌 대상으로 유지했다.

## W-001-CLICK-LAUNCH-001 — 클릭·포물선 발사·Cannon 정렬

2026-09-09 KST. Daniel의 PhysX 수정본 통과 뒤 Main은 최신 Game 씬·Cannon 계층·Ball PoolConfig와 Input System 구성을 읽어 첫 실제 플레이 연결을 구현했다. 두 읽기 보조는 각각 Pool/DI/Ball API와 씬 카메라·Cannon 축·Blocks 배치를 확인했고, 제작 보조는 격리 Click Launch Runtime Probe 두 파일만 맡았다. Main은 생산 코드·씬 부분 연결·컴파일·정적 검토와 기록을 맡았다. 사용자 오브젝트를 재생성하는 Builder나 다른 프로젝트의 ControlBox/MenuPresenter는 사용하지 않았다.

### 구현 계약

- `WorldPointerInput`: WorldObjects에서 VContainer로 PoolFactory와 중앙 ILoopEvents를 주입받고 UpdateTick 한 곳에서 pointer press를 읽는다. safe area 밖 또는 GraphicRaycaster UI 위 입력을 거절한 뒤 `WorldTargetProjector`에 전달한다.
- `WorldTargetProjector`: Main Camera ray를 명시적으로 연결한 Blocks Transform 평면에 투영하고 Inspector 로컬 XY 경계 안만 허용한다. 현재 모든 월드 Collider가 Default layer여서 Ground/Cannon을 오인할 수 있는 broad Physics.Raycast는 사용하지 않는다.
- `BallisticLaunch`·`ShotDirector`: Physics.gravity 아래 도달 가능한 낮은 궤도의 초기속도를 계산한다. Cannon 회전으로 머즐이 이동하는 값을 보정하고 Ball Pool에서 대여한 뒤 현재 epoch에 `TryLaunch`한다. Rigidbody가 이후 운동과 충돌 권위를 계속 가진다. 고정 배열로 최대 MaxPool shot을 추적하고 4초 또는 y=-1 아래에서 현재 대여만 반환한다.
- [당시 계약] `CannonModel/Controller/View/Head`: 발사 초기속도 방향을 Model 명령으로 두고, View가 기존 Cannon 루트 yaw·Body pitch·Head local +Y 발사관을 정렬했다. 현재는 위 `W-001-FLIGHT-PHYSICS-003`의 world-Yaw-only가 대체한다. 머즐은 Head에서 local +Y 방향 0.16 world unit이며, Awake에서 한 번 조립하고 실행 중 하이어라키나 컴포넌트를 만들지 않는 경계는 유지한다.
- `GameLifetimeScope`: 같은 WorldObjects에 WorldPointerInput이 있을 때만 scene component로 등록한다. 기존 검증 fixture처럼 없는 씬도 유지한다.

### 정적 확인과 씬 보존

Unity 6000.3.10f1에서 compiler error 0개를 확인했다. Cannon 4개, Shot 4개, GameLifetimeScope, ClickLaunchRuntimeProbe, ClickLaunchValidation의 총 11개 스크립트를 개별 진단해 warning 0·error 0이었다. 첫 compile에서 Unity 6의 `PointerEventData.eventSystem`을 읽을 수 없는 오류가 있었고, 생성에 사용한 `EventSystem`을 별도 필드로 기억해 비교하도록 수정했다. [정적 원자료](evidence/raw/click-launch-static-validation.json).

씬은 작업 직전 tracked HEAD와 바이트 단위로 같고 dirty=false였다. AI의 최초 저장 변경은 기존 `Cannon`에 CannonView와 루트/Body/Head/offset 참조, 기존 `WorldObjects`에 WorldPointerInput과 Camera/Blocks/Cannon/Ball Pool 참조·목표 범위·발사 값만 추가한 **40 YAML 행**이었다. 그 뒤 2026-09-09T07:28:48Z 씬 저장에서 기존 비풀링 `GameObjects/Ball` Prefab 인스턴스 63행이 제거됐고, Daniel이 자신의 작업이라고 확인했다. 삭제 의도는 더 추정하지 않으며 사용자 소유 최신 상태를 복구하지 않는다. 이번 fixture 수정 중에는 씬 MeshRenderer 두 곳의 Cast Shadows 변경과 Cube PoolConfig Prefab 연결이라는 별도 저장 변경을 추가로 관측했으며 작성자 의도는 추정하지 않았다. 2026-09-09T07:54:49Z 확인 시 scene diff는 +42/-65, SHA `bcf6bdc...`, dirty=false·루트 4개였다. 이후 사용자 병행 변경이 계속될 수 있어 이 값은 시점 관측으로만 남긴다. test-only 수정은 Scene·Prefab·SO를 저장하지 않았다. [변경 관측](evidence/raw/click-launch-concurrent-scene-change-20260909T072848Z.json) · [사용자 귀속 확인](evidence/raw/click-launch-scene-ball-user-attribution-20260909T074216Z.json) · [fixture 수정·보존](evidence/raw/click-launch-projection-fixture-fix-static.json).

### 사용자 1차 런타임 실패와 fixture 수정

Daniel의 1차 `Tools/Smesh Fest/Validation/Click Launch Runtime` 실행은 중앙 화면점 투영까지 통과한 뒤 **assertion 5, simulatedSteps 0, collisionContacts 0**에서 중단됐다. 실패 문구는 `Projection accepted a point outside the configured world bounds.`다. 화면 우측점은 screen bounds 안이고, 임시 Obstacle의 scale까지 역변환한 로컬 X가 설정한 ±5 안이어서 생산 `WorldTargetProjector`가 이를 받아들인 동작은 맞았다. 따라서 Ball Pool·발사·Cannon 정렬·PhysX 접촉에는 아직 도달하지 않았다. [1차 실패 스냅샷](evidence/raw/click-launch-runtime-failed-20260909T074802Z.json).

Probe는 같은 우측 화면점을 먼저 넓은 calibration bounds로 투영하고 non-zero local X를 확인한 다음, 그 절반 폭의 좁은 local bounds에서 같은 점을 거절하도록 수정했다. 이 방식은 카메라 FOV나 Obstacle scale이 우측점을 임의의 ±5 경계 밖으로 보낼 것이라는 가정에 의존하지 않는다. 생산 `WorldTargetProjector`와 Game 씬은 수정하지 않았다. Unity 6000.3.10f1 compile 요청 후 idle, compiler error 0, 변경 Probe warning 0·error 0, diff check 통과를 확인했다. 수정본 Play Mode는 아직 재실행하지 않았다. [fixture 수정 정적 근거](evidence/raw/click-launch-projection-fixture-fix-static.json).

수정본 격리 Probe를 먼저 다시 실행한 뒤 실제 Game 씬에서 Block 영역을 클릭해 Pool Ball 발사, Cannon 정렬, 첫 충돌과 속도·궤적·머즐 체감을 확인해야 한다. Daniel이 제거한 기존 비풀링 scene Ball은 현재 live 이름 검색 결과 0개다. W-001은 Ball PoolConfig의 Prefab을 대여하므로 그 인스턴스에 의존하지 않는다. 현재 Blocks는 native PhysX 충돌만 제공하며 HP·파괴·결과·재도전 수명은 다음 단위다. High 설정 Player 빌드·IL2CPP/AOT·기기 실행도 미실행이다.

## W-001-GROUND-TARGETABILITY-LAYER-001 — Obstacle Layer와 Ground 제외

2026-09-09 KST. 사용자 승인 범위대로 `Obstacle`을 사용자 Layer 8에 만들고 `PhysXConfig`의 포인터 mask를 이 한 Layer(`256`)로 제한했다. 현재 Game 씬의 ObstacleView 24개는 23개 명시 오브젝트와 prefab-backed 1개 override 모두 Layer 8이며, Ground는 Default Layer를 유지하고 같은 Collider 오브젝트에 `GroundSurface`를 명시적으로 연결했다.

`ObstacleView`는 씬 배치 상태에서는 targetable로 시작하고, 자신과 충돌한 상대 Collider에 `GroundSurface`가 직접 있을 때 해당 Obstacle만 비대상화한다. 풀 객체는 대여 때 복구되고 반환·폐기 때 해제된다. 이름·태그·부모 탐색이나 fallback은 없다. 기존 Ball 중력 코드는 이 작업 단위에서 건드리지 않았다.

Unity 6000.3.10f1 스크립트 refresh와 domain reload 뒤 `GroundSurface` 타입 및 Ground의 live 컴포넌트, Obstacle Layer 24개와 ObstacleView 24개를 확인했다. Console error는 0개이고 관련 스크립트 5개 진단은 warning 0·error 0이다. 작업 전 scene SHA는 `80b21c3...`, 후는 `b1eb93e...`이며 기존 사용자·병행 변경을 보존했다. Play Mode에서 “서 있는 Obstacle은 발사 가능, Ground 접촉 뒤 그 Obstacle만 발사 불가”는 아직 사용자 재확인 전이다. [정적·live 근거](evidence/raw/ground-targetability-layer-static-20260909T110202Z.json).

## W-001-BALL-GRAVITY-TRANSITION-001 — [대체됨] Straight 첫 충돌 중력 전환

이 절은 당시 구현 기록이다. 아래 `W-001-BALL-GRAVITY-TRANSITION-002`를 거쳐 현재는 문서 상단의 `W-001-FLIGHT-PHYSICS-003` **직접 Obstacle 충돌 또는 world-Z 경계 방식**으로 대체됐다.

Daniel이 실제 Game에서 Obstacle hit point보다 Ball이 훨씬 아래로 지나간다고 관측했다. 당시 `BallConfig`는 `Straight`, Ball Prefab은 `useGravity=true`였고, 계산된 초기속도는 hit point를 향했지만 발사 뒤 중력을 끄는 연결이 없었다. 따라서 Y 좌표 변환보다 누락된 비행 상태 전환이 직접 원인이었다.

`BallController.TryLaunch`는 이제 trajectory mode를 명시적으로 받아 Straight면 중력을 끄고 Curve면 고정 비행시간 공식의 전제대로 처음부터 중력을 켠다. `BallView.OnCollisionEnter`가 첫 물리 접촉 뒤 중력을 켠다. 반환·재대여·폐기에서는 Prefab에 저작된 원래 `useGravity`를 복구하며, 알 수 없는 mode는 거절하고 이전 2-인자 overload는 남기지 않았다. Scene·Prefab·Config asset은 이 수정으로 바꾸지 않았다.

생성 csproj 정적 빌드는 error 0이며 26개 warning은 Unity 직렬화 필드에 대한 CS0649다. 변경 6개 스크립트의 Unity 진단은 warning 0·error 0, refresh 뒤 Console error 0, live reflection에서 `TryLaunch` overload 1개를 확인했다. 수정 후 Play Mode 비행과 첫 충돌 중력 전환은 아직 실행하지 않았다. [정적 근거](evidence/raw/ball-gravity-transition-static-20260909T110202Z.json).

## W-001-BALL-GRAVITY-TRANSITION-002 — 궤적별 중력과 Obstacle 직접 충돌

이 절도 당시 구현 기록이다. 현재는 직접 같은 GameObject `ObstacleView` 충돌과 strict world-Z fallback을 함께 쓰는 문서 상단의 `W-001-FLIGHT-PHYSICS-003`이 대체한다.

Daniel이 Cannon Collider를 제거하고 Ball Prefab의 `Use Gravity=false`에서 Straight가 원하는 목표점으로 이동한다고 직접 확인했다. 이 관측으로 Screen→World Y 변환이 아니라 비행 중 중력이 원인임을 다시 확인했다. 작업 시작 시 최신 소스에는 앞선 기록의 궤적별 중력 전환이 남아 있지 않았지만, 현재 worktree가 미커밋 상태라 변경 주체는 추정하지 않았다.

최신 코드에 `TryLaunch(epoch, velocity, trajectoryMode)` 단일 API를 연결했다. Straight는 발사 시 `useGravity=false`, Curve는 고정 비행시간 속도 계산에 사용한 동일 중력을 받도록 발사 시 `true`다. Ball은 충돌 상대 Collider와 같은 GameObject에 `ObstacleView`가 직접 있을 때만 중력을 켠다. tag·이름·부모/자식 탐색 fallback은 없다. 반환·재대여·폐기 때는 Prefab의 저작 중력값을 복구한다. Daniel이 제거한 Cannon Collider와 Ball Prefab의 현재 `Use Gravity=false`, Scene·Config는 수정하지 않았다.

Unity 6000.3.10f1 스크립트 refresh 뒤 Console error 0, 변경 6개 스크립트 진단 warning 0·error 0, live reflection의 `TryLaunch` overload 1개를 확인했다. 생성 solution 정적 빌드는 error 0, Unity/MCP 참조 버전 충돌 warning 4개다. Click Launch Probe는 Straight 충돌 전 무중력, Obstacle 충돌 후 중력, PhysX Probe는 재대여 뒤 Curve 발사부터 중력과 반환 시 저작값 복구를 검사하도록 갱신했다. 새 코드의 Play Mode Probe와 실제 Straight/Curve 도착은 Daniel 실행 전이므로 정적 완료로만 기록한다. [정적 근거](evidence/raw/ball-gravity-mode-correction-static-20260909T114000Z.json).

## W-002-LEVEL-EDITOR-001 — Level Editor MVP 검토 상태

Terra Medium builder 구현과 Sol High 검토 뒤 `LevelConfig`은 순서/transform/PoolConfig 및 PoolConfig별 필요 개수≤MaxPool을 검증한다. 창은 직접 child `ObstacleView`만 Capture하고 명시 Bake에서만 SO를 저장하며, 선택 변경 시 미리보기를 폐기한다. `LevelSpawner`는 명시 `TrySpawn`과 `ReturnAll`만 제공하고 대여 실패는 보유 lease를 원자적으로 반환한다.

신규 스크립트 4개 Unity 정적 진단은 warning/error 0, Console error 0이다. `Tools/Smesh Fest/Validation/Level Editor and Spawn`과 실제 Bake는 **미실행**이다. 현재 24 Blocks는 Cube MaxPool=8·미등록 catalog 상태라 Bake/spawn 거절이 기대 동작이며, Scene/Prefab/기존 config/Blocks 제거는 수행하지 않았다.

## W-002-LEVEL-EDITOR-002 — Level1 Bake와 Game 통합 정적 확인

Daniel이 Cube PoolConfig `MaxPool=100`으로 변경한 뒤 `Assets/Project/Level/Level1.asset`을 Bake했고 Cube 24개 항목과 local TRS가 저장돼 있다. Game 씬에는 authored `Blocks`의 sibling `RuntimeBlocks`가 있고 local position은 `(0, 0.29, 0)`이며 authored Blocks와 local TRS가 일치한다. `WorldObjects`에는 `LevelSpawner`·`LevelSession`, `PoolContainer`에는 Ball·Cube가 연결돼 있다.

`GameFlow`는 `LevelSession.TryStart` 성공 뒤에만 loop를 시작한다. authored Blocks는 runtime에서 비활성화되고 시작 실패 또는 `ReturnAll` 시 복원된다. fallback/parallel pool은 없으며 `LevelSession`은 nested/ancestor root를 거절한다. 5개 스크립트 정적 진단 warning/error 0, Console 0이다. Level Editor validation 메뉴와 Play Mode는 아직 실행하지 않아 런타임은 미검증이다.

첫 validation 실행은 `OnPoolRent` 뒤 일반 `OnEnable`까지 Edit Mode preview scene에서 관측하려 해 false negative가 발생했다. 사용자 관측상 RuntimeBlocks 생성은 성공했다. 생산 코드는 유지하고 fixture를 `OnPoolRent` 시 inactive+world P/R, 완료 시 active+최종 local P/R/S 검증으로 교정했다. 기존 Play Mode Pool probe가 rent-before-enable 순서를 담당하며, 교정된 메뉴 재실행은 대기 중이다.

## W-004-GROUND-FADE-RETURN-001 — 정적 검증

Terra Medium 구현과 Sol High 독립 재검토를 사용했다. 첫 검토에서 material-index property block이 renderer override를 가릴 수 있는 경계, hierarchy-first 파괴 시 Loop 구독, 기존 runtime fixture의 새 필수 의존성 누락을 찾아 보완했고 최종 P0/P1은 없다.

Unity 6000.3.10f1 강제 refresh·compile 뒤 `GroundFadeReturn`, 공통 validation fixture, `ObstacleMvcRuntimeProbe` 진단은 warning/error 0이며 Console error 0이다. 생성된 runtime/editor C# 프로젝트 빌드는 각각 error 0이고 MCPForUnity 참조의 기존 `System.Net.Http`·`System.IO.Compression` 버전 충돌 warning 4개가 남는다. live Game 씬은 dirty=false이며 `WorldObjects/GameLifetimeScope`가 `Assets/Project/Config/GroundFadeConfig.asset`을 참조한다. Ball/Cube Prefab 모두 `GroundFadeReturn`을 한 개씩 가진다.

두 Fade Material은 임포트된 URP/Lit에서 Transparent surface, SrcAlpha/OneMinusSrcAlpha, ZWrite off, preserve specular off를 확인했다. 원본 opaque Material은 수정하지 않았다. 이번 단계는 Play Mode를 실행하지 않았으므로 Ground 접촉 뒤 1초 대기·1초 Fade·exactly-once Return, 재대여 복구와 겹친 Cube의 투명 정렬은 아직 런타임 근거가 없다.

### Ground Fade 시각 교정

Daniel이 Ball/Cube Prefab Renderer를 각 GroundFade Material로 직접 연결한 현재 값을 권위로 삼았다. 두 Prefab의 Renderer와 `fadeMaterial`이 같은 자산을 참조함을 확인했고, 두 Material만 URP/Lit Transparent alpha 상태로 변경하되 기존 그림자가 Fade 시작에 끊기지 않도록 ShadowCaster pass는 유지했다. `GroundFadeReturn`의 런타임 Material 교체와 shadow 상태 저장·변경·복구를 제거하여 Fade 동안 `_BaseColor.a` PropertyBlock만 변한다. Unity 정적 진단과 Console error는 0이며 실제 시각 결과는 Play Mode 확인 대기다.

사용자 런타임 관측에서 alpha 값은 감소하지만 화면은 opaque로 유지되고 alpha 0의 Pool 반환 때만 사라졌다. `RestoreVisual()`이 원래 비어 있던 material-index-0 범위에도 빈 `MaterialPropertyBlock`을 설정해 renderer-level alpha block보다 우선한 것이 원인이었다. 빈 원본 범위는 `null`로 제거했다. 이어진 Play Mode 확인에서 그림자는 여전히 없었고 두 Material에 `SHADOWCASTER` 비활성화가 다시 직렬화된 것을 확인했다. URP 17.3 `BaseShaderGUI.UpdateMaterialSurfaceOptions()`가 `_CastShadows` 속성이 없는 built-in Lit의 Transparent 상태를 검사할 때 ShadowCaster를 강제로 끄므로, YAML 제거 방식은 유지될 수 없었다. 현재는 `GroundFadeReturn.OnPoolCreated()`가 공유 Fade Material의 `ShadowCaster` pass를 활성화하고 결과를 검증한다. Prefab Renderer의 Cast Shadows/Receive Shadows, Directional Light, Ground Receive Shadows, URP shadow 지원은 모두 켜져 있다. 정적 진단·Console error는 0이고 실제 그림자 결과는 Play Mode 재확인 대기다.

## V-FINAL-001 — 프로젝트 완료 선언과 WebGL High 빌드

2026-09-10 KST Daniel이 프로젝트 완료를 선언했다. 이 결정에 맞춰 현재 코드·Config·기존 검증 기록을 발표용 [Project Overview](../Presentation/PROJECT_OVERVIEW.html)로 정리했다.

- 실행자·도구: Daniel의 Unity Editor 6000.3.10f1 WebGL Player Build.
- 설정: `ProjectSettings.asset`의 WebGL `managedStrippingLevel: 3`(High).
- 관측 결과: Editor 로그에 `Build completed with a result of 'Succeeded' in 276 seconds`가 기록됐고, 실행 구간은 2026-09-10 02:31:24–02:36:00 KST다.
- 산출물: `_Build/WebBuild/index.html`과 Brotli 압축된 data/framework/wasm 파일이 존재한다.
- 분류: **Player 빌드 실행 확인**. 단계별 Play Mode assertion은 각 작업 시점의 근거이며, 이번 문서화에서 최신 전체 Probe 묶음이나 별도 기기 실행·성능 측정을 다시 수행하지 않았다.

## W-000-MVC-CONTROLLER-001 — Controller 소유 MVC 정적 검증

2026-09-10 KST / R-024. Controller만 구체 View와 Model을 알고, View·Model은 Controller 및 서로를 참조하지 않도록 공통 MVC와 Ball·Obstacle·Cannon 경로를 교체했다. `ObView<TModel>`·View-owned Model/Controller·Bind/Unbind·recreate-on-rent 같은 이전 API와 호환 fallback은 남기지 않았다.

- `ObController<TView,TModel>`가 Model 관찰, View 갱신, 활성/비활성/파괴 정리를 소유한다. 활성화 중 초기 갱신이 실패하면 Model 및 View 생명주기 구독을 되돌린다.
- `PoolFactory`는 clone 주입 직후 `IPoolObjectComposer`를 호출한다. `WorldObjectControllerRegistry`가 `OnPoolCreated` 전에 Ball/Obstacle Model+Controller를 한 번 조립하며, Controller 없는 생산 View의 대여·반환은 즉시 실패한다.
- Ball/Obstacle Controller가 Config 적용, lease/epoch, Rigidbody와 충돌 상태를 소유한다. Registry가 Pool보다 먼저 종료될 때 활성 lease를 먼저 반환하고, 이후 controllerless 재대여는 quarantine된다.
- Cannon 조준 렌더가 실패하면 Model 방향을 이전 값으로 복구한다. 같은 실패 방향 재시도가 Model의 동일값 단축 경로로 렌더를 우회하지 못하도록 Click Launch Probe에 회귀 조건을 추가했다.
- ShotDirector·WorldPointerInput·ObstacleTargetRaycaster는 View 역참조 대신 Registry에서 Controller를 조회한다.

정적 확인은 `dotnet build Smesh-Fest-PhysX.sln --no-restore` 성공, error 0, 기존 MCPForUnity의 `System.Net.Http`·`System.IO.Compression` 참조 충돌 warning 4다. Unity 6000.3.10f1 Editor 로그에서도 2026-09-10 04:36 KST `Tundra build success`와 domain reload 완료를 확인했다. View/Model 역참조와 `ObViewOfT`, `ObView<T>`, `OwnedModel`, `View.Controller`, Bind/Unbind, `RecreateOnRent`, `#if false` 검색 결과는 0건이다. 이번 변경 파일의 diff whitespace 검사도 통과했다. 전체 worktree에는 이번 범위 밖의 기존 trailing whitespace가 남아 있어 이를 수정하거나 완료 근거에 포함하지 않았다.

Sol High 독립 생산 코드 검토에서 초기 생성 구독 누수, Registry 선행 종료, Cannon Model/View 불일치를 보완한 뒤 남은 P0/P1 correctness·lifecycle 결함은 없었다. Terra Medium의 첫 Probe 이식은 비활성 legacy 코드와 축약된 커버리지를 남겨 채택하지 않았고, Sol High가 원래 검증 범위를 Controller-owned API로 다시 이식했다. 새 성공 예정 카운터는 MVC 62, PhysX 66, Click Launch 39 assertions이며 **이번 작업에서는 Play Mode를 실행하지 않았으므로 통과 결과가 아니다**.

AI는 Scene·Prefab·Material·Config 자산을 수정하거나 재생성하지 않았다. 다음 확인은 Daniel이 `MVC Runtime → Ball MVC Runtime → Obstacle MVC Runtime → Pool Runtime → PhysX Lifecycle Runtime → Click Launch Runtime → Level Editor and Spawn` 순서로 실행하고, 마지막으로 실제 Game에서 `Obstacle 클릭 → Cannon Yaw → Muzzle 발사 → 충돌/중력 → Ground Fade → Pool 반환`을 확인하는 것이다. R-024 정정본의 WebGL High 빌드도 아직 재실행하지 않았다. [정적 근거](evidence/raw/mvc-controller-ownership-static-20260910.json).

## W-000-POOL-DESTROYING-DETACH-001 — 계층 선파괴 오류 수정

`ObView.OnDestroy → Controller.Dispose → PoolLease.Return → Transform.SetParent` 순서 때문에 Unity가 이미 파괴 중인 Ball/Obstacle을 Pool root로 재부모화하던 오류를 수정했다. `ObView`가 파괴 상태를 노출하고, 이 상태의 Controller는 정상 반환 대신 현재 Pool entry를 제거하고 lease version을 무효화한다. Unity가 실제 GameObject 파괴를 소유하므로 `OnPoolReturn`, 재부모화, 중복 Destroy는 실행하지 않는다. 정상 Pool 반환과 stale lease 거절 계약은 유지되며 fallback은 없다.

Ball/Obstacle 계층 선파괴 fixture에는 오류 로그 부재와 Pool/Registry 잔여 항목 0 조건을 추가했다. solution 정적 빌드는 error 0, 기존 MCPForUnity warning 4이며 독립 검토의 P0/P1은 없다. Play Mode/Probe는 이번 수정에서 실행하지 않았고, Scene·Prefab·Material·Config도 수정하지 않았다. [정적 근거](evidence/raw/pool-destroying-parent-fix-static-20260910.json).

발표용 [Project Overview](../Presentation/PROJECT_OVERVIEW.html)는 정상 반환과 계층 선파괴를 별도 카드로 분리하고, 이전 High 설정 WebGL 빌드·과거 Play Mode 기록과 최신 정적 수정의 검증 경계를 구분하도록 다시 제작했다. HTML parse, ID 중복, 외부 의존성, 필수 섹션, 데스크톱 브라우저 렌더는 통과했다. 이는 Unity Play Mode 재실행 증거가 아니다. [문서 검증](evidence/raw/presentation-overview-pool-fix-validation-20260910.json).

면접용 어조 검토에서는 설명형 문장을 모두 정중한 발표체로 바꾸고, 제목·도식·표 레이블만 명사형으로 유지했다. 평서체 종결 0건, HTML parse·데스크톱 렌더·30초 발표문 줄바꿈을 다시 확인했다. [어조 검증](evidence/raw/presentation-overview-interviewer-tone-20260910.json).

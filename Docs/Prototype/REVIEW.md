# 확인·검증 기록

초기 조사일: 2026-09-08. 각 절은 해당 시점의 기록이다. 최신 W-000-BALL-MVC-001은 Ball별 묶음 재사용의 Play Mode 19개 검사 결과다. 앞선 공통 MVC 58개·Pool 66개·DI/UI Pause 25개와 조건을 구분하며 High 설정 빌드 검증은 계속 미실행이다. 초기 상태 스냅샷은 [startup-baseline.json](evidence/raw/startup-baseline.json).

| 검증 ID / 관련 요구·작업 | 실제 확인 방법·담당 | 관측 결과 | 상태·한계 |
|---|---|---|---|
| V-001 / R-001, SF-START-001 | Main: Git 상태·HEAD·경로 | 기존 프로젝트, 브랜치 `SciptSkeleton`, HEAD `f63c05e`. IDE 파일 수정과 다수 미추적 Scripts 존재. | 통과: 현재 디스크 상태 조사. 사용자 기여량·완료율은 추정하지 않음. |
| V-002 / R-001, SF-START-001 | Main: ProjectVersion·manifest·렌더 설정·저장 Game 씬 | Unity 6000.3.10f1, URP 17.3.0, Luna 없음. Game 씬에 카메라·조명·Volume·EventSystem, 빈 GameObjects/UI(HUD) 영역. | 일부 확인: 직렬화 파일 읽기. Editor 미저장 상태·실제 렌더 미확인. |
| V-003 / R-005, SF-START-001 | Main: 현재 작업의 실행 메타데이터와 배정 도구 | Main `gpt-6-astra`·`ultra` 실제 기록 확인. 보조 `gpt-5.6-terra`·`medium`, `fork_turns: none` 명시 호출 수락·결과 반환. | 통과: Main 설정 및 보조 명시 배정. 보조의 별도 실행 모델 확인값·토큰·비용은 미제공. Docs 템플릿은 비활성 그대로이며 프로젝트 `.codex/config.toml` 없음. |
| V-004 / R-002, SF-START-001-CODE | 보조: Assets/Scripts의 실제 C# 읽기 | GameFlow는 MonoBehaviour와 빈 Awake. 나머지 Ball/Obstacle·Object·Screen·Pool·Navigator·UpdateLoop는 빈 선언. A안 기능과 물리 비교 코드 없음. | 통과: 코드 조사. Unity 컴파일/실행 미검증. |
| V-101 / W-001 | Unity 현재 소스 컴파일 | 실행하지 않음. 기존 Editor.log에는 옛 ObjectView/TView 파일명의 CS8773 이력이 있음. | 미실행: 과거 오류를 현재 실패로 단정하지 않음. |
| V-102 / W-001 | Unity Play Mode에서 조준·발사·충돌 | 첫 목표·역할 답변 후 구현·확인 예정 | 미실행 |
| V-103 / W-003 | 물리 비교 측정 | 비교 대상·조건이 미정 | 미실행 |

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

Daniel이 만든 현재 Ball Prefab은 MeshRenderer·SphereCollider·BallView, Ball PoolConfig는 Prefab 연결·Min 3·Max 7·200초이며 Game PoolContainer에 등록돼 있다. Cube Prefab은 MeshRenderer·BoxCollider만 있고 Cube PoolConfig의 Prefab은 비어 있으며 목록에 등록되지 않았다. 이는 다음 작업의 시작 상태이지 오류 판정이나 AI 수정 결과가 아니다.

실제 Ball 발사·이동·충돌·Cannon 회전, Obstacle MVC, Unity Physics/직접 구현 Physics, 화면 MVP, SO/Addressables, High 설정 빌드·IL2CPP/AOT·기기 실행은 미실행이다. 이번 19개는 객체 수명 격리 검사이며 실제 게임 플레이 검증이 아니다. 기존 공통 MVC 58개·Pool 66개·DI/UI Pause 25개 전체를 다시 실행한 결과도 아니다. 브랜치 전환·커밋·푸시는 하지 않았다.

# 프로젝트 인계

## 목표와 현재 단계

기존 /Volumes/Dock_SSD/Projects/Smesh-Fest-PhysX에서 선택한 A안을 제작한다. **전체 아키텍처를 먼저 마련한 뒤 플레이 기능을 구현한다.** [ARCHITECTURE](Prototype/ARCHITECTURE.md)의 10개 기준은 사용자 확정 원본이며 채택 여부를 다시 묻지 않는다.

최근 완료 W-000-MVC-REFERENCE-001(2026-09-09 KST): Daniel이 제공한 ProjectTemplate MVC를 실제 소스로 비교하고 R-019의 간소화·단계적 적용 기준을 기록했다. **현재 MVC 모델 연결은 골격이며, 다음 단위는 최소 Model 변경 → View 갱신 → 구독 해제 연결이다.** 참고 전체 복제나 모듈 부팅 체계 이식으로 확대하지 않는다. 이 단위는 문서·조사만 수행했다.

직전 완료 W-000-POOL-001: R-015~R-018을 반영한 Pool 기반·씬 DI 연결을 구현했고 실제 Play Mode 66개 검사와 기존 DI/UI Pause 25개 검사를 통과했다. ObView는 독립 MonoBehaviour이며 풀링 View만 IPoolable을 구현한다. O-006~O-008은 모두 해결했으므로 재질문하지 않는다. 이 검사 결과를 MVC 연결 검증으로 확대하지 않는다.

2026-09-08 최근 완료 W-000-DI-PAUSE-001(병행 편집 보존 후 재검증 포함): VContainer 주입·게임 시간·UI Pause를 현재 Game 씬에 연결하고 실제 Play Mode 검사 25개를 통과했다. 전체 아키텍처는 진행 중이다. 브랜치 SciptSkeleton, 사용자 코드·씬·Material·IDE 변경과 미추적 파일이 남아 있다. 커밋·푸시는 하지 않았다.

## 확정 결정과 미결정 구분

- R-011: **별도 조준 없이 Block 영역 클릭 → 해당 위치로 Ball 발사**, **Cannon은 Ball 진행 방향으로 회전**. [DESIGN](Prototype/DESIGN.md)이 조작 원본이다. 아키텍처 선행 순서는 유지한다.
- R-013: **설치된 VContainer 1.19.0 사용**, **UI에 별도 시간 관리자 없음**, **UI가 열린 동안 게임 Pause**. O-004·O-005는 답변받았으므로 재질문하지 않는다.
- UI별 Pause 소유자를 구분하고 마지막 요청이 해제되면 요청 배속을 복원한다. Stop 상태에서 UI 닫힘이 게임을 강제로 시작하지 않는다. 게임 물리·연출은 scaled time, fixedDeltaTime은 기존 값 유지다.
- R-015·R-016: R-018의 독립 ObView + 선택적 IPoolable, PoolConfig SO·PoolContainer 목록, Factory 사전 생성, 이벤트 구독형 MVC 묶음, 생성/대여와 반환에서 초기화. Model·Controller 재사용은 객체별 상태에 따라 정한다. 게임 씬에서 로비로 나갈 때 풀을 정리하고 미사용 시간도 PoolConfig에서 설정한다(초기 기본 300초). 확정 원본은 ARCHITECTURE이며 재질문하지 않는다.
- DI 조립 위치는 WorldObjects의 GameLifetimeScope 하나다. 여러 게임 scope가 Unity 전역 시간을 함께 소유하는 구조는 설계하지 않았다. Pool/Factory·MVC/MVP·SO·Addressables 수명 세부 계약은 후속이다.
- R-019: ProjectTemplate MVC를 그대로 복제하지 않고 현재 필요한 책임부터 간소화한다. 참고의 Model 관찰·View 갱신을 기존 Observable 위에 연결하는 기준은 [ARCHITECTURE](Prototype/ARCHITECTURE.md)에 있다. 참고의 BaseView는 PoolableView를 상속하지만 현재 프로젝트는 R-018을 우선한다.

## 누적 완료와 실행 근거

| 단위 | 확인된 결과 |
|---|---|
| Observable | 순수 C# 이벤트 수명 10개 검사 통과. 고정 구독자 1개·100회 워밍업 뒤 Publish 1,000회 관리 할당 0바이트 |
| LoopDispatcher / ILoopEvents | 시작·중단·재시작·Dispose·3phase·변경/예외 경계 Editor 검사 통과. 100 cycle 워밍업 뒤 1,000 cycle 총 3,000 Tick 관리 할당 0바이트 |
| GameClock / IGamePause | 중첩·중복·참조 owner·배율·Stop·폐기 Editor 검사 통과. 준비된 1,000 cycle 상태 관리 할당 0바이트 |
| 실제 DI·Pause 연결 | GameFlow가 주입으로 시작한 현재 씬에서 Play Mode assertion 25개 통과. Loop/Flow enable 수명, UI 중첩·동기 닫힘·파괴, Loop/물리/scaled Tween/scaled 파티클 정지·재개, 컨테이너 정리 후 timeScale=1 복원 |

명시적 Editor 메뉴 검사와 Play Mode coroutine 실행 근거이며 NUnit 결과가 아니다. 전체 게임 Zero Alloc이나 과거 대비 성능 개선의 증거가 아니다. 최신 [GameClock 결과](Prototype/evidence/raw/game-clock-validation.json), [Play Mode 결과](Prototype/evidence/raw/di-pause-playmode.json), [Editor 종료 상태](Prototype/evidence/raw/di-pause-editor-revalidated.json), [REVIEW](Prototype/REVIEW.md)에 조건과 실패/보완 기록이 있다.

첫 실행의 보존 확인 시 시작 기준 111개 중 108개가 동일했다. 변경은 기존 GameFlow·UpdateLoop와 씬의 scope 추가뿐이다. 기존 씬 컴포넌트·Transform·수동값은 모두 동일, Play 종료 뒤 dirty=false·root 3개다. [보존 검사](Prototype/evidence/raw/di-pause-preservation-check.json), [씬 변경](Prototype/evidence/raw/di-pause-scene-change.diff). 씬 복구본은 Docs/Prototype/evidence/backups/di-pause-20260908에 있다. 사용자 Observable 스타일과 Cannon 골격을 되돌리지 않았다.


최신 재검증 뒤 전체 기준에서는 104개가 동일하고, Assets/Scripts/InGame/UI/Navigator/ScreenNavi.cs, Assets/Scripts/InGame/UI/Navigator/ScreenNavi.cs.meta, Assets/Scripts/InGame/UI/Navigator.meta의 병행 삭제/이동을 관찰했다. AI가 삭제하거나 되돌리지 않았다. LoopDispatcher의 표현 정리도 보존하고 Editor Loop 검사를 통과했다. DI·Pause 검증은 기록된 실행 시점 기준이며 병행 편집은 이후에도 계속될 수 있다. [최신 소스 확인](Prototype/evidence/raw/di-pause-post-edit-validation.json).

## 연결 방법과 사용자 소유 작업

- WorldObjects에는 기존 GameFlow·UpdateLoop와 새 GameLifetimeScope가 붙어 있다. scope의 uiRoot는 기존 UI(HUD)다. 씬 minScale=0/maxScale=2와 기존 GameFlow 로그 초기화를 보존했다.
- 실제 여닫는 화면 루트에 ScreenPauseScope를 붙이면 주입된 상태의 OnEnable/OnDisable/OnDestroy가 게임 Pause 소유권을 관리한다. 기존 UI(HUD)는 비어 있는 상시 Canvas이므로 이 컴포넌트를 자동 부착하지 않았다. 동적 UI는 이후 Factory에서 주입하고, CanvasGroup만 숨기는 경우 Presenter의 열림/닫힘 수명에 연결해야 한다.
- Daniel은 하이어라키·카메라·씬·UI 배치와 조작감, 기존 코드 의도를 맡는다. UI 전체를 지금 새로 만들어야 하는 상태는 아니다. AI는 합의된 기반 코드·주입·수명 검증·자동 문서화를 맡았다.
- 현재 GameObjects 아래 Ground·Table·Blocks(24개)·Ball·Cannon이 있다. Ball에 Rigidbody를 추가하거나 클릭 발사/회전 기능을 구현하지 않았다. Object MVC·Screen MVP·Pool/Factory·Cannon·Ball/Obstacle은 후속 연결 단계다.

## 남은 작업과 다음 한 단계

다음 작은 목표는 **Model 상태 변경 → View 갱신 → 반환/해제 후 통지 없음 → 재연결 시 중복 없음**이다. W-000-MVC-001의 범위·Daniel/AI 분담·검증 계획은 [PLAN](Prototype/PLAN.md)에 있다. 현재 ObModel·ObController는 빈 클래스이고 ObView는 모델 연결 없는 MonoBehaviour이며 Ball의 MVC 조립도 미구현이다. 확정된 패턴을 다시 인터뷰하지 않고 R-019의 최소 연결부터 이어간다.

이미 마련한 Pool 기반은 유지한다. R-018에 따라 BallView는 ObView, IPoolable이며 MVC 조립/정리는 객체별 IPoolLifecycle 파츠와 훅으로 연결할 계획이다. Config의 MinPool은 사전 생성 수, MaxPool은 활성+비활성 총 상한, ReturnDelaySeconds는 초 단위(기본 300, 0은 자동 정리 끄기)다.

Pool/PoolFactory/Config/Container·IPoolable·PoolLifecycleRunner·사용 번호를 가진 PoolLease를 구현하고 수명 실패·재진입을 검증했다. PoolFactory는 비활성 부모 아래 복제 후 VContainer InjectGameObject를 호출하며 원본 Prefab 활성값을 토글하지 않는다. 객체별 정리 파츠는 자신이 만든 구독·Tween·파티클·비동기 작업을 반환/폐기에서 정리한다. Factory 하나의 UniTask 유지보수 루프는 UI Pause 중에도 실제 경과 시간으로 검사하고 Dispose에서 취소한다.

R-017/O-008 답변: 비활성 재고 정리 시 MinPool을 남긴다. 실제 Factory는 retainMinimum=true로 GameLifetimeScope에 등록한다. 기존 GameObjects/PoolContainer에 컴포넌트와 scope 참조만 부분 추가하며 기존 객체/수동값은 유지한다. 아직 실제 Ball prefab/PoolConfig 자산을 생성하지 않았다. Daniel은 실제 Prefab·설정·배치를 맡고 AI는 연결 코드를 준비한다.

그 뒤 객체별 MVC/MVP·SO·Addressables 수명을 연결하며 High 보존도 확장한다. UI Pause·DI·MVC 채택 여부는 다시 묻지 않는다. 아키텍처 이후 A안 플레이 단위를 구현한다. 철회한 W-001-AI 발사 선행 시작 기록은 실제 구현 성과가 아니다.

High: 세 주입 메서드에 Unity Preserve, GameClock·LoopDispatcher·UnityGameTime에 명시적 생성 factory를 적용했다. **High Player·Android IL2CPP/AOT·기기 실행은 미실행**이며 완료로 쓰지 않는다. Unity 6000.3.10f1, URP 17.3.0, Luna 없음. UniTask·DOTween·VContainer는 있고 Addressables는 현재 manifest/lock에 없다. A-08 채택이 미정인 것은 아니다.

사용자의 과거 중앙 UpdateLoop 적용 후 약 50% 레이턴시 개선은 과거 경험이다. Unity 제공 Physics 대 직접 구현 Physics의 비교는 Update 전달 방식 비교와 조건을 분리해 후속 설계한다. 대표 플레이·Daniel 수동 조작감·전체 아키텍처 완료 검증은 아직 없다.

이번 Pool 계약 조사에서 VContainer 생성·주입/활성화 순서, DOTween Kill/OnKill, ParticleSystem 정리 API를 확인했다. UniTask의 DOTween 확장 타입은 현재 로드된 어셈블리에서 찾지 못했으며 define/패키지를 바꾸지 않았다. [조사 근거](Prototype/evidence/raw/pool-contract-api-check.json). 이 조사 JSON은 구현 이전의 계약·API 확인 기록이다. 이후 구현/실행 결과와 구분한다.

## 검증 재실행과 주의할 점

Pool 최신 결과는 2026-09-08T15:01:55Z(2026-09-09 KST) [66개 통과](Prototype/evidence/raw/pool-runtime-validation.json)이며 빈 IPoolable의 준비된 반복 대여/반환 1,000회는 관리 할당 0바이트다. 계층을 먼저 파괴한 뒤 Factory Dispose하는 경계도 포함한다. 같은 최종 코드에서 DI/UI Pause도 15:02:23Z에 25개 재통과했다. 두 검사 후 Play 종료·씬 dirty=false·루트 3개·Console 오류 0건이다. [보존 검사](Prototype/evidence/raw/pool-preservation-check.json), [Editor 상태](Prototype/evidence/raw/pool-editor-final.json).

Tools/Smesh Fest/Validation/Pool Runtime은 현재 Game 씬을 Play한 뒤 실행한다. 실제 scope의 Factory 초기화를 확인하고 자체 임시 객체만 검사한다. JSON이 최신 시각·success=true인지 확인한 다음 Play를 종료한다. DI Pause Runtime을 함께 실행할 때는 서비스 종료를 포함하므로 마지막에 실행한다. High Player·전체 게임 Zero Alloc 검증을 대신하지 않는다.

Tools/Smesh Fest/Validation/Game Clock은 Editor에서 순수 C#만 검사한다. Tools/Smesh Fest/Validation/DI Pause Runtime은 현재 Game 씬을 Play한 뒤 한 번 실행한다. 성공 결과 JSON이 생성됐는지 확인한 뒤 Play를 종료한다. Runtime 검사는 마지막에 컨테이너를 DisposeCore로 종료하므로 같은 Play 세션을 이어서 게임 플레이 확인에 사용하지 않는다. 사용자 WorldObjects를 파괴하는 LifetimeScope.Dispose는 호출하지 않는다.

이전에 임시 코드 실행기는 검사 어셈블리를 만들지 못했으므로 일반 컴파일된 Editor 메뉴를 사용한다. UnityMCP domain reload 뒤 포트 재연결 경고가 있을 수 있다. null 필드뿐인 editor/state나 메뉴 도구의 attempted만으로 성공을 판단하지 말고 실제 메뉴·Console·결과 JSON을 확인한다.

## 모델·로드 순서·주요 경로

Main Astra·Ultra 실제 설정 확인. 이번 자료/제작 보조 Terra·Medium, 복잡한 수명 검토 Sol·High, 명시 배정·fork_turns:none·보조 최대 2개·재위임 없음. 단순 확인은 Luna·Low 정책을 유지한다. 실제 보조 토큰·비용은 미제공이다.

로드 순서: 이 문서 → ARCHITECTURE → README → 저장소 SKILL·모델 정책 → BRIEF → DESIGN → PLAN → REVIEW → 자동 REPORT. 현재 순서와 소유 범위는 [PLAN](Prototype/PLAN.md), 기준은 ARCHITECTURE가 원본이다. 단계·결정·작업·검증은 작업 에이전트가 기록기를 호출해 누적한다. 별도 상시 감시는 없고 REPORT.md는 생성물이므로 직접 편집하지 않는다.

주요 코드는 Assets/Scripts/Framework의 DI·Flow·Loop·Observer·Screen이며 검증 메뉴는 Assets/Editor/Validation, runtime probe는 Assets/Scripts/Test/DiPauseProbe.cs다. HANDOFF·Prototype 문서·루트 AGENTS.md·새 코드는 현재 미추적 상태다. 다른 clone에서 보인다고 보장하지 않는다.

```context-manifest
{
  "schema_version": 1,
  "saved_at": "2026-09-08T15:44:09.859175+00:00",
  "project_root": ".",
  "handoff_path": "Docs/HANDOFF.md",
  "required_files": [
    "AGENTS.md",
    "Docs/Prototype/ARCHITECTURE.md",
    "Docs/README.md",
    "Docs/Work-flow/prototype-workflow/SKILL.md",
    "Docs/Work-flow/prototype-workflow/references/model-routing.md",
    "Docs/Prototype/BRIEF.md",
    "Docs/Prototype/DESIGN.md",
    "Docs/Prototype/PLAN.md",
    "Docs/Prototype/REVIEW.md",
    "Docs/Prototype/evidence/REPORT.md"
  ],
  "optional_files": [
    "Docs/Prototype/evidence/events.jsonl",
    "Docs/Prototype/evidence/raw/startup-baseline.json",
    "Docs/Prototype/evidence/raw/preservation-check.json",
    "Assets/Scenes/Game.unity",
    "Assets/Scripts/Framework/Flow/GameFlow.cs",
    "Assets/Scripts/Framework/Loop/UpdateLoop.cs",
    "Packages/manifest.json",
    "ProjectSettings/ProjectVersion.txt",
    "Assets/Scripts/Framework/Observer/Observable.cs",
    "Assets/Editor/Validation/ObservableValidation.cs",
    "Docs/Prototype/evidence/raw/observable-validation.json",
    "Docs/Prototype/evidence/raw/observer-preservation-check.json",
    "Assets/Scripts/Framework/Loop/ILoopEvents.cs",
    "Assets/Scripts/Framework/Loop/LoopDispatcher.cs",
    "Assets/Editor/Validation/LoopValidation.cs",
    "Docs/Prototype/evidence/raw/loop-validation.json",
    "Docs/Prototype/evidence/raw/loop-preservation-check.json",
    "Assets/Scripts/Framework/DI/GameLifetimeScope.cs",
    "Assets/Scripts/Framework/Loop/GameClock.cs",
    "Assets/Scripts/Framework/Loop/IGamePause.cs",
    "Assets/Scripts/Framework/Loop/UnityGameTime.cs",
    "Assets/Scripts/Framework/Screen/ScreenPauseScope.cs",
    "Assets/Editor/Validation/GameClockValidation.cs",
    "Assets/Editor/Validation/DiPauseValidation.cs",
    "Assets/Scripts/Test/DiPauseProbe.cs",
    "Docs/Prototype/evidence/raw/game-clock-validation.json",
    "Docs/Prototype/evidence/raw/di-pause-playmode.json",
    "Docs/Prototype/evidence/raw/di-pause-preservation-check.json",
    "Docs/Prototype/evidence/raw/di-pause-editor-final.json",
    "Docs/Prototype/evidence/raw/di-pause-post-edit-validation.json",
    "Docs/Prototype/evidence/raw/di-pause-editor-revalidated.json",
    "Docs/Prototype/evidence/raw/pool-contract-baseline.json",
    "Docs/Prototype/evidence/raw/pool-contract-api-check.json",
    "Assets/Editor/Validation/PoolValidation.cs",
    "Assets/Scripts/Framework/Object/ObModel.cs",
    "Assets/Scripts/Framework/Object/ObController.cs",
    "Assets/Scripts/Framework/Object/ObView.cs",
    "Assets/Scripts/Framework/Pool/IPool.cs",
    "Assets/Scripts/Framework/Pool/IPoolLifecycle.cs",
    "Assets/Scripts/Framework/Pool/IPoolable.cs",
    "Assets/Scripts/Framework/Pool/Pool.cs",
    "Assets/Scripts/Framework/Pool/PoolConfig.cs",
    "Assets/Scripts/Framework/Pool/PoolContainer.cs",
    "Assets/Scripts/Framework/Pool/PoolFactory.cs",
    "Assets/Scripts/Framework/Pool/PoolLease.cs",
    "Assets/Scripts/Framework/Pool/PoolLifecycleRunner.cs",
    "Assets/Scripts/Framework/Pool/PoolSpawnArgs.cs",
    "Assets/Scripts/InGame/Ball/BallView.cs",
    "Assets/Scripts/Test/PoolRuntimeProbe.cs",
    "Docs/Prototype/evidence/raw/pool-editor-final.json",
    "Docs/Prototype/evidence/raw/pool-preservation-check.json",
    "Docs/Prototype/evidence/raw/pool-runtime-first-pass.json",
    "Docs/Prototype/evidence/raw/pool-runtime-validation.json",
    "Docs/Prototype/evidence/raw/pool-scene-change.diff",
    "Docs/Prototype/evidence/raw/mvc-reference-baseline.json",
    "Docs/Prototype/evidence/raw/mvc-reference-preservation-check.json",
    "Docs/Prototype/evidence/raw/mvc-reference-document-check.json"
  ],
  "validation": {
    "repository_inspected": true,
    "handoff_read": true,
    "tests_run": true,
    "build_run": false,
    "app_run": true,
    "manual_verification": false,
    "documentation_checks_run": true,
    "play_mode_run": true,
    "player_run": false,
    "pool_contract_runtime_run": true,
    "pool_runtime_assertions": 66,
    "di_pause_regression_assertions": 25,
    "latest_unit": "W-000-MVC-REFERENCE-001",
    "latest_unit_scope": "source_comparison_and_design_documentation",
    "mvc_implemented": false,
    "latest_unit_unity_run": false
  }
}
```

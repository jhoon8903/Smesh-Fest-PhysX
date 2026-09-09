# 프로젝트 인계

## 목표와 현재 상태

프로젝트: `/Volumes/Dock_SSD/Projects/Smesh-Fest-PhysX`. 선택한 A안은 **Block 영역 클릭으로 목표 선택·Ball 발사 → 파괴 → 결과 → 재도전 + Unity Physics/직접 구현 Physics 비교**다. Cannon은 Ball 진행 방향으로 회전한다. 별도 조준 단계는 없다. **전체 아키텍처를 먼저 정제·제작한 뒤 플레이 기능을 구현한다.**

최신 완료 단위는 **W-000-OBSTACLE-MVC-001: Obstacle별 View·Model·Controller 묶음 재사용**이다. Ball과 같은 객체별 원칙을 필요한 만큼만 적용하고 공통 베이스는 추출하지 않았다. 첫 ObstacleModel은 물리 비종속 대여 상태와 사용 세대만 소유한다. Unity 6000.3.10f1 Play Mode 격리 검사 25개를 통과했고 Play Mode는 종료했다. 다음은 **물리 권위와 Unity Physics/직접 구현 Physics 비교 조건을 정하는 단위**다. 플레이 기능과 전체 아키텍처는 아직 미완성이다.

현재 브랜치 `MVC-Obstacle`, HEAD `fc8b0ba`. 이 커밋은 PR #3의 Ball MVC 병합 결과이며 로컬 추적 정보 기준 `origin/MVC-Obstacle`과 ahead/behind 0/0이다. 이번 작업 시작 때 직전 context-save의 `Docs/HANDOFF.md`, 자동 기록 `Docs/Prototype/evidence/{REPORT.md,events.jsonl}`이 이미 수정 상태였고 Obstacle 골격·사용자 자산은 HEAD와 같았다. 현재는 Obstacle 생산 코드 3개와 검사 코드·증거·문서가 추가/수정 상태다. 작업 도중 AI/보조 소유 범위 밖에서 Ball/Cube Prefab에 Rigidbody가 추가된 저장 변경을 발견했으며 저자를 추정하거나 되돌리지 않았다. Game 씬과 Ball/Cube PoolConfig는 동일하다. AI는 커밋·푸시·브랜치 전환을 하지 않았다.

환경: Unity 6000.3.10f1, URP 17.3.0, VContainer 1.19.0. UniTask·DOTween 사용 기반이 있으며 Luna는 없다. Addressables는 채택된 기준이지만 현재 manifest/lock에 아직 없다.

## 확정 결정과 보존 원칙

기준의 단일 원본은 [ARCHITECTURE](Prototype/ARCHITECTURE.md)다. SRP·중앙 UpdateLoop·월드 MVC·UI MVP·Pool/Factory/Observer·Zero Alloc 지향·SO·Addressables·DI·High Stripping의 **10개 채택 여부를 다시 질문하지 않는다.**

- R-013: 설치된 VContainer 사용. UI에 별도 시간 관리자를 만들지 않으며 UI가 열리면 게임 Pause. 마지막 UI Pause 요청 해제 때 요청 배속을 복원하되 Stop 상태의 게임을 강제로 시작하지 않는다. Unity 전역 배율은 UnityGameTime이 관리하고 fixedDeltaTime은 보존한다.
- R-015~R-017: PoolConfig SO와 PoolContainer 목록을 Factory가 사전 생성한다. 생성/대여·반환 양쪽에서 초기화하고 Model/Controller의 유지·재생성은 객체별로 정한다. 재도전·스테이지 이동에서 유지하고 게임 씬을 나가 로비로 돌아갈 때 정리한다.
- PoolConfig의 MinPool은 사전 생성 수이자 비활성 재고 정리 후 남길 수, MaxPool은 활성+비활성 총 상한이다. 미사용 정리 시간은 ReturnDelaySeconds로 종류별 설정(초기 300초, 0은 자동 정리 끄기). 시간만으로 사용 중인 객체를 강제 반환하지 않는다. 비활성 정리 때 재고를 강제로 보충하지 않는다.
- R-018: **ObView는 독립 MonoBehaviour**, 풀링이 필요한 구체 View만 **ObView 계열 + IPoolable**. 공통 View를 Poolable 기반에 묶지 않는다.
- R-019: ProjectTemplate의 `Assets/Framework/MVC`는 설계 참고다. 필요한 책임부터 간소화해 적용한다. 참고의 Pool 상속·SO ModuleContainer·자동 타입 탐색 체계를 일괄 이식하지 않는다. 기존 GameLifetimeScope에서 필요한 의존성을 명시적으로 조립한다.
- R-021: Ball은 풀 인스턴스마다 View·Model·Controller를 한 번 조립해 인스턴스 수명 동안 재사용한다. 매 대여마다 Model/Controller를 새로 만들지 않고 반환·폐기에서 상태와 구독을 정리한다.
- R-022: 첫 BallModel은 물리 비종속 `IsRented`와 `RentalEpoch`만 소유한다. 위치·속도·충돌·Rigidbody 권위는 넣지 않는다. Ball → Obstacle MVC → 물리 순서이며 현재 Obstacle MVC까지 완료했다.
- Obstacle 적용: R-019·R-021의 객체별 조립 원칙을 구체 타입에 적용했다. 첫 ObstacleModel도 `IsRented`와 `RentalEpoch`만 소유하며 HP·파괴·위치·속도·충돌·Rigidbody 권위는 다음 단위 전까지 넣지 않는다.
- Daniel의 씬·하이어라키·Prefab·Material·ParticleSystem·Importer 수동값과 병행 코드를 보존한다. 전체 씬 재생성·자동 저장·사용자 설정 덮어쓰기를 검증 수단으로 사용하지 않는다. 과거 루트 수나 배치로 되돌리지 않는다.
- Main Astra·Ultra, 보조는 저장소의 모델·Effort 정책을 따른다. 자동 기록은 작업 중 에이전트가 기존 기록기를 호출하는 방식이며 상주 감시가 아니다.

## 완료한 연결과 사용 계약

| 기반 | 현재 코드·연결 |
|---|---|
| Observer·Loop·시간 | Observable<T>, LoopDispatcher/ILoopEvents, GameClock/IGamePause, UnityGameTime, GameFlow/UpdateLoop. 개별 월드 객체는 중앙 Loop의 필요한 phase를 구독한다. |
| DI·UI Pause | WorldObjects의 GameLifetimeScope에서 주입·씬 서비스 수명 관리. ScreenPauseScope가 실제 화면의 활성/비활성과 Pause 요청을 연결한다. 상시 HUD 전체를 자동으로 Pause 대상으로 만들지 않는다. 동적 화면은 주입 후 사용하며 CanvasGroup만 숨길 때는 Presenter 수명에 연결해야 한다. |
| Pool | PoolFactory·PoolConfig·PoolContainer·PoolLease·PoolLifecycleRunner. 비활성 부모 아래 복제 → DI → 초기화 → 활성화. 원본 Prefab 활성값을 토글하지 않는다. 사용 번호로 오래된 lease/중복 반환을 거절한다. |
| Pool 정리 | 각 IPoolLifecycle 파츠가 구독·Tween·파티클·비동기 작업을 정리한다. Factory는 UI Pause 중에도 실제 경과 시간으로 비활성 재고를 검사하고 Dispose에서 유지보수를 취소한다. 실패한 객체는 정상 재고에 섞지 않는다. 객체 파괴와 Addressables 자산 해제는 다른 책임이다. |
| Model–View | ObModel은 상태 변경 후 NotifyChanged, 선택적 ObView<TModel>은 Bind/Unbind와 RefreshView(model)을 제공한다. 기존 ObView는 그대로이며 Pool을 요구하지 않는다. |
| Ball MVC | BallView가 OnPoolCreated에서 BallModel/BallController를 한 번 만들고 대여 세대와 현재 PoolLease를 연결한다. Controller 정상 반환과 오래된 세대 거절을 지원하며 반환·풀 종료·Unity OnDestroy에서 같은 idempotent 정리를 사용한다. 입력·Loop·물리 상태는 아직 연결하지 않았다. |
| Obstacle MVC | ObstacleView가 OnPoolCreated에서 ObstacleModel/ObstacleController를 한 번 만들고 같은 묶음을 재대여한다. 유효하지 않은 lease의 준비 실패도 Model·View까지 되돌리며 정상/오래된 반환과 반환·풀 종료·Unity OnDestroy의 idempotent 정리를 지원한다. HP·파괴·물리 상태는 아직 연결하지 않았다. |

View의 비활성 Bind는 모델 참조만 보관한다. 활성화 때 한 번 구독·갱신하고 비활성화 때 기본/추가 모델 구독을 해제하되 모델 참조는 유지한다. Model 교체·Unbind는 옛 연결을 정리한다. 구체 풀링 객체는 반환/폐기에서 Controller의 구독 정리와 View.Unbind를 호출한다. 파생 Unity 수명 콜백은 base를 호출한다. 같은 모델의 재연결도 이전 관찰의 갱신·실패 정리가 새 관찰을 건드리지 않게 보호했다. 같은 모델의 재귀 NotifyChanged는 기존 Observer 계약대로 거절한다.

Ball과 Obstacle의 Model은 대여 여부와 0이 아닌 사용 세대를 관리한다. 각 Controller는 현재 세대와 PoolLease가 모두 유효할 때만 반환한다. 각 View는 같은 Model/Controller를 재대여하며 관찰은 활성 중 한 번만 연결한다. 씬 계층이 Factory보다 먼저 파괴될 때도 Unity OnDestroy가 즉시 묶음을 정리하고, 뒤의 Pool Dispose는 안전하게 중복 정리한다. 공통 ObController는 여전히 빈 기반이고 Cannon은 골격이다. Obstacle의 실제 HP·파괴·물리·표현은 미구현이며 테스트용 ProbeController를 실제 게임 Controller로 해석하지 않는다.

## 검증 증거와 한계

아래에서 Obstacle MVC 25개만 이번 작업에서 직접 실행한 최신 결과다. 나머지는 이전 실행 기록을 읽어 확인했으며 이번 작업에서 전체 재실행하지 않았다.

| 단위 | 실제 기록 |
|---|---|
| Observer·Loop·GameClock | 명시적 Editor 메뉴에서 수명/상태 검사 통과. 조건을 고정한 반복 구간의 관리 할당 0바이트. 상세 조건은 REVIEW와 각 원자료 참조. |
| DI·UI Pause | Play Mode 25개 검사 통과. Loop·물리·scaled Tween/파티클의 정지/재개와 수명 정리를 포함한다. [결과](Prototype/evidence/raw/di-pause-playmode.json) |
| Pool | Play Mode 66개 검사 통과. DI 순서·재사용·실패/재진입·정리·비활성 MinPool 유지·계층 선파괴를 포함한다. [결과](Prototype/evidence/raw/pool-runtime-validation.json) |
| Model–View | Unity 6000.3.10f1 Play Mode 58개 검사 통과. 활성/교체/해제·훅 예외/재진입, 실제 PoolFactory DI와 묶음 유지/재생성·반환/종료를 임시 객체로 확인했다. [결과](Prototype/evidence/raw/mvc-runtime-validation.json) |
| Ball MVC | Unity 6000.3.10f1 Play Mode **19개 검사 통과**. Controller/PoolLease 정상 반환, 같은 묶음 재대여, 이전 세대 거절, 활성 Pool Dispose와 계층 선파괴 정리를 임시 BallView로 확인했다. [결과](Prototype/evidence/raw/ball-mvc-runtime-validation.json) |
| Obstacle MVC | Unity 6000.3.10f1 Play Mode **25개 검사 통과**. Ball과 같은 수명 경계에 더해 잘못된 lease 준비 실패 롤백, 생성 후 미대여 파괴와 파괴 오류 로그 부재를 임시 ObstacleView로 확인했다. [결과](Prototype/evidence/raw/obstacle-mvc-runtime-validation.json) |

MVC 측정은 사전 생성 모델/View/delegate를 100회 워밍업한 후 통지 1,000회와 Unbind/Bind 1,000회를 각각 측정해 0바이트다. 객체 생성·실제 렌더링·Pool 대여/반환·측정 중 로그/assertion은 제외했다. 전체 게임 Zero Alloc이나 성능 향상률의 증거가 아니다. Daniel의 과거 중앙 UpdateLoop 적용 후 약 50% 개선 경험도 이번 프로젝트 측정값이 아니다.

최종 Ball MVC 실행 후 Play Mode는 종료됐고 Game 씬은 dirty=false·루트 4개, Console 오류 0건이었다. domain reload 때 MCP stdio 포트 재연결 경고가 있었지만 결과 파일·Console·씬 상태를 다시 확인했다. 검사 전후 Game.unity, Ball/Cube Prefab, Ball/Cube PoolConfig의 저장 파일 해시는 모두 동일하다. [Ball 보존 결과](Prototype/evidence/raw/ball-mvc-preservation-check.json) · [Ball Editor 종료 기록](Prototype/evidence/raw/ball-mvc-editor-final.json). 앞선 공통 MVC 보존 근거와 과거 실패·보완은 [REVIEW](Prototype/REVIEW.md)에 있다.

최종 Obstacle MVC 실행 후 Play Mode는 종료됐고 Game 씬은 dirty=false·루트 4개, 컴파일/도메인 reload 대기 없음, 종료 후 Console 항목 0개였다. 5개 스크립트 정적 검사는 오류·경고 0건이다. 검사 전후 Game.unity와 Ball/Cube PoolConfig 해시는 동일하다. Ball/Cube Prefab의 Rigidbody 추가는 격리 검사와 무관한 동시 외부 변경으로 분리해 보존했다. [Obstacle 보존 결과](Prototype/evidence/raw/obstacle-mvc-preservation-check.json) · [Obstacle Editor 종료 기록](Prototype/evidence/raw/obstacle-mvc-editor-final.json). 독립 Sol High 검토가 찾은 검사 사각지대 2개를 보완한 뒤 25개로 재실행했다.

이번 context-save에서 인계 manifest 71개 경로와 문서 7개의 로컬 링크 136개를 확인해 누락 0개, diff 공백 오류 0개다. HANDOFF는 Git 추적 파일이며 ignored가 아니다. [Obstacle MVC 문서 검사](Prototype/evidence/raw/obstacle-mvc-document-check.json).

**High 설정 빌드 검증·Android IL2CPP/AOT·기기 실행은 미실행**이다. 이전의 “High Player”는 별도 Unity 기능명이 아니라 Managed Stripping Level=High로 만든 실제 앱/게임 빌드를 줄여 쓴 표현이었다. 이후에는 **“High 설정 빌드 검증”**으로 쓴다. 현재 주입 멤버 Preserve와 명시적 생성 factory 조치가 있지만 Editor/Play Mode 통과로 실제 빌드의 코드 보존을 확정하지 않는다.

## 다음 작업·미결정과 분담

다음 작은 단위는 **Ball/Obstacle의 물리 권위와 Unity Physics/직접 구현 Physics 비교 조건 확정**이다. 코어 루프·세이브·핵심 아키텍처에 영향을 주는 되돌리기 비싼 결정이므로 구현 전에 Daniel의 비교 목적과 원하는 권위 위치를 쉬운 예시로 확인한다. Obstacle MVC가 끝났다는 사실만으로 Rigidbody를 Model 또는 Controller의 권위로 자동 결정하지 않는다.

- Daniel: 씬·카메라·Prefab·공/표적 배치·설정값·조작감, 본인이 만드는 코드의 의도. 씬을 다시 만들도록 요구하지 않는다.
- AI: 최신 Ball/Obstacle 코드와 실제 Rigidbody·Collider 구성을 다시 읽고, 물리 권위 선택지별로 무엇이 깨지는지와 동일 비교 조건·작은 검증안을 제시한다. 사용자가 만든 씬/Prefab/PoolConfig는 승인 없이 재생성하지 않는다.
- 현재 사용자 자산 기준: Ball Prefab은 MeshRenderer·SphereCollider·Rigidbody·BallView, Ball PoolConfig는 Prefab 연결·Min 3·Max 7·200초이고 Game PoolContainer에 등록돼 있다. Cube Prefab은 MeshRenderer·BoxCollider·Rigidbody이고 ObstacleView는 아직 없으며, Cube PoolConfig의 Prefab은 비어 있고 Game 목록에는 아직 없다. 두 Rigidbody는 이번 Obstacle 작업 중 외부에서 추가된 저장 변경으로 보존했으며 AI 결과로 귀속하지 않는다.
- 후속 세부 계약: 위치·속도·충돌·Rigidbody 권위, 두 Physics 방식의 동일 입력/초기조건/측정 지표, 화면별 MVP, SO 런타임 상태 범위, Addressables 로드/취소/해제 핸들 소유권. O-001~O-008과 R-021·R-022를 처음부터 다시 질문하지 않는다.
- 전체 아키텍처를 연결한 뒤 클릭 발사·Cannon 회전, 파괴·결과·재도전 순서로 진행한다. 최초 발사 선행 제안은 구현 전 철회했으며 성과로 계산하지 않는다. Unity Physics 비교와 Update 전달 방식 비교는 조건을 분리한다. 플랫폼·기한·납품 목적은 필요한 시점에만 확인한다.

## 재개 순서·주요 경로

이 문서 → [ARCHITECTURE](Prototype/ARCHITECTURE.md) → [README](README.md) 및 저장소 스킬/모델 정책 → [BRIEF](Prototype/BRIEF.md)·[DESIGN](Prototype/DESIGN.md) → [PLAN](Prototype/PLAN.md) → [REVIEW](Prototype/REVIEW.md) → [자동 REPORT](Prototype/evidence/REPORT.md). 기준·기획·작업·검증의 원본을 분리하며 과거 시점 기록을 현재 상태로 오독하지 않는다.

주요 코드: `Assets/Scripts/Framework/{Object,Observer,Loop,Flow,DI,Pool,Screen}`, `Assets/Scripts/InGame/{Ball,Obstacle}`. Obstacle 검증 원본은 `Assets/Scripts/Test/ObstacleMvcRuntimeProbe.cs`, 메뉴는 `Assets/Editor/Validation/ObstacleMvcValidation.cs`의 `Tools/Smesh Fest/Validation/Obstacle MVC Runtime`이다. Play Mode에서 임시 객체로 실행하고 최신 success 결과를 확인한 뒤 종료한다. 공통 MVC/Pool Runtime도 임시 객체를 사용하며 DI Pause Runtime은 서비스 종료를 포함하므로 함께 검사할 때 마지막에 실행한다. Inspector·씬 재생성 메뉴를 검증 대신 실행하지 않는다.

자동 기록은 `Docs/Work-flow/prototype-workflow/scripts/workflow_log.py`와 `Docs/Prototype/evidence`를 사용한다. REPORT는 직접 수정하지 않는다. 이번 인계 저장의 문서/경로 검사와 이전 Unity 실행 증거를 구분한다.

```context-manifest
{
  "schema_version": 1,
  "saved_at": "2026-09-08T18:12:57Z",
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
    "Assets/Scenes/Game.unity",
    "Packages/manifest.json",
    "Packages/packages-lock.json",
    "ProjectSettings/ProjectVersion.txt",
    "Assets/Scripts/Framework/DI/GameLifetimeScope.cs",
    "Assets/Scripts/Framework/Flow/GameFlow.cs",
    "Assets/Scripts/Framework/Loop/UpdateLoop.cs",
    "Assets/Scripts/Framework/Loop/LoopDispatcher.cs",
    "Assets/Scripts/Framework/Loop/GameClock.cs",
    "Assets/Scripts/Framework/Loop/UnityGameTime.cs",
    "Assets/Scripts/Framework/Observer/Observable.cs",
    "Assets/Scripts/Framework/Object/ObModel.cs",
    "Assets/Scripts/Framework/Object/ObView.cs",
    "Assets/Scripts/Framework/Object/ObViewOfT.cs",
    "Assets/Scripts/Framework/Object/ObController.cs",
    "Assets/Scripts/Framework/Pool/Pool.cs",
    "Assets/Scripts/Framework/Pool/PoolConfig.cs",
    "Assets/Scripts/Framework/Pool/PoolFactory.cs",
    "Assets/Scripts/Framework/Pool/IPoolable.cs",
    "Assets/Scripts/Framework/Pool/IPoolLifecycle.cs",
    "Assets/Scripts/Framework/Pool/PoolLifecycleRunner.cs",
    "Assets/Scripts/Framework/Pool/PoolLease.cs",
    "Assets/Scripts/Framework/Screen/ScreenPauseScope.cs",
    "Assets/Scripts/InGame/Ball/BallModel.cs",
    "Assets/Scripts/InGame/Ball/BallController.cs",
    "Assets/Scripts/InGame/Ball/BallView.cs",
    "Assets/Scripts/InGame/Obstacle/ObstacleModel.cs",
    "Assets/Scripts/InGame/Obstacle/ObstacleController.cs",
    "Assets/Scripts/InGame/Obstacle/ObstacleView.cs",
    "Assets/Scripts/Test/MvcRuntimeProbe.cs",
    "Assets/Editor/Validation/MvcValidation.cs",
    "Assets/Scripts/Test/BallMvcRuntimeProbe.cs",
    "Assets/Scripts/Test/BallMvcRuntimeProbe.cs.meta",
    "Assets/Editor/Validation/BallMvcValidation.cs",
    "Assets/Editor/Validation/BallMvcValidation.cs.meta",
    "Assets/Scripts/Test/ObstacleMvcRuntimeProbe.cs",
    "Assets/Scripts/Test/ObstacleMvcRuntimeProbe.cs.meta",
    "Assets/Editor/Validation/ObstacleMvcValidation.cs",
    "Assets/Editor/Validation/ObstacleMvcValidation.cs.meta",
    "Assets/Project/Prefabs/Ball.prefab",
    "Assets/Project/Prefabs/Ball.prefab.meta",
    "Assets/Project/Prefabs/Cube.prefab",
    "Assets/Project/Prefabs/Cube.prefab.meta",
    "Assets/Project/So/Ball.asset",
    "Assets/Project/So/Ball.asset.meta",
    "Assets/Project/So/Cube.asset",
    "Assets/Project/So/Cube.asset.meta",
    "Docs/Prototype/evidence/events.jsonl",
    "Docs/Prototype/evidence/raw/di-pause-playmode.json",
    "Docs/Prototype/evidence/raw/pool-runtime-validation.json",
    "Docs/Prototype/evidence/raw/mvc-runtime-validation.json",
    "Docs/Prototype/evidence/raw/mvc-preservation-check.json",
    "Docs/Prototype/evidence/raw/mvc-editor-final.json",
    "Docs/Prototype/evidence/raw/ball-mvc-runtime-validation.json",
    "Docs/Prototype/evidence/raw/ball-mvc-preservation-check.json",
    "Docs/Prototype/evidence/raw/ball-mvc-editor-final.json",
    "Docs/Prototype/evidence/raw/ball-mvc-document-check.json",
    "Docs/Prototype/evidence/raw/obstacle-mvc-runtime-validation.json",
    "Docs/Prototype/evidence/raw/obstacle-mvc-preservation-check.json",
    "Docs/Prototype/evidence/raw/obstacle-mvc-editor-final.json",
    "Docs/Prototype/evidence/raw/obstacle-mvc-document-check.json"
  ],
  "validation": {
    "scope": "current_context_save",
    "repository_inspected": true,
    "handoff_read": true,
    "tests_run": true,
    "build_run": false,
    "app_run": true,
    "app_run_scope": "Unity Editor Play Mode isolated Obstacle MVC probe only; no Player build, device, physics, input, or gameplay run",
    "manual_verification": false,
    "documentation_checks_run": true,
    "prior_results_reviewed": true,
    "ball_mvc_runtime_probe_run": false,
    "obstacle_mvc_runtime_probe_run": true,
    "mvc_core_matches_recorded_hashes": true,
    "previous_runtime_results": {
      "obstacle_mvc_assertions": 25,
      "ball_mvc_assertions": 19,
      "mvc_assertions": 58,
      "pool_assertions": 66,
      "di_pause_assertions": 25
    },
    "git_branch": "MVC-Obstacle",
    "git_head": "fc8b0baafa77eac32fcfe51823f3d95bae3e4eb8",
    "upstream_ahead": 0,
    "upstream_behind": 0,
    "worktree_clean_before_context_save": false,
    "preexisting_dirty_paths": [
      "Docs/HANDOFF.md",
      "Docs/Prototype/evidence/REPORT.md",
      "Docs/Prototype/evidence/events.jsonl"
    ],
    "concurrent_external_changes_preserved": [
      "Assets/Project/Prefabs/Ball.prefab: Rigidbody added",
      "Assets/Project/Prefabs/Cube.prefab: Rigidbody added"
    ],
    "mvc_binding_implemented": true,
    "ball_mvc_implemented": true,
    "obstacle_mvc_implemented": true,
    "high_setting_build_run": false
  }
}
```

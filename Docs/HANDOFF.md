# 프로젝트 인계

## 목표와 현재 상태

프로젝트: `/Volumes/Dock_SSD/Projects/Smesh-Fest-PhysX`. 선택한 A안은 **Block 영역 클릭으로 목표 선택·Ball 발사 → 파괴 → 결과 → 재도전 + Unity Physics/직접 구현 Physics 비교**다. Cannon은 Ball 진행 방향으로 회전한다. 별도 조준 단계는 없다. **전체 아키텍처를 먼저 정제·제작한 뒤 플레이 기능을 구현한다.**

최신 구현 단위는 **W-003-PHYSX-LIFECYCLE-001: Rigidbody 권위의 Ball 발사와 Ball/Obstacle 풀 물리 초기화**다. Unity 6000.3.10f1 일반 컴파일과 14개 변경 스크립트 정적 진단은 오류·경고 0건이었다. Daniel의 첫 local PhysicsScene Play Mode 실행은 assertion 10에서 대여 직후 Sleep/Wake 복합 검사가 실패해 시뮬레이션 전에 중단됐다. 비활성 `OnPoolRent` 뒤 활성화되는 순서에 맞춰 두 View가 `OnEnable`에서 상태를 재적용하도록 수정했고, 수정된 5개 스크립트 정적 진단도 warning 0·error 0이다. 수정본 재실행은 대기다. 이전 Obstacle MVC 25개 통과와 새 PhysX 런타임 검증을 구분한다. 클릭 입력·Cannon·HP/파괴·결과/재도전과 직접 구현 물리 비교는 아직 미완성이다.

현재 브랜치 `MVC-Obstacle`, HEAD `ca1c691`. 이번 PhysX 작업 시작 시 기존 dirty 경로는 Daniel이 Rigidbody와 ObstacleView를 연결한 `Assets/Scenes/Game.unity` 하나였다. 현재는 Ball/Obstacle 생산 코드, 공통 Pool의 파괴 경계, 격리 검사, 증거·문서가 추가/수정 상태다. Game 씬과 Ball/Cube Prefab·PoolConfig의 시작/종료 해시는 동일하며 AI는 씬·Prefab·SO를 저장하거나 재생성하지 않았다. AI는 커밋·푸시·브랜치 전환을 하지 않았다.

환경: Unity 6000.3.10f1, URP 17.3.0, VContainer 1.19.0. UniTask·DOTween 사용 기반이 있으며 Luna는 없다. Addressables는 채택된 기준이지만 현재 manifest/lock에 아직 없다.

## 확정 결정과 보존 원칙

기준의 단일 원본은 [ARCHITECTURE](Prototype/ARCHITECTURE.md)다. SRP·중앙 UpdateLoop·월드 MVC·UI MVP·Pool/Factory/Observer·Zero Alloc 지향·SO·Addressables·DI·High Stripping의 **10개 채택 여부를 다시 질문하지 않는다.**

- R-013: 설치된 VContainer 사용. UI에 별도 시간 관리자를 만들지 않으며 UI가 열리면 게임 Pause. 마지막 UI Pause 요청 해제 때 요청 배속을 복원하되 Stop 상태의 게임을 강제로 시작하지 않는다. Unity 전역 배율은 UnityGameTime이 관리하고 fixedDeltaTime은 보존한다.
- R-015~R-017: PoolConfig SO와 PoolContainer 목록을 Factory가 사전 생성한다. 생성/대여·반환 양쪽에서 초기화하고 Model/Controller의 유지·재생성은 객체별로 정한다. 재도전·스테이지 이동에서 유지하고 게임 씬을 나가 로비로 돌아갈 때 정리한다.
- PoolConfig의 MinPool은 사전 생성 수이자 비활성 재고 정리 후 남길 수, MaxPool은 활성+비활성 총 상한이다. 미사용 정리 시간은 ReturnDelaySeconds로 종류별 설정(초기 300초, 0은 자동 정리 끄기). 시간만으로 사용 중인 객체를 강제 반환하지 않는다. 비활성 정리 때 재고를 강제로 보충하지 않는다.
- R-018: **ObView는 독립 MonoBehaviour**, 풀링이 필요한 구체 View만 **ObView 계열 + IPoolable**. 공통 View를 Poolable 기반에 묶지 않는다.
- R-019: ProjectTemplate의 `Assets/Framework/MVC`는 설계 참고다. 필요한 책임부터 간소화해 적용한다. 참고의 Pool 상속·SO ModuleContainer·자동 타입 탐색 체계를 일괄 이식하지 않는다. 기존 GameLifetimeScope에서 필요한 의존성을 명시적으로 조립한다.
- R-021: Ball은 풀 인스턴스마다 View·Model·Controller를 한 번 조립해 인스턴스 수명 동안 재사용한다. 매 대여마다 Model/Controller를 새로 만들지 않고 반환·폐기에서 상태와 구독을 정리한다.
- R-022: 첫 BallModel은 물리 비종속 `IsRented`와 `RentalEpoch`만 소유한다. R-023 구현 뒤에도 위치·속도·충돌·Rigidbody 상태를 Model에 복제하지 않는다.
- Obstacle 적용: R-019·R-021의 객체별 조립 원칙을 구체 타입에 적용했다. 첫 ObstacleModel도 `IsRented`와 `RentalEpoch`만 소유하며 HP·파괴·위치·속도·충돌 상태는 아직 넣지 않는다.
- R-023: 채용 공고의 PhysX 요구에 맞춰 첫 플레이는 Unity PhysX로 만든다. 현재 위치·회전·선속도·각속도 권위는 Rigidbody이고 Model에 복제하지 않는다. 대표 플레이 뒤 직접 구현 물리의 대안 권위와 동일 비교 조건을 별도 `PHYSICS_AUTHORITY.md`로 남긴다.
- Daniel의 씬·하이어라키·Prefab·Material·ParticleSystem·Importer 수동값과 병행 코드를 보존한다. 전체 씬 재생성·자동 저장·사용자 설정 덮어쓰기를 검증 수단으로 사용하지 않는다. 과거 루트 수나 배치로 되돌리지 않는다.
- Main Astra·Ultra, 보조는 저장소의 모델·Effort 정책을 따른다. 자동 기록은 작업 중 에이전트가 기존 기록기를 호출하는 방식이며 상주 감시가 아니다.

## 완료한 연결과 사용 계약

| 기반 | 현재 코드·연결 |
|---|---|
| Observer·Loop·시간 | Observable<T>, LoopDispatcher/ILoopEvents, GameClock/IGamePause, UnityGameTime, GameFlow/UpdateLoop. 개별 월드 객체는 중앙 Loop의 필요한 phase를 구독한다. |
| DI·UI Pause | WorldObjects의 GameLifetimeScope에서 주입·씬 서비스 수명 관리. ScreenPauseScope가 실제 화면의 활성/비활성과 Pause 요청을 연결한다. 상시 HUD 전체를 자동으로 Pause 대상으로 만들지 않는다. 동적 화면은 주입 후 사용하며 CanvasGroup만 숨길 때는 Presenter 수명에 연결해야 한다. |
| Pool | PoolFactory·PoolConfig·PoolContainer·PoolLease·PoolLifecycleRunner. 비활성 부모 아래 복제 → DI → 초기화 → 활성화. 원본 Prefab 활성값을 토글하지 않는다. 사용 번호로 오래된 lease/중복 반환을 거절한다. Unity 계층이 먼저 파괴되면 lease를 즉시 무효화하고, 생성/대여/반환 callback 도중 파괴된 객체도 격리해 재고로 넣지 않는다. |
| Pool 정리 | 각 IPoolLifecycle 파츠가 구독·Tween·파티클·비동기 작업을 정리한다. Factory는 UI Pause 중에도 실제 경과 시간으로 비활성 재고를 검사하고 Dispose에서 유지보수를 취소한다. 실패한 객체는 정상 재고에 섞지 않는다. 객체 파괴와 Addressables 자산 해제는 다른 책임이다. |
| Model–View | ObModel은 상태 변경 후 NotifyChanged, 선택적 ObView<TModel>은 Bind/Unbind와 RefreshView(model)을 제공한다. 기존 ObView는 그대로이며 Pool을 요구하지 않는다. |
| Ball MVC + PhysX | BallView가 OnPoolCreated에서 BallModel/BallController를 한 번 만들고 대여 세대·PoolLease·같은 루트의 dynamic Rigidbody/Collider를 연결한다. 비활성 대여 callback에서 속도를 지우고 활성화 `OnEnable`에서 발사 전 Sleep을 재적용하며, `TryLaunch(epoch, velocity)`가 현재 세대에 finite·0이 아닌 초기속도를 한 번만 적용한다. 반환·풀 종료·Unity OnDestroy에서 속도와 managed 상태를 함께 정리한다. |
| Obstacle MVC + PhysX | ObstacleView가 같은 묶음과 dynamic Rigidbody/Collider를 연결한다. 비활성 대여 callback에서 속도를 지우고 활성화 `OnEnable`에서 WakeUp, 반환/폐기 때 속도를 지우고 Sleep한다. HP·파괴·충돌 결과·표현은 아직 연결하지 않았다. |

View의 비활성 Bind는 모델 참조만 보관한다. 활성화 때 한 번 구독·갱신하고 비활성화 때 기본/추가 모델 구독을 해제하되 모델 참조는 유지한다. Model 교체·Unbind는 옛 연결을 정리한다. 구체 풀링 객체는 반환/폐기에서 Controller의 구독 정리와 View.Unbind를 호출한다. 파생 Unity 수명 콜백은 base를 호출한다. 같은 모델의 재연결도 이전 관찰의 갱신·실패 정리가 새 관찰을 건드리지 않게 보호했다. 같은 모델의 재귀 NotifyChanged는 기존 Observer 계약대로 거절한다.

Ball과 Obstacle의 Model은 대여 여부와 0이 아닌 사용 세대를 관리한다. Rigidbody가 런타임 물리 상태를 소유하며 Controller는 발사/초기화 명령만 내린다. 각 View는 같은 Model/Controller를 재대여하며 관찰은 활성 중 한 번만 연결한다. 씬 계층이 Factory보다 먼저 파괴될 때 Unity OnDestroy가 즉시 묶음을 정리하고 lease도 즉시 무효다. 공통 ObController는 여전히 빈 기반이고 Cannon은 골격이다. Obstacle의 실제 HP·파괴·충돌 규칙·표현은 미구현이며 테스트용 Probe를 실제 게임 흐름으로 해석하지 않는다.

## 검증 증거와 한계

아래의 runtime assertion 수는 시점이 다른 실행 기록이다. 이번 PhysX Probe는 Daniel이 1차 실행했지만 시뮬레이션 전 실패했고, 활성화 수정본은 아직 재실행하지 않았다.

| 단위 | 실제 기록 |
|---|---|
| Observer·Loop·GameClock | 명시적 Editor 메뉴에서 수명/상태 검사 통과. 조건을 고정한 반복 구간의 관리 할당 0바이트. 상세 조건은 REVIEW와 각 원자료 참조. |
| DI·UI Pause | Play Mode 25개 검사 통과. Loop·물리·scaled Tween/파티클의 정지/재개와 수명 정리를 포함한다. [결과](Prototype/evidence/raw/di-pause-playmode.json) |
| Pool | 과거 Play Mode 66개 검사 통과. 이번에 callback 중 Unity 객체 파괴를 격리하는 생산 코드와 회귀 2개를 추가했으나 새 전체 Pool Runtime은 아직 재실행하지 않았다. [과거 결과](Prototype/evidence/raw/pool-runtime-validation.json) |
| Model–View | Unity 6000.3.10f1 Play Mode 58개 검사 통과. 활성/교체/해제·훅 예외/재진입, 실제 PoolFactory DI와 묶음 유지/재생성·반환/종료를 임시 객체로 확인했다. [결과](Prototype/evidence/raw/mvc-runtime-validation.json) |
| Ball MVC | Unity 6000.3.10f1 Play Mode **19개 검사 통과**. Controller/PoolLease 정상 반환, 같은 묶음 재대여, 이전 세대 거절, 활성 Pool Dispose와 계층 선파괴 정리를 임시 BallView로 확인했다. [결과](Prototype/evidence/raw/ball-mvc-runtime-validation.json) |
| Obstacle MVC | Unity 6000.3.10f1 Play Mode **25개 검사 통과**. Ball과 같은 수명 경계에 더해 잘못된 lease 준비 실패 롤백, 생성 후 미대여 파괴와 파괴 오류 로그 부재를 임시 ObstacleView로 확인했다. [결과](Prototype/evidence/raw/obstacle-mvc-runtime-validation.json) |
| PhysX 수명 | 최초 Unity 일반 컴파일 오류 0, 변경 스크립트 정적 진단 0/0. 1차 Play Mode는 assertion 10·시뮬레이션 0회에서 Sleep/Wake 복합 검사 실패. 활성화 뒤 상태 재적용과 검사 분리 후 수정 5개 스크립트 진단 0/0이며 재실행 대기. [1차 실패](Prototype/evidence/raw/physx-lifecycle-runtime-failed-20260909T061136Z.json) · [수정 컴파일](Prototype/evidence/raw/physx-activation-fix-editor-compile.json) |

MVC 측정은 사전 생성 모델/View/delegate를 100회 워밍업한 후 통지 1,000회와 Unbind/Bind 1,000회를 각각 측정해 0바이트다. 객체 생성·실제 렌더링·Pool 대여/반환·측정 중 로그/assertion은 제외했다. 전체 게임 Zero Alloc이나 성능 향상률의 증거가 아니다. Daniel의 과거 중앙 UpdateLoop 적용 후 약 50% 개선 경험도 이번 프로젝트 측정값이 아니다.

최종 Ball MVC 실행 후 Play Mode는 종료됐고 Game 씬은 dirty=false·루트 4개, Console 오류 0건이었다. domain reload 때 MCP stdio 포트 재연결 경고가 있었지만 결과 파일·Console·씬 상태를 다시 확인했다. 검사 전후 Game.unity, Ball/Cube Prefab, Ball/Cube PoolConfig의 저장 파일 해시는 모두 동일하다. [Ball 보존 결과](Prototype/evidence/raw/ball-mvc-preservation-check.json) · [Ball Editor 종료 기록](Prototype/evidence/raw/ball-mvc-editor-final.json). 앞선 공통 MVC 보존 근거와 과거 실패·보완은 [REVIEW](Prototype/REVIEW.md)에 있다.

최종 Obstacle MVC 실행 후 Play Mode는 종료됐고 Game 씬은 dirty=false·루트 4개, 컴파일/도메인 reload 대기 없음, 종료 후 Console 항목 0개였다. 5개 스크립트 정적 검사는 오류·경고 0건이다. 검사 전후 Game.unity와 Ball/Cube PoolConfig 해시는 동일하다. Ball/Cube Prefab의 Rigidbody 추가는 격리 검사와 무관한 동시 외부 변경으로 분리해 보존했다. [Obstacle 보존 결과](Prototype/evidence/raw/obstacle-mvc-preservation-check.json) · [Obstacle Editor 종료 기록](Prototype/evidence/raw/obstacle-mvc-editor-final.json). 독립 Sol High 검토가 찾은 검사 사각지대 2개를 보완한 뒤 25개로 재실행했다.

이번 PhysX 작업의 최초 및 활성화 수정 Unity 컴파일 뒤 compiler error는 0개다. 수정된 5개 스크립트 진단도 warning 0·error 0이다. domain reload 중 경고는 MCP bridge 포트 재연결/변경뿐이며 Console을 정리한 뒤 항목 0개다. Game 씬은 Play Mode가 아닌 상태에서 dirty=false·루트 4개이며 live 조회로 ObstacleView 24개, Rigidbody 25개를 확인했다. 기준 JSON의 23개는 Game.unity diff에 명시적으로 추가된 블록 수이고 live 24/25는 prefab으로 해석된 Obstacle/Ball을 포함한다. Game 씬·Ball/Cube Prefab·PoolConfig의 해시는 활성화 수정 전후에도 동일하다. [최초 컴파일·Editor](Prototype/evidence/raw/physx-lifecycle-editor-compile.json) · [수정 컴파일](Prototype/evidence/raw/physx-activation-fix-editor-compile.json) · [자산 보존](Prototype/evidence/raw/physx-lifecycle-preservation-check.json).

이번 context-save는 context-manifest 84개 경로와 문서 7개의 로컬 링크 151개를 확인해 누락 0개, Game.unity의 기존 사용자 diff를 제외한 현재 변경의 공백 오류 0개다. HANDOFF는 Git 추적 파일이며 ignored가 아니다. 원자료는 `Prototype/evidence/raw/physx-activation-fix-document-check.json`이다.

**High 설정 빌드 검증·Android IL2CPP/AOT·기기 실행은 미실행**이다. 이전의 “High Player”는 별도 Unity 기능명이 아니라 Managed Stripping Level=High로 만든 실제 앱/게임 빌드를 줄여 쓴 표현이었다. 이후에는 **“High 설정 빌드 검증”**으로 쓴다. 현재 주입 멤버 Preserve와 명시적 생성 factory 조치가 있지만 Editor/Play Mode 통과로 실제 빌드의 코드 보존을 확정하지 않는다.

## 다음 작업·미결정과 분담

바로 다음 체크포인트는 Daniel이 수정 반영 뒤 `Tools/Smesh Fest/Validation/PhysX Lifecycle Runtime`을 Play Mode에서 다시 실행하는 것이다. 성공하면 다음 작은 구현은 **W-001의 클릭 위치 → Ball Pool 대여/초기 발사 → 같은 방향으로 Cannon 회전 → 첫 실제 충돌**이다.

- Daniel: 검증 메뉴 실행과 실제 씬·카메라·공/표적 배치·Inspector 값·조작감을 맡는다. 성공 로그나 실패 메시지와 체감을 전달한다. 씬을 다시 만들 필요는 없다.
- AI: Probe 결과를 확인한 뒤 입력 전달·월드 목표점·Pool 대여·발사·Cannon 방향 명령을 기존 DI/MVC/Pool에 작은 파츠로 연결한다. 사용자 Scene/Prefab/PoolConfig는 승인 없이 재생성하지 않는다.
- 현재 사용자 자산 기준: Ball Prefab은 MeshRenderer·SphereCollider·dynamic Rigidbody·BallView, Ball PoolConfig는 Prefab 연결·Min 3·Max 7·200초이고 Game PoolContainer에 등록돼 있다. Cube Prefab은 MeshRenderer·BoxCollider·dynamic Rigidbody·ObstacleView다. Cube PoolConfig의 Prefab은 비어 있고 Game 목록에는 아직 없다.
- 현재 Game 씬의 ObstacleView 24개는 씬 배치 객체라 Pool `OnPoolCreated`를 자동으로 거치지 않는다. native Rigidbody 충돌은 가능하지만 MVC/대여 초기화는 아직 연결되지 않는다. W-001에서 현재 Blocks를 고정 배치로 둘지 Cube PoolConfig로 생성할지 실제 게임 흐름에 맞춰 정한다.
- 후속 세부 계약: 화면별 MVP, HP/파괴/결과, SO 런타임 상태 범위, Addressables 로드/취소/해제 핸들. 대표 PhysX 플레이 뒤 물리 권위 전환과 직접 구현 Physics의 동일 입력/fixed step/초기조건/측정 지표를 별도 문서화한다. O-001~O-008과 R-021~R-023을 처음부터 다시 질문하지 않는다.

## 재개 순서·주요 경로

이 문서 → [ARCHITECTURE](Prototype/ARCHITECTURE.md) → [README](README.md) 및 저장소 스킬/모델 정책 → [BRIEF](Prototype/BRIEF.md)·[DESIGN](Prototype/DESIGN.md) → [PLAN](Prototype/PLAN.md) → [REVIEW](Prototype/REVIEW.md) → [자동 REPORT](Prototype/evidence/REPORT.md). 기준·기획·작업·검증의 원본을 분리하며 과거 시점 기록을 현재 상태로 오독하지 않는다.

주요 코드: `Assets/Scripts/Framework/{Object,Observer,Loop,Flow,DI,Pool,Screen}`, `Assets/Scripts/InGame/{Ball,Obstacle}`. 최신 검증 원본은 `Assets/Scripts/Test/PhysXRuntimeProbe.cs`, 메뉴 연결은 `Assets/Editor/Validation/PhysXValidation.cs`다. 첫 실행의 복합 오류는 두 상태를 분리하도록 보완했다. Play Mode에서 `Tools/Smesh Fest/Validation/PhysX Lifecycle Runtime`을 다시 실행하고 `[PhysXValidation] Passed ... assertions.` 또는 새 첫 오류를 확인한 뒤 Play Mode를 종료한다. 임시 local PhysicsScene만 사용하며 저장 씬을 만들지 않는다. 공통 Pool Runtime의 새 callback 파괴 회귀도 아직 재실행 전이다. DI Pause Runtime은 서비스 종료를 포함하므로 여러 검사를 함께 할 때 마지막에 실행한다. Inspector·씬 재생성 메뉴를 검증 대신 실행하지 않는다.

자동 기록은 `Docs/Work-flow/prototype-workflow/scripts/workflow_log.py`와 `Docs/Prototype/evidence`를 사용한다. REPORT는 직접 수정하지 않는다. 이번 인계 저장의 문서/경로 검사와 이전 Unity 실행 증거를 구분한다.

```context-manifest
{
  "schema_version": 1,
  "saved_at": "2026-09-09T06:35:21Z",
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
    "Assets/Scripts/Test/PoolRuntimeProbe.cs",
    "Assets/Scripts/Test/PhysXRuntimeProbe.cs",
    "Assets/Scripts/Test/PhysXRuntimeProbe.cs.meta",
    "Assets/Editor/Validation/PhysXValidation.cs",
    "Assets/Editor/Validation/PhysXValidation.cs.meta",
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
    "Docs/Prototype/evidence/raw/obstacle-mvc-document-check.json",
    "Docs/Prototype/evidence/raw/physx-lifecycle-baseline.json",
    "Docs/Prototype/evidence/raw/physx-lifecycle-editor-compile.json",
    "Docs/Prototype/evidence/raw/physx-lifecycle-preservation-check.json",
    "Docs/Prototype/evidence/raw/physx-lifecycle-document-check.json",
    "Docs/Prototype/evidence/raw/physx-lifecycle-runtime-validation.json",
    "Docs/Prototype/evidence/raw/physx-lifecycle-runtime-failed-20260909T061136Z.json",
    "Docs/Prototype/evidence/raw/physx-activation-fix-editor-compile.json",
    "Docs/Prototype/evidence/raw/physx-activation-fix-document-check.json"
  ],
  "validation": {
    "scope": "current_context_save",
    "repository_inspected": true,
    "handoff_read": true,
    "tests_run": false,
    "unity_compile_run": true,
    "unity_compile_errors": 0,
    "static_script_diagnostics_run": true,
    "static_script_diagnostic_files": 5,
    "static_script_warnings": 0,
    "static_script_errors": 0,
    "build_run": false,
    "app_run": false,
    "app_run_scope": "Daniel ran the first isolated PhysX probe; Main did not enter Play Mode. The activation-order fix awaits a user rerun.",
    "manual_verification": true,
    "documentation_checks_run": true,
    "prior_results_reviewed": true,
    "ball_mvc_runtime_probe_run": false,
    "obstacle_mvc_runtime_probe_run": false,
    "pool_runtime_probe_rerun": false,
    "physx_runtime_probe_run": true,
    "physx_runtime_probe_result_exists": true,
    "physx_runtime_probe_success": false,
    "physx_runtime_probe_assertions": 10,
    "physx_runtime_probe_simulated_steps": 0,
    "physx_activation_fix_implemented": true,
    "physx_activation_fix_runtime_rerun": false,
    "live_scene_inspected": true,
    "live_scene_dirty": false,
    "live_scene_root_count": 4,
    "live_obstacle_view_count": 24,
    "live_rigidbody_count": 25,
    "previous_runtime_results": {
      "obstacle_mvc_assertions": 25,
      "ball_mvc_assertions": 19,
      "mvc_assertions": 58,
      "pool_assertions": 66,
      "di_pause_assertions": 25
    },
    "git_branch": "MVC-Obstacle",
    "git_head": "ca1c6914da43ff094c47ab8dc8275521b5dfd3cd",
    "upstream_ahead": 0,
    "upstream_behind": 0,
    "worktree_clean_before_context_save": false,
    "preexisting_dirty_paths": [
      "Assets/Scenes/Game.unity"
    ],
    "user_owned_changes_preserved": [
      "Assets/Scenes/Game.unity: Rigidbody and ObstacleView connections"
    ],
    "mvc_binding_implemented": true,
    "ball_mvc_implemented": true,
    "obstacle_mvc_implemented": true,
    "physx_lifecycle_implemented": true,
    "physics_authority": "Unity Rigidbody owns runtime position, rotation, linear velocity, and angular velocity",
    "high_setting_build_run": false
  }
}
```

# 프로젝트 인계

> **2026-09-09 최신 계약:** targetable Obstacle Collider 직접 hit만 Cannon Head Muzzle에서 발사한다. Cannon은 world Yaw만 회전하고 child의 수동 local rotation을 보존한다. Straight는 같은 GameObject의 `ObstacleView` 직접 충돌 또는 FixedTick에서 Ball의 월드 Z가 `BallConfig.GravityActivationWorldZ`(기본 0)를 **엄격히 초과**하는 첫 조건 중 먼저 발생한 때 중력을 켠다. Curve는 고정 비행시간·발사 즉시 중력이다. tag·name·parent 검색 fallback과 legacy 경로는 두지 않는다. Config 기반 CCD와 Ball/Obstacle/Ground 전용 Layer Matrix를 쓴다.

## 목표와 현재 상태

프로젝트: `/Volumes/Dock_SSD/Projects/Smesh-Fest-PhysX`. 선택한 A안은 **Block 영역 클릭으로 목표 선택·Ball 발사 → 파괴 → 결과 → 재도전 + Unity Physics/직접 구현 Physics 비교**다. Cannon은 Ball 진행 방향으로 회전한다. 별도 조준 단계는 없다. **전체 아키텍처를 먼저 정제·제작한 뒤 플레이 기능을 구현한다.**

최신 구현 단위는 **W-001-FLIGHT-PHYSICS-003: CCD·물리 Layer 최적화 + Cannon Yaw-only + Straight 충돌 또는 world-Z 중력 전환**이다. BallConfig/ObstacleConfig가 씬·Pool Rigidbody에 물성을 적용하고 Ball은 `ContinuousDynamic`, Obstacle은 `Continuous`다. Obstacle 8·Ball 9·Ground 10을 사용하며 gameplay 충돌은 Ball↔Obstacle, Ball↔Ground, Obstacle↔Ground, Obstacle↔Obstacle만 켰다. 최신 Unity 스크립트 진단 4개는 warning/error 0, Console error 0이며 최신 Click/PhysX Probe와 실제 Game Play Mode는 사용자 확인 대기다. 과거 PhysX 수명 42개 통과는 최신 계약의 실행 증거로 대신하지 않는다.

현재 브랜치 `PhysX`, 기준 HEAD `4f84c080864e76f9e001200da1d36dc6963cc5e7`. W-001 시작 때 worktree와 Game.unity는 tracked HEAD와 같았고 씬은 dirty=false였다. AI의 최초 씬 저장은 기존 Cannon의 CannonView와 WorldObjects의 WorldPointerInput 및 참조만 추가한 40 YAML 행이었다. 그 뒤 기존 비풀링 `GameObjects/Ball` Prefab 인스턴스가 제거됐고 Daniel이 자신의 작업이라고 확인했다. 이번 fixture 수정 중에는 씬 MeshRenderer 두 곳의 Cast Shadows 변경과 Cube PoolConfig Prefab 연결이라는 별도 저장 변경을 추가로 관측했다. 작성자 의도는 추정하지 않고 모두 보존했다. 2026-09-09T07:54:49Z 확인 시 씬 diff는 +42/-65, SHA `bcf6bdc...`, dirty=false·루트 4개였으며 이후 병행 변경이 계속될 수 있다. 이번 수정은 Test C#과 문서·증거만 편집했고 Scene·Prefab·SO를 저장하지 않았다. AI는 커밋·푸시·브랜치 전환을 하지 않았다.

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
| Ball MVC + PhysX | BallView가 Config 물성·`ContinuousDynamic`을 주입/대여 때 적용한다. Straight는 중력을 끄고 같은 GameObject의 `ObstacleView` 직접 충돌 또는 FixedTick의 엄격한 world-Z 경계 중 먼저 발생한 조건에서 한 번 켠다. Curve는 발사부터 켠다. 반환/폐기 때 속도·중력·대기 상태와 managed 수명을 초기화한다. |
| Obstacle MVC + PhysX | ObstacleView가 Config 물성·`Continuous`를 씬 배치와 Pool clone 모두에 적용한다. GroundSurface 충돌 뒤 targetable을 끄고, 대여/반환의 속도·Sleep/Wake 수명은 유지한다. |
| 클릭 발사 + Cannon | WorldPointerInput이 중앙 UpdateTick에서 Obstacle 전용 mask로 직접 Raycast하고 FixedTick에서 Ball Z 경계를 전달한다. BallView는 같은 GameObject의 `ObstacleView` 직접 충돌만 중력 전환으로 인정한다. ShotDirector는 Muzzle 발사·고정 배열 반환을 맡는다. CannonView는 yawRoot의 world Y만 회전하고 Body/Head의 수동 local rotation은 보존한다. |

View의 비활성 Bind는 모델 참조만 보관한다. 활성화 때 한 번 구독·갱신하고 비활성화 때 기본/추가 모델 구독을 해제하되 모델 참조는 유지한다. Model 교체·Unbind는 옛 연결을 정리한다. 구체 풀링 객체는 반환/폐기에서 Controller의 구독 정리와 View.Unbind를 호출한다. 파생 Unity 수명 콜백은 base를 호출한다. 같은 모델의 재연결도 이전 관찰의 갱신·실패 정리가 새 관찰을 건드리지 않게 보호했다. 같은 모델의 재귀 NotifyChanged는 기존 Observer 계약대로 거절한다.

Ball과 Obstacle의 Model은 대여 여부와 0이 아닌 사용 세대를 관리한다. Rigidbody가 런타임 물리 상태를 소유하며 Controller는 발사/초기화 명령만 내린다. 각 View는 같은 Model/Controller를 재대여하며 관찰은 활성 중 한 번만 연결한다. 씬 계층이 Factory보다 먼저 파괴될 때 Unity OnDestroy가 즉시 묶음을 정리하고 lease도 즉시 무효다. 공통 ObController는 여전히 빈 기반이다. Cannon은 객체 수명 동안 Model/Controller를 한 번 조립하고 Model의 명령 방향만 View에 통지하며 Ball 물리 상태를 복제하지 않는다. Obstacle의 실제 HP·파괴·충돌 규칙·표현은 미구현이며 테스트용 Probe를 실제 게임 흐름으로 해석하지 않는다.

## 검증 증거와 한계

아래의 runtime assertion 수는 시점과 범위가 다른 실행 기록이다. PhysX 수정본은 Daniel 실행으로 통과했다. W-001 Click Launch 1차 실행은 fixture 오류를 확인한 실패 근거이며 발사·충돌 통과로 합산하지 않는다.

| 단위 | 실제 기록 |
|---|---|
| Observer·Loop·GameClock | 명시적 Editor 메뉴에서 수명/상태 검사 통과. 조건을 고정한 반복 구간의 관리 할당 0바이트. 상세 조건은 REVIEW와 각 원자료 참조. |
| DI·UI Pause | Play Mode 25개 검사 통과. Loop·물리·scaled Tween/파티클의 정지/재개와 수명 정리를 포함한다. [결과](Prototype/evidence/raw/di-pause-playmode.json) |
| Pool | 과거 Play Mode 66개 검사 통과. 이번에 callback 중 Unity 객체 파괴를 격리하는 생산 코드와 회귀 2개를 추가했으나 새 전체 Pool Runtime은 아직 재실행하지 않았다. [과거 결과](Prototype/evidence/raw/pool-runtime-validation.json) |
| Model–View | Unity 6000.3.10f1 Play Mode 58개 검사 통과. 활성/교체/해제·훅 예외/재진입, 실제 PoolFactory DI와 묶음 유지/재생성·반환/종료를 임시 객체로 확인했다. [결과](Prototype/evidence/raw/mvc-runtime-validation.json) |
| Ball MVC | Unity 6000.3.10f1 Play Mode **19개 검사 통과**. Controller/PoolLease 정상 반환, 같은 묶음 재대여, 이전 세대 거절, 활성 Pool Dispose와 계층 선파괴 정리를 임시 BallView로 확인했다. [결과](Prototype/evidence/raw/ball-mvc-runtime-validation.json) |
| Obstacle MVC | Unity 6000.3.10f1 Play Mode **25개 검사 통과**. Ball과 같은 수명 경계에 더해 잘못된 lease 준비 실패 롤백, 생성 후 미대여 파괴와 파괴 오류 로그 부재를 임시 ObstacleView로 확인했다. [결과](Prototype/evidence/raw/obstacle-mvc-runtime-validation.json) |
| PhysX 수명 | 1차 Play Mode는 assertion 10·시뮬레이션 0회에서 실패. 활성화 뒤 상태 재적용과 검사 분리 후 Daniel의 수정본 실행이 **42 assertions, 15 simulated steps, contact 1회, Obstacle displacement 0.6153807**로 통과. [통과](Prototype/evidence/raw/physx-lifecycle-runtime-passed-20260909T064144Z.json) · [1차 실패](Prototype/evidence/raw/physx-lifecycle-runtime-failed-20260909T061136Z.json) |
| 클릭 발사 | 1차 격리 Probe는 assertion 5·시뮬레이션 0회에서 경계 fixture 가정 실패. 생산 투영식은 유지하고 Probe만 수정했으며 Unity compiler error 0, 변경 Probe warning/error 0/0. 수정본 재실행과 실제 Game 클릭 대기. [1차 실패](Prototype/evidence/raw/click-launch-runtime-failed-20260909T074802Z.json) · [수정 정적 근거](Prototype/evidence/raw/click-launch-projection-fixture-fix-static.json) |
| 최신 비행 물리 | Config CCD·yaw-only·Straight 직접 충돌 또는 Z 경계와 Layer Matrix를 구현. 최신 Unity 스크립트 진단 4개 warning/error 0, Console error 0. 최신 disposable Click/PhysX Probe와 실제 Game Play Mode는 아직 미실행 |

작업 중 AI의 임시 Editor 정리 코드가 내부 `PhysicsManager` 타입을 잘못 판별해 로드 객체를 과도하게 해제했고 Unity가 Fatal Error로 종료됐다. Daniel이 재실행한 뒤 Game 씬 dirty=false·루트 4개, Console error 0, 컴파일 idle, Obstacle/Ball/Ground 레이어와 Layer Matrix 유지, 임시 `__Codex` 스크립트 부재를 확인했다. 이 사고는 최신 Play Mode 통과 근거가 아니며, 내부 설정 객체를 직접 로드·정리하는 우회 검증은 다시 사용하지 않는다.

MVC 측정은 사전 생성 모델/View/delegate를 100회 워밍업한 후 통지 1,000회와 Unbind/Bind 1,000회를 각각 측정해 0바이트다. 객체 생성·실제 렌더링·Pool 대여/반환·측정 중 로그/assertion은 제외했다. 전체 게임 Zero Alloc이나 성능 향상률의 증거가 아니다. Daniel의 과거 중앙 UpdateLoop 적용 후 약 50% 개선 경험도 이번 프로젝트 측정값이 아니다.

최종 Ball MVC 실행 후 Play Mode는 종료됐고 Game 씬은 dirty=false·루트 4개, Console 오류 0건이었다. domain reload 때 MCP stdio 포트 재연결 경고가 있었지만 결과 파일·Console·씬 상태를 다시 확인했다. 검사 전후 Game.unity, Ball/Cube Prefab, Ball/Cube PoolConfig의 저장 파일 해시는 모두 동일하다. [Ball 보존 결과](Prototype/evidence/raw/ball-mvc-preservation-check.json) · [Ball Editor 종료 기록](Prototype/evidence/raw/ball-mvc-editor-final.json). 앞선 공통 MVC 보존 근거와 과거 실패·보완은 [REVIEW](Prototype/REVIEW.md)에 있다.

최종 Obstacle MVC 실행 후 Play Mode는 종료됐고 Game 씬은 dirty=false·루트 4개, 컴파일/도메인 reload 대기 없음, 종료 후 Console 항목 0개였다. 5개 스크립트 정적 검사는 오류·경고 0건이다. 검사 전후 Game.unity와 Ball/Cube PoolConfig 해시는 동일하다. Ball/Cube Prefab의 Rigidbody 추가는 격리 검사와 무관한 동시 외부 변경으로 분리해 보존했다. [Obstacle 보존 결과](Prototype/evidence/raw/obstacle-mvc-preservation-check.json) · [Obstacle Editor 종료 기록](Prototype/evidence/raw/obstacle-mvc-editor-final.json). 독립 Sol High 검토가 찾은 검사 사각지대 2개를 보완한 뒤 25개로 재실행했다.

이번 PhysX 작업의 최초 및 활성화 수정 Unity 컴파일 뒤 compiler error는 0개다. 수정된 5개 스크립트 진단도 warning 0·error 0이다. Daniel 실행의 통과 스냅샷으로 임시 local PhysicsScene 수명은 완료했다. 뒤이은 W-001 최초 구현도 compiler error 0, 관련 11개 스크립트 warning 0·error 0이었다. 1차 Probe 실패 뒤 test-only 수정 역시 compiler error 0, 변경 Probe warning 0·error 0이다. W-001 직전 scene SHA `b00cdcf...`, 최초 부분 저장 뒤 `7d1a187...`, Daniel의 scene Ball 제거 뒤 `6dbcdba...`였다. 추가 저장 변경을 보존한 2026-09-09T07:54:49Z 관측 SHA는 `bcf6bdc...`, dirty=false·루트 4개였다. [PhysX 통과](Prototype/evidence/raw/physx-lifecycle-runtime-passed-20260909T064144Z.json) · [W-001 최초 정적 확인](Prototype/evidence/raw/click-launch-static-validation.json) · [1차 런타임 실패](Prototype/evidence/raw/click-launch-runtime-failed-20260909T074802Z.json) · [fixture 수정](Prototype/evidence/raw/click-launch-projection-fixture-fix-static.json).

이번 context-save는 context-manifest 84개 경로와 문서 7개의 로컬 링크 151개를 확인해 누락 0개, Game.unity의 기존 사용자 diff를 제외한 현재 변경의 공백 오류 0개다. HANDOFF는 Git 추적 파일이며 ignored가 아니다. 원자료는 `Prototype/evidence/raw/physx-activation-fix-document-check.json`이다.

**High 설정 빌드 검증·Android IL2CPP/AOT·기기 실행은 미실행**이다. 이전의 “High Player”는 별도 Unity 기능명이 아니라 Managed Stripping Level=High로 만든 실제 앱/게임 빌드를 줄여 쓴 표현이었다. 이후에는 **“High 설정 빌드 검증”**으로 쓴다. 현재 주입 멤버 Preserve와 명시적 생성 factory 조치가 있지만 Editor/Play Mode 통과로 실제 빌드의 코드 보존을 확정하지 않는다.

## 다음 작업·미결정과 분담

바로 다음 체크포인트는 Daniel이 Play Mode에서 **(1) `Tools/Smesh Fest/Validation/PhysX Lifecycle Runtime`, (2) `Tools/Smesh Fest/Validation/Click Launch Runtime`, (3) 실제 Game**을 차례로 확인하는 것이다. 실제 Game에서는 얇은 Obstacle을 뚫지 않는지, Cannon의 Y만 바뀌는지, Straight가 같은 GameObject의 Obstacle 직접 충돌 또는 Z>0 뒤 중력을 받되 Z=0에서는 꺼져 있는지, Curve가 발사부터 중력을 받는지 본다.

- Daniel: 두 검증 메뉴와 실제 씬 조작을 실행하고 CCD·Yaw-only·Straight/Curve 중력 시점을 판단한다. 제거한 Cannon Collider와 Ball Prefab `Use Gravity=false`는 그대로 유지한다.
- AI: 결과가 실패면 첫 assertion/Console과 실제 장면을 기준으로 최소 수정한다. 둘 다 통과하면 다음 계약으로 고정 Blocks의 HP/파괴·결과·재도전 수명을 설계한다. 발사 속도 10, 머즐 offset 0.16, 목표 로컬 center (0, 0.225)/extents (0.225, 0.3), 반환 4초/y=-1은 첫 체감 조정값이며 Inspector에서 바꿀 수 있다.
- 현재 관측 자산 기준: Ball Prefab은 MeshRenderer·SphereCollider·dynamic Rigidbody·BallView, Ball PoolConfig는 Prefab 연결·Min 3·Max 7·200초이고 Game PoolContainer에 등록돼 있다. Cube Prefab은 MeshRenderer·BoxCollider·dynamic Rigidbody·ObstacleView다. Cube PoolConfig의 Prefab 참조는 별도 저장 변경으로 연결됐지만 Game 목록에는 아직 없다. 이 변경의 작성자 의도는 추정하지 않는다.
- 현재 Game 씬의 ObstacleView 24개는 Pool `OnPoolCreated`를 거치지 않지만 GameLifetimeScope가 Blocks 하이어라키에 ObstacleConfig를 주입한다. HP·파괴·재도전용 MVC 수명 연결은 여전히 후속이다.
- 후속 세부 계약: 화면별 MVP, HP/파괴/결과, SO 런타임 상태 범위, Addressables 로드/취소/해제 핸들. 대표 PhysX 플레이 뒤 물리 권위 전환과 직접 구현 Physics의 동일 입력/fixed step/초기조건/측정 지표를 별도 문서화한다. O-001~O-008과 R-021~R-023을 처음부터 다시 질문하지 않는다.

## 재개 순서·주요 경로

이 문서 → [ARCHITECTURE](Prototype/ARCHITECTURE.md) → [README](README.md) 및 저장소 스킬/모델 정책 → [BRIEF](Prototype/BRIEF.md)·[DESIGN](Prototype/DESIGN.md) → [PLAN](Prototype/PLAN.md) → [REVIEW](Prototype/REVIEW.md) → [자동 REPORT](Prototype/evidence/REPORT.md). 기준·기획·작업·검증의 원본을 분리하며 과거 시점 기록을 현재 상태로 오독하지 않는다.

주요 코드: `Assets/Scripts/Framework/{Object,Observer,Loop,Flow,DI,Pool,Screen}`, `Assets/Scripts/InGame/{Ball,Obstacle,Cannon,Shot}`. PhysX 수명 검증은 `Assets/Scripts/Test/PhysXRuntimeProbe.cs`와 `Assets/Editor/Validation/PhysXValidation.cs`, 최신 클릭 발사 검증은 `Assets/Scripts/Test/ClickLaunchRuntimeProbe.cs`와 `Assets/Editor/Validation/ClickLaunchValidation.cs`다. Play Mode에서 `Tools/Smesh Fest/Validation/Click Launch Runtime`을 실행해 `[ClickLaunchValidation] Passed ... assertions.` 또는 첫 오류를 확인한다. 임시 local PhysicsScene만 사용하며 저장 씬을 만들지 않는다. 그 뒤 실제 Game 화면에서 Block 영역을 클릭한다. 공통 Pool Runtime의 새 callback 파괴 회귀는 아직 재실행 전이다. DI Pause Runtime은 서비스 종료를 포함하므로 여러 검사를 함께 할 때 마지막에 실행한다. Inspector·씬 재생성 메뉴를 검증 대신 실행하지 않는다.

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

## W-002-LEVEL-EDITOR-001 — 재개 주의점

신규 `LevelConfig`·명시 Capture/Bake EditorWindow·명시 `LevelSpawner.TrySpawn/ReturnAll`이 있다. 자동 Spawn/Bake, Scene·Prefab·기존 config 변경, old Blocks 제거는 없으며 spawn은 기존 PoolFactory 대여가 하나라도 실패하면 보유 lease를 원자적으로 반환한다. Blocks 기대 Capture=24지만 Cube MaxPool=8·Factory catalog 미등록이라 Bake/spawn은 의도적으로 막힌다.

신규 스크립트 4개 정적 진단 warning/error 0·Console error 0이다. `Tools/Smesh Fest/Validation/Level Editor and Spawn`과 실제 Bake는 미실행이다. 다음은 사용자 승인 뒤 capacity/catalog 및 authored Blocks→runtime 전환을 수동 통합·검증하는 단계다.

## 최신 인계 — Level1 Bake 및 런타임 전환 (정적 완료 / 런타임 미검증)

- Cube PoolConfig `MaxPool=100`; `Assets/Project/Level/Level1.asset` Bake 완료, Cube 24개 항목(순서·local TRS).
- Game 씬: `RuntimeBlocks`는 authored `Blocks` sibling이며 local position `(0, 0.29, 0)`, authored Blocks와 local TRS 일치. `WorldObjects`의 `LevelSpawner`·`LevelSession` 및 `PoolContainer`의 Ball·Cube 연결을 보존한다.
- `GameFlow`는 `LevelSession.TryStart` 성공 뒤에만 loop를 시작한다. authored Blocks는 runtime에서 비활성화하고 시작 실패 또는 `ReturnAll`에서 복원한다. fallback/parallel pool 없음. `LevelSession`은 nested/ancestor root를 거절한다.
- 5개 스크립트 Unity 정적 진단 warning/error 0, Console 0. Level Editor validation 메뉴와 Play Mode는 아직 실행하지 않았으므로 런타임 동작은 미검증이다.

2026-09-10 첫 Level Editor validation은 Edit Mode preview scene에서 일반 `MonoBehaviour.OnEnable` 관측을 요구해 실패했지만, 사용자는 Play Mode에서 `RuntimeBlocks` 생성을 확인했다. 생산 Pool 순서는 변경하지 않았다. 검증 fixture만 `OnPoolRent` 시 inactive+world position/rotation, `TrySpawn` 완료 뒤 active+최종 local TRS를 확인하도록 수정했으며 Unity 정적 진단과 Console은 0이다. 수정 후 validation 재실행 결과는 대기 중이다.

## 최신 인계 — Ground Fade 반환 (정적 완료 / Play Mode 확인 대기)

- `GroundFadeReturn`이 Ball/Cube의 `GroundSurface` 직접 충돌을 받아 Config 기준 1초 대기 후 1초 선형 Fade하고 현재 PoolLease를 한 번 반환한다.
- `Assets/Project/Config/GroundFadeConfig.asset`에서 두 시간을 조정한다. Ball/Cube Prefab Renderer는 시작부터 각 GroundFade URP/Lit Transparent Material을 사용하고 `fadeMaterial`도 같은 자산을 참조한다. Fade는 `_BaseColor.a` PropertyBlock만 낮추며, 반환·재대여 때 원래 property block을 복구한다. legacy/fallback은 없다.
- alpha 값만 감소하고 화면은 opaque로 남던 원인은 빈 material-index-0 PropertyBlock이 renderer-level block보다 우선한 것이었다. 빈 원본 block은 이제 `null`로 제거한다.
- built-in URP/Lit는 Transparent Material을 검증할 때 ShadowCaster pass를 다시 끈다. 따라서 Material YAML을 직접 고치는 방식은 사용하지 않고, `GroundFadeReturn.OnPoolCreated()`가 공유 Fade Material의 `ShadowCaster` pass를 활성화하고 실패 시 즉시 예외를 낸다. 두 Prefab Renderer의 Cast Shadows는 켜져 있다. 실제 그림자는 Play Mode 재확인 대기다.
- `ShotDirector`는 대기/Fade 중인 Ball을 lifetime·y 경계로 먼저 반환하지 않는다. Game 씬 `GameLifetimeScope.groundFadeSettings` 참조와 두 Prefab 컴포넌트 연결은 재로딩 뒤 확인했다.
- Unity compile·관련 진단·Console은 오류 0, Sol High 최종 P0/P1 없음. Play Mode에서는 `Ground 충돌 → 1초 유지 → 1초 Fade → 반환`, 재대여 복구, 여러 Cube 겹침 표현을 Daniel이 확인해야 한다.

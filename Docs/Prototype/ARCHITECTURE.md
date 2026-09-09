# 기본 아키텍처 — 확정 기준

상태: **사용자 기준 확정 / 세부 설계·아키텍처 제작 진행 중**. 결정일: 2026-09-08. Observable·LoopDispatcher·VContainer·게임 시간/UI Pause·Pool·공통 Model–View와 Ball/Obstacle 객체별 MVC에 이어 첫 Unity PhysX 수명 연결을 구현했다. 2026-09-09 Play Mode의 이전 Ball 19개·Obstacle 25개는 과거 수명 결과다. 새 PhysX 충돌 Probe의 1차 사용자 실행은 시뮬레이션 전 Sleep/Wake 검사에서 실패했고 활성화 순서 수정·컴파일 뒤 재실행 대기다. MVP·SO/Addressables 수명 연결과 High 설정 빌드 검증은 후속이며 단위별 실행 결과는 REVIEW에 남긴다.

출처: Daniel이 “내가 만드는 게임들은 대부분 아래 설명한 아키텍처 기반으로 작동해”라고 제시한 1–8번과 “문서에 기록하여 다음부터 질문하지 않도록” 요청한 메시지, 이후 “추가로 나는 DI 의존성 주입으로 코드 작성을 해”라는 추가 기준과 “Code Stripping을 High로 하기 때문에 관리도 해야해”라는 후속 기준. 이 문서는 해당 기준의 단일 원본이다.

## 다음 작업에서 적용할 규칙

- 아래 10가지는 AI 제안이나 승인 대기 항목이 아니다. **이미 정한 기본값으로 적용하고 같은 선택을 다시 묻지 않는다.**
- 현재 순서는 아키텍처 정제 → 합의한 아키텍처 제작 → A안 플레이 기능 제작이다. 작은 기능을 먼저 만들기 위해 이 순서를 건너뛰지 않는다.
- 사용자가 바꾸겠다고 하지 않는 한 기준을 유지한다. 현 구현과 충돌하면 구체적인 충돌 지점만 설명하고 그 부분을 함께 다듬는다.
- 구현 여부와 기준 채택 여부를 구분한다. 코드가 빈 골격이거나 패키지가 아직 없다는 이유로 이 기준의 채택을 재질문하지 않는다.
- R-019: ProjectTemplate의 MVC는 설계 의도를 확인하는 참고다. 현재 프로젝트에 필요한 책임부터 간소화해 단계적으로 연결하며, 참고 전체를 같은 상속·모듈 구조로 복제하지 않는다.

## 10개 확정 기준

| ID | 사용자 기준 | 설계·리뷰에 적용할 내용 |
|---|---|---|
| A-01 | 단일 책임 원칙과 작은 파츠 | 하나의 스크립트에 전체 기능을 몰아넣지 않는다. 각 부분의 책임을 작게 나누고 조합한다. |
| A-02 | 중앙 UpdateLoop | 게임 시간을 중앙에서 관리하고 Update·Fixed·Late 등의 Loop 이벤트를 발행한다. 각 대상은 필요한 이벤트를 구독해 동작한다. |
| A-03 | 월드 오브젝트는 MVC | 월드 오브젝트의 Model·View·Controller 역할을 분리한다. 구체적인 상태 소유와 참조 방향은 기존 구조를 바탕으로 다듬는다. |
| A-04 | UI 객체는 MVP | UI의 Model·View·Presenter 역할을 분리한다. 화면 처리에 월드 MVC를 일괄 적용하지 않는다. |
| A-05 | Pool + Factory + Observer | 오브젝트는 Pool에서 제공하고 생성에 Factory, 관찰·통지에 Observer 패턴을 적용한다. 대여·반환·정리 시 Tween, 활성 상태 등 초기화와 파티클 정리를 필수로 다룬다. |
| A-06 | Zero Alloc 지향 | UniTask, TMP의 text.SetText 등을 사용한다. 반복 실행 구간에서 불필요한 할당을 줄이며 실제 할당량으로 확인한다. |
| A-07 | ScriptableObject 활용 | SO를 아키텍처의 데이터 자산으로 활용한다. 구체적인 용도·스키마·런타임 변경 정책은 세부 설계에서 정한다. |
| A-08 | Addressables 메모리 관리 | Addressables를 활용해 리소스 수명과 메모리를 관리한다. Pool의 오브젝트 수명과 로드 자산의 수명·해제 책임을 연결한다. |
| A-09 | DI 의존성 주입 | 필요한 의존성을 외부에서 주입받도록 코드를 작성한다. 객체의 역할과 의존 관계를 명확히 드러내며, 이 방식을 기본 아키텍처에 적용한다. |
| A-10 | High Code Stripping 대응 | Managed Stripping Level High를 전제로 설계한다. DI·리플렉션·동적 생성/로딩에 필요한 타입과 멤버의 보존을 관리하고, 실제 대상 Player 빌드에서 동작을 검증한다. |

## 초기화·정리에서 검토할 상황

아래는 A-02·A-05·A-06·A-08을 구현할 때 확인할 항목이다. 특정 인터페이스명이나 호출 순서를 이미 확정했다는 뜻은 아니다.

- 풀에서 다시 꺼낸 객체가 이전 위치·활성 상태·게임 상태를 잘못 이어받지 않는가.
- 반환된 객체의 Tween이나 완료 콜백이 다음 대여 상태를 건드리지 않는가.
- 파티클과 필요한 하위 효과가 반환·정리 후 남지 않는가.
- Loop·Observer 구독이 대여마다 중복되거나 반환 뒤 계속 호출되지 않는가.
- 이전 사용의 비동기 작업이 반환·재사용 후 완료되어 새 상태를 덮지 않는가.
- 사용 중인 객체 또는 풀에 보관된 객체가 필요한 Addressables 자산을 너무 일찍 해제하지 않는가. 풀 자체를 정리할 때 로드 참조가 누락되지 않는가.

## 확정하지 않은 세부 설계

채택 여부를 다시 묻지 않고, 실제 구현에 필요한 세부 계약만 순서대로 다듬는다. 지금 한 번에 모두 답해야 하는 질문 목록이 아니다.

- 게임 일시정지·UI 시간은 아래 R-013 계약으로 확정했다. Observer의 발행 중 구독 변경 처리는 PLAN의 현재 구현 계약을 따른다.
- MVC/MVP의 생성·연결·해제 책임과 참조 방향.
- DI는 설치된 VContainer 1.19.0과 현재 씬 GameLifetimeScope를 사용한다. Pool 재사용·자산 해제와 스코프의 연결은 후속 세부 설계다.
- Pool의 대여·반환·폐기 단계와 Tween·파티클·비동기 작업·구독의 정리 책임.
- SO의 용도와 런타임 상태 보관 범위.
- Addressables 로드 핸들 소유자, 풀과의 수명 관계, 오류·취소·최종 해제 처리.

## High Stripping 관리 기준

[DECISION:user / A-10] High를 기본으로 적용하며 사용 여부를 다시 묻지 않는다. 오류를 가리기 위해 Stripping 수준을 임의로 낮추지 않는다.

- DI가 리플렉션으로 찾는 생성자·주입 멤버, 동적으로 생성하는 타입 등 정적 분석에서 사용이 드러나지 않는 경로를 확인한다. 사용하는 DI 도구가 정해지면 해당 도구의 코드 생성·보존 지원을 함께 확인한다.
- 필요한 범위에 `UnityEngine.Scripting.Preserve` 또는 `link.xml`을 적용한다. 어셈블리 전체 보존을 기본값으로 두지 않고 대상·보존 이유·확인 경로를 기록한다.
- 타입에 `[Preserve]`를 붙였다고 모든 멤버가 보존된다고 가정하지 않는다. 필요한 생성자·메서드·필드의 실제 보존 범위를 확인한다. [Unity 코드 보존 문서](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping-preserving.html)
- Factory 등록, DI 구성, Addressables 콘텐츠나 리플렉션 사용 경로가 바뀌면 기존 보존 규칙이 충분한지 검토한다. 패턴이나 패키지 이름만 보고 보존 완료로 판단하지 않는다.
- 대상 플랫폼·Scripting Backend·High 설정을 기록한 Player 빌드에서 시작 시 DI 구성, 객체 생성·풀 재사용, 사용 중인 동적 콘텐츠 로드와 콜백 경로를 실행한다. Editor/Play Mode 통과만으로 Stripping 대응을 완료 처리하지 않는다. [Unity High 설정과 검증 지침](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping-configure.html)
- IL2CPP를 사용하는 대상에서는 제네릭 AOT 코드 생성 문제와 제거된 코드의 보존 문제를 구분한다. 보존 어노테이션만으로 모든 AOT 경로가 해결됐다고 기록하지 않는다.

현재 GameFlow·UpdateLoop·ScreenPauseScope의 주입 메서드에 `[Inject, UnityEngine.Scripting.Preserve]`를 적용했다. GameClock·LoopDispatcher·UnityGameTime은 명시적 생성 factory로 등록해 생성 경로를 드러낸다. 어셈블리 전체 보존이나 `link.xml`은 추가하지 않았다. 실제 High Player 빌드·IL2CPP/AOT 검증은 미실행이며 이 소스 조치만으로 대응 완료라 하지 않는다.

## VContainer와 UI Pause 계약 — R-013

[DECISION:user] Daniel이 VContainer 설치를 완료했다. UI에는 별도 시간 관리자를 만들지 않고 게임 시간과 분리하며, 열려 있는 동안 게임을 Pause한다. 같은 선택을 재질문하지 않는다.

[DECISION:agent / W-000-DI-PAUSE-001] 현재 단일 게임 씬의 조립 위치는 기존 WorldObjects에 붙는 `GameLifetimeScope`다. UI 루트 참조는 기존 UI(HUD)를 가리키고 그 자식에 주입한다. 이후 동적으로 만든 UI는 Factory에서 주입한 뒤 사용해야 한다. 기존 WorldObjects·UI 배치와 Inspector 수동값을 우선한다.

| 부분 | 하나의 책임 |
|---|---|
| `GameLifetimeScope` | VContainer 등록·기존 컴포넌트/UI 주입·씬 서비스 소유 |
| `GameFlow` | 게임 시작·중단 의도 |
| `GameClock` / `IGamePause` | 요청 배율·실행 의도·UI별 Pause 소유자 관리 |
| `UnityGameTime` | 유효 배율을 Unity timeScale에 적용하고 scope 종료 시 이전 값 복원 |
| `UpdateLoop` → `LoopDispatcher` → `ILoopEvents` | Unity 프레임을 받아 필요한 구독자에 전달 |
| `ScreenPauseScope` | 화면 루트 활성화/비활성화·파괴와 Pause 요청 수명 연결 |

- 열린 화면마다 자신의 Pause 요청을 보유한다. 같은 화면의 중복 요청은 한 번으로 취급한다. 둘 중 하나만 닫으면 계속 Pause이며 마지막 화면을 닫으면 보관한 요청 배율로 돌아간다.
- 게임 Stop과 UI Pause는 별개다. 게임을 중단한 뒤 UI를 닫아도 Start를 대신 호출하지 않는다. Pause 중 배율을 바꿔도 0을 유지하고 재개 시 새 요청 배율을 적용한다.
- `ScreenPauseScope`는 실제 여닫는 화면 루트에 붙인다. 현재 비어 있는 상시 HUD Canvas에는 자동으로 붙이지 않는다. 화면을 SetActive로 열고 닫으면 요청을 획득·해제하며, 파괴 시에도 해제한다. CanvasGroup만 숨기는 화면은 이후 Presenter에서 동일 수명을 연결해야 한다.
- UI의 새 Clock/Loop는 없다. UI 입력·표시는 게임 Loop 구독에 의존시키지 않는다. 물리와 게임 연출은 scaled time을 사용한다. 게임 Tween의 independent update나 ParticleSystem의 useUnscaledTime을 켜면 이 Pause 계약 밖에서 계속 움직이므로 게임 효과에는 적용하지 않는다.
- `fixedDeltaTime`은 기존 값을 유지해 물리의 게임 시간 기준 고정 간격을 보존한다. UpdateLoop에는 이미 scaled된 delta를 넘긴다. [Unity timeScale](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-timeScale.html)
- UpdateLoop 컴포넌트 비활성화는 전달기만 멈추고 GameFlow의 실행 의도를 지우지 않는다. 재활성화 시 현재 clock 상태를 따라 전달을 복구한다. 게임 자체의 Stop은 GameFlow/명시적 StopLoop가 담당한다.
- GameClock은 메인 스레드 동기 상태다. StateChanged 예외는 상태 변경 후 호출자에게 전파한다. 핸들러는 예외를 던지지 않아야 하며 이후 핸들러 실행까지 보장하는 fault isolation은 제공하지 않는다.
- 지금은 단일 GameLifetimeScope가 Unity 전역 시간을 소유한다. 여러 게임 scope를 가산 로드하거나 전역 시간 소유자를 추가할 때는 수명 계약을 다시 설계한다. 화면 여러 개를 열기 위해 새 게임 scope를 만들지 않는다.

## Pool/Factory 수명 계약 — R-015·R-016

상태: **사용자 계약 반영 / W-000-POOL-001 기반 구현·Play Mode 66개 검사 통과**. 최초 조사는 빈 Pool/MVC 골격을 기준으로 했고, R-018까지 반영한 최종 구현을 검증했다. 이후 Ball의 물리 비종속 MVC 수명 연결까지 적용했으며 Addressables와 물리 상태 연결은 후속이다.

[DECISION:user / R-015] O-006·O-007 답변을 받았다. 아래 내용을 다음 작업에서도 유지하며 재질문하지 않는다.

1. [R-018 정정 우선] `ObView`는 독립적으로 사용할 수 있다. 풀링 대상 View만 `ObView`를 상속하고 `IPoolable` 인터페이스를 구현한다. R-015의 Poolable 상속 설명보다 후속 정정을 우선한다.
2. `PoolContainer`에 풀링 대상을 모으고, SO `PoolConfig`로 Pool Root Name·MinPool·MaxPool 등을 설정한다.
3. Factory가 사전 설정에 따라 풀을 생성한다.
4. 월드 풀 객체는 이벤트를 구독하는 MVC 묶음으로 구성한다. `BallView`는 R-018·R-021에 따라 `ObView<BallModel>, IPoolable`이며 Model/Controller를 풀 인스턴스 수명 동안 유지한다.
5. Model·Controller는 재사용할 수 있으나, 상태와 객체 종류에 따라 재사용 또는 재생성을 선택한다. 전 타입의 영구 재사용을 강제하지 않는다.
6. Factory로 꺼낼 때 초기화하고 반환할 때 다시 초기화한다.
7. 같은 스테이지 재도전·다음 스테이지에서 풀을 유지하고, 게임 씬을 종료하여 로비로 돌아갈 때 정리한다. 5분 이상 미사용 객체도 정리한다.

[DECISION:user / R-016] **반환 대기 시간도 PoolConfig에서 객체 종류별로 설정한다.** 5분은 초기 기본값이며 고정 상수가 아니다.

[DECISION:user / R-018] “ObView는 단독일 수도 있고 … Pooling을 사용하는 객체라면 OBView를 상속 받고 IPoolable을 인터페이스로” — **ObView의 풀 의존성을 없앤다.** `ObView : MonoBehaviour`, `BallView : ObView, IPoolable`이 현재 계약이다. 모든 ObView에 풀링을 강제한 이전 AI 상속 구현은 철회했다. 같은 선택을 다시 묻지 않는다.

[DECISION:agent] `IPoolable`은 자신의 GameObject와 생성·대여·반환·폐기 계약을 제공한다. 공통 상태 전이·활성화·파츠 호출은 Pool 소유의 `PoolLifecycleRunner`가 담당한다. 강제 Poolable 기반 클래스는 두지 않는다. `PoolLease`가 한 번의 사용 번호를 가지며 이전 사용의 반환을 거절한다. `PoolConfig`는 직렬화를 위해 MonoBehaviour 참조를 저장하고 생성 전에 IPoolable 구현을 검사한다. `ReturnDelaySeconds`는 초 단위·기본 300, 0은 자동 정리 끄기다. MinPool은 사전 생성 수이자 비활성 정리의 보관 하한, MaxPool은 활성+비활성 총 보유 상한이다. 정리만으로 재고를 추가 생성하지는 않는다. 설정 오류는 SO 값을 고쳐 숨기지 않고 검출한다.

[DECISION:user / R-017, O-008 해결] “비활성 재고 정리시 Minpool 만 남기고 정리” — 자동 시간 정리는 **비활성 재고만 대상으로 하고 비활성 보관 수가 MinPool 아래로 내려가지 않게 한다.** 활성 객체는 시간만으로 강제 반환하지 않는다. 게임 씬의 Factory는 `retainMinimum: true`로 등록한다. 코어의 하한 선택 인자는 검증용으로 남지만 실제 운영 정책은 MinPool 유지다.

```mermaid
stateDiagram-v2
    보관중 --> 대여준비: 꺼내기
    대여준비 --> 사용중: 데이터와 참조 준비 완료
    사용중 --> 반환중: 사용 종료 표시
    반환중 --> 보관중: 구독과 작업, 효과 정리 완료
    보관중 --> 폐기: 풀 수명 종료
```

| 단계 | 지켜야 할 경계와 제안한 책임 |
|---|---|
| 최초 준비 | Factory가 생성·DI·타입 연결을 조립한다. 사용자 프리팹/씬 값을 출발점으로 삼고 필요한 참조를 미리 확보한다. DI 완료 전 활성화 콜백이 게임 로직을 시작하지 않게 한다. |
| 대여 | Pool이 사용 가능한 객체를 구분한다. 선택된 재사용 단위의 담당 파츠가 이번 사용의 데이터·위치·물리 상태를 준비한 뒤 노출한다. 구독은 한 번만 연결한다. |
| 반환 시작 | 중복 반환·다른 풀로 반환·이전 사용의 콜백을 먼저 거절한다. 사용 번호 등으로 현재 대여를 구분하고, 정리 콜백보다 먼저 사용 종료 상태로 전환한다. |
| 반환 정리 | 각 파츠가 자신이 만든 Loop/Observer 구독·비동기 작업·Tween·파티클·잔상을 정리한다. Pool 공통부가 Ball의 HP/속도 등 게임별 필드를 직접 알지 않게 한다. 정리 완료 뒤 비활성 보관하고, 실패한 객체는 정상 재고에 섞지 않는다. |
| 최종 폐기 | 게임 씬 종료 시 새 대여를 막고 사용 중/보관 중 객체와 진행 중 생성을 정리한다. 생성 방식에 맞는 해제 경로를 사용하며 로드 자산을 사용 객체보다 먼저 해제하지 않는다. |

Model·Controller의 유지/재생성은 R-015대로 객체별 조립 파츠가 맡는다. `IPoolable`과 `IPoolLifecycle`의 생성·대여·반환·폐기 훅으로 연결하고, 풀 공통부는 게임별 모델 데이터를 소유하지 않는다. 묶음 재사용의 초기화 책임과 논리 객체 재생성의 반복 비용을 실제 객체별로 확인한다.

Factory는 비활성 부모 아래에 복제한 뒤 DI를 주입한다. VContainer의 원본 활성화 토글 경로를 사용하지 않으며 원본 Prefab의 활성값을 수정하지 않는다. 재대여는 이미 같은 scope에서 주입한 인스턴스를 사용한다. 풀에서 만든 복제물·루트의 최종 파괴 책임은 Factory에 있다.

미사용 검사 기반은 Factory 하나의 UniTask 유지보수 루프와 실제 경과 시간이다. 1초마다 검사하므로 설정한 시간이 지난 뒤 다음 검사에서 정리된다. UI Pause 중에도 비활성 메모리를 정리하기 위한 루프이며 월드 동작은 기존 중앙 UpdateLoop를 계속 사용한다. 새 UI Clock이나 객체별 Update는 만들지 않는다. Factory Dispose는 이 루프를 먼저 취소한다. 현재 자산 공급은 PoolConfig의 직접 Prefab 참조이며, Addressables 핸들 로드·해제 연결은 후속 단위다. 객체 Destroy를 Addressables 자산 해제 완료로 기록하지 않는다.

정리 구현에서 확인한 근거:

- DOTween의 Kill(false)는 완료 지점으로 이동시키지 않지만 OnKill 자체는 호출될 수 있다. 재활용 Tween의 예전 참조도 주의해야 한다. 따라서 단순 Kill 호출만으로 이전 사용의 영향이 사라졌다고 보지 않고, 사용 종료 표시·참조 정리·콜백 소유권을 함께 다룬다. [DOTween 공식 문서](https://dotween.demigiant.com/documentation.php).
- ParticleSystem.Stop의 기본 정지는 방출 중단이며, 즉시 풀 보관 시 기존 입자까지 없애려면 StopEmittingAndClear와 자식 포함 처리를 구분해야 한다. 잔여 효과를 계속 보여줄지는 게임별 효과 수명이며, 효과가 살아 있는 객체를 정리 완료 재고로 취급하지 않는다. [Unity Stop](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ParticleSystem.Stop.html), [StopEmittingAndClear](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ParticleSystemStopBehavior.StopEmittingAndClear.html).
- UniTask와 DOTween 파일은 모두 있지만 현재 로드된 어셈블리에서 DOTweenAsyncExtensions를 찾지 못했다. 설치 소스는 UNITASK_DOTWEEN_SUPPORT 조건부다. 확장 메서드 사용 가능을 가정하지 않고 실제 연결 시 활성화/컴파일을 확인한다. 이 조사에서 define·패키지는 변경하지 않았다.
- VContainer 1.19.0의 Instantiate 경로에는 원본의 활성 상태를 일시 변경하고 복제·주입 뒤 복원하는 구현이 있다. Pool Factory는 사용자 자산 값을 쓰지 않는 생성 경로와 DI/활성화 순서를 실제 실행으로 확인해야 한다. 재대여마다 자동 Inject되거나 scope가 모든 복제물을 자동 Destroy한다고 가정하지 않는다.

함께 확인할 첫 결과: 같은 테스트 객체를 두 번 사용해도 첫 사용의 Tween·입자·콜백이 남지 않고, 중복 반환으로 두 번 꺼내지지 않는가. 추가 검증은 Pause 중 비활성 정리, 정리 중 재진입, 초기화/정리 실패, 풀 종료, 준비 이후 반복 대여/반환의 관리 할당을 포함한다. R-022에 따라 실제 Ball 위치·속도·충돌 초기화는 물리 권위를 정하는 후속 단위에서 확인한다. High 설정 빌드에서는 실제 선택한 Factory 생성·DI·재대여 경로를 별도로 확인한다. 실행 결과는 REVIEW에 기록한다.

## MVC 참고와 간소화 적용 기준 — R-019

[DECISION:user / 2026-09-09 KST] Daniel이 `/Volumes/Dock_SSD/Projects/ProjectTemplate/Assets/Framework/MVC`를 참고로 제공하고 “똑같이 만들필요는 없지만 지금 프로젝트에는 간소화해서 붙여나가야” 한다고 정했다. 최신 R-018의 **독립 ObView + 필요한 View만 IPoolable**을 우선한다. 참고를 제시한 것이 기존 아키텍처 결정을 취소하거나 프레임워크 전체 이식을 요청한 것은 아니다.

[FACT / W-000-MVC-REFERENCE-001] Main은 참고 Core·Observer·BaseView와 CellModel/CellView·BoardBuilder의 연결을, Terra·Medium은 Pool·DI와 LivesView의 실제 대여/반환 호출을 읽었다. 소스 비교 결과이며 참고 프로젝트의 빌드·실행 품질을 검증한 결과가 아니다.

| 비교 지점 | ProjectTemplate에서 확인한 구조 | 현재 Smesh와 적용 방향 |
|---|---|---|
| Model 변경 → View 갱신 | `CellModel : Observable`이 상태 변경 후 Raise. `BaseView<TModel>.Model` 대입으로 구독·RefreshView 연결, 교체·비활성화·반환 때 해제. | 비교 당시 Observable<T>는 일반 이벤트 전달기이며 ObModel·ObController·ObView의 모델 연결은 골격이었다. 이후 W-000-MVC-001에서 기존 전달기를 재사용해 아래의 모델 통지·View 연결을 구현했다. |
| View와 Pool 관계 | `BaseView<TModel> : PoolableView`로 모델을 사용하는 View의 기반이 Pool 수명도 가진다. | R-018 유지. 모델 연결은 ObView 쪽 책임, 풀링은 필요한 구체 View의 IPoolable 계약이다. 참고의 상속 체인을 그대로 가져오면 이 결정과 충돌한다. |
| 기동·DI 조립 | SO Modular와 ModuleContainer가 순서/단계별 비동기 초기화, 모듈별 VContainer 등록, 지속 수명을 관리한다. | 기존 씬 GameLifetimeScope와 명시적 Factory 조립을 유지한다. 이 단위에 전역 ModuleContainer·자동 타입 탐색·초기화 속성 체계를 추가하지 않는다. SO 데이터 설정 사용은 A-07대로 계속 적용한다. |
| Pool 사용·정리 | 타입별 Pool에서 View를 직접 Get/Return. PoolableView가 공통 Tween·구독 정리도 담당한다. | 기존 PoolLease·수명 훅·PoolConfig 반환 시간·비활성 MinPool 정책을 유지한다. MVC는 해당 훅에 조립/해제를 연결하고 Pool 내부에 게임 상태를 넣지 않는다. |

[DECISION:agent] 간소화는 책임을 한 스크립트에 합치는 것이 아니라, 현재 사용하는 연결만 만드는 것으로 적용한다. 별도의 Observer 구현을 복제하지 않고 검증된 `Framework.Observer.Observable<T>`를 재사용한다. Controller는 입력·Loop를 받아 Model의 상태 변경을 요청하고, View는 Model을 관찰해 표현한다. 의존성은 구체적인 조립 코드에서 주입한다. 공통 Controller 기반의 구체 API와 객체별 상태 필드는 실제 첫 연결에 필요한 만큼 정한다.

W-000-MVC-001로 구현·검증한 최소 MVC 단위의 설계:

1. Model이 상태를 바꾼 뒤 변경을 통지한다. View는 처음 연결할 때 현재 상태를 표시하고, 이후 통지를 받아 갱신한다.
2. Model 교체·View 비활성화·명시적 연결 해제에서 이전 구독을 정리한다. 다시 활성화할 때 최신 상태를 표시하고 구독을 중복시키지 않는다.
3. 풀링 객체의 반환 훅에서 View 연결과 Controller가 소유한 구독을 해제한다. Model/Controller 자체의 유지·재생성은 R-015대로 객체별로 정하며, 공통부가 일괄 Dispose하거나 재생성하지 않는다.
4. 먼저 최소 테스트 객체에서 `상태 변경 → 표현 갱신 → 반환 후 통지 없음 → 재대여 시 한 번만 갱신`을 확인한다. Ball의 발사 상태·물리·Cannon 회전은 이 기반 확인에 넣지 않는다.

이 방식은 현재 씬에서 관계와 종료 책임을 직접 추적하기 쉽다. 반대로 여러 씬이 공유하는 모듈의 비동기 초기화 순서가 실제 요구가 되면 참고의 ModuleContainer 방식이 유용할 수 있다. 그 필요가 생길 때 확장하며, 지금 명시적 조립을 택했다는 이유로 SO·Addressables 채택을 미루거나 취소하지 않는다. 명시적 타입 참조 역시 High Player 검증을 대신하지 않는다.

참고 원본(다른 로컬 프로젝트): [BaseView](</Volumes/Dock_SSD/Projects/ProjectTemplate/Assets/Framework/MVC/View/BaseView.cs>), [Observable](</Volumes/Dock_SSD/Projects/ProjectTemplate/Assets/Framework/MVC/Observer/Observable.cs>), [ModuleContainer](</Volumes/Dock_SSD/Projects/ProjectTemplate/Assets/Framework/MVC/Core/ModuleContainer.cs>), [BoardBuilder](</Volumes/Dock_SSD/Projects/ProjectTemplate/Assets/_Game/Board/BoardBuilder.cs>). 현재 저장소 밖의 조사 근거이며 빌드 의존성이나 필수 인계 파일은 아니다. 확인 파일의 시점별 해시는 [조사 기준](evidence/raw/mvc-reference-baseline.json)에 기록했다.

W-000-MVC-REFERENCE-001은 비교·설계만 수행했다. 후속 진행 요청(R-020)에 따른 실제 코드와 검증은 아래 W-000-MVC-001이다.

### 구현한 Model–View 연결 계약 — W-000-MVC-001

[DECISION:agent / R-019·R-020] `ObModel`은 private `Observable<ObModel>`로 변경을 통지한다. 파생 모델은 자기 상태를 바꾼 뒤 `NotifyChanged()`를 호출한다. 외부에는 Subscribe/Unsubscribe와 ObserverCount만 제공하며 공통 reset·Dispose·게임 상태 필드를 강제하지 않는다.

기존 `ObView : MonoBehaviour`는 그대로다. 모델이 필요한 View는 `ObView<TModel> : ObView`를 사용할 수 있으며 풀 관련 인터페이스를 자동으로 갖지 않는다. 모델 연결 API는 `Bind(model)`·`Unbind()`, 표현 구현점은 `RefreshView(model)`이다. 구체 View의 참조는 외부 조립 코드에서 넣는다.

| 시점 | 실제 계약 |
|---|---|
| 활성 View에 Bind | 모델 관찰 시작 → OnModelBound → 최신 상태 RefreshView. 동일 인스턴스 재대입은 중복 처리하지 않는다. null은 거절하고 분리는 Unbind로 표현한다. |
| 비활성/disabled View에 Bind | 모델 참조만 보관한다. 구독·훅·표현은 OnEnable까지 시작하지 않는다. |
| Model 변경 | 활성 관찰 대상의 변경만 RefreshView로 전달한다. 같은 모델의 재귀 통지는 기존 Observable 계약대로 예외다. |
| 비활성/disabled | 기본 통지 구독과 OnModelUnbound의 추가 구독을 정리한다. 모델 참조는 재활성화를 위해 유지한다. |
| 재활성화 | 한 번만 재구독하고 그동안 바뀐 최신 상태를 표시한다. |
| Unbind / 파괴 | retained Model 참조와 관찰을 분리한다. 풀 반환/폐기의 Unbind 호출은 구체 IPoolable/조립 파츠 책임이다. Unity 수명 콜백을 override하는 파생 View는 base를 호출한다. |
| 초기 연결 실패 / 연결 중 교체 | 실패한 관찰의 내부 구독·참조를 정리하고 예외를 전달한다. 훅에서 같은 모델을 다시 연결해도 이전 관찰의 갱신·실패 정리가 새 관찰을 건드리지 않게 사용 세대를 구분한다. |

OnModelBound/OnModelUnbound는 활성 관찰 시작·종료마다 대응되므로, 파생 View의 추가 모델 이벤트도 여기서 쌍으로 관리한다. 단순 비활성화를 풀 반환으로 취급하지 않는다. 실제 반환 시 Controller의 Loop 구독을 끊고 View.Unbind 후 객체별 상태를 초기화한다. Model/Controller 묶음 유지와 재생성 두 방식은 임시 객체로 모두 검사했으며 공통부에서 하나를 강제하지 않았다.

현재 공통 ObController는 빈 기반이고 Cannon별 Model·Controller는 골격이다. W-000-MVC-001의 ProbeController는 주입받은 ILoopEvents → Model 변경 → View 갱신과 구독 소유권을 보여주는 최소 테스트 구현이며 게임 Controller 구현 완료를 의미하지 않는다. Ball과 Obstacle의 객체별 수명 계약은 아래 절에서 실제 코드로 연결했고, 첫 PhysX 명령·초기화 계약은 R-023 절을 따른다. 새로운 DI 모듈·리플렉션 기반 모델 생성·패키지는 추가하지 않았다. [공통 코드](../../Assets/Scripts/Framework/Object/ObViewOfT.cs), [공통 실행 결과](evidence/raw/mvc-runtime-validation.json).

### Ball 객체별 MVC 묶음 계약 — W-000-BALL-MVC-001

[DECISION:user / R-021·R-022 / 2026-09-09 KST] 각 풀 Ball은 View·Model·Controller를 한 번 조립해 인스턴스가 파괴될 때까지 재사용한다. 매 대여마다 논리 객체를 재생성하지 않는다. 첫 BallModel은 물리와 무관한 `IsRented`와 `RentalEpoch`만 소유하며 위치·속도·충돌·Rigidbody 권위는 Obstacle MVC 다음의 물리 단위로 미룬다.

| 시점 | Ball의 실제 계약 |
|---|---|
| `OnPoolCreated` | `BallModel`과 `BallController`를 한 번 만들고 `BallView`가 묶음 수명을 소유한다. |
| `OnPoolRent` | Model의 세대를 0이 아닌 다음 값으로 올리고 대여 중으로 전환 → 비활성 View에 Bind → Controller가 현재 PoolLease와 세대를 보관한다. |
| 활성화 | 공통 `ObView<BallModel>`이 Model을 한 번 관찰하고 최신 상태를 갱신한다. 현재 수명 상태에는 시각 표현이 없어 RefreshView는 의도적으로 비어 있다. |
| `TryReturn(epoch)` | Controller·Model·PoolLease가 모두 같은 현재 세대일 때만 반환한다. 이전 세대의 callback/반환 요청은 새 대여를 건드리지 않는다. |
| `OnPoolReturn` | Controller의 현재 lease/세대 해제 → View.Unbind → Model 대여 상태 해제. Model/Controller 인스턴스와 마지막 세대 값은 다음 대여까지 유지한다. |
| 풀 종료 / 계층 선파괴 | 풀 콜백과 Unity `OnDestroy`가 같은 idempotent 정리 경로를 사용한다. 씬 계층이 Factory보다 먼저 파괴돼도 Controller·Model·관찰을 즉시 끊고, 뒤이은 Pool Dispose는 안전하게 중복 정리한다. |

Ball MVC 단위 당시에는 실제 입력·Loop·물리 구독을 넣지 않았다. 현재 R-023에서 초기 PhysX 속도 명령만 추가했으며, 매 프레임 Rigidbody 값을 Model에 복사하거나 빈 Tick을 등록하지 않는다. 이후 늦은 충돌·Tween·UniTask 완료는 대여 세대를 캡처하고 `IsCurrentRental(epoch)`를 확인해야 한다.

Ball MVC 단위 종료 당시 Daniel 소유 Ball Prefab은 MeshRenderer·SphereCollider·BallView를 가지고 Rigidbody는 없었다. Ball PoolConfig는 Prefab 연결, Min 3, Max 7, 비활성 정리 200초이고 Game 씬 PoolContainer에 등록돼 있었다. 격리된 임시 PoolFactory 검사에서 Controller 정상 반환, PoolLease 정상 반환, 같은 묶음 재대여, 오래된 세대 거절, 활성 풀 종료, 씬 계층 선파괴 후 정리를 포함해 19개 assertion을 통과했다. [Ball 코드](../../Assets/Scripts/InGame/Ball/BallView.cs), [실행 결과](evidence/raw/ball-mvc-runtime-validation.json), [당시 사용자 자산 보존](evidence/raw/ball-mvc-preservation-check.json).

### Obstacle 객체별 MVC 묶음 계약 — W-000-OBSTACLE-MVC-001

[IMPLEMENTED:agent / R-019·R-021 원칙 적용 / 2026-09-09 KST] Obstacle도 풀 인스턴스마다 View·Model·Controller를 한 번 조립해 재사용한다. Ball과 코드가 닮았다는 이유만으로 공통 베이스를 먼저 추출하지 않았고, 첫 상태는 `IsRented`와 0이 아닌 `RentalEpoch`로 제한했다.

수명 순서와 이전 세대 거절은 Ball 계약과 같되 구체 타입이 직접 소유한다. `ObstacleView.OnPoolCreated`가 묶음을 만들고, 대여에서 Model 시작 → View Bind → Controller lease 연결, 반환에서 Controller → View → Model 순서로 정리한다. 준비 중 유효하지 않은 lease가 들어오면 이미 시작한 Model과 View 연결까지 되돌린다. 풀 종료와 Unity `OnDestroy`는 같은 idempotent 정리를 사용한다. 물리·HP·파괴·위치·속도·충돌·시각 표현은 아직 권위가 정해지지 않아 넣지 않았다.

Unity 6000.3.10f1의 임시 PoolFactory/ObstacleView 검사에서 같은 묶음 재대여, 세대 전진, 오래된 lease/Controller 거절, 정상 반환, 활성 Pool Dispose, 계층 선파괴, 생성 후 미대여 파괴, 잘못된 lease 준비 실패 롤백과 파괴 오류 로그 부재를 포함해 25개 assertion을 통과했다. [Obstacle 코드](../../Assets/Scripts/InGame/Obstacle/ObstacleView.cs), [실행 결과](evidence/raw/obstacle-mvc-runtime-validation.json).

이 작업은 Cube Prefab·PoolConfig·Game 목록을 연결하지 않았다. 작업 중 외부에서 Ball/Cube Prefab에 Rigidbody가 추가된 저장 변경을 감지했지만 AI/보조가 만든 것으로 귀속하지 않고 사용자 소유 최신 상태로 보존했다. 다음 물리 단위에서는 이 실제 Rigidbody 구성과 Daniel의 의도를 다시 읽고, 상태 권위·초기화 책임·Unity Physics 대 직접 구현 Physics의 동일 비교 조건을 정한다. [보존 기록](evidence/raw/obstacle-mvc-preservation-check.json).

## Unity PhysX 런타임 권위와 풀 수명 — R-023

[DECISION:user / 2026-09-09 KST] 채용 공고의 PhysX 요구에 맞춰 첫 플레이는 Unity PhysX로 구현한다. 직접 구현 물리와의 비교 및 물리 권위 설명은 대표 PhysX 플레이를 확인한 뒤 별도 문서로 남긴다.

| 책임 | 현재 계약 |
|---|---|
| 런타임 물리 권위 | Rigidbody가 위치·회전·선속도·각속도를 소유한다. BallModel/ObstacleModel에는 이를 복제하지 않고 대여 상태·세대만 둔다. |
| 컴포넌트 계약 | 풀 소스의 BallView/ObstacleView와 같은 루트에 Collider와 **dynamic Rigidbody**가 있어야 한다. 코드는 `AddComponent`하거나 `isKinematic`, mass, gravity, constraints, collision detection, interpolation, damping을 덮어쓰지 않는다. |
| Ball 대여 | 비활성 `OnPoolRent`에서 속도·각속도를 0으로 만들고, 활성화 `OnEnable`에서 현재 lease·epoch를 확인해 발사 전 Sleep을 재적용한다. finite·0이 아닌 초기속도를 현재 대여에 한 번만 적용하고 WakeUp한다. |
| Obstacle 대여 | 비활성 `OnPoolRent`에서 속도·각속도를 0으로 만들고, 활성화 `OnEnable`에서 현재 lease·epoch를 확인해 충돌 대기 상태로 WakeUp한다. |
| 반환·활성 풀 종료 | 두 Rigidbody의 속도·각속도를 0으로 만들고 Sleep한다. Controller의 lease/epoch와 View/Model 연결도 같은 수명 경계에서 해제한다. |
| 충돌 결과 | 현재 단위는 PhysX 접촉과 Obstacle의 물리 이동만 다룬다. 피해·HP·파괴·점수·결과·연출은 뒤의 게임 규칙 계층이다. |

Ball 발사는 Controller가 Rigidbody에 명령하지만, 충돌 후의 Transform/velocity를 Controller가 다시 계산해 덮어쓰지 않는다. 따라서 현재 권위는 “Controller가 명령, PhysX Rigidbody가 상태 소유”다. 직접 구현 물리를 붙일 때는 이 구현과 동시에 같은 Transform을 쓰게 하지 않고, 별도의 물리 백엔드가 권위를 넘겨받는 경계를 문서와 동일 조건 Probe로 정의한다.

계층이 Factory보다 먼저 파괴되는 경로에서는 Unity의 파괴된 객체가 CLR 참조로 남아 있어도 PoolLease가 즉시 무효다. 해당 lease의 Return은 false이고 죽은 객체를 비활성 재고로 넣지 않는다. Pool의 Count는 최종 Dispose 때 정리되므로 계층 선파괴는 정상 재사용 흐름이 아니라 씬 종료용 방어 경로다.

격리 검증은 별도 local PhysicsScene에서 임시 dynamic Ball/Obstacle을 대여하고 발사 → `PhysicsScene.Simulate` → `OnCollisionEnter` → Obstacle 변위를 확인한다. 정상 반환·동일 body 재대여·오래된 lease/epoch 거절·활성 Factory Dispose와 Inspector 물리값 보존도 함께 검사한다. 1차 사용자 실행은 충돌 시뮬레이션 전 대여 직후 Sleep/Wake 복합 검사에서 실패했다. Pool은 비활성 clone의 `OnPoolRent` 뒤 활성화하므로, 활성화 시점에 두 View가 의도한 Sleep/Wake를 재적용하도록 수정했다. 검증은 이제 두 상태를 분리하고 임시 원본의 Rigidbody 설정을 Factory 초기화 전부터 비교한다. 이 Probe는 실제 Game Prefab의 질량·중력·배치·조작감을 대표하지 않으며 수정본 Play Mode 재실행 대기다. [검증 코드](../../Assets/Scripts/Test/PhysXRuntimeProbe.cs) · [1차 실패](evidence/raw/physx-lifecycle-runtime-failed-20260909T061136Z.json) · [수정 컴파일](evidence/raw/physx-activation-fix-editor-compile.json).

향후 `PHYSICS_AUTHORITY.md`는 대표 플레이 뒤 작성한다. 최소 내용은 권위 전환 표, PhysX/직접 구현 각각의 입력·fixed step·초기 상태·충돌 처리, 비교 지표(재현성·오차·CPU·GC·조작감), 혼합 금지 규칙과 Player/기기 검증 범위다. 문서 계획은 직접 구현 방식 채택이나 비교 통과를 뜻하지 않는다.

## 성능 근거를 기록하는 방법

UniTask와 SetText 사용 자체를 프로젝트 전체 Zero Alloc 달성으로 기록하지 않는다. 측정 구간·워밍업·조건·GC Alloc 결과를 함께 남긴다. UniTask의 구조체 기반 비동기 및 PlayerLoop 통합은 도구의 특성이고, 사용 코드의 할당과 수명 관리는 별도로 검증한다. [UniTask 공식 문서](https://github.com/Cysharp/UniTask)

Addressables는 로드와 해제의 참조 수를 관리한다. 풀 반환과 자산 해제를 같은 행동으로 가정하지 않고 수명에 맞게 설계한다. 이번 프로젝트의 구체적인 핸들 정책은 아직 정하지 않았다. [Addressables 메모리 관리](https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/MemoryManagement.html)

사용자가 설명한 과거 UpdateLoop 적용 후 약 50% 레이턴시 개선은 **사용자 제공 과거 경험**이다. 이번 프로젝트의 측정 결과나 보장 수치가 아니다. 물리 구현 비교의 기획 원본은 [DESIGN](DESIGN.md)에 둔다.

## 현재 구현과의 관계

### 중앙 Loop 책임 분리

[DECISION:agent / W-000-LOOP-001] A-01·A-02·A-05·A-09를 적용해 다음의 작은 기반부터 만든다. 시간 배율의 적용 범위나 DI 라이브러리를 새로 확정하는 결정은 아니다.

| 부분 | 책임 | 연결 상태 |
|---|---|---|
| `ILoopEvents` | 대상에게 Update·Fixed·Late 구독/해제만 제공 | 구현 완료 |
| `LoopDispatcher` | 실행 상태, 외부에서 받은 delta 전달, 구독 해제/정리 | 구현·Editor 검사 통과 |
| 기존 `UpdateLoop` | Unity Update·FixedUpdate·LateUpdate를 전달기에 연결 | GameClock·LoopDispatcher를 주입받음; 사용자 min/max 배율 유지 |
| 기존 `GameFlow` | 게임 흐름에서 Loop 기동·중단 요청 | UpdateLoop를 주입받아 Start/비활성화에 기동·중단; 로그 초기화 유지 |
| 이후 월드 Controller·UI Presenter | 주입받은 구독 경로로 필요한 작업 실행 | MVC/MVP·Pool 수명 단위에서 연결 |

전달기는 주어진 delta를 그대로 전달하고 자체 시간이나 Unity Time을 소유하지 않는다. UnityGameTime이 전역 배율을 적용하며 UpdateLoop가 이미 배율이 적용된 delta를 넘긴다. 이중 배율을 곱하지 않는다.

W-000-CORE-001 시작 시 확인: 사용자 `GameFlow`에는 Awake의 로그 레벨 초기화가 있었고, `UpdateLoop`에는 배율 Clamp와 빈 StartLoop가 있었다. 두 컴포넌트는 WorldObjects에 붙어 있으며 씬의 minScale=0, maxScale=2 값을 보존한다. Object MVC, Screen MVP, Pool/Factory, Ball/Obstacle은 실행 연결 전 골격이다. 초기 조사와 최신 조사 시점을 구분한 근거는 [REVIEW](REVIEW.md)에 있다. 사용자 작업이 계속되므로 수정 전 담당 부분의 최신 상태를 확인한다.

R-012 재개 조사에서 사용자 `InGame/Cannon`의 Controller·Model·View·Head 골격과 Observable의 스타일 정리를 확인했다. 현재 구현을 보존해 재사용하고 초기 생성본으로 되돌리지 않는다. Cannon·Object MVC·Screen MVP·Pool 골격에는 아직 Loop·DI 연결 코드가 없었다.

AI가 기존 빈 `Observable.cs`를 `Observable<T>`로 구현했다. 등록·해제·발행 중 변경·예외 후 복구를 Editor에서 검사했고, 안정된 Publish 경로의 제한된 할당 측정도 남겼다. 역할과 동작 계약은 [PLAN](PLAN.md), 실제 증거는 [검사 결과](evidence/raw/observable-validation.json)다. Observer가 준비됐다고 DI·Loop·Pool 연결이나 High Stripping 대응이 완료된 것은 아니다.

패키지 확인: UniTask는 manifest에 있고 DOTween 플러그인 파일도 있다. Addressables는 현재 manifest/lock에 없다. 이는 A-08의 채택이 미정이라는 뜻이 아니며, 이 기록 작업에서는 패키지를 설치하지 않았다.

작업 순서와 소유 범위는 [PLAN](PLAN.md), 요구 출처는 [BRIEF](BRIEF.md), 재개 지점은 [HANDOFF](../HANDOFF.md)에 둔다. 다른 문서에는 이 10개 기준을 복제하지 않고 이 원본을 연결한다.

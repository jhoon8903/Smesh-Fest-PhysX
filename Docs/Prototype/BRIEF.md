# 요청과 제약

기록일: 2026-09-08. 출처는 이번 사용자 요청과 현재 저장소이며 이전 대화의 미확인 내용을 복원하지 않는다.

| 요구 | 상태·내용 |
|---|---|
| R-001 | [DECISION:user] 기존 `/Volumes/Dock_SSD/Projects/Smesh-Fest-PhysX`에서 프로젝트 작업 시작. 새 프로젝트 생성 요청이 아님. |
| R-002 | [DECISION:user] 선택한 A안: Block 영역 클릭으로 목표 선택·발사 → 파괴 → 결과 → 재도전 + 물리 비교. 최초 ‘조준·발사’ 표현은 R-011로 구체화. 기획 원본은 [DESIGN](DESIGN.md). |
| R-003 | [DECISION:user] “이번에 함께 확인할 작은 목표와 내가 할 일·AI가 할 일을 먼저 정하자. 합의한 범위부터 제작”. |
| R-004 | [DECISION:user] 결정·작업·검증 결과를 자동 문서화. 기록기는 [evidence/REPORT](evidence/REPORT.md)를 생성한다. |
| R-005 | [DECISION:user] Main Astra·Ultra, 보조는 [저장소 정책](../Work-flow/prototype-workflow/references/model-routing.md). |
| R-011 | [DECISION:user] 별도 조준 없이 Block이 있는 영역을 클릭하면 해당 위치로 Ball 발사. 클릭이 조준을 겸하며 Cannon은 Ball 진행 방향으로 회전. 조작 확정 원본은 [DESIGN](DESIGN.md). |

[DECISION:user / R-007] 사용자가 제시한 10개 기본 아키텍처와 반복 질문 금지의 단일 원본은 [ARCHITECTURE](ARCHITECTURE.md)다. 새 작업은 이 기준을 읽고 그대로 이어간다.

[DECISION:user / R-008] “추가로 나는 DI 의존성 주입으로 코드 작성을 해” — A-09로 추가했다. 사용 여부를 재질문하지 않으며 특정 DI 라이브러리는 아직 지정되지 않았다.

[DECISION:user / R-009] “Code Stripping을 High로 하기 때문에 관리도 해야해” — A-10으로 추가했다. High 대응과 코드 보존 관리를 기본으로 적용하고 실제 Player 빌드 증거로 검증한다.

## 현재 우선순위

[DECISION:user / R-006] “지금 해야 하는 것은 전체 아키텍처를 만들고 나서해야해”, “내가 어떻게 만들지에 대한 구현 계획을 안물어 보았잖아?” — Daniel의 구현 계획을 먼저 듣고 전체 아키텍처를 합의·제작한 후 플레이 기능을 진행한다.

## 확정 제약

- [DECISION:user] 현재 씬·Prefab·Material·ParticleSystem·Importer 수동값과 진행 중인 코드를 보존한다. 자동 씬 재생성이나 저장은 실행하지 않는다.
- [DECISION:agent] 초기 조사·기록의 AI 쓰기 범위는 Docs와 루트 AGENTS.md였다. R-010 이후 첫 독립 기반 Observer와 Editor 검증 파일로 범위를 넓혔다. 현재 제작 소유 범위와 다음 종속 계약은 PLAN을 따른다.
- [FACT] 초기 디스크 상태와 실제 설정 확인은 [REVIEW](REVIEW.md), 작업 상태는 [PLAN](PLAN.md)에서 관리한다.

## 열린 질문

- O-001 / 답변 수신, R-011로 조작 구체화: 후속 첫 플레이 목표는 Block 영역 클릭 → 해당 위치로 Ball 1개 발사·충돌과 진행 방향에 맞춘 Cannon 회전. Daniel은 씬·카메라·공/표적 배치·조작감, AI는 코드·검증·기록 분담을 선택했다. 전체 아키텍처 선행 순서는 유지한다.
- O-002 / 답변 수신: Unity 제공 Physics와 직접 구현 Physics 비교. 중앙 UpdateLoop 구독 방식으로 과거 약 50% 레이턴시 개선 경험도 설명했다. 이번 프로젝트 측정 결과가 아니며 구체적인 비교 조건은 미정.
- O-003 / 답변 수신: 10개 기본 기준을 제시했다. [ARCHITECTURE](ARCHITECTURE.md)를 확정 원본으로 사용하며 전체 구조 설명을 처음부터 다시 요청하지 않는다. 구체적인 시간·수명·책임 계약과 제작 순서는 이 기준 위에서 다듬는다.
- [OPEN] 플랫폼·기한·외부 납품 목적은 이번 요청에서 지정되지 않았다. 첫 목표 판단에 필요한 시점에만 확인한다.

자동 기록은 현재 작업 에이전트가 단계·변경·검증 직후 실행한다. 사용자의 Editor 작업은 제공받은 결과와 디스크 변경 근거를 출처와 함께 기록하며, 미저장 조작을 상시 수집하지 않는다.

[DECISION:user / R-010] “작업 진행하자 하이어라키에 오브젝트는 만들어 두었고, 코드 폴더는 만들어 두었어” — 기존 사용자 구성을 보존하며 아키텍처 작업 진행. 첫 실제 코드 단위와 소유 범위는 PLAN의 W-000-CORE-001.

새 세부 확인 O-004(DI 도구/기존 구현), O-005(물리·연출·UI 시간 적용)는 PLAN에 기록했다. 기존 10개 채택 결정은 다시 묻지 않는다.

[DECISION:user / R-012] 조작 정정 뒤 “진행하자” — 기존 아키텍처 선행 순서로 작업 계속. 이번 독립 범위는 PLAN의 W-000-LOOP-001이며 O-004·O-005의 답변으로 간주하지 않는다.

[DECISION:user / R-013] O-004·O-005 답변: VContainer를 설치했다. UI에는 별도 시간이 필요 없으며 게임 시간에서 분리한다. UI가 열려 있을 때 게임은 Pause 상태여야 한다. 설치된 manifest/lock은 VContainer 1.19.0이다.

[DECISION:user / R-014] “다음 진행하자” — 다음 아키텍처 단위인 Pool/Factory의 대여·반환·정리 계약을 다듬는다. 당시 O-006·O-007을 질문했으며 이후 R-015로 답변받았다. 확정 10개 기준과 VContainer/UI Pause 선택은 유지한다.

[DECISION:user / R-015] Poolable 상속, PoolConfig SO(Pool Root Name·MinPool·MaxPool 등), PoolContainer 목록, Factory 사전 생성, 이벤트 구독형 MVC 묶음, BallView의 ObView·Poolable 기반을 사용한다. Model·Controller 재사용 여부는 상태와 객체 종류에 따라 다르다. 생성/대여와 반환 양쪽에서 초기화한다. 재도전·스테이지 이동에서 유지하고 게임 씬에서 로비로 나갈 때 정리하며 5분 이상 미사용 객체는 정리한다. O-006·O-007 해결. 구체적인 계약의 원본은 ARCHITECTURE다.

[DECISION:user / R-016] “반환 타임도 PoolConfig에서 설정” — 반환 대기 시간을 객체 종류별 설정에 포함한다. 초기 기본값 300초를 하드코딩 정책으로 쓰지 않는다.

[DECISION:user / R-017] “비활성 재고 정리시 Minpool 만 남기고 정리” — O-008 해결. 비활성 미사용 재고를 설정 시간에 따라 정리하고 MinPool을 유지한다. 활성 사용 객체는 시간만으로 반환하지 않는다. 같은 질문을 반복하지 않는다.

[DECISION:user / R-018] “ObView는 단독일 수도 있고 … Pooling을 사용하는 객체라면 OBView를 상속 받고 IPoolable을 인터페이스로” — ObView를 Poolable 기반에 묶은 AI 구현 방향을 정정했다. ObView는 독립 MonoBehaviour이며 풀링이 필요한 View만 ObView와 IPoolable을 사용한다. R-015의 상속 설명과 이전 AI 상속 체인보다 이 후속 결정을 우선한다.

[DECISION:user / R-019 / 2026-09-09 KST] ProjectTemplate의 `Assets/Framework/MVC`를 참고로 제공하고 “똑같이 만들필요는 없지만 지금 프로젝트에는 간소화해서 붙여나가야” 한다고 지정했다. 기존 구조와 실제 차이를 확인한 뒤 필요한 책임부터 적용한다. R-018과 확정된 Pool 정책은 유지한다. 비교 근거와 적용 기준의 원본은 [ARCHITECTURE](ARCHITECTURE.md)의 R-019 절이다.

[DECISION:user / R-020 / 2026-09-09 KST] 최소 Model–View 연결·구독 해제 단위와 분담을 제시한 뒤 “다음 작업 진행핮”으로 진행을 요청했다. W-000-MVC-001의 코드·임시 객체 검증·문서 갱신을 수행한다. 기존 씬·Prefab 배치와 전체 아키텍처 선행 순서는 유지한다.

[DECISION:user / R-021 / 2026-09-09 KST] Ball의 메모리·수명 관리는 **풀 객체별 View·Model·Controller 묶음 재사용**으로 진행한다. 각 대여·반환에서 런타임 상태와 구독을 초기화하며 매 대여마다 Model/Controller를 새로 생성하지 않는다. Daniel이 만든 최신 Ball/Cube Prefab·PoolConfig·Game 씬 연결은 사용자 소유 값으로 보존한다.

[DECISION:user / R-022 / 2026-09-09 KST] 첫 BallModel은 물리 비종속 대여 상태와 사용 세대만 소유한다. 위치·속도·충돌 상태는 Physics 권위를 정하기 전에 넣지 않는다. 이번 컨텍스트에서 Ball MVC를 구현·검증하고, 다음 컨텍스트는 Obstacle MVC를 같은 원칙으로 구현한 뒤 물리 구현으로 진행한다.

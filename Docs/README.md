# 프로젝트 문서

이 저장소에는 사람과 AI가 함께 프로토타입을 준비하고 제작하는 워크플로우와 스킬 지침을 보관한다. 개인 컴퓨터의 스킬 폴더 없이 프로젝트와 함께 읽을 수 있도록 상대 경로로 연결했다.

진행 중인 제작을 재개할 때는 먼저 [확정된 기본 아키텍처](Prototype/ARCHITECTURE.md)와 [현재 인계](HANDOFF.md)를 읽는다. 10개 아키텍처 기준은 사용자 결정이며 채택 여부를 반복 질문하지 않는다. 저장소 루트 [AGENTS.md](../AGENTS.md)에도 이 로드 순서를 연결했다.

## 처음 읽는 순서

1. [스킬 설명서](Work-flow/prototype-workflow/GUIDE.md): 무엇을 도와주는지, 사람과 AI가 어떻게 분담하는지.
2. [프로젝트 시작 절차·검토표](Work-flow/prototype-workflow/references/startup-flow.md): 위치·범위·분담·첫 확인 결과를 정하는 순서.
3. [에이전트 역할·작업 시퀀스](Work-flow/prototype-workflow/references/orchestration.md): 필요한 역할만 배정하는 방법.
4. [모델·Effort 정책과 설정 템플릿](Work-flow/prototype-workflow/references/model-routing.md): Main Astra · Ultra, 보조 Luna · Low / Terra · Medium / Sol · High.
5. [문서 작성 계약](Work-flow/prototype-workflow/references/documents.md): 누가 무엇을 언제 기록하는지.
6. [AI 실행 지침](Work-flow/prototype-workflow/SKILL.md): 이 프로젝트에 적용할 스킬 원본.

## 다른 환경에서 사용하기

프로젝트를 받은 위치에서 이 문서를 열면 된다. AI와 작업할 때는 다음처럼 전달할 수 있다.

> Docs/Work-flow/prototype-workflow/SKILL.md와 GUIDE.md를 읽고 현재 요청 범위를 먼저 확인해줘. 사람과 AI가 맡을 일, 함께 확인할 작은 결과를 정한 뒤 진행해줘.

별도 개인 스킬 등록은 이 문서들을 읽고 따르기 위한 필수 조건이 아니다. 사용하는 AI 환경의 스킬 자동 인식 여부는 별도이며, 이 폴더만으로 에이전트나 감시 작업이 자동 실행되지는 않는다.

`game-design-workshop`, `context-save` 같은 다른 개인 스킬은 선택 사항이다. 설치되어 있지 않으면 포함된 시작 검토표·문서 계약을 사용한다.

## 저장 구조

```text
Docs/
├── README.md
└── Work-flow/
    └── prototype-workflow/
        ├── SKILL.md
        ├── GUIDE.md
        ├── VALIDATION.md
        ├── agents/openai.yaml
        ├── codex/config.toml       # 비활성 설정 템플릿
        ├── codex/agents/           # 역할별 모델·Effort 설정
        ├── references/
        └── scripts/
```

자동 기록 도구와 격리된 시험도 포함했다. 사용법은 [기록 방법](Work-flow/prototype-workflow/references/recording.md), 실제 확인 범위는 [검증 기록](Work-flow/prototype-workflow/VALIDATION.md)에서 볼 수 있다.

## 현재 범위

2026-09-08 사용자가 이 기존 프로젝트의 작업 시작과 A안(Block 영역 클릭으로 목표 선택·발사 → 파괴 → 결과 → 재도전 + 물리 비교)을 명시했다. **현재는 Daniel이 제시한 10개 확정 기준을 바탕으로 전체 아키텍처를 다듬고 제작하는 단계**다. 하이어라키·코드 폴더 준비 후 진행 요청에 따라 Observable과 LoopDispatcher의 독립 기반을 구현하고 Unity Editor 검사를 통과했다. R-013의 VContainer·UI Pause 결정을 반영해 GameFlow·Loop를 현재 씬에 연결했고 Play Mode 검사 25개를 통과했다. 현재 R-015·R-016의 IPoolable·PoolConfig·Factory·MVC 생성 계획과 객체별 반환 시간을 반영해 Pool 기반을 구현했고 R-018 인터페이스 구조로 Play Mode 66개 검사와 DI/UI Pause 25개 검사를 통과했다. Model·Controller 재사용은 객체별로 정하고 게임 씬을 나갈 때 풀을 정리한다. R-018에 따라 ObView는 독립적으로 유지하고 풀링 View만 IPoolable을 구현한다. R-017로 비활성 미사용 재고만 정리하고 그중 MinPool을 남기는 정책까지 확정했다. 아키텍처를 만든 뒤 개별 플레이 기능을 구현한다. 발사 기능 선행 제작은 구현 전에 철회했다. 현재 순서는 [제작 계획](Prototype/PLAN.md)에서 관리한다. R-011에 따라 별도 조준 단계 없이 클릭이 조준·발사를 겸하며 Cannon은 Ball 진행 방향으로 회전한다. 조작 원본은 [DESIGN](Prototype/DESIGN.md)이다.

- [확정 아키텍처 원본](Prototype/ARCHITECTURE.md) · [요청과 제약](Prototype/BRIEF.md) · [기획 원본](Prototype/DESIGN.md)
- [첫 목표와 분담](Prototype/PLAN.md) · [실제 확인 결과](Prototype/REVIEW.md)
- [자동 제작 기록](Prototype/evidence/REPORT.md) · [다음 작업 인계](HANDOFF.md)

2026-09-09 업데이트: R-019에 따라 Daniel의 ProjectTemplate MVC를 비교하고 **현재 필요한 책임부터 간소화해 연결**하는 기준을 [ARCHITECTURE](Prototype/ARCHITECTURE.md)에 기록했다. 이번 비교는 문서·설계 작업이며 MVC 코드는 아직 골격이다. 다음 단위는 Model 변경 → View 갱신 → 구독 해제 연결이다. 참고의 Pool 상속·모듈 부팅 구조를 그대로 복제하지 않는다.

기본 협업 순서는 **이번 목표 → 사람·AI 분담 → 작은 결과 제작 → 함께 확인 → 다음 작업 조정**이다. 사용자는 원하는 제작을 직접 맡고 AI는 합의된 범위와 자동 기록을 지원한다.

이 프로젝트에서 수정하는 워크플로우는 저장소 안의 사본을 기준으로 관리한다. 개인 설치판과 자동 동기화되지 않는다. 이 파일들이 Git 커밋에 포함되면 다른 환경의 clone에서도 같은 문서를 읽을 수 있다.

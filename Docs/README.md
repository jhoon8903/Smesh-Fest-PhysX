# 프로젝트 문서

이 저장소에는 사람과 AI가 함께 프로토타입을 준비하고 제작하는 워크플로우와 스킬 지침을 보관한다. 개인 컴퓨터의 스킬 폴더 없이 프로젝트와 함께 읽을 수 있도록 상대 경로로 연결했다.

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

현재는 프로젝트를 시작하기 위한 협업 흐름과 스킬 문서를 저장소에 보관하는 단계다. 문서 배치 요청을 게임 코드·씬·패키지 변경이나 제작 시작 승인으로 해석하지 않는다.

기본 협업 순서는 **이번 목표 → 사람·AI 분담 → 작은 결과 제작 → 함께 확인 → 다음 작업 조정**이다. 사용자는 원하는 제작을 직접 맡고 AI는 합의된 범위와 자동 기록을 지원한다.

이 프로젝트에서 수정하는 워크플로우는 저장소 안의 사본을 기준으로 관리한다. 개인 설치판과 자동 동기화되지 않는다. 이 파일들이 Git 커밋에 포함되면 다른 환경의 clone에서도 같은 문서를 읽을 수 있다.

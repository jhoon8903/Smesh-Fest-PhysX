# 체크포인트 자동 문서 기록

이 기록기는 에이전트가 호출할 때 이벤트를 남기고 한국어 보고서를 자동 갱신한다. 백그라운드 감시, 대화 전체 수집, Codex hooks 설정은 하지 않는다. **각 단계의 시작·완료와 중요한 결정 직후에 호출하는 것은 워크플로우를 진행하는 에이전트의 책임**이다.

Python 3의 표준 라이브러리만 사용한다. 별도 패키지를 설치하지 않는다. 아래 경로에서 `SKILL_DIR`는 이 스킬 디렉터리, `RECORD_ROOT`는 프로젝트 안의 전용 기록 디렉터리를 뜻한다. 실제 명령에는 확인한 절대 경로를 사용한다.

**위치와 시작 조건:** `init`은 지정한 폴더와 파일을 실제로 만든다. 스킬/시작 흐름 준비 모드에서는 대상 프로젝트에 실행하지 않는다. 사용자가 프로젝트의 절대 경로와 시작을 결정한 뒤에 초기화한다. 기록기 자체의 시험은 격리된 임시 폴더에서 수행한다.

## 프로젝트를 받은 뒤 도구 확인

프로젝트 루트에서 다음 명령으로 임시 폴더 시험을 실행할 수 있다. 게임 프로젝트나 제작 기록을 초기화하지 않는다. Python 3만 필요하다.

```sh
python3 Docs/Work-flow/prototype-workflow/scripts/test_workflow_log.py
```

실제 제작 기록을 시작하기로 정한 뒤에는 `SKILL_DIR`를 `Docs/Work-flow/prototype-workflow`, `RECORD_ROOT`를 `Docs/Prototype/evidence`로 해석한다. 아래는 사용 설명이며 현재 프로젝트 로그를 자동 생성하라는 지시가 아니다.

## 실행 순서

```text
python3 SKILL_DIR/scripts/workflow_log.py init --root RECORD_ROOT --title "프로토타입 제작 기록"
python3 SKILL_DIR/scripts/workflow_log.py record --root RECORD_ROOT --event-file EVENT_JSON
python3 SKILL_DIR/scripts/workflow_log.py render --root RECORD_ROOT
python3 SKILL_DIR/scripts/workflow_log.py verify --root RECORD_ROOT
```

- `init`: `events.jsonl`에 소유권·형식·제목·현재 UTC 시각을 기록하고 `REPORT.md`를 만든다. 같은 제목으로 재호출하면 기존 기록을 보존한다. 다른 제목은 거절한다.
- `record`: 검증한 이벤트를 한 행 추가하고 보고서를 자동 갱신한다. 보고서 생성을 위해 별도 `render`를 매번 호출할 필요는 없다.
- `render`: 저장된 기록만으로 보고서를 다시 만든다. 누락된 자동 보고서를 복구할 때 사용할 수 있다.
- `verify`: 과거 파일 해시와 현재 파일을 비교해 JSON으로 출력한다. 과거 기록이나 보고서를 변경하지 않는다. 비교 결과를 근거로 보존하려면 출력을 별도 파일에 저장한 뒤 새 리뷰 이벤트의 `evidence`로 연결한다.

출력은 JSON이며 성공 시 종료 코드 `0`, 입력·파일·잠금 오류 시 `1`이다. `verify`가 발견한 변경·누락은 검사 결과이며 명령 오류가 아니다. `counts`와 `evidence[].status`를 확인한다.

## 이벤트 형식

```json
{
  "id": "first-play-start-001",
  "stage": "build",
  "kind": "start",
  "actor": "builder",
  "task_id": "first-play-001",
  "status": "in_progress",
  "summary": "발사와 충돌을 포함한 첫 플레이 구현 시작",
  "evidence": ["/absolute/project/docs/PROTOTYPE_PLAN.md"],
  "metadata": {
    "validation_level": "pending",
    "scope": "탄환 1종, 목표물 1종",
    "human_role": "제작 범위 결정"
  }
}
```

| 필드 | 규칙 |
|---|---|
| `stage` | 필수. `request`, `design`, `plan`, `build`, `review`, `document` |
| `kind` | 필수. `start`, `finish`, `checkpoint`, `request`, `decision`, `review`, `handoff` |
| `actor`, `summary` | 필수. 비어 있지 않은 문자열. 실제 담당과 실제 발생 내용을 쓴다. |
| `id` | 선택. 재시도에 사용할 고유 문자열. 생략하면 UUID를 생성한다. 같은 ID에 다른 입력을 넣으면 거절한다. |
| `task_id` | 선택. 시간 측정 시 동일 작업의 `start`와 `finish`에 같은 값을 사용한다. 작업 구간마다 새 ID를 쓴다. |
| `status` | 선택. `in_progress`, `done`, `blocked` 등 실제 상태를 나타내는 문자열. 생략하면 보고서에 상태 미지정으로 표시한다. |
| `evidence` | 선택. 파일 경로 문자열 배열. 상대 경로는 `--root` 기준이므로 절대 경로를 권장한다. |
| `metadata` | 선택. JSON 객체. 검증 조건·결과·사람의 판단·변경 요청·제약 등 필요한 맥락만 쓴다. |
| `metadata.validation_level` | `pending`(기본값), `static`, `runtime`, `device`. 검증을 실행한 근거에 따라 선택한다. |

`recorded_at`은 입력할 수 없다. 명령 실행 시 실제 UTC 시각을 기록기가 지정한다. 예상 소요 시간·과거 날짜를 기록 시각으로 위장하지 않는다. 추가 필드는 `metadata` 안에 넣는다.

`runtime`은 기록자가 실제로 실행한 테스트 또는 플레이 근거를 연결할 때 쓴다. Python 기록기 테스트 통과를 Unity 플레이 검증으로 적지 않는다. `device`는 실제 대상 기기의 실행 근거가 있을 때만 쓴다. 계획된 테스트, 코드 작성 완료, 빌드 예정은 해당 검증 통과를 뜻하지 않는다. 검증 수준은 분류 값이므로 통과·실패·제한 사항은 `status`, `summary`, `metadata`에도 명시한다.

## 에이전트 배정 메타데이터

[모델 배정 정책](model-routing.md)에 따라 시작 이벤트의 `metadata`에 아래 정보를 포함한다. 종료 이벤트에도 같은 `task_id`와 배정값을 붙이고 결과·재작업 여부를 추가한다. 기존 기록기가 메타데이터를 이벤트와 보고서에 보존하므로 별도 문서 생성 모델이나 기록기 확장이 필요하지 않다.

```json
{
  "assigned_model": "gpt-5.6-luna",
  "reasoning_effort": "low",
  "routing_reason": "지정 파일의 메타데이터 지원 확인",
  "context_mode": "none; 필요한 경로와 질문만 전달",
  "task_scope": "기록기 소스와 사용 문서 읽기",
  "escalation_count": 0,
  "escalation_reason": null,
  "execution_confirmation": "호출에 두 설정을 명시; 실제 적용 확인값은 환경이 제공할 때 별도 기록",
  "observed_model": null,
  "observed_reasoning_effort": null,
  "usage": null,
  "validation_level": "pending"
}
```

이 예시는 실제 실행 기록이 아니다. `assigned_*`와 `reasoning_effort`는 배정값이며, `observed_*`는 환경에서 확인된 값만 적는다. 토큰·비용이 제공되지 않으면 `usage: null`을 유지한다. 종료에는 `outcome`, `rework_required`를 실제 결과에 맞게 추가한다. 기록 전 작업은 `retrospective: true`로 남기며 시작 시각을 소급하지 않는다.

## 완료 이벤트와 관측 시간

앞의 `first-play-001` 작업이 실제로 끝나면 별도 이벤트 파일을 작성한다.

```json
{
  "id": "first-play-finish-001",
  "stage": "build",
  "kind": "finish",
  "actor": "builder",
  "task_id": "first-play-001",
  "status": "done",
  "summary": "발사·충돌 코드를 작성하고 정적 검토 완료. Unity 플레이는 대기 중",
  "evidence": ["/absolute/project/Assets/Scripts/Projectile.cs"],
  "metadata": {
    "validation_level": "static",
    "next_validation": "사용자의 Unity 실행 확인"
  }
}
```

동일 `task_id`에 `start` 1개와 `finish` 1개가 있는 구간만 UTC 차이를 계산한다. 짝이 없거나 중복된 구간은 제외한다. 순수 작업시간을 추정하거나 병렬 에이전트의 시간을 합산하지 않는다. 이 값에는 사용자 답변 대기와 리뷰가 포함될 수 있다. AI 미사용 비교 기록 없이 절약 시간·향상 배수를 주장하지 않는다. 첫 플레이까지의 시간을 보이려면 구현을 시작할 때 그 구간의 `start`를 남기고 **실제로 첫 플레이를 확인한 후** `finish`를 남긴다.

## 증거와 재시도

- 파일은 기록 시 SHA256과 크기를 저장한다. 이후 파일이 바뀌어도 과거 해시는 유지된다. 해시는 결과물의 정체성을 확인하며 제작자·품질·테스트 통과 자체를 증명하지 않는다.
- 없는 파일은 `missing`으로 기록한다. 디렉터리·심볼릭 링크·특수 파일은 상태만 남기고 재귀 수집하거나 따라가지 않는다. 폴더 대신 검증에 필요한 실제 파일을 지정한다.
- 보고서 링크는 현재 파일을 연다. 과거 내용을 보존해야 하는 성능 결과·영상·캡처는 버전별 파일로 저장하거나 커밋 식별자를 `metadata`에 함께 남긴다.
- 같은 ID와 같은 입력의 재시도는 `already_recorded`이며 이벤트·시각·해시를 추가하거나 바꾸지 않는다. ID를 생략해도 동일 JSON 객체는 입력 해시로 중복을 막는다. 의도적으로 동일 작업을 다시 기록하려면 새 명시적 ID를 쓴다.
- 다시 측정한 해시·결과는 새 ID의 이벤트로 추가한다. 이전 입력을 수정해 같은 ID로 재시도하지 않는다.
- `verify` 상태: `unchanged`(동일 해시), `changed`(내용 변경), `missing`(기존 파일 사라짐), `unavailable`(일반 파일로 읽을 수 없음), `now_available`(기록 당시 없던 파일이 생김), `unverified`(양 시점 모두 해시 확인 불가).

## 소유권과 동시 기록

`events.jsonl` 첫 행과 `REPORT.md` 첫 줄의 표식으로 소유권을 확인한다. 기록기 소유가 아닌 동일 이름 파일을 덮어쓰지 않는다. `REPORT.md`는 생성물이므로 사람의 추가 설명은 별도 파일에 작성한다. 기존 기록 파일의 손상을 발견하면 조용히 삭제·수정하거나 잘린 행을 복구하지 않고 오류로 멈춘다.

`init`, `record`, `render`, `verify`는 루트 안의 임시 `.workflow-log.lock` 디렉터리로 직렬화한다. 보고서는 임시 파일에서 작성한 뒤 원자적으로 교체한다. 프로세스가 강제 종료되어 잠금이 남으면 다른 기록 프로세스가 없는지 확인한 후 그 **빈 잠금 디렉터리만** 제거한다. 잠금은 자동으로 강제 탈취하지 않는다. 보고서 교체 전에 종료되어도 이벤트가 온전히 저장됐다면 같은 이벤트를 재시도하거나 `render`를 실행하면 된다.

## 기록기 검증

```text
python3 SKILL_DIR/scripts/test_workflow_log.py
```

임시 디렉터리에서 동시 프로세스 기록, 재시도, 누락·변경 근거, 잘못된 입력, 소유하지 않은 문서 보존, 시간 짝 계산, 보고서 재생성, 손상된 기록 보존을 확인한다. 실제 프로젝트에는 테스트 기록을 만들지 않는다.

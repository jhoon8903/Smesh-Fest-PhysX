#!/usr/bin/env python3
"""Append observed workflow checkpoints and regenerate an evidence-aware report."""

import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import stat
import sys
import tempfile
import time
import uuid


MARKER = "<!-- prototype-workflow:generated:v1 -->"
LOG_NAME = "events.jsonl"
REPORT_NAME = "REPORT.md"
STAGES = {
    "request": "1. 기획 요청", "design": "2. 기획", "plan": "3. 제작 단계 제안",
    "build": "4. 제작", "review": "5. 리뷰", "document": "6. 문서로 증명",
}
KINDS = {"start", "finish", "checkpoint", "request", "decision", "review", "handoff"}
LEVELS = {"pending": "미검증", "static": "정적 확인", "runtime": "실행 확인", "device": "실기기 확인"}
FIELDS = {"id", "stage", "kind", "actor", "summary", "task_id", "status", "evidence", "metadata"}


def utc_now():
    return datetime.now(timezone.utc).isoformat(timespec="microseconds").replace("+00:00", "Z")


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"중복 JSON 키: {key}")
        result[key] = value
    return result


def parse_json(value):
    def invalid_constant(token):
        raise ValueError(f"허용되지 않는 JSON 상수: {token}")
    return json.loads(value, object_pairs_hook=unique_object, parse_constant=invalid_constant)


def require_text(value, name):
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{name}: 비어 있지 않은 문자열이 필요합니다.")


def validate_timestamp(value):
    require_text(value, "저장된 기록 시각")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None or parsed.utcoffset().total_seconds() != 0:
        raise ValueError("저장된 기록 시각은 UTC여야 합니다.")


def validate_digest(value):
    if not isinstance(value, str) or len(value) != 64 or any(c not in "0123456789abcdef" for c in value):
        raise ValueError("저장된 SHA256 형식이 올바르지 않습니다.")


def validate_event(event):
    if not isinstance(event, dict):
        raise ValueError("이벤트는 JSON 객체여야 합니다.")
    if "recorded_at" in event:
        raise ValueError("recorded_at은 기록기가 실제 UTC 시각으로 지정합니다.")
    unknown = set(event) - FIELDS
    if unknown:
        raise ValueError(f"지원하지 않는 필드: {', '.join(sorted(unknown))}")
    for name in ("stage", "kind", "actor", "summary"):
        require_text(event.get(name), name)
    if event["stage"] not in STAGES:
        raise ValueError(f"stage는 다음 중 하나여야 합니다: {', '.join(STAGES)}")
    if event["kind"] not in KINDS:
        raise ValueError(f"지원하지 않는 kind: {event['kind']}")
    for name in ("id", "task_id", "status"):
        if name in event:
            require_text(event[name], name)
    evidence = event.get("evidence", [])
    if not isinstance(evidence, list):
        raise ValueError("evidence는 파일 경로 문자열의 배열이어야 합니다.")
    for path in evidence:
        require_text(path, "evidence 경로")
        if "\x00" in path:
            raise ValueError("evidence 경로에 NUL을 포함할 수 없습니다.")
    metadata = event.get("metadata", {})
    if not isinstance(metadata, dict):
        raise ValueError("metadata는 JSON 객체여야 합니다.")
    level = metadata.get("validation_level", "pending")
    if not isinstance(level, str) or level not in LEVELS:
        raise ValueError("metadata.validation_level은 pending/static/runtime/device 중 하나여야 합니다.")
    return event


@contextmanager
def directory_lock(root, timeout=15):
    lock = root / ".workflow-log.lock"
    deadline = time.monotonic() + timeout
    while True:
        try:
            lock.mkdir()
            break
        except FileExistsError:
            if time.monotonic() >= deadline:
                raise ValueError(f"기록 잠금 대기 시간 초과: {lock}. 다른 기록 작업이 끝났는지 확인하세요.")
            time.sleep(0.025)
    try:
        yield
    finally:
        lock.rmdir()


def assert_regular_or_absent(path):
    if path.is_symlink() or (path.exists() and not path.is_file()):
        raise ValueError(f"일반 파일만 기록 대상으로 사용할 수 있습니다: {path}")


def assert_report_owned(root):
    path = root / REPORT_NAME
    assert_regular_or_absent(path)
    if path.exists():
        with path.open(encoding="utf-8") as stream:
            if stream.readline().rstrip("\r\n") != MARKER:
                raise ValueError(f"기록기 소유가 아닌 문서는 덮어쓰지 않습니다: {path}")


def load_log(root):
    path = root / LOG_NAME
    assert_regular_or_absent(path)
    if not path.exists():
        raise ValueError("초기화된 기록이 없습니다. init을 먼저 실행하세요.")
    records = []
    with path.open(encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            try:
                if not line.endswith("\n"):
                    raise ValueError("완결되지 않은 행")
                record = parse_json(line)
                if not isinstance(record, dict):
                    raise ValueError("JSON 객체가 아닌 행")
                records.append(record)
            except (ValueError, TypeError) as exc:
                raise ValueError(f"기록 손상: {path}:{line_number}: {exc}") from exc
    if not records or records[0].get("_type") != "prototype-workflow.init" or records[0].get("schema_version") != 1:
        raise ValueError(f"기록기 소유가 아닌 이벤트 파일입니다: {path}")
    header = records[0]
    require_text(header.get("title"), "기록 제목")
    validate_timestamp(header.get("recorded_at"))
    seen = set()
    for row in records[1:]:
        # Validate the saved shape before appending or replacing any generated file.
        if row.get("_type") != "prototype-workflow.event":
            raise ValueError("알 수 없는 이벤트 형식입니다.")
        evidence = row.get("evidence")
        if not isinstance(evidence, list):
            raise ValueError("저장된 evidence 형식이 올바르지 않습니다.")
        for item in evidence:
            if not isinstance(item, dict) or item.get("state") not in {"file", "missing", "directory", "symlink", "non_regular", "unreadable"}:
                raise ValueError("저장된 파일 근거 상태가 올바르지 않습니다.")
            require_text(item.get("path"), "저장된 파일 경로")
            if item["state"] == "file":
                validate_digest(item.get("sha256"))
                if type(item.get("bytes")) is not int or item["bytes"] < 0:
                    raise ValueError("저장된 파일 크기가 올바르지 않습니다.")
        public = {key: value for key, value in row.items() if key in FIELDS}
        public["evidence"] = [item["path"] for item in evidence]
        validate_event(public)
        require_text(row.get("id"), "저장된 이벤트 id")
        validate_digest(row.get("_input_sha256"))
        validate_timestamp(row.get("recorded_at"))
        if row["id"] in seen:
            raise ValueError(f"중복 저장된 이벤트 id: {row['id']}")
        seen.add(row["id"])
    return header, records[1:]


def snapshot_file(raw_path, root):
    path = Path(raw_path).expanduser()
    if not path.is_absolute():
        path = root / path
    path = Path(os.path.abspath(path))
    result = {"path": str(path)}
    try:
        info = path.lstat()
        if stat.S_ISLNK(info.st_mode):
            result["state"] = "symlink"
        elif stat.S_ISDIR(info.st_mode):
            result["state"] = "directory"
        elif not stat.S_ISREG(info.st_mode):
            result["state"] = "non_regular"
        else:
            digest = hashlib.sha256()
            with path.open("rb") as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(chunk)
            result.update(state="file", sha256=digest.hexdigest(), bytes=info.st_size)
    except FileNotFoundError:
        result["state"] = "missing"
    except OSError as exc:
        result.update(state="unreadable", error=type(exc).__name__)
    return result


def md(value):
    return str(value).replace("\\", "\\\\").replace("|", "\\|").replace("<", "&lt;").replace(">", "&gt;").replace("\r", " ").replace("\n", "<br>")


def file_link(path):
    safe_path = path.replace("<", "%3C").replace(">", "%3E").replace("\n", "%0A").replace("\r", "%0D")
    label = Path(path).name.replace("[", "\\[").replace("]", "\\]")
    return f"[{md(label)}](<{safe_path}>)"


def timing_rows(events):
    groups = {}
    unpaired = 0
    for event in events:
        if event["kind"] not in {"start", "finish"}:
            continue
        if not event.get("task_id"):
            unpaired += 1
            continue
        groups.setdefault(event["task_id"], []).append(event)
    rows = []
    for task, entries in groups.items():
        starts = [e for e in entries if e["kind"] == "start"]
        finishes = [e for e in entries if e["kind"] == "finish"]
        if len(starts) != 1 or len(finishes) != 1:
            unpaired += len(entries)
            continue
        start, finish = starts[0], finishes[0]
        duration = (datetime.fromisoformat(finish["recorded_at"].replace("Z", "+00:00")) - datetime.fromisoformat(start["recorded_at"].replace("Z", "+00:00"))).total_seconds()
        if duration < 0:
            unpaired += len(entries)
            continue
        rows.append((task, start["recorded_at"], finish["recorded_at"], duration))
    return rows, unpaired


def build_report(header, events):
    lines = [MARKER, f"# {md(header['title'])}", "",
             f"초기화: {header['recorded_at']} · 기록된 체크포인트: {len(events)}개", "",
             "이 문서는 기록 명령을 호출할 때 자동 갱신됩니다. 모든 시각은 기록기가 관측한 UTC입니다. 호출하지 않은 작업은 수집하지 않습니다.", "",
             "검증 수준은 기록자가 선택한 분류입니다. 파일 해시는 당시 파일의 식별값이며 실행 성공이나 품질을 입증하지 않습니다. 완료 여부와 검증 수준을 함께 확인하세요.", "",
             "## 검증 근거 구분", "", "| 수준 | 이벤트 수 | 의미 |", "|---|---:|---|"]
    explanations = {"pending": "미실행·계획·근거 미제출", "static": "코드·설정·문서 등 정적 확인", "runtime": "실제로 실행한 테스트·플레이 확인", "device": "실기기에서 실행한 확인"}
    for level, label in LEVELS.items():
        count = sum(e.get("metadata", {}).get("validation_level", "pending") == level for e in events)
        lines.append(f"| {label} | {count} | {explanations[level]} |")
    lines.extend(["", "## 관측된 작업 구간", "",
                  "동일 task_id의 start 1개와 finish 1개가 있는 구간만 계산합니다. 대기·리뷰 시간도 포함하며 순수 노동시간이나 AI로 절약한 시간이 아닙니다. 병렬 작업 시간을 합산하지 않습니다.", ""])
    timings, unpaired = timing_rows(events)
    if timings:
        lines.extend(["| 작업 | 시작 UTC | 종료 UTC | 관측 경과 초 |", "|---|---|---|---:|"])
        for task, start, finish, duration in timings:
            lines.append(f"| {md(task)} | {start} | {finish} | {duration:.3f} |")
    else:
        lines.append("계산 가능한 start/finish 쌍이 아직 없습니다.")
    if unpaired:
        lines.extend(["", f"시작·종료 짝이 없거나 중복되어 시간 계산에서 제외한 이벤트: {unpaired}개."])
    lines.extend(["", "## 단계별 기록"])
    state_names = {"missing": "기록 시 파일 없음", "directory": "디렉터리 — 재귀 수집 안 함", "symlink": "심볼릭 링크 — 따라가지 않음", "non_regular": "일반 파일 아님", "unreadable": "읽을 수 없음"}
    for stage, label in STAGES.items():
        lines.extend(["", f"### {label}", ""])
        matching = [e for e in events if e["stage"] == stage]
        if not matching:
            lines.append("아직 기록되지 않았습니다.")
        for event in matching:
            level = LEVELS[event.get("metadata", {}).get("validation_level", "pending")]
            lines.append(f"- **{md(event['summary'])}**")
            lines.append(f"  - 기록: {event['recorded_at']} · 담당: {md(event['actor'])} · 종류: {event['kind']} · 상태: {md(event.get('status', '상태 미지정'))} · 검증: {level}")
            lines.append(f"  - 이벤트 ID: `{md(event['id'])}`" + (f" · 작업: `{md(event['task_id'])}`" if event.get("task_id") else ""))
            if not event["evidence"]:
                lines.append("  - 첨부된 파일 근거 없음")
            for item in event["evidence"]:
                detail = f"SHA256 `{item['sha256']}` · {item['bytes']} bytes" if item["state"] == "file" else state_names.get(item["state"], item["state"])
                lines.append(f"  - {file_link(item['path'])}: {detail}")
            extra = {k: v for k, v in event.get("metadata", {}).items() if k != "validation_level"}
            if extra:
                lines.append(f"  - 추가 기록: {md(canonical(extra))}")
    lines.extend(["", "## 기록의 한계", "",
                  "- 최초 요청 이전의 시간과 기록되지 않은 활동은 복원하지 않습니다.",
                  "- 비교 실험 없이 생산성 향상 배수나 절약 노동시간을 계산하지 않습니다.",
                  "- 현재 파일과의 차이는 verify로 확인합니다. 과거 이벤트와 해시는 수정하지 않습니다.",
                  "- 이 파일은 자동 생성됩니다. 본문을 편집하면 다음 기록 때 교체됩니다. 별도 설명은 사용자 문서에 작성하고 그 파일을 evidence로 연결하세요.", ""])
    return "\n".join(lines)


def write_report(root, header, events):
    assert_report_owned(root)
    content = build_report(header, events)
    temp_name = None
    try:
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=root, prefix=".REPORT.", suffix=".tmp", delete=False) as stream:
            temp_name = stream.name
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temp_name, root / REPORT_NAME)
    finally:
        if temp_name and os.path.exists(temp_name):
            os.unlink(temp_name)


def append_record(root, record, create=False):
    path = root / LOG_NAME
    mode = "x" if create else "a"
    with path.open(mode, encoding="utf-8") as stream:
        stream.write(canonical(record) + "\n")
        stream.flush()
        os.fsync(stream.fileno())


def initialize(root, title):
    require_text(title, "title")
    root.mkdir(parents=True, exist_ok=True)
    with directory_lock(root):
        assert_report_owned(root)
        if (root / LOG_NAME).exists() or (root / LOG_NAME).is_symlink():
            header, events = load_log(root)
            if header["title"] != title:
                raise ValueError("기존 기록의 제목과 다릅니다. 기존 기록은 변경하지 않았습니다.")
            created = False
        else:
            if (root / REPORT_NAME).exists():
                raise ValueError("이벤트 기록 없이 REPORT.md만 존재합니다. 기존 보고서를 보존하세요.")
            header = {"_type": "prototype-workflow.init", "schema_version": 1, "title": title, "recorded_at": utc_now()}
            append_record(root, header, create=True)
            events, created = [], True
        write_report(root, header, events)
    return {"status": "initialized" if created else "already_initialized", "root": str(root), "report": str(root / REPORT_NAME)}


def record_event(root, event):
    validate_event(event)
    input_hash = hashlib.sha256(canonical(event).encode("utf-8")).hexdigest()
    with directory_lock(root):
        assert_report_owned(root)
        header, events = load_log(root)
        for existing in events:
            same_id = "id" in event and existing["id"] == event["id"]
            same_implicit = "id" not in event and existing["_input_sha256"] == input_hash
            if same_id or same_implicit:
                if existing["_input_sha256"] != input_hash:
                    raise ValueError(f"같은 id에 다른 입력을 기록할 수 없습니다: {event['id']}")
                write_report(root, header, events)
                return {"status": "already_recorded", "id": existing["id"], "recorded_at": existing["recorded_at"]}
        saved = dict(event)
        saved.update(_type="prototype-workflow.event", id=event.get("id", str(uuid.uuid4())), recorded_at=utc_now(), _input_sha256=input_hash)
        saved["evidence"] = [snapshot_file(path, root) for path in event.get("evidence", [])]
        append_record(root, saved)
        events.append(saved)
        write_report(root, header, events)
    return {"status": "recorded", "id": saved["id"], "recorded_at": saved["recorded_at"], "report": str(root / REPORT_NAME)}


def render(root):
    with directory_lock(root):
        header, events = load_log(root)
        write_report(root, header, events)
    return {"status": "rendered", "events": len(events), "report": str(root / REPORT_NAME)}


def verify(root):
    with directory_lock(root):
        _, events = load_log(root)
        entries = []
        for event in events:
            for previous in event["evidence"]:
                current = snapshot_file(previous["path"], root)
                if previous["state"] == "file" and current["state"] == "file":
                    status = "unchanged" if previous["sha256"] == current["sha256"] else "changed"
                elif previous["state"] == "file":
                    status = "missing" if current["state"] == "missing" else "unavailable"
                elif current["state"] == "file":
                    status = "now_available"
                else:
                    status = "unverified"
                entries.append({"event_id": event["id"], "path": previous["path"], "status": status,
                                "recorded": previous, "current": current})
    return {"status": "verified", "checked_at": utc_now(), "evidence": entries,
            "counts": {label: sum(item["status"] == label for item in entries) for label in ("unchanged", "changed", "missing", "unavailable", "now_available", "unverified")}}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    for command in ("init", "record", "render", "verify"):
        part = sub.add_parser(command)
        part.add_argument("--root", required=True, type=Path)
        if command == "init":
            part.add_argument("--title", required=True)
        elif command == "record":
            part.add_argument("--event-file", required=True, type=Path)
    args = parser.parse_args(argv)
    root = Path(os.path.abspath(args.root.expanduser()))
    try:
        if args.command == "init":
            result = initialize(root, args.title)
        elif args.command == "record":
            event = parse_json(args.event_file.read_text(encoding="utf-8"))
            result = record_event(root, event)
        elif args.command == "render":
            result = render(root)
        else:
            result = verify(root)
    except (ValueError, OSError, KeyError, TypeError) as exc:
        print(json.dumps({"status": "error", "message": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 1
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

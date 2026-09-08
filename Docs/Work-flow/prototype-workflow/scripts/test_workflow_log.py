#!/usr/bin/env python3
"""Behavioral tests; all records and artifacts stay in temporary directories."""

from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
import workflow_log as log


class WorkflowLogTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "workflow"
        self.script = Path(log.__file__).resolve()

    def initialize(self):
        return log.initialize(self.root, "프로토타입 제작 증거")

    def event(self, **overrides):
        value = {"id": "check-1", "stage": "build", "kind": "checkpoint", "actor": "builder", "summary": "첫 플레이 준비"}
        value.update(overrides)
        return value

    def file_bytes(self):
        return (self.root / log.LOG_NAME).read_bytes(), (self.root / log.REPORT_NAME).read_bytes()

    def test_init_and_retries_preserve_original_event_and_hash(self):
        self.assertEqual(self.initialize()["status"], "initialized")
        self.assertEqual(self.initialize()["status"], "already_initialized")
        artifact = Path(self.temp.name) / "result.txt"
        artifact.write_text("first", encoding="utf-8")
        event = self.event(evidence=[str(artifact)])
        recorded = log.record_event(self.root, event)
        before = self.file_bytes()
        artifact.write_text("second", encoding="utf-8")
        retried = log.record_event(self.root, event)
        self.assertEqual(retried["status"], "already_recorded")
        self.assertEqual(recorded["recorded_at"], retried["recorded_at"])
        self.assertEqual(before, self.file_bytes())
        with self.assertRaises(ValueError):
            log.record_event(self.root, self.event(summary="different"))
        self.assertEqual(before, self.file_bytes())

    def test_implicit_ids_deduplicate_and_explicit_ids_repeat(self):
        self.initialize()
        event = self.event()
        del event["id"]
        first = log.record_event(self.root, event)
        second = log.record_event(self.root, event)
        self.assertEqual(first["id"], second["id"])
        log.record_event(self.root, self.event(id="intentional-repeat"))
        self.assertEqual(len(log.load_log(self.root)[1]), 2)

    def test_file_evidence_tracks_missing_changed_and_nonrecursive_paths(self):
        self.initialize()
        existing = self.root / "existing.txt"
        missing = self.root / "missing.txt"
        folder = self.root / "folder"
        folder.mkdir()
        (folder / "nested.txt").write_text("do not collect", encoding="utf-8")
        existing.write_text("first", encoding="utf-8")
        symlink = self.root / "linked.txt"
        symlink.symlink_to(existing)
        log.record_event(self.root, self.event(evidence=["existing.txt", "missing.txt", "folder", "linked.txt"]))
        stored = log.load_log(self.root)[1][0]["evidence"]
        self.assertEqual([e["state"] for e in stored], ["file", "missing", "directory", "symlink"])
        self.assertEqual(len(stored), 4)
        self.assertEqual(log.verify(self.root)["counts"]["unchanged"], 1)
        existing.write_text("changed", encoding="utf-8")
        missing.write_text("now exists", encoding="utf-8")
        result = log.verify(self.root)
        self.assertEqual(result["counts"]["changed"], 1)
        self.assertEqual(result["counts"]["now_available"], 1)
        self.assertEqual(result["counts"]["unverified"], 2)
        existing.unlink()
        self.assertEqual(log.verify(self.root)["counts"]["missing"], 1)

    def test_invalid_event_never_modifies_existing_log_or_report(self):
        self.initialize()
        log.record_event(self.root, self.event())
        before = self.file_bytes()
        bad_events = [self.event(id="bad", recorded_at="2000-01-01T00:00:00Z"),
                      self.event(id="bad", stage="unknown"), self.event(id="bad", evidence=[{}]),
                      self.event(id="bad", metadata={"validation_level": "passed"}),
                      self.event(id="bad", kind="bogus"), self.event(id="bad", actor="")]
        for event in bad_events:
            with self.subTest(event=event):
                with self.assertRaises(ValueError):
                    log.record_event(self.root, event)
                self.assertEqual(before, self.file_bytes())
        bad_file = Path(self.temp.name) / "invalid.json"
        bad_file.write_text('{"stage": "build",', encoding="utf-8")
        run = subprocess.run([sys.executable, str(self.script), "record", "--root", str(self.root), "--event-file", str(bad_file)], capture_output=True, text=True)
        self.assertEqual(run.returncode, 1)
        self.assertEqual(before, self.file_bytes())

    def test_unowned_documents_and_logs_are_preserved(self):
        self.root.mkdir()
        report = self.root / log.REPORT_NAME
        report.write_text("사용자가 작성한 문서", encoding="utf-8")
        with self.assertRaises(ValueError):
            self.initialize()
        self.assertEqual(report.read_text(encoding="utf-8"), "사용자가 작성한 문서")
        self.assertFalse((self.root / log.LOG_NAME).exists())
        report.unlink()
        (self.root / log.LOG_NAME).write_text('{"other": true}\n', encoding="utf-8")
        with self.assertRaises(ValueError):
            self.initialize()
        self.assertEqual((self.root / log.LOG_NAME).read_text(encoding="utf-8"), '{"other": true}\n')
        self.assertFalse(report.exists())

    def test_replaced_report_blocks_append_and_symlink_is_preserved(self):
        self.initialize()
        report = self.root / log.REPORT_NAME
        report.write_text("사용자 교체 문서", encoding="utf-8")
        before = self.file_bytes()
        with self.assertRaises(ValueError):
            log.record_event(self.root, self.event())
        self.assertEqual(before, self.file_bytes())
        report.unlink()
        target = Path(self.temp.name) / "user.md"
        target.write_text("보존할 파일", encoding="utf-8")
        report.symlink_to(target)
        with self.assertRaises(ValueError):
            log.render(self.root)
        self.assertEqual(target.read_text(encoding="utf-8"), "보존할 파일")

    def test_concurrent_process_appends_and_retries_are_serialized(self):
        self.initialize()
        inputs = []
        for index in range(12):
            path = Path(self.temp.name) / f"event-{index}.json"
            path.write_text(json.dumps(self.event(id=f"parallel-{index}", summary=f"작업 {index}")), encoding="utf-8")
            inputs.append(path)

        def invoke(path):
            return subprocess.run([sys.executable, str(self.script), "record", "--root", str(self.root), "--event-file", str(path)], capture_output=True, text=True)

        with ThreadPoolExecutor(max_workers=8) as pool:
            results = list(pool.map(invoke, inputs + inputs[:4]))
        for result in results:
            self.assertEqual(result.returncode, 0, result.stderr)
        header, events = log.load_log(self.root)
        self.assertEqual(len(events), 12)
        self.assertEqual(len({event["id"] for event in events}), 12)
        self.assertEqual((self.root / log.REPORT_NAME).read_text(encoding="utf-8"), log.build_report(header, events))
        self.assertFalse((self.root / ".workflow-log.lock").exists())

    def test_timing_requires_one_matching_task_pair_and_never_sums(self):
        def event(event_id, kind, task, second):
            return self.event(id=event_id, kind=kind, task_id=task, recorded_at=f"2026-09-08T00:00:{second:02d}Z")
        rows, unpaired = log.timing_rows([
            event("s1", "start", "a", 0), event("s2", "start", "b", 1),
            event("f1", "finish", "a", 5), event("f2", "finish", "b", 5),
            event("s3", "start", "c", 2), event("f4", "finish", "d", 5),
        ])
        self.assertEqual([(r[0], r[3]) for r in rows], [("a", 5), ("b", 4)])
        self.assertEqual(unpaired, 2)
        self.initialize()
        for level in log.LEVELS:
            log.record_event(self.root, self.event(id=level, metadata={"validation_level": level}))
        self.assertEqual(len(log.load_log(self.root)[1]), 4)

    def test_render_recovers_missing_report_without_rewriting_log(self):
        self.initialize()
        log.record_event(self.root, self.event())
        old_log, report = self.file_bytes()
        (self.root / log.REPORT_NAME).unlink()
        log.render(self.root)
        self.assertEqual(self.file_bytes(), (old_log, report))

    def test_corrupt_log_is_not_extended_or_silently_repaired(self):
        self.initialize()
        with (self.root / log.LOG_NAME).open("a", encoding="utf-8") as stream:
            stream.write('{"partial":')
        before = self.file_bytes()
        with self.assertRaises(ValueError):
            log.record_event(self.root, self.event())
        self.assertEqual(before, self.file_bytes())

    def test_incomplete_saved_evidence_blocks_future_appends(self):
        self.initialize()
        artifact = self.root / "artifact.txt"
        artifact.write_text("evidence", encoding="utf-8")
        log.record_event(self.root, self.event(evidence=[str(artifact)]))
        log_file = self.root / log.LOG_NAME
        records = [json.loads(line) for line in log_file.read_text(encoding="utf-8").splitlines()]
        del records[1]["evidence"][0]["sha256"]
        log_file.write_text("".join(json.dumps(row) + "\n" for row in records), encoding="utf-8")
        before = self.file_bytes()
        with self.assertRaises(ValueError):
            log.record_event(self.root, self.event(id="next"))
        self.assertEqual(before, self.file_bytes())


if __name__ == "__main__":
    unittest.main()

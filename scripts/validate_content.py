#!/usr/bin/env python3
"""Validate mission definitions and fixtures against CONTRACT.md v1.0.

Usage: python scripts/validate_content.py
Exit code 0 = all valid, 1 = at least one problem.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

EVENTS = {
    "calibration_completed", "target_located", "target_selected", "control_activated",
    "checklist_opened", "object_grabbed", "object_released", "object_placed",
    "status_reported", "paused", "resumed", "recentered",
}
INPUTS = {"controller_ray", "controller_grip", "hand_pinch", "hand_grab", "simulator"}
RESULTS = {"correct", "incorrect", "neutral"}
EVENT_KEYS = {"sessionId", "missionId", "timestampMs", "event", "targetId", "input", "result"}
STEP_KEYS = {"stepId", "order", "instruction", "expectedEvent", "targetId", "critical"}
MISSION_KEYS = {"missionId", "version", "title", "fictional", "steps"}


def event_errors(e: dict) -> list[str]:
    errs = []
    if set(e) != EVENT_KEYS:
        errs.append(f"keys {sorted(set(e) ^ EVENT_KEYS)} missing/unknown")
    if not isinstance(e.get("timestampMs"), int) or e["timestampMs"] < 0:
        errs.append("timestampMs must be an integer >= 0")
    if e.get("event") not in EVENTS:
        errs.append(f"event {e.get('event')!r} not in contract")
    if e.get("input") not in INPUTS:
        errs.append(f"input {e.get('input')!r} not in contract")
    if e.get("result") not in RESULTS:
        errs.append(f"result {e.get('result')!r} not in contract")
    if not e.get("targetId"):
        errs.append("targetId is required")
    return errs


def mission_errors(m: dict) -> list[str]:
    errs = []
    if set(m) != MISSION_KEYS:
        errs.append(f"mission keys {sorted(set(m) ^ MISSION_KEYS)} missing/unknown")
    if m.get("fictional") is not True:
        errs.append("fictional must be true until SME review")
    orders = []
    for s in m.get("steps", []):
        if set(s) != STEP_KEYS:
            errs.append(f"step {s.get('stepId')}: keys {sorted(set(s) ^ STEP_KEYS)} missing/unknown")
        if s.get("expectedEvent") not in EVENTS:
            errs.append(f"step {s.get('stepId')}: expectedEvent not in contract")
        orders.append(s.get("order"))
    if len(orders) != len(set(orders)):
        errs.append("step order values must be unique")
    return errs


def main() -> int:
    failures = 0

    for path in sorted((ROOT / "Assets/ASTRA/Content/Missions").glob("*.json")):
        errs = mission_errors(json.loads(path.read_text(encoding="utf-8")))
        failures += report(path, errs, expect_valid=True)

    for path in sorted((ROOT / "fixtures").glob("session-events.*.json")):
        events = json.loads(path.read_text(encoding="utf-8"))
        errs = [f"[{i}] {msg}" for i, e in enumerate(events) for msg in event_errors(e)]
        failures += report(path, errs, expect_valid=".error." not in path.name)

    for path in sorted((ROOT / "fixtures").glob("*.json")):
        json.loads(path.read_text(encoding="utf-8"))  # every fixture must at least parse

    print("OK" if failures == 0 else f"{failures} file(s) failed")
    return 1 if failures else 0


def report(path: Path, errs: list[str], expect_valid: bool) -> int:
    rel = path.relative_to(ROOT)
    if expect_valid and errs:
        print(f"FAIL {rel}")
        for e in errs:
            print(f"   - {e}")
        return 1
    if not expect_valid and not errs:
        print(f"FAIL {rel}: error fixture was accepted, validator is too loose")
        return 1
    print(f"ok   {rel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

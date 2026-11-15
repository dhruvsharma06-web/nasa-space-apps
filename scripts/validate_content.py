#!/usr/bin/env python3
"""Validate mission definitions and fixtures against CONTRACT.md v1.1.

Also contains a reference implementation of the step-matching + scoring rule
(CONTRACT.md §4-§5). It re-scores the event fixtures and checks they match the
expected session-result fixtures, so the C# scorer has a verified oracle.

Usage: python scripts/validate_content.py
Exit code 0 = all valid, 1 = at least one problem.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MISSIONS_DIR = ROOT / "Assets/ASTRA/Content/Missions"
FIXTURES_DIR = ROOT / "fixtures"

EVENTS = {
    "calibration_completed", "target_located", "target_selected", "control_activated",
    "checklist_opened", "object_grabbed", "object_released", "object_placed",
    "status_reported", "paused", "resumed", "recentered",
}
NEUTRAL_WHEN_UNMATCHED = {"target_located", "object_grabbed", "object_released", "paused", "resumed", "recentered"}
INPUTS = {"controller_ray", "controller_grip", "hand_pinch", "hand_grab", "simulator", "gaze"}
RESULTS = {"correct", "incorrect", "neutral"}

EVENT_KEYS = {"sessionId", "missionId", "timestampMs", "event", "targetId", "input", "result"}
STEP_KEYS = {"stepId", "order", "instruction", "hint", "expectedEvent", "targetId", "critical"}
MISSION_KEYS = {"missionId", "level", "version", "title", "fictional", "steps"}
RESULT_KEYS = {
    "sessionId", "missionId", "passed", "completedSteps", "skippedSteps",
    "omittedCriticalSteps", "sequenceErrors", "incorrectActions", "completionTimeMs",
}
COACH_KEYS = {"sessionId", "missionId", "advisory", "summary", "hints", "modelVersion"}
ERROR_CODES = {"INVALID_INPUT", "NOT_FOUND", "PAYLOAD_TOO_LARGE", "RATE_LIMITED", "UPSTREAM_UNAVAILABLE", "INTERNAL"}


# ---------------------------------------------------------------- validation

def mission_errors(m: dict, filename_stem: str) -> list[str]:
    errs = []
    if set(m) != MISSION_KEYS:
        errs.append(f"mission keys {sorted(set(m) ^ MISSION_KEYS)} missing/unknown")
    if m.get("missionId") != filename_stem:
        errs.append(f"missionId {m.get('missionId')!r} must equal the file name {filename_stem!r}")
    if not isinstance(m.get("level"), int) or not 0 <= m["level"] <= 8:
        errs.append("level must be an integer 0-8")
    elif not str(m.get("missionId", "")).startswith(f"l{m['level']}-"):
        errs.append(f"missionId must start with 'l{m['level']}-'")
    if m.get("fictional") is not True:
        errs.append("fictional must be true until SME review")
    steps = m.get("steps", [])
    if not steps:
        errs.append("mission needs at least one step")
    if not any(s.get("critical") is True for s in steps):
        errs.append("mission needs at least one critical step")
    seen_ids, seen_orders, seen_pairs = set(), set(), set()
    for s in steps:
        sid = s.get("stepId")
        if set(s) != STEP_KEYS:
            errs.append(f"step {sid}: keys {sorted(set(s) ^ STEP_KEYS)} missing/unknown")
        if s.get("expectedEvent") not in EVENTS:
            errs.append(f"step {sid}: expectedEvent {s.get('expectedEvent')!r} not in contract")
        for field in ("stepId", "instruction", "hint", "targetId"):
            if not isinstance(s.get(field), str) or not s.get(field).strip():
                errs.append(f"step {sid}: {field} must be a non-empty string")
        if not isinstance(s.get("order"), int) or s["order"] < 1:
            errs.append(f"step {sid}: order must be an integer >= 1")
        if not isinstance(s.get("critical"), bool):
            errs.append(f"step {sid}: critical must be true/false")
        if sid in seen_ids:
            errs.append(f"duplicate stepId {sid!r}")
        if s.get("order") in seen_orders:
            errs.append(f"duplicate order {s.get('order')}")
        pair = (s.get("expectedEvent"), s.get("targetId"))
        if pair in seen_pairs:
            errs.append(f"step {sid}: another step already expects {pair}; steps must be distinguishable")
        seen_ids.add(sid)
        seen_orders.add(s.get("order"))
        seen_pairs.add(pair)
    return errs


def event_errors(e: dict, mission_id: str | None, prev_ts: int) -> list[str]:
    errs = []
    if set(e) != EVENT_KEYS:
        errs.append(f"keys {sorted(set(e) ^ EVENT_KEYS)} missing/unknown")
    if not e.get("sessionId"):
        errs.append("sessionId is required")
    if mission_id is not None and e.get("missionId") != mission_id:
        errs.append(f"missionId {e.get('missionId')!r} does not match the mission")
    ts = e.get("timestampMs")
    if not isinstance(ts, int) or isinstance(ts, bool) or ts < 0:
        errs.append("timestampMs must be an integer >= 0")
    elif ts < prev_ts:
        errs.append("timestampMs must not decrease")
    if e.get("event") not in EVENTS:
        errs.append(f"event {e.get('event')!r} not in contract")
    if e.get("input") not in INPUTS:
        errs.append(f"input {e.get('input')!r} not in contract")
    if e.get("result") not in RESULTS:
        errs.append(f"result {e.get('result')!r} not in contract")
    if not e.get("targetId"):
        errs.append("targetId is required")
    return errs


# ------------------------------------------------- reference scoring (oracle)

def match(steps: list[dict], pointer: int, event: str, target: str) -> tuple[str, int | None]:
    """Step-matching rule, CONTRACT.md §4. Returns (outcome, matched step index)."""
    for k in range(pointer, len(steps)):
        if steps[k]["expectedEvent"] == event and steps[k]["targetId"] == target:
            if k == pointer or all(not steps[j]["critical"] for j in range(pointer, k)):
                return "Correct", k
            return "OutOfOrder", k
    if any(s["expectedEvent"] == event and s["targetId"] == target for s in steps[:pointer]):
        return "Ignored", None  # repeat of an already-completed step
    if event in NEUTRAL_WHEN_UNMATCHED:
        return "Ignored", None
    return "Incorrect", None


def score(mission: dict, session_id: str, trace: list[dict]) -> dict:
    """Scoring rule, CONTRACT.md §5."""
    steps = sorted(mission["steps"], key=lambda s: s["order"])
    prev = 0
    for e in trace:
        errs = event_errors(e, mission["missionId"], prev)
        if errs:
            raise ValueError("; ".join(errs))
        prev = e["timestampMs"]

    pointer = 0
    completed, skipped, sequence_errors, incorrect = [], [], [], []
    for e in trace:
        outcome, k = match(steps, pointer, e["event"], e["targetId"])
        if outcome == "Correct":
            skipped += [steps[j]["stepId"] for j in range(pointer, k)]
            completed.append(steps[k]["stepId"])
            pointer = k + 1
        elif outcome == "OutOfOrder":
            sequence_errors.append(steps[k]["stepId"])
        elif outcome == "Incorrect":
            incorrect.append(e["targetId"])

    remaining = steps[pointer:]
    omitted = [s["stepId"] for s in remaining if s["critical"]]
    skipped += [s["stepId"] for s in remaining if not s["critical"]]
    return {
        "sessionId": session_id,
        "missionId": mission["missionId"],
        "passed": not omitted,
        "completedSteps": completed,
        "skippedSteps": skipped,
        "omittedCriticalSteps": omitted,
        "sequenceErrors": sequence_errors,
        "incorrectActions": incorrect,
        "completionTimeMs": trace[-1]["timestampMs"] if trace else 0,
    }


def stamp(mission: dict, trace: list[dict]) -> list[str]:
    """The 'result' the mission runner must write for each event (CONTRACT.md §4)."""
    steps = sorted(mission["steps"], key=lambda s: s["order"])
    pointer, out = 0, []
    for e in trace:
        outcome, k = match(steps, pointer, e["event"], e["targetId"])
        if outcome == "Correct":
            pointer = k + 1
        out.append({"Correct": "correct", "Ignored": "neutral"}.get(outcome, "incorrect"))
    return out


# ------------------------------------------------------------- layouts (v1.4)

LAYOUTS_DIR = ROOT / "Assets/ASTRA/Content/Layouts"
LAYOUT_KEYS = {"missionId", "scene", "panels", "controls", "tools", "decoys", "indicators"}
CONTROL_TYPES = {"button", "toggle", "dial", "lever", "checklist", "socket"}
ID_RE = __import__("re").compile(r"^[a-z0-9_]{1,64}$")


def layout_errors(lay: dict, filename_stem: str, missions: dict) -> list[str]:
    errs = []
    extra = set(lay) - LAYOUT_KEYS
    if extra: errs.append(f"unknown keys {sorted(extra)}")
    m = missions.get(lay.get("missionId"))
    if m is None: return errs + [f"missionId {lay.get('missionId')!r} is not a known mission"]
    if filename_stem != f"l{m['level']}-layout": errs.append(f"file must be named l{m['level']}-layout.json")
    def reach(pos, what):
        if not (isinstance(pos, list) and len(pos) == 3 and all(isinstance(x, (int, float)) for x in pos)):
            errs.append(f"{what}: position must be [x, y, z]"); return
        x, y, z = pos
        if (x * x + z * z) ** 0.5 > 0.7 + 1e-9: errs.append(f"{what}: {round((x*x+z*z)**0.5, 3)} m from the seat (max 0.7)")
        if not 0.7 <= y <= 1.5: errs.append(f"{what}: height {y} m (must be 0.7-1.5)")
        if z < 0: errs.append(f"{what}: behind the seat (z < 0)")
    panels = {}
    for p in lay.get("panels", []):
        pid = p.get("id", "?")
        if pid in panels: errs.append(f"duplicate panel id {pid!r}")
        panels[pid] = p
        reach(p.get("position"), f"panel {pid}")
        if not (isinstance(p.get("size"), list) and len(p["size"]) == 2): errs.append(f"panel {pid}: size must be [w, h]")
        if not p.get("title"): errs.append(f"panel {pid}: title required")
    placed = []
    tool_ids = {t.get("targetId") for t in lay.get("tools", [])}
    for group in ("controls", "decoys"):
        for c in lay.get(group, []):
            tid = c.get("targetId", "?"); placed.append((group, tid))
            if not ID_RE.match(tid): errs.append(f"{group} {tid!r}: bad targetId")
            if c.get("type") not in CONTROL_TYPES: errs.append(f"{group} {tid}: type {c.get('type')!r} not in {sorted(CONTROL_TYPES)}")
            if c.get("panel") not in panels: errs.append(f"{group} {tid}: unknown panel {c.get('panel')!r}")
            else:
                w, h = panels[c["panel"]].get("size", [0, 0])
                at = c.get("at", [None, None])
                if not (isinstance(at, list) and len(at) == 2 and abs(at[0]) <= w / 2 and abs(at[1]) <= h / 2):
                    errs.append(f"{group} {tid}: 'at' {at} is outside its panel")
            if not c.get("label"): errs.append(f"{group} {tid}: label required")
            if "trigger" in c:
                typ = c.get("type")
                if typ in ("toggle", "lever"):
                    if c["trigger"] not in ("ON", "OFF"): errs.append(f"{group} {tid}: trigger must be ON or OFF")
                elif typ == "dial":
                    if c["trigger"] not in (c.get("positions") or ["OFF", "ON"]): errs.append(f"{group} {tid}: trigger must be one of its positions")
                else: errs.append(f"{group} {tid}: trigger is only valid on toggle, lever or dial")
            if c.get("type") == "socket" and c.get("accepts") not in tool_ids:
                errs.append(f"{group} {tid}: socket 'accepts' must name a tool in this layout")
    for t in lay.get("tools", []):
        placed.append(("tools", t.get("targetId", "?")))
        reach(t.get("position"), f"tool {t.get('targetId')}")
    for pid, p in panels.items():
        if p.get("target"): placed.append(("panels", pid))
    for i in lay.get("indicators", []):
        if i.get("panel") not in panels: errs.append(f"indicator {i.get('id')}: unknown panel")
    mission_targets = {s["targetId"] for s in m["steps"]}
    ids = [tid for _, tid in placed]
    for tid in sorted(mission_targets):
        n = ids.count(tid)
        if n != 1: errs.append(f"mission target {tid!r} placed {n} times (must be exactly 1)")
    for group, tid in placed:
        if group == "decoys" and tid in mission_targets: errs.append(f"decoy {tid!r} is a mission target")
        if group != "decoys" and tid not in mission_targets and not (group == "panels"):
            errs.append(f"{group} {tid!r} is not used by the mission (make it a decoy)")
    return errs


# ---------------------------------------------------------------------- main

def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> int:
    failures = 0
    missions: dict[str, dict] = {}
    levels: dict[int, str] = {}

    for path in sorted(MISSIONS_DIR.glob("*.json")):
        m = load(path)
        errs = mission_errors(m, path.stem)
        if isinstance(m.get("level"), int):
            if m["level"] in levels:
                errs.append(f"level {m['level']} already used by {levels[m['level']]}")
            levels[m["level"]] = path.stem
        failures += report(path, errs)
        missions[m.get("missionId")] = m

    for path in sorted(LAYOUTS_DIR.glob("*.json")) if LAYOUTS_DIR.exists() else []:
        failures += report(path, layout_errors(load(path), path.stem, missions))

    for path in sorted(FIXTURES_DIR.glob("*.json")):
        load(path)  # every fixture must at least parse

    mission = missions.get("l7-restore-power-v1")
    if mission is None:
        print("FAIL l7-restore-power-v1 mission is required by the fixtures")
        return 1

    # Event fixtures: ok/empty valid, error must be rejected.
    for name in ("ok", "empty"):
        path = FIXTURES_DIR / f"session-events.{name}.json"
        events, prev, errs = load(path), 0, []
        for i, e in enumerate(events):
            errs += [f"[{i}] {msg}" for msg in event_errors(e, mission["missionId"], prev)]
            prev = e.get("timestampMs", prev) if isinstance(e.get("timestampMs"), int) else prev
        if not errs and stamp(mission, events) != [e["result"] for e in events]:
            errs.append(f"'result' values must be {stamp(mission, events)} (runner rule)")
        failures += report(path, errs)
    err_path = FIXTURES_DIR / "session-events.error.json"
    try:
        score(mission, "demo-002", load(err_path))
        failures += report(err_path, ["error fixture was accepted; validator is too loose"])
    except ValueError:
        failures += report(err_path, [])

    # Expected results must equal the reference scorer's output.
    for name, session_id in (("ok", "demo-001"), ("empty", "demo-003")):
        events = load(FIXTURES_DIR / f"session-events.{name}.json")
        expected_path = FIXTURES_DIR / f"session-result.{name}.json"
        expected = load(expected_path)
        errs = []
        if set(expected) != RESULT_KEYS:
            errs.append(f"keys {sorted(set(expected) ^ RESULT_KEYS)} missing/unknown")
        actual = score(mission, session_id, events)
        for key in RESULT_KEYS & set(expected):
            if expected[key] != actual[key]:
                errs.append(f"{key}: fixture {expected[key]!r} != reference scorer {actual[key]!r}")
        failures += report(expected_path, errs)

    # Hand-specified scoring cases (used by the C# scorer/runner tests).
    cases_path = FIXTURES_DIR / "scoring-cases.json"
    cases_doc, errs = load(cases_path), []
    case_mission = missions.get(cases_doc.get("missionId"))
    if case_mission is None:
        errs.append(f"unknown missionId {cases_doc.get('missionId')!r}")
    else:
        for case in cases_doc.get("cases", []):
            try:
                actual = score(case_mission, case["sessionId"], case["events"])
            except ValueError as exc:
                errs.append(f"{case['name']}: invalid events: {exc}")
                continue
            for key in RESULT_KEYS:
                if case["expected"].get(key) != actual[key]:
                    errs.append(f"{case['name']}.{key}: expected {case['expected'].get(key)!r} != reference {actual[key]!r}")
            stamped = stamp(case_mission, case["events"])
            if stamped != [e["result"] for e in case["events"]]:
                errs.append(f"{case['name']}: event 'result' values must be {stamped}")
    failures += report(cases_path, errs)

    # Coach fixtures.
    for name in ("ok", "empty"):
        path = FIXTURES_DIR / f"coach-debrief.{name}.json"
        c, errs = load(path), []
        missing = COACH_KEYS - set(c)
        if missing:
            errs.append(f"missing keys {sorted(missing)}")
        if c.get("advisory") is not True:
            errs.append("advisory must be true")
        failures += report(path, errs)
    # Ask-the-coach fixtures (CONTRACT v1.3 §9.2)
    ask = load(FIXTURES_DIR / "coach-ask.ok.json")
    ask_keys = {"sessionId", "missionId", "advisory", "answer", "stepId", "modelVersion"}
    errs = [] if set(ask) == ask_keys else [f"keys {sorted(set(ask) ^ ask_keys)} missing/unknown"]
    if ask.get("advisory") is not True: errs.append("advisory must be true")
    if not isinstance(ask.get("answer"), str) or not 0 < len(ask["answer"]) <= 400: errs.append("answer must be 1-400 chars")
    m = missions.get(ask.get("missionId"))
    if m is None or ask.get("stepId") not in {s["stepId"] for s in m["steps"]}: errs.append("stepId must exist in the mission")
    failures += report(FIXTURES_DIR / "coach-ask.ok.json", errs)
    code = load(FIXTURES_DIR / "coach-ask.error.json").get("error", {}).get("code")
    failures += report(FIXTURES_DIR / "coach-ask.error.json", [] if code in ERROR_CODES else [f"error.code {code!r} not in contract"])

    path = FIXTURES_DIR / "coach-debrief.error.json"
    c = load(path)
    code = c.get("error", {}).get("code")
    failures += report(path, [] if code in ERROR_CODES else [f"error.code {code!r} not in contract"])

    print("OK" if failures == 0 else f"{failures} file(s) failed")
    return 1 if failures else 0


def report(path: Path, errs: list[str]) -> int:
    rel = path.relative_to(ROOT).as_posix()
    if errs:
        print(f"FAIL {rel}")
        for e in errs:
            print(f"   - {e}")
        return 1
    print(f"ok   {rel}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

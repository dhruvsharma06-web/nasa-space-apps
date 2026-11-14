// ASTRA Academy — browser reference implementation of CONTRACT.md §1, §4, §5.
// Mirrors scripts/validate_content.py (match / score / stamp). The C# MissionRunner and
// MissionScorer must give identical results; tools/level-sim/test_scoring.mjs proves this
// file against every fixture. Pure functions: no clock, no randomness, no I/O.

export const EVENTS = [
  "calibration_completed", "target_located", "target_selected", "control_activated",
  "checklist_opened", "object_grabbed", "object_released", "object_placed",
  "status_reported", "paused", "resumed", "recentered",
];
export const NEUTRAL_WHEN_UNMATCHED = ["target_located", "object_grabbed", "object_released", "paused", "resumed", "recentered"];
export const INPUTS = ["controller_ray", "controller_grip", "hand_pinch", "hand_grab", "simulator", "gaze"];
export const RESULTS = ["correct", "incorrect", "neutral"];
const EVENT_KEYS = ["sessionId", "missionId", "timestampMs", "event", "targetId", "input", "result"];

export const sortedSteps = mission => [...mission.steps].sort((a, b) => a.order - b.order);

const same = (step, event, target) => step.expectedEvent === event && step.targetId === target;

/** Step-matching rule, CONTRACT.md §4. Returns [outcome, matchedIndex|null]. */
export function match(steps, pointer, event, target) {
  for (let k = pointer; k < steps.length; k++) {
    if (same(steps[k], event, target)) {
      const between = steps.slice(pointer, k);
      if (k === pointer || between.every(s => !s.critical)) return ["Correct", k];
      return ["OutOfOrder", k];
    }
  }
  if (steps.slice(0, pointer).some(s => same(s, event, target))) return ["Ignored", null]; // repeat
  if (NEUTRAL_WHEN_UNMATCHED.includes(event)) return ["Ignored", null];
  return ["Incorrect", null];
}

export const resultFor = outcome => (outcome === "Correct" ? "correct" : outcome === "Ignored" ? "neutral" : "incorrect");

const isInt = v => typeof v === "number" && Number.isInteger(v);

/** Validates one SessionEvent (CONTRACT.md §1). Returns an error message or null. */
export function eventError(e, missionId, prevTs) {
  const keys = Object.keys(e ?? {});
  const extra = keys.filter(k => !EVENT_KEYS.includes(k)), missing = EVENT_KEYS.filter(k => !keys.includes(k));
  if (extra.length || missing.length) return `keys missing/unknown: ${[...missing, ...extra].join(", ")}`;
  if (!e.sessionId) return "sessionId is required";
  if (missionId != null && e.missionId !== missionId) return `missionId '${e.missionId}' does not match the mission`;
  if (!isInt(e.timestampMs) || e.timestampMs < 0) return "timestampMs must be an integer >= 0";
  if (e.timestampMs < prevTs) return "timestampMs must not decrease";
  if (!EVENTS.includes(e.event)) return `event '${e.event}' is not a contract event`;
  if (!INPUTS.includes(e.input)) return `input '${e.input}' is not a contract input`;
  if (!RESULTS.includes(e.result)) return `result '${e.result}' is not a contract result`;
  if (!e.targetId) return "targetId is required";
  return null;
}

/** Deterministic scoring, CONTRACT.md §5. Throws Error on the first invalid event. */
export function score(mission, sessionId, trace) {
  const steps = sortedSteps(mission);
  let prev = 0;
  trace.forEach((e, i) => {
    const err = eventError(e, mission.missionId, prev);
    if (err) throw new Error(`event ${i}: ${err}`);
    prev = e.timestampMs;
  });
  let pointer = 0;
  const completed = [], skipped = [], sequenceErrors = [], incorrect = [];
  for (const e of trace) {
    const [outcome, k] = match(steps, pointer, e.event, e.targetId);
    if (outcome === "Correct") {
      for (let j = pointer; j < k; j++) skipped.push(steps[j].stepId);
      completed.push(steps[k].stepId);
      pointer = k + 1;
    } else if (outcome === "OutOfOrder") {
      sequenceErrors.push(steps[k].stepId);
    } else if (outcome === "Incorrect") {
      incorrect.push(e.targetId);
    }
  }
  const remaining = steps.slice(pointer);
  const omitted = remaining.filter(s => s.critical).map(s => s.stepId);
  skipped.push(...remaining.filter(s => !s.critical).map(s => s.stepId));
  return {
    sessionId,
    missionId: mission.missionId,
    passed: omitted.length === 0,
    completedSteps: completed,
    skippedSteps: skipped,
    omittedCriticalSteps: omitted,
    sequenceErrors,
    incorrectActions: incorrect,
    completionTimeMs: trace.length ? trace[trace.length - 1].timestampMs : 0,
  };
}

/** Live step tracking for one attempt, CONTRACT.md §4 (IMissionRunner). */
export class MissionRunner {
  constructor(mission, sessionId) {
    if (!mission || !Array.isArray(mission.steps) || mission.steps.length === 0) throw new Error("mission has no steps");
    const orders = mission.steps.map(s => s.order);
    if (new Set(orders).size !== orders.length) throw new Error("duplicate step order");
    this.mission = mission;
    this.sessionId = sessionId;
    this.steps = sortedSteps(mission);
    this.pointer = 0;
    this.events = [];
  }
  get currentStep() { return this.steps[this.pointer] ?? null; }
  get isComplete() { return this.pointer >= this.steps.length; }

  report(eventName, targetId, input, timestampMs) {
    if (!EVENTS.includes(eventName)) throw new Error(`event '${eventName}' is not a contract event`);
    if (!INPUTS.includes(input)) throw new Error(`input '${input}' is not a contract input`);
    if (!targetId) throw new Error("targetId is required");
    const last = this.events.length ? this.events[this.events.length - 1].timestampMs : 0;
    if (!isInt(timestampMs) || timestampMs < 0 || timestampMs < last) throw new Error("timestampMs must be >= 0 and not decrease");

    const [outcome, k] = match(this.steps, this.pointer, eventName, targetId);
    if (outcome === "Correct") this.pointer = k + 1;
    const event = {
      sessionId: this.sessionId, missionId: this.mission.missionId, timestampMs,
      event: eventName, targetId, input, result: resultFor(outcome),
    };
    this.events.push(event);
    return {
      outcome,
      stepId: outcome === "Correct" || outcome === "OutOfOrder" ? this.steps[k].stepId : null,
      hint: this.currentStep ? this.currentStep.hint : null,
      event,
    };
  }
}

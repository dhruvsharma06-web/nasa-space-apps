// ASTRA Academy — offline coach, browser reference implementation (CONTRACT.md §9, prompt S1.2).
// Deterministic: same mission + result + events -> same text. No network, clock or randomness.
// The Unity OfflineCoach (C#) must produce identical output; this file is its specification.
// All advice is built only from the mission file — it never invents procedures.

import { sortedSteps, match } from "./scoring.js";

export const OFFLINE_MODEL_VERSION = "offline-1";
const MAX_HINTS = 3, MAX_HINT_CHARS = 200, MAX_SUMMARY_CHARS = 600;

const plural = (n, word) => `${n} ${word}${n === 1 ? "" : "s"}`;
const unquote = s => s.replace(/\.\s*$/, "");
const clip = (s, n) => (s.length <= n ? s : s.slice(0, n - 1).trimEnd() + "…");

/** Summary sentence(s), CONTRACT §9 limits apply. */
export function offlineSummary(result) {
  const wrong = result.incorrectActions.length, order = result.sequenceErrors.length;
  const mistakes = `${plural(wrong, "wrong action")} and ${plural(order, "out-of-order action")} recorded.`;
  if (result.passed && wrong === 0 && order === 0) return "Passed. All critical steps completed with no errors.";
  if (result.passed) return `Passed. All critical steps completed; ${mistakes}`;
  return `Not passed. ${plural(result.omittedCriticalSteps.length, "critical step")} not completed; ${mistakes}`;
}

/**
 * For each OutOfOrder event: [blocking step, step attempted too early].
 * The blocking step is the first CRITICAL step still pending before the early one; optional steps
 * never block (CONTRACT.md §4), so naming them would give wrong advice.
 */
function pendingBeforeEachSequenceError(mission, events) {
  const steps = sortedSteps(mission);
  let p = 0;
  const pairs = [];
  for (const e of events) {
    const [outcome, k] = match(steps, p, e.event, e.targetId);
    if (outcome === "Correct") p = k + 1;
    else if (outcome === "OutOfOrder") pairs.push([steps.slice(p, k).find(s => s.critical), steps[k]]);
  }
  return pairs;
}

/** Offline debrief. Shape = CoachResponse (CONTRACT.md §9). */
export function offlineCoach(request, mission) {
  const base = { sessionId: request.sessionId, missionId: request.missionId, advisory: true, modelVersion: OFFLINE_MODEL_VERSION };
  if (!request.events || request.events.length === 0) return { ...base, summary: null, hints: [], reason: "NO_EVENTS" };

  const result = request.result;
  const byId = Object.fromEntries(mission.steps.map(s => [s.stepId, s]));
  const hints = [];
  const push = h => { if (h && !hints.includes(h) && hints.length < MAX_HINTS) hints.push(clip(h, MAX_HINT_CHARS)); };

  // 1. Every omitted critical step: its own hint from the mission file.
  for (const id of result.omittedCriticalSteps) push(byId[id]?.hint);
  // 2. Every sequence error: which step had to come first.
  for (const [pending, early] of pendingBeforeEachSequenceError(mission, request.events)) {
    push(`Order matters: "${unquote(pending.instruction)}" comes before "${unquote(early.instruction)}".`);
  }
  // 3. Wrong controls: one general tip.
  if (result.incorrectActions.length) {
    push(`Check the panel label before you act: ${plural(result.incorrectActions.length, "action")} used a control that was not part of the current step.`);
  }
  return { ...base, summary: clip(offlineSummary(result), MAX_SUMMARY_CHARS), hints };
}

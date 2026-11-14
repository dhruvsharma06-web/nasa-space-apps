/*
 * ASTRA Academy instructor dashboard: pure aggregation functions (no DOM, no clock, no network).
 * Works as a classic browser script (window.AstraAgg) and as a Node module (require), so the page runs
 * from file:// with no build step and test.mjs can check the same code.
 *
 * Input: session logs as written by LevelController.WriteSessionLog (Assets/ASTRA/App/LevelController.cs):
 *   { sessionId, missionId, result: SessionResult (CONTRACT §5), events: SessionEvent[] (CONTRACT §1) }
 * plus an optional free-text "callsign". No other personal data is read or kept.
 */
(function (root, factory) {
  const api = factory();
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  else root.AstraAgg = api;
})(typeof self !== "undefined" ? self : this, function () {
  "use strict";

  const NEUTRAL_WHEN_UNMATCHED = ["target_located", "object_grabbed", "object_released", "paused", "resumed", "recentered"];
  const MEDALS = ["Gold", "Silver", "Bronze", "None"];

  // ---------------------------------------------------------------- scoring rules (docs/SCORING.md)
  // Same rules and constants as Astra.Core.Assessment.ResultRating.
  const R = {
    start: 100, missed: -25, outOfOrder: -10, wrong: -8,
    parMsPerStep: 8000, timeBlockMs: 5000, maxTimePenalty: 15,
    swiftBonus: 5, swiftParPercent: 70, notPassedCap: 59, goldMin: 90, silverMin: 75,
  };
  const ID_SUFFIXES = ["_button", "_toggle", "_dial", "_lever", "_selector", "_breaker", "_switch", "_display", "_panel"];

  function humanizeId(id) {
    if (!id) return "";
    for (const s of ID_SUFFIXES) {
      if (id.length > s.length && id.endsWith(s)) { id = id.slice(0, -s.length); break; }
    }
    return id.replace(/_/g, " ").toUpperCase();
  }

  const sortedSteps = mission => [...(mission && mission.steps || [])].sort((a, b) => a.order - b.order);
  const stepById = (mission, stepId) => sortedSteps(mission).find(s => s.stepId === stepId) || null;

  function ratingFor(score, passed) {
    if (!passed) return "None";
    if (score >= R.goldMin) return "Gold";
    if (score >= R.silverMin) return "Silver";
    return "Bronze";
  }

  /** The 0–100 score, medal and explaining lines for one result (mirrors ResultRating.From). */
  function rate(result, mission) {
    const lines = [];
    let score = R.start;
    const add = (label, points) => { score += points; lines.push({ label, points }); };
    const num = id => { const s = stepById(mission, id); return s ? String(s.order) : id; };
    for (const id of result.omittedCriticalSteps || []) add("Missed critical step " + num(id), R.missed);
    for (const id of result.sequenceErrors || []) add("Out of order: step " + num(id), R.outOfOrder);
    for (const id of result.incorrectActions || []) add("Wrong control: " + humanizeId(id), R.wrong);

    const par = R.parMsPerStep * sortedSteps(mission).length;
    const time = Math.max(0, result.completionTimeMs || 0);
    const over = time - par;
    const timePenalty = over < R.timeBlockMs ? 0 : Math.min(R.maxTimePenalty, Math.floor(over / R.timeBlockMs));
    if (timePenalty > 0) add(`Time: ${Math.floor(over / 1000)} s over par of ${Math.floor(par / 1000)} s`, -timePenalty);

    const errors = (result.omittedCriticalSteps || []).length + (result.sequenceErrors || []).length + (result.incorrectActions || []).length;
    if (errors === 0 && par > 0 && time * 100 < par * R.swiftParPercent) add(`Swift: under ${R.swiftParPercent}% of par`, R.swiftBonus);
    if (score > R.start) add("Maximum score is 100", R.start - score);
    if (!result.passed && score > R.notPassedCap) add("Not passed: score capped at " + R.notPassedCap, R.notPassedCap - score);
    if (score < 0) add("Minimum score is 0", -score);
    if (lines.length === 0) lines.push({ label: "Clean run: every step in order", points: 0 });
    return { score, medal: ratingFor(score, result.passed), lines };
  }

  // ---------------------------------------------------------------- step matching (CONTRACT §4)
  const same = (step, event, target) => step.expectedEvent === event && step.targetId === target;

  function match(steps, pointer, event, target) {
    for (let k = pointer; k < steps.length; k++) {
      if (same(steps[k], event, target)) {
        const between = steps.slice(pointer, k);
        if (k === pointer || between.every(s => !s.critical)) return ["Correct", k];
        return ["OutOfOrder", k];
      }
    }
    if (steps.slice(0, pointer).some(s => same(s, event, target))) return ["Ignored", null];
    if (NEUTRAL_WHEN_UNMATCHED.includes(event)) return ["Ignored", null];
    return ["Incorrect", null];
  }

  /**
   * Replays an attempt's events: each gets outcome (Correct | OutOfOrder | Incorrect | Ignored), the step it
   * matched (if any) and the step the trainee was on when it happened.
   */
  function classify(mission, events) {
    const steps = sortedSteps(mission);
    let pointer = 0;
    return (events || []).map(e => {
      const current = steps[pointer] || null;
      const [outcome, k] = steps.length ? match(steps, pointer, e.event, e.targetId) : ["Ignored", null];
      if (outcome === "Correct") pointer = k + 1;
      return {
        timestampMs: e.timestampMs, event: e.event, targetId: e.targetId, input: e.input, outcome,
        stepId: k == null ? null : steps[k].stepId,
        currentStepId: current ? current.stepId : null,
      };
    });
  }

  // ---------------------------------------------------------------- loading
  const isObj = v => v !== null && typeof v === "object" && !Array.isArray(v);

  /** Checks one parsed session log and returns an attempt, or throws an Error with a readable message. */
  function normalizeLog(log, source) {
    const where = source ? `${source}: ` : "";
    if (!isObj(log)) throw new Error(where + "not a session log (expected a JSON object)");
    if (typeof log.missionId !== "string" || !log.missionId) throw new Error(where + "missing missionId");
    if (!isObj(log.result)) throw new Error(where + "missing result");
    if (typeof log.result.passed !== "boolean") throw new Error(where + "result.passed must be true or false");
    const events = Array.isArray(log.events) ? log.events : [];
    const list = k => (Array.isArray(log.result[k]) ? log.result[k].map(String) : []);
    const callsign = typeof log.callsign === "string" ? log.callsign.trim().slice(0, 24) : "";
    return {
      sessionId: String(log.sessionId || log.result.sessionId || source || "unknown"),
      missionId: log.missionId,
      callsign,
      source: source || "",
      result: {
        sessionId: String(log.result.sessionId || log.sessionId || ""),
        missionId: String(log.result.missionId || log.missionId),
        passed: log.result.passed,
        completedSteps: list("completedSteps"),
        skippedSteps: list("skippedSteps"),
        omittedCriticalSteps: list("omittedCriticalSteps"),
        sequenceErrors: list("sequenceErrors"),
        incorrectActions: list("incorrectActions"),
        completionTimeMs: Number(log.result.completionTimeMs) || 0,
      },
      events: events.filter(isObj).map(e => ({
        timestampMs: Number(e.timestampMs) || 0, event: String(e.event || ""), targetId: String(e.targetId || ""),
        input: String(e.input || ""), result: String(e.result || ""),
      })),
    };
  }

  /** Several logs from one file: a single log, an array of logs, or { logs: [...] }. */
  function logsFromJson(parsed, source) {
    if (Array.isArray(parsed)) return parsed.map((l, i) => normalizeLog(l, `${source}#${i + 1}`));
    if (isObj(parsed) && Array.isArray(parsed.logs)) return parsed.logs.map((l, i) => normalizeLog(l, `${source}#${i + 1}`));
    return [normalizeLog(parsed, source)];
  }

  /** Keeps the first copy of each sessionId (the same log dropped twice is counted once). */
  function dedupe(attempts) {
    const seen = new Set();
    return attempts.filter(a => (seen.has(a.sessionId) ? false : (seen.add(a.sessionId), true)));
  }

  // ---------------------------------------------------------------- aggregation
  const missionOf = (missions, id) => missions[id] || null;
  const levelOf = (missions, id) => { const m = missionOf(missions, id); return m ? m.level : null; };
  const instructionOf = (mission, stepId) => { const s = stepById(mission, stepId); return s ? s.instruction : ""; };
  const mean = xs => (xs.length ? xs.reduce((a, b) => a + b, 0) / xs.length : 0);

  /** Adds score, medal and the event replay to each attempt (missions: { missionId: mission }). */
  function enrich(attempts, missions) {
    return attempts.map(a => {
      const mission = missionOf(missions, a.missionId);
      const rating = mission ? rate(a.result, mission) : { score: null, medal: "None", lines: [] };   // unknown mission: not scored
      return Object.assign({}, a, {
        level: mission ? mission.level : null,
        title: mission ? mission.title : a.missionId,
        known: !!mission,
        score: rating.score,
        medal: rating.medal,
        lines: rating.lines,
        timeline: mission ? classify(mission, a.events) : [],
      });
    });
  }

  function countBy(items, key) {
    const map = new Map();
    for (const it of items) {
      const k = key(it);
      const cur = map.get(k);
      if (cur) cur.count++; else map.set(k, Object.assign({ count: 1 }, it));
    }
    return [...map.values()].sort((a, b) => b.count - a.count || (a.level ?? 99) - (b.level ?? 99) || String(a.id).localeCompare(String(b.id)));
  }

  /** Everything the dashboard shows. attempts must come from enrich(). */
  function aggregate(attempts, missions) {
    const trainees = new Set(attempts.map(a => a.callsign || "(no callsign)"));
    const passed = attempts.filter(a => a.result.passed);
    const scored = attempts.filter(a => a.score != null);

    const byLevel = new Map();
    for (const a of attempts) {
      const key = a.missionId;
      if (!byLevel.has(key)) byLevel.set(key, []);
      byLevel.get(key).push(a);
    }
    const levels = [...byLevel.entries()].map(([missionId, list]) => {
      const medals = { Gold: 0, Silver: 0, Bronze: 0, None: 0 };
      for (const a of list) medals[a.medal] = (medals[a.medal] || 0) + 1;
      const p = list.filter(a => a.result.passed);
      const m = missionOf(missions, missionId);
      return {
        missionId, level: m ? m.level : null, title: m ? m.title : missionId,
        attempts: list.length, passed: p.length, passRate: p.length / list.length,
        avgTimeMs: Math.round(mean(list.map(a => a.result.completionTimeMs))),
        avgScore: Math.round(mean(list.filter(a => a.score != null).map(a => a.score))),
        medals,
      };
    }).sort((a, b) => (a.level ?? 99) - (b.level ?? 99));

    const missed = [], outOfOrder = [], wrong = [];
    for (const a of attempts) {
      const mission = missionOf(missions, a.missionId);
      for (const id of a.result.omittedCriticalSteps) {
        const s = stepById(mission, id);
        missed.push({ id: a.missionId + "/" + id, missionId: a.missionId, level: a.level, stepId: id, order: s ? s.order : null, instruction: instructionOf(mission, id) });
      }
      for (const id of a.result.sequenceErrors) {
        const s = stepById(mission, id);
        outOfOrder.push({ id: a.missionId + "/" + id, missionId: a.missionId, level: a.level, stepId: id, order: s ? s.order : null, instruction: instructionOf(mission, id) });
      }
      for (const ev of a.timeline.filter(t => t.outcome === "Incorrect")) {
        const s = stepById(mission, ev.currentStepId);
        wrong.push({ id: a.missionId + "/" + ev.targetId, missionId: a.missionId, level: a.level, targetId: ev.targetId, label: humanizeId(ev.targetId), duringOrder: s ? s.order : null, duringInstruction: s ? s.instruction : "" });
      }
      if (!mission) for (const id of a.result.incorrectActions)
        wrong.push({ id: a.missionId + "/" + id, missionId: a.missionId, level: null, targetId: id, label: humanizeId(id), duringOrder: null, duringInstruction: "" });
    }

    const medals = { Gold: 0, Silver: 0, Bronze: 0, None: 0 };
    for (const a of attempts) medals[a.medal] = (medals[a.medal] || 0) + 1;

    return {
      overview: {
        attempts: attempts.length, trainees: trainees.size, passed: passed.length,
        passRate: attempts.length ? passed.length / attempts.length : 0,
        avgScore: Math.round(mean(scored.map(a => a.score))),
        avgTimeMs: Math.round(mean(attempts.map(a => a.result.completionTimeMs))),
        medals,
      },
      levels,
      struggles: { missed: countBy(missed, x => x.id), outOfOrder: countBy(outOfOrder, x => x.id), wrong: countBy(wrong, x => x.id) },
    };
  }

  // ---------------------------------------------------------------- CSV
  const CSV_COLUMNS = ["callsign", "sessionId", "level", "missionId", "passed", "score", "medal", "timeS",
    "missedCritical", "outOfOrder", "wrongControls", "skippedSteps", "events"];

  function csvCell(v) {
    let s = v == null ? "" : String(v);
    if (/^[=+\-@\t\r]/.test(s)) s = "'" + s;   // never let a spreadsheet run a cell as a formula
    return /[",\n\r]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s;
  }

  function toCSV(attempts) {
    const rows = [CSV_COLUMNS.join(",")];
    for (const a of attempts) {
      rows.push([
        a.callsign, a.sessionId, a.level, a.missionId, a.result.passed, a.score, a.medal,
        (a.result.completionTimeMs / 1000).toFixed(1),
        a.result.omittedCriticalSteps.join(" "), a.result.sequenceErrors.join(" "),
        a.result.incorrectActions.join(" "), a.result.skippedSteps.join(" "), a.events.length,
      ].map(csvCell).join(","));
    }
    return rows.join("\r\n") + "\r\n";
  }

  return { R, MEDALS, humanizeId, rate, ratingFor, match, classify, normalizeLog, logsFromJson, dedupe, enrich, aggregate, toCSV, CSV_COLUMNS, sortedSteps };
});

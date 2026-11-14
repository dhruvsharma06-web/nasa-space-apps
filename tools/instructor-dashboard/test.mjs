// Instructor dashboard: aggregation checks on the sample data.   node tools/instructor-dashboard/test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
import vm from "node:vm";
import { score } from "../level-sim/scoring.js";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..", "..");
const Agg = createRequire(import.meta.url)("./aggregate.js");

// The dashboard's own copy of the missions (what the page uses). The checks below run on it, so a teammate editing
// mission text never breaks these tests; the last test compares it with the source files.
const bundled = { window: {} };
vm.runInNewContext(readFileSync(join(here, "data/missions.js"), "utf8"), bundled);
vm.runInNewContext(readFileSync(join(here, "sample/sample-data.js"), "utf8"), bundled);
const missions = JSON.parse(JSON.stringify(bundled.window.ASTRA_MISSIONS));   // plain objects from the vm realm
const sourceMissions = {};
for (const f of readdirSync(join(root, "Assets/ASTRA/Content/Missions")).filter(f => f.endsWith(".json")))
  { const m = JSON.parse(readFileSync(join(root, "Assets/ASTRA/Content/Missions", f), "utf8")); sourceMissions[m.missionId] = m; }

const sampleFiles = readdirSync(join(here, "sample")).filter(f => f.endsWith(".json")).sort();
const rawLogs = sampleFiles.map(f => JSON.parse(readFileSync(join(here, "sample", f), "utf8")));
const attempts = Agg.enrich(rawLogs.map((l, i) => Agg.normalizeLog(l, sampleFiles[i])), missions);

const stepsOf = id => [...missions[id].steps].sort((a, b) => a.order - b.order);
const perfect = id => ({ passed: true, omittedCriticalSteps: [], sequenceErrors: [], incorrectActions: [], completionTimeMs: 5000 * stepsOf(id).length });

test("there are about 20 sample attempts and every one loads", () => {
  assert.ok(attempts.length >= 20, `only ${attempts.length}`);
  for (const a of attempts) { assert.ok(a.known, a.missionId); assert.ok(a.callsign, a.sessionId); }
});

test("every sample result is exactly what the scorer gives for its events", () => {
  for (const log of rawLogs)
    assert.deepEqual(score(missions[log.missionId], log.sessionId, log.events), log.result, log.sessionId);
});

test("score rules: perfect run = 100 Gold", () => {
  for (const id of Object.keys(missions)) {
    const r = Agg.rate(perfect(id), missions[id]);
    assert.equal(r.score, 100); assert.equal(r.medal, "Gold");
  }
});

test("score rules: the docs/SCORING.md example is 90 Gold", () => {
  const r = Agg.rate({ passed: true, omittedCriticalSteps: [], sequenceErrors: [], incorrectActions: ["power_bus_a_dial"], completionTimeMs: 66000 }, missions["l4-emergency-v1"]);
  assert.equal(r.score, 90); assert.equal(r.medal, "Gold");
  assert.deepEqual(r.lines, [{ label: "Wrong control: POWER BUS A", points: -8 }, { label: "Time: 10 s over par of 56 s", points: -2 }]);
});

test("score rules: not passed is capped at 59 with no medal; never below 0", () => {
  const m = missions["l7-restore-power-v1"];
  assert.equal(Agg.rate({ passed: false, omittedCriticalSteps: ["crosstie"], sequenceErrors: [], incorrectActions: [], completionTimeMs: 1000 }, m).score, 59);
  const low = Agg.rate({ passed: false, omittedCriticalSteps: ["calibrate", "ack_warning", "identify_bus", "isolate_bus", "crosstie"], sequenceErrors: [], incorrectActions: [], completionTimeMs: 1000 }, m);
  assert.equal(low.score, 0); assert.equal(low.medal, "None");
});

test("every sample score equals 100 plus its lines, within 0..100", () => {
  for (const a of attempts) {
    assert.equal(a.score, 100 + a.lines.reduce((s, l) => s + l.points, 0), a.sessionId);
    assert.ok(a.score >= 0 && a.score <= 100);
    if (!a.result.passed) assert.ok(a.score <= 59 && a.medal === "None");
  }
});

test("the event replay agrees with each result", () => {
  for (const a of attempts) {
    const count = o => a.timeline.filter(t => t.outcome === o).length;
    assert.equal(count("Incorrect"), a.result.incorrectActions.length, a.sessionId + " wrong controls");
    assert.equal(count("OutOfOrder"), a.result.sequenceErrors.length, a.sessionId + " out of order");
    assert.equal(count("Correct"), a.result.completedSteps.length, a.sessionId + " completed");
  }
});

test("cohort overview adds up", () => {
  const agg = Agg.aggregate(attempts, missions);
  const o = agg.overview;
  assert.equal(o.attempts, attempts.length);
  assert.equal(o.passed, attempts.filter(a => a.result.passed).length);
  assert.equal(Object.values(o.medals).reduce((s, n) => s + n, 0), attempts.length);
  assert.equal(agg.levels.reduce((s, l) => s + l.attempts, 0), attempts.length);
  assert.equal(o.trainees, new Set(attempts.map(a => a.callsign)).size);
  for (const l of agg.levels) {
    assert.equal(l.passRate, l.passed / l.attempts);
    assert.equal(Object.values(l.medals).reduce((s, n) => s + n, 0), l.attempts);
  }
  assert.deepEqual(agg.levels.map(l => l.level), [...agg.levels.map(l => l.level)].sort((a, b) => a - b));
});

test("struggles are counted, sorted and carry the mission text", () => {
  const s = Agg.aggregate(attempts, missions).struggles;
  const total = k => attempts.reduce((n, a) => n + a.result[k].length, 0);
  assert.equal(s.missed.reduce((n, x) => n + x.count, 0), total("omittedCriticalSteps"));
  assert.equal(s.outOfOrder.reduce((n, x) => n + x.count, 0), total("sequenceErrors"));
  assert.equal(s.wrong.reduce((n, x) => n + x.count, 0), total("incorrectActions"));
  for (const list of [s.missed, s.outOfOrder, s.wrong])
    for (let i = 1; i < list.length; i++) assert.ok(list[i - 1].count >= list[i].count);
  for (const x of s.missed.concat(s.outOfOrder)) assert.ok(x.instruction.length > 0, x.id);
  for (const x of s.wrong) assert.ok(x.duringInstruction.length > 0, x.id);
});

test("CSV has a header and one row per attempt, and blocks formulas", () => {
  const rows = Agg.toCSV(attempts).trim().split("\r\n");
  assert.equal(rows[0], Agg.CSV_COLUMNS.join(","));
  assert.equal(rows.length, attempts.length + 1);
  const evil = Agg.enrich([Agg.normalizeLog({ missionId: "l0-safety-v1", callsign: "=HYPERLINK(\"x\")", result: { passed: true }, events: [] }, "x")], missions);
  assert.ok(Agg.toCSV(evil).split("\r\n")[1].startsWith("\"'=HYPERLINK"));
});

test("bad files are rejected with a reason; arrays and duplicates are handled", () => {
  assert.throws(() => Agg.normalizeLog(null, "a.json"), /a\.json: not a session log/);
  assert.throws(() => Agg.normalizeLog({ missionId: "l0-safety-v1" }, "b.json"), /missing result/);
  assert.throws(() => Agg.normalizeLog({ missionId: "l0-safety-v1", result: { passed: "yes" } }, "c.json"), /passed/);
  assert.equal(Agg.logsFromJson(rawLogs.slice(0, 3), "many.json").length, 3);
  assert.equal(Agg.dedupe(Agg.logsFromJson(rawLogs.concat(rawLogs), "x")).length, rawLogs.length);
});

test("an unknown mission is counted but not scored", () => {
  const [a] = Agg.enrich([Agg.normalizeLog({ missionId: "l9-new-v1", result: { passed: true, incorrectActions: ["x_button"] }, events: [] }, "x")], missions);
  assert.equal(a.known, false); assert.equal(a.score, null);
  const agg = Agg.aggregate([a], missions);
  assert.equal(agg.overview.attempts, 1); assert.equal(agg.struggles.wrong[0].targetId, "x_button");
});

test("the bundled missions match the source files (rerun make-sample.mjs if not)", () => {
  // Step structure (ids, order, critical, event, target) is fixed by CONTRACT §2 and must match exactly.
  // Wording may change in the source; that only prints a warning, so text edits never fail CI.
  const shape = steps => [...steps].sort((a, b) => a.order - b.order).map(s => [s.stepId, s.order, s.critical, s.expectedEvent, s.targetId]);
  const stale = [];
  for (const [id, m] of Object.entries(sourceMissions)) {
    const b = missions[id];
    assert.ok(b, `${id} is missing from data/missions.js`);
    assert.deepEqual(shape(b.steps), shape(m.steps), `${id}: step structure differs`);
    const text = ms => [ms.title, ...[...ms.steps].sort((a, b) => a.order - b.order).map(s => s.instruction)];
    if (JSON.stringify(text(b)) !== JSON.stringify(text(m))) stale.push(id);
  }
  if (stale.length) console.warn(`note: mission text changed in ${stale.join(", ")}; run node tools/instructor-dashboard/make-sample.mjs to refresh the dashboard copy`);
  assert.equal(bundled.window.ASTRA_SAMPLE.length, rawLogs.length);
});

import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { fileURLToPath } from "node:url";
import { score, MissionRunner } from "./scoring.js";
import { offlineCoach } from "./coach.js";

const load = rel => JSON.parse(fs.readFileSync(fileURLToPath(new URL(rel, import.meta.url)), "utf8"));
const fx = n => load(`../../fixtures/${n}`);
const mission = load("../../Assets/ASTRA/Content/Missions/l7-restore-power-v1.json");
const l3 = load("../../Assets/ASTRA/Content/Missions/l3-cabin-v1.json");
const okEvents = fx("session-events.ok.json");
const okResult = fx("session-result.ok.json");
const cases = fx("scoring-cases.json").cases;
const caseByName = n => cases.find(c => c.name === n);
const coachReq = (events, result, sessionId) => ({ sessionId, missionId: mission.missionId, result, events });
const stepHint = id => mission.steps.find(s => s.stepId === id).hint;

function replay(m, sessionId, events) {
  const r = new MissionRunner(m, sessionId);
  for (const e of events) r.report(e.event, e.targetId, e.input, e.timestampMs);
  return r;
}

test("1. ok trace scores to session-result.ok.json", () => {
  assert.deepEqual(score(mission, "demo-001", okEvents), okResult);
});

test("2. empty trace scores to session-result.empty.json", () => {
  assert.deepEqual(score(mission, "demo-003", fx("session-events.empty.json")), fx("session-result.empty.json"));
});

test("3. error trace throws", () => {
  assert.throws(() => score(mission, "demo-002", fx("session-events.error.json")));
});

test("4. scoring cases", async t => {
  for (const c of cases) {
    await t.test(c.name, () => assert.deepEqual(score(mission, c.sessionId, c.events), c.expected));
  }
});

test("5. MissionRunner replay of ok trace", () => {
  const r = replay(mission, "demo-001", okEvents);
  assert.deepEqual(r.events.map(e => e.result), okEvents.map(e => e.result));
  assert.equal(r.isComplete, true);
  assert.deepEqual(score(mission, "demo-001", r.events), okResult);
});

test("6. MissionRunner replay of scoring cases stamps same results", async t => {
  for (const c of cases) {
    await t.test(c.name, () => {
      const r = replay(mission, c.sessionId, c.events);
      assert.deepEqual(r.events.map(e => e.result), c.events.map(e => e.result));
    });
  }
});

test("7. determinism", () => {
  assert.deepEqual(score(mission, "demo-001", okEvents), score(mission, "demo-001", okEvents));
  const req = coachReq(okEvents, okResult, "demo-001");
  assert.deepEqual(offlineCoach(req, mission), offlineCoach(req, mission));
});

test("8. MissionRunner rejects invalid reports", () => {
  const mk = () => new MissionRunner(mission, "s");
  assert.throws(() => mk().report("bogus_event", "calibration", "simulator", 0));
  assert.throws(() => mk().report("calibration_completed", "calibration", "bogus_input", 0));
  assert.throws(() => mk().report("calibration_completed", "", "simulator", 0));
  assert.throws(() => mk().report("calibration_completed", "calibration", "simulator", -1));
  const r = mk();
  r.report("calibration_completed", "calibration", "simulator", 500);
  assert.throws(() => r.report("target_located", "power_distribution_panel", "simulator", 499));
});

test("9. report feedback", () => {
  const steps = [...mission.steps].sort((a, b) => a.order - b.order);
  const r = new MissionRunner(mission, "s");
  const f = r.report("calibration_completed", "calibration", "simulator", 100);
  assert.equal(f.outcome, "Correct");
  assert.equal(f.stepId, "calibrate");
  assert.equal(f.hint, steps[1].hint);
  // Out of order: crosstie while locate_panel..isolate_bus pending (critical ack_warning between)
  const before = r.currentStep;
  const o = r.report("control_activated", "crosstie_breaker", "simulator", 200);
  assert.equal(o.outcome, "OutOfOrder");
  assert.equal(o.stepId, "crosstie");
  assert.equal(r.currentStep, before);
  assert.equal(r.currentStep.stepId, "locate_panel");
  // Complete the mission
  let last;
  for (const s of steps.slice(1)) last = r.report(s.expectedEvent, s.targetId, "simulator", 300 + s.order);
  assert.equal(last.outcome, "Correct");
  assert.equal(r.isComplete, true);
  assert.equal(last.hint, null);
});

test("10. offlineCoach empty events", () => {
  const out = offlineCoach(coachReq([], fx("session-result.empty.json"), "demo-003"), mission);
  assert.deepEqual(Object.keys(out).sort(), Object.keys(fx("coach-debrief.empty.json")).sort());
  assert.equal(out.summary, null);
  assert.deepEqual(out.hints, []);
  assert.equal(out.reason, "NO_EVENTS");
  assert.equal(out.advisory, true);
  assert.equal(out.modelVersion, "offline-1");
});

test("11. offlineCoach ok fixture", () => {
  const out = offlineCoach(coachReq(okEvents, okResult, "demo-001"), mission);
  assert.equal(out.summary, "Passed. All critical steps completed; 1 wrong action and 0 out-of-order actions recorded.");
  assert.deepEqual(out.hints, ["Check the panel label before you act: 1 action used a control that was not part of the current step."]);
  assert.equal("reason" in out, false);
});

test("12. offlineCoach out_of_order_then_fixed", () => {
  const c = caseByName("out_of_order_then_fixed");
  const out = offlineCoach(coachReq(c.events, c.expected, c.sessionId), mission);
  const stepText = id => mission.steps.find(s => s.stepId === id).instruction.replace(/\.\s*$/, "");
  assert.equal(out.hints[0], `Order matters: "${stepText("isolate_bus")}" comes before "${stepText("crosstie")}".`);
});

test("13. offlineCoach critical_step_omitted", () => {
  const c = caseByName("critical_step_omitted");
  const out = offlineCoach(coachReq(c.events, c.expected, c.sessionId), mission);
  assert.ok(out.summary.startsWith("Not passed. 2 critical steps not completed;"), out.summary);
  assert.equal(out.hints[0], stepHint("crosstie"));
  assert.equal(out.hints[1], stepHint("verify_recovery"));
  assert.ok(out.hints[2].startsWith("Order matters: "), out.hints[2]);
});

test("14. offlineCoach limits", async t => {
  const all = [{ name: "ok fixture", id: "demo-001", events: okEvents, result: okResult }, ...cases.map(c => ({ name: c.name, id: c.sessionId, events: c.events, result: c.expected }))];
  for (const x of all) {
    await t.test(x.name, () => {
      const out = offlineCoach(coachReq(x.events, x.result, x.id), mission);
      assert.ok(out.hints.length <= 3);
      for (const h of out.hints) assert.ok(h.length <= 200);
      assert.ok(out.summary.length <= 600);
      assert.equal(out.advisory, true);
    });
  }
});

test("15. l3-cabin-v1 sanity", () => {
  const steps = [...l3.steps].sort((a, b) => a.order - b.order);
  const r = new MissionRunner(l3, "l3-sim");
  steps.forEach((s, i) => r.report(s.expectedEvent, s.targetId, "simulator", (i + 1) * 1000));
  assert.equal(r.isComplete, true);
  const res = score(l3, "l3-sim", r.events);
  assert.equal(res.passed, true);
  assert.equal(res.completedSteps.length, 7);
  assert.equal(steps.length, 7);
});

test("16. offlineCoach names the first pending CRITICAL step, not an optional one", async () => {
  // L3: COMMS CHECK pressed first. locate_panel (optional) is pending, but the blocker is lights_on (critical).
  const { readFileSync } = await import("node:fs");
  const l3 = JSON.parse(readFileSync(new URL("../../Assets/ASTRA/Content/Missions/l3-cabin-v1.json", import.meta.url), "utf8"));
  const runner = new MissionRunner(l3, "s16");
  let t = 1000;
  runner.report("control_activated", "comms_check_button", "simulator", t);
  for (const s of [...l3.steps].sort((a, b) => a.order - b.order)) runner.report(s.expectedEvent, s.targetId, "simulator", (t += 1000));
  const result = score(l3, "s16", runner.events);
  assert.deepEqual(result.sequenceErrors, ["comms_check"]);
  const coach = offlineCoach({ sessionId: "s16", missionId: l3.missionId, result, events: runner.events }, l3);
  assert.equal(coach.hints[0], 'Order matters: "Set CABIN LIGHTS to ON" comes before "Press COMMS CHECK".');
});

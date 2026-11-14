// Generates the dashboard's mission table and its sample session logs.
//   node tools/instructor-dashboard/make-sample.mjs
// Writes:
//   data/missions.js        window.ASTRA_MISSIONS, from Assets/ASTRA/Content/Missions/*.json
//   sample/*.json           ~20 fictional session logs, in the exact shape LevelController.WriteSessionLog saves
//   sample/sample-data.js   the same logs as window.ASTRA_SAMPLE, so "Load sample data" works from file://
// Every attempt is played through the reference runner and scorer (tools/level-sim/scoring.js, proven equal to the
// C# MissionRunner/MissionScorer by tools/level-sim/test_scoring.mjs), so each result is what the app would write.
// Seeded, so running it again gives the same files. Callsigns are fictional; there is no personal data.
import { readFileSync, writeFileSync, readdirSync, mkdirSync, rmSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { MissionRunner, score, sortedSteps } from "../level-sim/scoring.js";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..", "..");
const missionsDir = join(root, "Assets/ASTRA/Content/Missions");
const layoutsDir = join(root, "Assets/ASTRA/Content/Layouts");

const missions = {};
for (const f of readdirSync(missionsDir).filter(f => f.endsWith(".json")).sort()) {
  const m = JSON.parse(readFileSync(join(missionsDir, f), "utf8"));
  missions[m.missionId] = {
    missionId: m.missionId, level: m.level, title: m.title,
    steps: sortedSteps(m).map(s => ({ stepId: s.stepId, order: s.order, instruction: s.instruction, critical: s.critical, expectedEvent: s.expectedEvent, targetId: s.targetId })),
  };
}
const decoys = {};
if (existsSync(layoutsDir))
  for (const f of readdirSync(layoutsDir).filter(f => f.endsWith(".json"))) {
    const l = JSON.parse(readFileSync(join(layoutsDir, f), "utf8"));
    decoys[l.missionId] = (l.decoys || []).map(d => d.targetId);
  }

// Seeded random numbers (mulberry32).
let seed = 20261115;
const rand = () => { seed |= 0; seed = (seed + 0x6D2B79F5) | 0; let t = Math.imul(seed ^ (seed >>> 15), 1 | seed); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
const between = (a, b) => a + rand() * (b - a);
const pick = xs => xs[Math.floor(rand() * xs.length)];

const byLevel = level => Object.values(missions).find(m => m.level === level);

/** One attempt: what the trainee does, step by step. */
function actions(mission, kind) {
  const steps = mission.steps;
  const critical = steps.map((s, i) => [s, i]).filter(([s]) => s.critical).map(([, i]) => i);
  const acts = steps.map(s => ({ event: s.expectedEvent, targetId: s.targetId }));
  const decoy = (decoys[mission.missionId] || [])[0];
  if (kind.includes("wrong") && decoy) {
    const at = pick(critical.length ? critical : [0]);
    acts.splice(at, 0, { event: "control_activated", targetId: decoy });
  }
  if (kind.includes("order")) {
    // presses a later critical step too early, then does it again in the right place
    const i = critical.find(k => k > 0 && k + 1 < steps.length && steps[k + 1].critical);
    if (i != null) {
      const at = acts.findIndex(a => a.targetId === steps[i].targetId && a.event === steps[i].expectedEvent);
      acts.splice(at, 0, { event: steps[i + 1].expectedEvent, targetId: steps[i + 1].targetId });
    }
  }
  if (kind.includes("skip")) {
    const i = critical.length > 1 ? critical[critical.length - 2] : critical[0];
    const at = acts.findIndex(a => a.targetId === steps[i].targetId && a.event === steps[i].expectedEvent);
    acts.splice(at, 1);
  }
  return acts;
}

function play(mission, callsign, n, kind) {
  const sessionId = `sample-${callsign.toLowerCase()}-${String(n).padStart(2, "0")}`;
  const runner = new MissionRunner(mission, sessionId);
  const [lo, hi] = kind.includes("slow") ? [9000, 14000] : kind.includes("swift") ? [1500, 2400] : [2600, 6500];
  const input = rand() < 0.7 ? "gaze" : "simulator";
  let t = Math.round(between(1500, 3000));
  for (const a of actions(mission, kind)) {
    runner.report(a.event, a.targetId, input, t);
    t += Math.round(between(lo, hi));
  }
  const result = score(mission, sessionId, runner.events);
  return { sessionId, missionId: mission.missionId, result, events: runner.events, callsign };
}

const plan = [
  ["ORION", 0, "clean"], ["ORION", 1, "clean"], ["ORION", 4, "wrong"], ["ORION", 4, "clean"], ["ORION", 7, "order"],
  ["VEGA", 0, "swift"], ["VEGA", 2, "wrong"], ["VEGA", 3, "clean"], ["VEGA", 4, "skip"], ["VEGA", 4, "wrong"],
  ["LYRA", 0, "slow"], ["LYRA", 1, "wrong"], ["LYRA", 2, "order"], ["LYRA", 3, "wrong slow"],
  ["ATLAS", 2, "clean"], ["ATLAS", 3, "skip"], ["ATLAS", 3, "clean"], ["ATLAS", 4, "wrong order"], ["ATLAS", 7, "skip"],
  ["NOVA", 4, "wrong"], ["NOVA", 5, "clean"], ["NOVA", 7, "wrong"],
  ["KEPLER", 6, "order"], ["KEPLER", 7, "clean"],
];

const counters = {};
const logs = plan.map(([callsign, level, kind]) => {
  counters[callsign] = (counters[callsign] || 0) + 1;
  const mission = byLevel(level);
  if (!mission) throw new Error("no mission for level " + level);
  return play(mission, callsign, counters[callsign], kind);
});

mkdirSync(join(here, "data"), { recursive: true });
const sampleDir = join(here, "sample");
if (existsSync(sampleDir)) rmSync(sampleDir, { recursive: true });
mkdirSync(sampleDir, { recursive: true });

const banner = "// Generated by make-sample.mjs. Do not edit by hand.\n";
writeFileSync(join(here, "data/missions.js"), banner + "window.ASTRA_MISSIONS = " + JSON.stringify(missions, null, 1) + ";\n");
for (const log of logs) writeFileSync(join(sampleDir, log.sessionId + ".json"), JSON.stringify(log, null, 4) + "\n");
writeFileSync(join(sampleDir, "sample-data.js"), banner + "window.ASTRA_SAMPLE = " + JSON.stringify(logs) + ";\n");

const passed = logs.filter(l => l.result.passed).length;
console.log(`wrote ${Object.keys(missions).length} missions and ${logs.length} sample logs (${passed} passed)`);

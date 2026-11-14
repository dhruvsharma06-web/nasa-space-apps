# SPRINT UPDATE: all 8 levels by tomorrow night

Paste **Part A + your own Part** to your AI, after your existing Master Prompt.

---

## PART A — EVERYONE (paste this first)

```
STAGE UPDATE — SPRINT MODE — ASTRA Academy
This replaces the 8-week timeline in your Master Prompt. Everything else in it (contract, ownership,
quality rules, stage report format, "never commit/push without asking me") still applies.

DEADLINE: tomorrow night (the team lead confirms the exact time). Feature freeze: tomorrow 5:00 pm.
Merge checkpoint: tomorrow 12:00 pm — all 8 levels playable at greybox quality.

TARGET: all 8 levels playable Briefing → Demonstration → Practice → Qualification → Debrief,
in the browser (WebGL) AND on the phone VR headset (Irusu Ultra = Android phone, Google Cardboard,
3DoF head tracking, input = GAZE + TAP). Real scoring (Core MissionRunner/MissionScorer, already on main),
debrief with coach. Plain-looking is OK; broken is not.

UNITY: 6000.3.28f1 (Unity 6.3 LTS) only. Install Android Build Support (+ OpenJDK + SDK & NDK) and
Web Build Support. Do NOT create your own Unity project.

HOW A LEVEL IS BUILT NOW ("level factory"):
A level = 2 data files. A generator builds the playable scene from them (arrives with tonight's commit):
  1) Assets/ASTRA/Content/Missions/l<N>-<name>-v1.json   (mission; CONTRACT.md §2; validator must pass)
  2) Assets/ASTRA/Content/Layouts/l<N>-layout.json        (where each control goes; format below)
Write both NOW. You don't need Unity for this. Check with:  python scripts/validate_content.py

LAYOUT FORMAT (l<N>-layout.json). Units: metres. Seat eye at (0, 1.2, 0) facing +Z; x = right, y = up, z = forward.
{
  "missionId": "l3-cabin-v1",
  "scene": "L3_Cabin",
  "panels": [
    { "id": "cabin_control_panel", "title": "CABIN CONTROL", "position": [0.45, 1.0, 0.45],
      "size": [0.40, 0.30], "tilt": 15, "target": true }
  ],
  "controls": [
    { "targetId": "cabin_lights_toggle", "type": "toggle", "label": "CABIN LIGHTS",
      "panel": "cabin_control_panel", "at": [-0.11, 0.05] },
    { "targetId": "air_fan_dial", "type": "dial", "label": "AIR FAN",
      "panel": "cabin_control_panel", "at": [0.09, 0.05], "positions": ["OFF", "LOW", "HIGH"] },
    { "targetId": "tool_bay_socket_1", "type": "socket", "label": "BAY 1",
      "panel": "tool_bay_panel", "at": [0.0, 0.0], "accepts": "torque_wrench_tool" }
  ],
  "tools": [
    { "targetId": "torque_wrench_tool", "label": "TORQUE WRENCH", "position": [-0.25, 1.05, 0.40] }
  ],
  "decoys": [
    { "targetId": "aux_heater_toggle", "type": "toggle", "label": "AUX HEATER",
      "panel": "cabin_control_panel", "at": [-0.11, -0.09] }
  ]
}
Types: button, toggle, dial, lever, checklist (a tab on a panel), socket (needs "accepts"), panel
("target": true makes the panel itself a target, e.g. target_located / target_selected).
Rules (CONTRACT §8): every target ≤ 0.7 m horizontally from (0,0,0), height 0.7–1.5 m, nothing behind
the seat. Every mission targetId appears exactly once in the layout (in controls, tools, or a panel
with "target": true). 1–3 decoys per level so mistakes can be practised.
The EVENT a control raises comes from the mission step that uses it (decoys raise control_activated).
On the phone, grabbing = look at the tool + tap (object_grabbed), then look at the socket + tap
(object_placed with the SOCKET's id).

WRITING RULES: docs/content-style-guide.md (junior professionals; instruction ≤ 15 words, verb first,
controls in CAPS exactly as labelled; hint ≤ 25 words = where + what to check + why; fictional only,
no agency logos, no real procedures).

GIT (to avoid breaking each other):
- Work on your own branch: feat/xr-*, feat/core-*, feat/content-*.
- Pull main after tonight's commit (the team lead announces it) and work on top of it.
- Commit ONLY your own folders. NEVER commit Packages/, ProjectSettings/, Library/, or .meta files
  outside your own folders. Never push to main directly; open a PR and wait for review.
- Never put API keys anywhere.

CHECKPOINTS (print a STAGE REPORT and stop at each):
  C1 tonight: mission + layout files for your levels written and validator OK
  C2 tomorrow 12:00 pm: your levels playable in the browser build, Briefing → Debrief
  C3 tomorrow 5:00 pm (freeze): your levels tested on the phone; bugs fixed; nothing new after this

Acknowledge in one line, then start your part.
```

---

## PART P1 — XR & Interaction (L0, L1 + phone input + UI)

```
YOUR SPRINT TASKS (XR & Interaction):
1) NOW: write l0-safety-v1.json + l0-layout.json and l1-orientation-v1.json + l1-layout.json.
   L0: calibrate, recenter, pause, resume (+ 1 status_reported "READY" button). L1: locate, select,
   press a button, flip a toggle, grab a tool, place it in its socket. Validator must pass.
2) PHONE INPUT (top priority after tonight's commit): a GazeReticle on the camera — a ray from the
   view centre; hover feedback on the target; TAP (screen touch or headset button) = activate/select;
   1.0 s dwell = target_located. Raise everything via InteractionEvents.Raise(event, targetId, input)
   with input = InputModality.Gaze ("gaze"; this constant arrives with tonight's commit, CONTRACT v1.4).
   Grab on phone = look + tap tool → object_grabbed; look + tap matching socket → object_placed (socket id).
   Must also work in the browser (mouse click = tap) so everyone can test there.
3) REAL UI (CONTRACT §7, keep the public signatures): InstructionPanel, PhaseBanner, DebriefPanel
   (PASS/RETRY, missed / out-of-order / wrong lists, coach text labelled "AI coach (advisory)"),
   PauseMenu (Resume, Recenter, Restart, Exit, Finish). World-space, readable on a phone headset,
   TextMeshPro, large text, high contrast.
4) Hub scene: 8 level cards, locked/unlocked from ProgressState (FileProgressStore on main),
   "Coming soon" if a scene is missing.
5) IMPROVE GRAPHICS AND UI (after 1–4 work): make the world-space UI look polished on the phone headset
   (layout, spacing, contrast, readable at ~0.8–1 m, consistent style across instruction / phase / debrief /
   alarm / pause / Hub) and upgrade the shared interactable visuals (buttons, toggles, dials, levers, sockets,
   tools: better shapes, materials, press/hover feedback). Keep public signatures and prefab names unchanged
   so the level factory keeps working. Test in the browser build and on the phone.
Do NOT edit Assets/ASTRA/App/ (the integration owner wires your UI into the LevelController).
Done = L0 + L1 playable in browser and phone; gaze+tap works everywhere; UI shows on every level.
```

---

## PART P2 — Core Logic (L4, L7 + power panel)

```
YOUR SPRINT TASKS (Core Logic):
1) NOW: write l4-emergency-v1.json + l4-layout.json (guided power-fault procedure: acknowledge warning,
   open checklist, identify faulty bus, isolate it, close cross-tie, report) and l7-layout.json for the
   existing l7-restore-power-v1 (DO NOT change l7's steps — it is the test oracle).
   Validator must pass. Use the same targetIds in L4/L7 where the hardware is the same.
2) Power-panel feedback script for L4 and L7 (Assets/ASTRA/Levels/L4_Emergency/ and L7_Finale/):
   subscribe to LevelController.PhaseChanged (InjectFault on Practice and Qualification) and
   EventRecorded (PowerBusSimulation.Apply only on Correct). Drive the bus lights, the caution
   annunciator text ("CAUTION — POWER: BUS B FAULT") and the status light. Never call the runner.
3) Keep all EditMode tests green on Unity 6.3. Add tests if you change Core.
4) Parity check: the same click order in tools/level-sim (browser 2D) and the WebGL build must give
   identical SessionResults (except ids/timings). Report any difference with the exact trace.
Done = L4 + L7 playable in browser and phone with the fault, lights and correct debrief.
```

---

## PART P3 — Content & AI Coach (L2, L3 + coach) — for your own AI

```
YOUR SPRINT TASKS (Content & AI Coach):
1) NOW: write l2-equipment-v1.json + l2-layout.json (grab tool → place in socket → secure latch →
   second tool → place → report; object_placed uses the SOCKET id) and l3-layout.json matching the
   existing L3 cabin (CABIN CONTROL on the right at chest height, COMMS to its left, CHECKLIST display
   above). Validator must pass.
2) Unity coach (Assets/ASTRA/Integrations/Coach/): OfflineCoach in C# that gives EXACTLY the same
   output as tools/level-sim/coach.js (port it; same summary/hint rules; tests against the fixtures);
   HttpCoachClient (POST to the coach proxy, 8 s timeout, fall back to OfflineCoach on any error);
   CoachProvider returns OfflineCoach if no proxy URL, else HttpCoachClient.
3) "Ask the coach" button in Practice (with P1's UI): show the consent line once
   ("Your question is sent to Google Gemini. Do not include personal information."), POST /api/v1/coach/ask.
4) Review every other level's mission text against docs/content-style-guide.md; send fixes to the owner.
Done = L2 + L3 playable in browser and phone; every debrief shows coach text (AI or offline).
```

---

## PART P4 — L5/L6 levels, quality, demo & submission (integration is done by the team lead)

```
YOUR SPRINT TASKS (P4: levels L5/L6 + quality + demo). The team lead does integration (merges, builds,
LevelController wiring, phone build). Do NOT edit Packages/, ProjectSettings/, Assets/ASTRA/App/ or CONTRACT.md.
Your folders: Assets/ASTRA/Levels/L5_EVA/, Assets/ASTRA/Levels/L6_Robotics/, Assets/ASTRA/Tests/PlayMode/,
.github/workflows/, docs/demo/, README.md (send README changes to the team lead for review).

1) L5 EVA + L6 ROBOTICS (missions + layouts already exist: l5-eva-v1 / l5-layout, l6-robotics-v1 / l6-layout)
   - Play both in the browser build (number keys 5 and 6) and the 2D simulator (tools/level-sim).
   - Fix anything unclear in the text (docs/content-style-guide.md) or the layout (reach rules, labels);
     run python scripts/validate_content.py after every change.
   - Level feedback scripts (like Levels/L3_Cabin/L3CabinView.cs): L5EVAView.cs (tether anchor light turns green
     after the hook is placed; SUIT PRESSURE shows NOMINAL; DEPRESS VALVE shows a caution caption if touched) and
     L6RoboticsView.cs (CAPTURE indicator after the capture lever; brakes light after BRAKES ON). Class name must be
     "<scene without _>View" so the level factory attaches it; serialized fields: "level" (LevelController) and one
     Renderer per indicator id in camelCase. React only to Correct outcomes; never call the runner.

2) TESTS
   - PlayMode smoke test (Assets/ASTRA/Tests/PlayMode/, own asmdef referencing ASTRA.App, ASTRA.Core, ASTRA.XR):
     load the L3_Cabin scene, skip to Practice, raise every L3 mission step in order through
     InteractionEvents.Raise (eventName = step.expectedEvent, targetId = step.targetId, input "simulator"),
     then do the same in Qualification with ONE wrong control first (spare_a_button). Assert QualificationScored
     reports passed == true and incorrectActions == ["spare_a_button"].
   - Parity check: same click order in the 2D simulator and the browser build → identical SessionResult
     (ignoring ids/timings). Report any difference with the exact trace.

3) CI
   - .github/workflows/ci.yml already validates content + scans secrets. Add a job that runs
     `node --test tools/level-sim/test_scoring.mjs` and the coach-proxy pytest (services/coach-proxy, mock mode).
     Unity tests in CI only if a UNITY_LICENSE secret exists; otherwise document "run locally before merge".

4) DEMO & SUBMISSION (owner of the pitch)
   - docs/demo/demo-script.md: 3-minute script (hook → problem → live demo of one hero level → how scoring and
     the coach work → impact → next steps), timed.
   - Slides (≤ 8): include the architecture (Unity + level factory + Core scoring + Gemini coach with offline
     fallback), the honest labels (fictional, educational, not certification), and real numbers only.
   - Record the BACKUP VIDEO (60–120 s) from the frozen build after 5 pm; screenshots for README and slides.
   - Submission checklist: repo link, video link, slides PDF, README with screenshot + how to run; submit at least
     1 hour before the deadline; open every link in an incognito window.

5) README.md draft: what ASTRA is, screenshot, how to run (browser build, 2D simulator, coach proxy, phone build),
   tech stack, team, honest limitations. Send to the team lead to commit.

Checkpoints: C1 tonight (L5/L6 played + view scripts drafted), C2 tomorrow 12:00 (tests + CI green, demo script),
C3 tomorrow 5:00 pm (video recorded, slides done, submission form drafted). Print a STAGE REPORT at each.
```

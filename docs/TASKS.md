# TASKS

Feature IDs map to PRD requirements. Owners are filled in at kickoff.

| ID | Feature | Priority | PRD | Owner | Acceptance check |
|---|---|---|---|---|---|
| F0 | Platform spike: interactive cube on the headset | P0 | §8.4, G-04 | | Grab a cube with a controller on the physical headset |
| F1 | Academy hub with lock/unlock and progress | P0 | FR-APP-01 | | Hub shows modules; finishing L1 unlocks L3 |
| F2 | Module phase FSM (brief → demo → practice → qualify → debrief) | P0 | FR-APP-02, FR-SIM-01 | | EditMode tests cover every legal/illegal transition |
| F3 | Pause / recenter / restart / exit everywhere | P0 | FR-APP-03 | | Reachable from every scene, both input modes |
| F4 | Semantic input layer + controller fallback | P0 | FR-XR-02/04/08 | | Same mission completes with controllers only |
| F5 | Hand-tracking adapter (capability-gated) | P0* | FR-XR-03 | | Works on the headset if validated in M0; otherwise hidden |
| F6 | L1 Cadet Orientation | P0 | §6 | | Point/select/grab/release/place tutorial completes |
| F7 | L3 Cabin Familiarization | P0 | §6 | | Identify + activate requested controls |
| F8 | L4 Emergency: simplified power fault | P0 | FR-SIM-03/05 | | `restore-power-v1` reaches Restored |
| F9 | Event log + deterministic scorer | P0 | FR-AS-01/02/03 | | `fixtures/session-events.ok.json` scores identically every run |
| F10 | Debrief UI (pass/retry, actionable) | P0 | FR-AS-04 | | Wrong bus selection shows the right retry hint |
| F11 | Subtitles + visual alarm indicators | P0 | FR-APP-04 | | Every audio cue has a visual equivalent |
| F12 | Local progress persistence | P0 | FR-APP-05 | | Restart app; progress kept, not corrupted |
| F13 | Scripted offline guidance | P0 | FR-AI-01 | | Works in airplane mode |
| F14 | Session export JSON/CSV (anonymized) | P1 | FR-AS-05 | | Export re-scores to the same result (AT-07) |
| F15 | Coach proxy + Gemini advisory debrief | P1 | FR-AI-02/03 | | Mock + real modes; no state change (AT-08) |
| F16 | Operator desktop view | P1 | FR-IC-01/02 | | Shows live phase + event log |
| F17 | L2 Equipment handling | P1 | §6 | | Only if tracking is good enough after M0 |

## M0 platform spike checklist (blocks everything else)
- [ ] Choose the primary headset + OS version (PRD §13 Q1)
- [ ] Create the Unity 6 LTS project in this repo root (URP template)
- [ ] Install OpenXR Plugin, XR Plug-in Management, XR Interaction Toolkit, Input System (+ XR Hands if supported)
- [ ] Minimal stereo scene, tracked head pose, one grabbable cube, deployed to the headset
- [ ] Test hand tracking on the device; controller fallback works
- [ ] Record frame timing, tracking stability, comfort notes in `docs/STATUS.md`
- [ ] Pin package versions in `Packages/manifest.json`; write the device test matrix
- [ ] Record the decisions in `docs/DECISIONS.md`

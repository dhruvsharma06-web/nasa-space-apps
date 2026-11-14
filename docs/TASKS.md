# TASKS

Feature IDs map to PRD requirements. Owners are filled in at kickoff.

| ID | Feature | Priority | PRD | Owner | Acceptance check |
|---|---|---|---|---|---|
| F0 | Unity project + interactive cube on the headset | P0 | §8.4, G-04 | Integration Lead | Grab a cube with a controller on the physical headset |
| F1 | Interactable prefabs + XR rig + interaction bus | P0 | FR-XR-01/02/04/05 | XR & Interaction | Every action prints a contract event; controllers only |
| F2 | Hand-tracking support (capability-gated) | P0* | FR-XR-03/08 | XR & Interaction | Works on the headset if supported; otherwise hidden, controllers still work |
| F3 | UI: instruction, hint, alarm, phase, debrief, pause menu | P0 | FR-APP-02/03/04, FR-AS-04 | XR & Interaction | Debrief renders `fixtures/session-result.ok.json` correctly |
| F4 | Hub with lock/unlock | P0 | FR-APP-01 | XR & Interaction | Passing L0 unlocks L1 |
| F5 | Phase FSM, mission runner, scorer | P0 | FR-SIM-01, FR-AS-01/02/03 | Core Logic | All `fixtures/` oracle cases pass in EditMode tests |
| F6 | Power-bus simulation | P0 | FR-SIM-03/05 | Core Logic | Fault → isolate → cross-tie → Restored, deterministic |
| F7 | Progress save + unlock rules | P0 | FR-APP-05 | Core Logic | Kill app mid-save; progress still loads (AT-06) |
| F8 | LevelController + level template + session log | P0 | §8, FR-AS-01 | Integration Lead | Any level runs Briefing → Debrief with the template |
| F9 | Content style guide + review of all missions | P0 | §10 | Content & AI Coach | Every mission file passes the validator and the style checklist |
| F10 | Offline coach | P0 | FR-AI-01 | Content & AI Coach | Debrief advice with no network, deterministic |
| F11 | L0 Safety, L1 Orientation | P0 | §6 | XR & Interaction | Playable on the headset |
| F12 | L3 Cabin | P0 | §6 | Content & AI Coach | Playable on the headset |
| F13 | L4 Emergency, L7 Finale | P0 | §6, §6.1 | Core Logic | Playable on the headset; L7 scores match the oracle rules |
| F14 | L2 Equipment | P1 | §6 | Content & AI Coach | Playable on the headset if grab tracking is reliable |
| F15 | Coach proxy + HTTP coach client (Gemini) | P1 | FR-AI-02/03 | Content & AI Coach | Mock + real modes; falls back offline; no state change (AT-08) |
| F16 | Session export JSON/CSV | P1 | FR-AS-05 | Integration Lead | Export re-scores identically (AT-07) |
| F17 | L5 EVA, L6 Robotics | P2 | §6 | Integration Lead | Only after Sync 3 passes |

## M0 platform spike checklist (blocks everything else)
- [ ] Choose the primary headset + OS version (PRD §13 Q1)
- [ ] Create the Unity 6 LTS project in this repo root (URP template)
- [ ] Install OpenXR Plugin, XR Plug-in Management, XR Interaction Toolkit, Input System (+ XR Hands if supported)
- [ ] Minimal stereo scene, tracked head pose, one grabbable cube, deployed to the headset
- [ ] Test hand tracking on the device; controller fallback works
- [ ] Record frame timing, tracking stability, comfort notes in `docs/STATUS.md`
- [ ] Pin package versions in `Packages/manifest.json`; write the device test matrix
- [ ] Record the decisions in `docs/DECISIONS.md`

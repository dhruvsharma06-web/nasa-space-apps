# Product Requirements Document (PRD)
## ASTRA Academy — OpenXR Astronaut Training Simulator
**Document ID:** ASTRA-PRD-002  
**Revision:** D — 8-level curriculum, 4-person team split, tech stack, contract v1.1  
**Date:** 14 November 2026  
**Status:** Approved for build; headset choice (M0) and subject-matter review pending  
**Companion documents:** `CONTRACT.md` (exact interfaces, wins on any conflict), `docs/TEAM.md`, `docs/TASKS.md`  
**Primary application engine:** Unity 6 LTS (exact version/package compatibility to be pinned after spike)  
**Primary XR interface:** OpenXR, where supported by the target Unity player/platform

---

## 1. Executive summary
ASTRA Academy is a portable, level-based extended reality (XR) astronaut-inspired training application. It teaches spatial coordination, equipment handling, spacecraft cabin familiarity, procedural operations, and simulated emergency response through interactive missions. A trainee uses a supported headset to reach, select, manipulate, and operate virtual objects. The app measures task completion, accuracy, sequencing, and time, then provides feedback.

ASTRA is a self-contained **Unity** application using the **OpenXR Plugin** on supported XR headsets. It contains its own scenes, interactions, mission logic, training content, and assessment. Builds are produced and validated for each supported headset and runtime; support for one OpenXR device does not guarantee support for every OpenXR device.

The first release is an educational prototype, **not astronaut certification, clinical fitness training, or an operational flight simulator**. Realistic concepts are simplified and clearly labeled. ASTRA's original professional-style spacecraft procedures and malfunction simulation are retained as an expandable subsystem, but most are outside the MVP.

## 2. Problem and opportunity
Traditional astronaut-themed learning is often passive, while professional spacecraft simulators are expensive and inaccessible. ASTRA aims to provide affordable, repeatable, embodied practice in spatial and procedural tasks, using commodity XR hardware. The platform must avoid overstating fidelity: consumer XR headsets cannot simulate microgravity, g-forces, spacesuit resistance, or validated astronaut physical conditioning.

## 3. Goals and success criteria
| ID | Goal | MVP acceptance criterion |
|---|---|---|
| G-01 | Deliver a coherent XR training journey | Five P0 levels (L0, L1, L3, L4, L7) playable end to end on the target headset; L2 as P1; L5–L6 as stretch |
| G-02 | Support natural interaction | Select, grab, release, move, and activate supported virtual objects using tracked hands or controllers |
| G-03 | Assess procedural performance | Log every scored action, step completion, incorrect action, and timing |
| G-04 | Prove OpenXR deployment | Run the Unity application on one specifically selected standalone headset using its supported Unity OpenXR path |
| G-05 | Deliver a standalone OpenXR build | Install, launch, interact and complete an assessment on a supported headset without external XR software |
| G-06 | Keep mission content reusable | Missions, scoring rules, and interaction semantics are defined separately from headset-specific input bindings |
| G-07 | Enable optional AI coaching | A deterministic evaluator generates structured results that an optional Gemini service can explain; training works without Gemini |
| G-08 | Maintain user comfort | Seated or stationary play, accessible pause/exit, short sessions, no forced locomotion |

**MVP pilot targets (proposed, not validated):** at least 5 internal testers; ≥80% complete orientation without developer intervention; ≥90% of scored interactions recorded; no critical crashes in a 10-minute demonstration. Frame-rate and latency thresholds will be set after hardware profiling.

## 4. Audience and personas
**Primary:** students/teen learners and beginners interested in space STEM and hands-on XR activities. **Secondary:** teachers, demo operators, and facilitators. **Future:** advanced trainees using more technically rigorous scenarios after subject-matter validation.

- **Cadet:** learns through guided physical interactions, practices and earns skill badges.
- **Instructor/operator:** launches sessions, observes progress, resets scenarios and optionally injects approved faults.
- **Content designer:** authors missions, procedures and rubrics without modifying core runtime code.
- **XR integrator:** maintains Unity OpenXR settings, input profiles and device builds.

Age range and safeguarding requirements must be agreed before testing with minors. Follow device manufacturers' age and health guidance; use supervised sessions where appropriate.

## 5. Product boundaries
### In scope for MVP
- Unity XR application, headset/controller input, hand tracking where device/plugin support is confirmed.
- Training hub, the P0 levels (L0, L1, L3, L4, L7), onboarding and calibration; L2 if time allows.
- Virtual tools, buttons, levers, tether interactions, basic control panel, simulated power fault.
- Mission state machine, deterministic scoring, local session logs, results/debrief UI.
- Optional AI instructor prototype using approved mission context and structured events.
- One validated standalone OpenXR headset deployment.

### Out of scope for MVP
- Complete flight-certified or NASA-endorsed training.
- Accurate full-body biomechanics, microgravity, haptic force feedback, g-load simulation.
- Full six-degree-of-freedom spacecraft flight dynamics and detailed orbital propagation.
- Every spacecraft subsystem in the original ASTRA PRD; all ten malfunction categories.
- Multiplayer crew sessions, unrestricted generative missions, real spacecraft software, formal certification.
- Assumption that every OpenXR runtime supports the same hand-tracking and passthrough features.

## 6. Training structure and progression
Each module follows **Briefing → Guided demonstration → Practice → Qualification attempt → Debrief**. The cadet can repeat exercises; speed alone cannot override missed critical steps.

| Level | Module | Core interactions | Completion criterion | Priority | missionId |
|---|---|---|---|---|---|
| L0 | Safety & Calibration | Recenter, verify tracking, learn pause/exit | Setup completed | P0 | `l0-safety-v1` |
| L1 | Cadet Orientation | Point, select, grab, release, place objects | Complete gesture/control tutorial | P0 | `l1-orientation-v1` |
| L2 | Microgravity Equipment Handling | Transfer virtual tools, place objects in sockets, secure equipment | Finish object-handling sequence without critical-step omissions | P1 | `l2-equipment-v1` |
| L3 | Spacecraft Cabin Familiarization | Locate labelled controls, operate buttons/toggles/dials | Identify and activate requested controls | P0 | `l3-cabin-v1` |
| L4 | Emergency Response | Read caution, diagnose a simplified power fault, follow checklist (with hints in Practice) | Restore safe simulated state using correct procedure | P0 | `l4-emergency-v1` |
| L5 | EVA & Tethering | Clip/unclip a virtual tether, use a tool at a fixed work site | Procedure-specific qualification | P2 (stretch) | `l5-eva-v1` |
| L6 | Robotics Arm | Move a simplified robotic arm to capture a target | Procedure-specific qualification | P2 (stretch) | `l6-robotics-v1` |
| L7 | Integrated Mission (finale) | Combine cabin controls (L3), tool handling (L2) and the power procedure (L4), no hints | Complete all critical actions and receive debrief | P0 | `l7-restore-power-v1` |

**MVP:** L0, L1, L3, L4 and L7 are P0. L2 is P1, depending on tracking maturity. L5 and L6 are stretch goals, started only after Sync 3 passes. Level numbers describe curriculum order, not release order. Each level is unlocked by passing the previous available level.

### 6.1 Illustrative integrated mission
**Operation: Restore Station Power** (`l7-restore-power-v1`, fictional educational scenario; the canonical step list is the mission file):
1. Calibrate view and complete safety check.
2. Locate power distribution panel.
3. Acknowledge simulated warning.
4. Read the electronic checklist.
5. Identify the affected virtual power bus.
6. Operate the correct controls in sequence.
7. Verify simulated system recovery.
8. Report completion and review debrief.

No real spacecraft emergency procedure is implied by this fictional sequence. Any claim of operational realism requires SME review and source traceability.

## 7. Functional requirements
### 7.1 Application and UX
| ID | Requirement | Priority |
|---|---|---|
| FR-APP-01 | Show academy hub with modules, locked/unlocked states and progress | P0 |
| FR-APP-02 | Provide briefing, practice, qualification and debrief states per module | P0 |
| FR-APP-03 | Allow pause, recenter, restart and exit from every mission | P0 |
| FR-APP-04 | Offer subtitles and non-audio indicators for instructions/alarms | P0 |
| FR-APP-05 | Persist local progress and session results | P0 |
| FR-APP-06 | Provide optional achievements, badges and instructor profiles | P1 |

### 7.2 XR interactions
| ID | Requirement | Priority |
|---|---|---|
| FR-XR-01 | Use Unity XR Interaction Toolkit and Unity Input System through supported OpenXR bindings on selected headset | P0 |
| FR-XR-02 | Support controller-based pointing/selecting as reliable fallback | P0 |
| FR-XR-03 | Support tracked-hand pinch/select/grab only on platforms where hands are exposed and validated | P0* |
| FR-XR-04 | Expose platform-neutral interaction events: Select, Grab, Release, Activate, Recenter | P0 |
| FR-XR-05 | Provide visible hover/selection feedback and forgiving target sizes | P0 |
| FR-XR-06 | Implement virtual tools, panels and simulated tether interaction | P0 |
| FR-XR-07 | Support seated/stationary calibration and user-height/hand-reach adjustments | P0 |
| FR-XR-08 | Provide alternative input when hand tracking is unavailable or unreliable | P0 |

*Hand tracking is a target for the demo, but the chosen headset and plugin combination must be validated in the initial spike.

### 7.3 Mission and simulation
| ID | Requirement | Priority |
|---|---|---|
| FR-SIM-01 | Run mission objectives through an explicit finite-state machine | P0 |
| FR-SIM-02 | Define objectives, allowed actions, step order, scoring and feedback as versioned data | P0 |
| FR-SIM-03 | Model a simplified electrical system with defined state transitions and failure conditions | P0 |
| FR-SIM-04 | Prevent AI-generated text from directly changing simulated vehicle state | P0 |
| FR-SIM-05 | Provide scenario reset, retry and deterministic scripted faults | P0 |
| FR-SIM-06 | Add environmental/life-support/thermal/GNC simulation only after MVP | P2 |

### 7.4 Assessment
| ID | Requirement | Priority |
|---|---|---|
| FR-AS-01 | Log task ID, event type, timestamp, input modality and outcome | P0 |
| FR-AS-02 | Identify omissions, incorrect actions, sequence errors and completion time | P0 |
| FR-AS-03 | Apply rubric with mandatory critical-step checks | P0 |
| FR-AS-04 | Display pass/retry outcome and actionable debrief | P0 |
| FR-AS-05 | Export anonymized JSON/CSV session data | P1 |
| FR-AS-06 | Add 3D replay and comparative analytics | P2 |

### 7.5 Instructor and AI
| ID | Requirement | Priority |
|---|---|---|
| FR-IC-01 | Provide simple desktop/operator view of mission state and event log | P1 |
| FR-IC-02 | Allow operator to start, pause, reset and select scripted scenarios | P1 |
| FR-IC-03 | Permit instructor fault injection only from approved fault catalogue | P2 |
| FR-AI-01 | Provide scripted offline guidance when AI is unavailable | P0 |
| FR-AI-02 | Optionally use Gemini to explain objectives, offer hints and summarize structured performance | P1 |
| FR-AI-03 | Validate all AI-requested scenario actions against an allowlist and mission state | P1 |
| FR-AI-04 | Make AI responses advisory; scoring and correctness remain deterministic | P0 |

## 8. Technical architecture
### 8.1 Shared Unity application
- **Unity project:** scenes, XR Interaction Toolkit interactables, UI, sound, mission logic, and assets.
- **Input abstraction:** converts platform-specific hand/controller input to semantic events.
- **Mission core:** platform-agnostic C# logic with explicit states, conditions and rubrics.
- **Simulation core:** small deterministic state machine for virtual electrical/control systems.
- **Assessment:** local event log, scoring and debrief generation.
- **Optional services:** Gemini coach and desktop operator console, communicating through a documented local/API protocol.

### 8.2 Supported runtime and deployment
The MVP is a **standalone Unity XR application** built for one selected OpenXR-compatible headset. Other headsets are supported only after testing and a platform-specific build when required.

| Environment | Planned route | Status |
|---|---|---|
| Primary standalone XR headset | Unity player build + OpenXR Plugin + required device runtime/extensions | MVP target; select and validate on physical hardware |
| Additional OpenXR-compatible headsets | Rebuild, configure interaction profiles and validate required features | Future compatibility testing |
| Unity Editor | XR Device Simulator and mock inputs for iteration | Development-only; not evidence of headset compatibility |

Headset pose and input come from the active OpenXR runtime. Features such as hand tracking and passthrough are optional and must be capability-checked. A controller-based interaction fallback is required where supported.

### 8.3 Tech stack (exact versions pinned by the Integration Lead after M0 and recorded in `docs/STATUS.md`)
| Area | Choice |
|---|---|
| Engine / language | Unity 6 LTS, C# |
| XR | OpenXR Plugin, XR Plug-in Management, XR Interaction Toolkit, Input System; XR Hands only where the headset supports it |
| Rendering | URP, tuned for standalone headsets, subject to profiling |
| Editor testing | XR Device Simulator (development only; not headset evidence) |
| Tests | Unity Test Framework (NUnit): EditMode for Core, PlayMode for scenes |
| Content | Validated JSON missions; `scripts/validate_content.py` (Python 3.11+) |
| AI coach (P1) | `services/coach-proxy`: Python 3.11+ + FastAPI, Gemini via the official Google Gen AI SDK; key only in the proxy |
| Assets | FBX / glTF (GLB), Git LFS for binaries |
| Repo / CI | GitHub (protected `main`, PRs), GitHub Actions: content validation + gitleaks; Unity tests via GameCI once a license secret exists |

Do not assume identical feature support across OpenXR platforms. Hand tracking, passthrough, anchors, controller profiles and mixed-reality extensions are capability-gated.

### 8.4 Standalone headset readiness gate
During week 1–2:
1. Select a primary headset and verify Unity, OpenXR Plugin, XR Interaction Toolkit and operating-system compatibility.
2. Deploy a minimal stereo scene with tracked head pose and one grabbable object.
3. Validate hand tracking on device, where supported, and controller interaction as fallback.
4. Measure comfort, frame rate, tracking stability and input reliability.
5. Freeze the supported package versions and device test matrix before implementing full missions.

The MVP is blocked until an end-to-end interactive scene works on physical hardware.

### 8.5 Data model
Repository layout (ownership in `docs/TEAM.md`):
```
Assets/ASTRA/
  Core/          mission phases, runner, scoring, simulation, progress (pure C#, no engine refs)
  XR/            input adapters, interactables, interaction event bus
  UI/            instruction panel, alarms, phase banner, debrief, pause menu
  App/           LevelController (scene glue), loaders, session log writer
  Levels/L<n>_*/ one folder per level: scene + level-only scripts/prefabs
  Scenes/        Hub
  Content/       mission JSON (one file per level)
  Integrations/  coach client (offline + HTTP), operator view (later)
  Art/           models, materials, audio (Git LFS)
  Tests/         EditMode (Core) and PlayMode (scenes)
Packages/  ProjectSettings/        created at M0
services/coach-proxy/              optional Gemini proxy
fixtures/  scripts/  docs/  CONTRACT.md
```
Sample event (full schema in `CONTRACT.md` §1):
```json
{
  "sessionId": "demo-001",
  "missionId": "l7-restore-power-v1",
  "timestampMs": 12842,
  "event": "control_activated",
  "targetId": "power_bus_b_selector",
  "input": "hand_pinch",
  "result": "correct"
}
```

## 9. Non-functional requirements
| ID | Requirement | MVP approach |
|---|---|---|
| NFR-PERF-01 | Maintain comfortable rendering on target hardware | Profile headset frame timing and target its supported refresh budget; record actual measurements |
| NFR-PERF-02 | Avoid network-dependent head tracking where possible | Use local OpenXR headset tracking and rendering; measure motion-to-photon performance on the target device |
| NFR-UX-01 | No artificial camera movement or forced locomotion | Seated/stationary interactions; teleport only if later needed and comfort-tested |
| NFR-UX-02 | Short training sessions and immediate exit | Default module duration ~5–10 minutes, adjustable; pause and remove headset at any time |
| NFR-ACC-01 | Accessible interaction | Controller fallback, subtitles, visual alarms, adjustable reach and clear labels |
| NFR-SEC-01 | Minimize collection of personal data | Local-only default logs; explicit consent for voice/cloud AI; avoid recording raw camera data |
| NFR-SEC-02 | Protect optional service keys | No embedded Gemini API secrets in distributed clients; use controlled backend/proxy |
| NFR-REL-01 | Recover from failed tracking or connectivity | Pause safely, offer recalibration or fallback input |
| NFR-QUAL-01 | Make scoring reproducible | Unit-test mission state transitions and grading against scripted event traces |

## 10. Content integrity and safety
- Use fictional or openly documented spacecraft designs; distinguish illustrative procedures from authentic flight procedures.
- Do not represent completion as a professional astronaut qualification.
- For emergency scenarios, use approved educational scripts and deterministic state transitions.
- Limit movement to a clear, obstacle-free space; no running, jumping, or rapid turning required.
- Check headset fit, device temperature, discomfort, age guidance and supervision requirements.
- Obtain SME review before marketing scenarios as technically accurate astronaut procedures.
- Do not import export-controlled vehicle specifications or restricted operational data into public repositories or AI prompts.
- Do not use NASA insignia/branding in a way that implies endorsement.

## 11. Release roadmap
| Milestone | Scope | Exit gate |
|---|---|---|
| M0 — Platform spike + parts built alone (week 1–2) | Unity project, headset chosen, interactive cube on device; each person builds their parts against `CONTRACT.md` and fixtures | Sync 1: shared prefabs, runner/scorer, LevelController + level template merged; template level plays Briefing → Debrief in the Editor |
| M1 — First real slice (week 3–4) | Real runner + scorer behind the LevelController; L4 on the headset | Sync 2: L4 end to end on the headset with a real debrief |
| M2 — P0 levels (week 5–6) | L0, L1, L3, L4, L7; progress save; offline coach | Sync 3: all P0 levels on the headset, repeatable scores |
| M3 — Harden, polish, P1 (week 7) | L2, Gemini coach, comfort/perf/security pass | Sync 4: feature freeze, `rc-1` tagged |
| M3b — Demo (week 8) | Rehearsals, backup video, submission | Final: reliable 10-minute demo on the target headset |
| M4 — Post-MVP | L5, L6 (if not reached), operator view, more headsets | Validated device-specific builds and content |

Dates are planning estimates, not commitments. Replan after M0. Stage details per person are in `docs/prompts/`.

## 12. Key risks and mitigations
| Risk | Impact | Mitigation |
|---|---|---|
| Target headset or runtime incompatibility | High | Validate hardware and OpenXR feature support during M0; pin compatible Unity packages |
| Hand tracking differs by headset | High | Capability checks, controller fallback, input abstraction |
| Low frame rate or tracking instability causes discomfort | High | Profile on device, optimize rendering, avoid artificial locomotion, provide immediate pause/exit |
| Full ASTRA systems scope overwhelms MVP | High | One simplified electrical fault; defer orbital physics and full systems |
| AI invents unsafe/incorrect procedures | High | Fixed approved scenarios; deterministic scoring; allowlisted actions |
| Tracking misses small switches | Medium | Larger interaction targets, hover confirmation, calibration, optional physical mock-up |
| Unity/plugin licensing or platform requirements change | Medium | Check current terms and supported package matrix before release |
| Unrealistic training claims | Medium | Educational labeling and SME review |

## 13. Dependencies and open decisions
1. **Which standalone headset and OS version is the primary test target?**
2. **Which OpenXR extensions and interaction profiles are available on the selected headset?**
3. ~~Will hand tracking be mandatory?~~ **Decided (Rev D):** no. Controllers are the guaranteed path; hand tracking is a capability-gated target.
4. ~~Will Gemini be required?~~ **Decided (Rev D):** no. Gemini coaching is P1; the offline coach is P0.
5. What is the final age group, session duration and supervision model?
6. Is a low-cost physical panel mock-up in scope?
7. ~~Who owns mission-content validation?~~ **Decided (Rev D):** Content & AI Coach owns the style guide and reviews every mission file; final safety review is still open.

## 14. Acceptance test plan
- **AT-01:** Start app on selected standalone headset, calibrate and enter academy hub.
- **AT-02:** Complete orientation with supported input; repeat with controller fallback.
- **AT-03:** Locate and operate the correct virtual cockpit control.
- **AT-04:** Trigger scripted power fault; complete checklist; verify simulated recovery.
- **AT-05:** Intentionally select wrong control; system records the error and gives accurate retry guidance.
- **AT-06:** Exit/restart module without corrupted progress.
- **AT-07:** Export session log and reproduce scoring from the same event sequence.
- **AT-08:** Run optional AI debrief using only structured session facts; verify no unauthorized state change.
- **AT-09:** Install and run the packaged application directly on the primary supported headset without a separate XR host application.
- **AT-10:** Conduct comfort, accessibility and device-temperature checks on actual hardware.

## 15. Changes from ASTRA-PRD-001
| Original ASTRA draft | Revised ASTRA Academy |
|---|---|
| Professional astronaut trainee focus | Student-first educational training with optional advanced mode |
| Phone crew station + laptop sim host as mandatory architecture | Self-contained Unity application targeting a supported standalone OpenXR headset |
| Seated cockpit operations only; EVA explicitly excluded | Cockpit/procedures in MVP; EVA and robotics on roadmap |
| Large detailed spacecraft subsystem catalogue | Simplified deterministic electrical fault for MVP |
| Instructor-led sessions are primary | Self-guided level progression; optional instructor tools |
| No cloud dependency; offline voice | Offline baseline; optional consent-based Gemini coaching |
| Full telemetry and 3D replay in initial scope | Event logs and deterministic assessment first; replay later |
| High-fidelity simulation and 36-week release plan | Educational prototype and staged 8-week illustrative plan |
| HoloKit/Unity/Flutter/.NET multi-part stack | Unity application with modular integrations; no unnecessary framework mandates |

## 16. Team and ownership (Rev D)
| Role | System | Levels |
|---|---|---|
| XR & Interaction Developer | Interactables, XR rig, hand/controller input, all UI, Hub | L0, L1 |
| Core Logic Developer | Phase FSM, mission runner, scorer, power simulation, progress save, EditMode tests | L4, L7 |
| Content & AI Coach Developer | Content style guide, hints/subtitles, offline coach, HTTP coach client, coach proxy | L2, L3 |
| Integration Lead | Unity project, packages, headset builds, LevelController, CI, PlayMode smoke tests, demo/pitch | L5, L6 (stretch) |

Each level owner writes their own mission file and scene. Folder ownership and hand-offs are in `docs/TEAM.md`; the exact interfaces are in `CONTRACT.md`. Everyone works independently against fixtures and stubs until Sync 1 (end of week 2), the first combine.

## 17. Revision history
| Rev | Date | Change |
|---|---|---|
| C | 2026-11-14 | Standalone Unity OpenXR application |
| D | 2026-11-14 | 8 levels (L5 EVA and L6 Robotics split; finale is L7); priorities per level; 4-person ownership; tech stack table; layout with `Levels/` and `App/`; open decisions 3, 4, 7 resolved; roadmap mapped to sync points; contract v1.1 |

---

**Positioning statement:** *ASTRA Academy is a standalone, Unity-built XR astronaut-inspired training application that turns physical interaction and mission procedures into measurable, adaptive learning experiences. It is built in Unity for supported OpenXR headsets, with guided missions, deterministic assessment and optional AI coaching.*

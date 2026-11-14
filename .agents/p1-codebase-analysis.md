# P1 codebase analysis

Reviewed **2026-11-14**, against `docs/prompts/P1-xr-interaction.md`, contract v1.2, PRD, team ownership and the checked-out source. This is an analysis, not implementation-stage completion.

This document records the pre-implementation baseline. Source implementation has since begun with user authorization; see [current implementation evidence](evidence/p1-implementation.md) for changes, test results and the user-selected Unity 6.3 LTS target.

## Role and delivery priority

We are **P1 — XR & Interaction Developer**. P1 is the teammate prompt identifier, not the PRD feature-priority label. Our feature IDs are **F1 (interactables/rig), F2 (capability-gated hands), F3 (UI), F4 (Hub), F11 (L0/L1)** in `docs/TASKS.md`.

P1 provides shared systems used by every level, then two curriculum levels. The required order is S0 setup/boundary inventory → S1 shared XR and UI → S2 real L4 support → S3 L0/L1 and final Hub → S4 hardening → S5 frozen demo. S1.1–S1.6 ship by end of week 1; remaining S1 work by end of week 2. L0/L1 scenes are S3 work, not the first deliverable. The team has not recorded calendar windows, so schedule drift cannot yet be calculated.

The human Integration Lead owns Unity setup, packages, builds, App/LevelController and the template. P2 owns Core/assessment/progress. P3 owns coach/backend and content review. Our Sol lead coordinates **P1's work**; it does not replace those owners.

## Source inventory: what exists

The asset tree contains **22 C# files, 6 assembly definitions, 0 scenes, 0 prefabs and 0 `.meta` files**. Seven C# files belong to P1: three in XR and four in UI. L0/L1 and the shared scene/template folders contain placeholders rather than playable assets.

| Source | Actual behavior | P1 implication |
|---|---|---|
| `XR/Interactables/InteractionEvents.cs:7–36` | `InteractionReport` holds event/target/input; `Raise` synchronously publishes it; static reset clears subscribers when entering play mode. | A usable source-level event boundary exists. It performs no validation, timestamps, scoring or deduplication. Emitters enforce their own single-action behavior; runner validates later. No Unity execution was verified. |
| `XR/Input/SemanticInput.cs:7–13` | Enum: Select, Grab, Release, Activate, Recenter. | Semantic vocabulary only; no input binding or forwarding implementation. |
| `XR/Input/ISemanticInputSource.cs:9–13` | Interface exposes Modality, IsAvailable and ActionPerformed. | No controller, simulator or hand implementation exists. Avoid creating a second independent event-emission path when adding adapters. |
| `UI/InstructionPanel.cs:9–13` | Instruction/hint methods log; ClearHint does nothing. | No displayed instruction/hint, text reference or prefab. |
| `UI/AlarmIndicator.cs:9–11` | Logs alarm state/caption. | No flashing visual, caption rendering or sound-independent feedback. |
| `UI/PhaseBanner.cs:7–9` | Logs phase. | No rendered phase banner. |
| `UI/DebriefPanel.cs:11–18` | Logs pass/retry and coach text. | No result details, rendered text or buttons. **Required `onRetry` and `onBackToHub` UnityEvents are absent.** |
| `XR/ASTRA.XR.asmdef:4`, `UI/ASTRA.UI.asmdef:4` | XR references Core; UI references Core and XR. | Correct current assembly direction. Package assembly references must be added by P1 when those package types are used. Missing future references are not current compiler errors. |

Paths in this table are relative to `Assets/ASTRA/`. The existing bus is functional source, while the input interface and UI bodies are incomplete by design. Do not characterize every missing implementation as a regression or claim successful compilation from static inspection.

## P1 S1 deliverable checklist

Every S1 step remains incomplete. This is the class/prefab inventory required by P1 S0.2; class names for concrete control variants beyond the prompt's named classes remain a lead implementation decision.

| P1 step | Deliverables | Current gap / required check |
|---|---|---|
| S1.1 | `InputModalityTag`, interactor tagging and acting-interactor modality lookup. | Entirely absent. Controller ray/grip, hand pinch/grab and simulator must report their actual route. |
| S1.2 | Abstract `ControlTarget`. | Absent. Validate target ID and the five allowed control events; 0.3 s cooldown; one report per activation; hover/press feedback within frame. |
| S1.3 | Button, Toggle, Dial and Lever variants plus prefabs under `XR/Prefabs/`. | Absent. Minimum 6 cm targets, label slots; flip/detent/threshold semantics verified. |
| S1.4 | `GrabbableTool`, `PlacementSocket`, tool/socket prefabs and home-pose behavior. | Absent. Object IDs for grab/release, socket ID for placement; matching tool only; return out-of-reach tool after 3 s. |
| S1.5 | `LocatableTarget`. | Absent. At least 1 s dwell; once per attempt; reset correctly. |
| S1.6 | `ConsoleInteractionLogger`, `AstraXRRig` component/prefab and public Recenter entry point. | Absent. OnEnable/OnDisable cleanup, controller ray/direct-grab/poke, runtime-gated hands, seated camera offset and one completed-recenter report. |
| S1.7 | Real InstructionPanel, AlarmIndicator, PhaseBanner, DebriefPanel and a prefab for each under `UI/Prefabs/`. | Four logging stubs only. Preserve public methods; add missing debrief UnityEvents; readable high-contrast text at 1 m; complete debrief; captions; alarm ≤3 flashes/s. |
| S1.8 | `PauseMenu`, pause button/menu and six UnityEvents. | Entirely absent. Controller menu plus reachable world button; Resume/Recenter/Restart/Exit/Finish; Finish visible only in Qualification. |
| S1.9 | `XR/Dev/UI_Preview.unity` and its fixture-loading support. | Absent. Load both session-result fixtures via repo-relative Path.Combine plus L7 mission; verify wrong action `power_bus_a` and omissions are readable. |
| S1.10 | `Scenes/Hub.unity`, eight level cards and required local scene/UI support. | Absent. Lock state from ProgressState, scene availability via CanStreamedLevelBeLoaded, Coming soon, contract scene names; temporary fresh state until real store arrives. |
| S1.11 / Sync 1 | Sandbox with one of each prefab, event evidence, `XR/Prefabs/README.md`, two staged handoffs. | Absent. Prefab/rig/logger handoff by week 1; UI/pause/Hub by week 2; one-line prefab usage notes. Commits/PRs need authorization. |

No dev scenes should be added to Build Settings. New Unity assets require real import and stable `.meta` files. An XR/UI test folder also needs its own compatible test asmdef so NUnit code does not compile into the production assembly; shared Core tests and integration PlayMode tests belong to other owners.

## External dependencies: inspect, do not implement as P1

| Source / gate | Actual state | Impact |
|---|---|---|
| `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectVersion.txt` | Absent. | No pinned Unity project/package versions. Cannot prove Unity compilation, package API compatibility, import or simulator readiness. P1 S0.1 says do S0.2 while waiting; do not create a separate project. |
| `App/LevelController.cs:17–36` | Declares public events/properties; Awake only loads mission JSON when assigned. | No bus subscription, attempts, pause clock, runner/scorer, save, phase flow or debrief wiring. Owned by Integration, not P1. |
| `Levels/_Template/` | Placeholder only; no LevelTemplate.unity. | L0/L1 scenes cannot yet be copied from the canonical template. |
| `Core/Mission/IMissionRunner.cs`, `Core/Assessment/IMissionScorer.cs` | Interfaces and result types exist; real implementations absent. | UI/sandbox work uses contracts/fixtures; final scoring requires P2's real implementation. |
| `Core/Progress/ProgressState.cs:8–12,30–34` | Fresh state unlocks only L0; IProgressStore interface exists. No FileProgressStore or ProgressRules. | Initial Hub can use fresh state as P1 permits; saved progress/final unlocking must wait for Core. |
| `Core/Mission/ModulePhaseMachine.cs:10–39` | Actual transition implementation exists. | The whole Unity tree is not merely empty stubs. This Core logic remains P2-owned and was not executed in Unity during this review. |
| `Integrations/Coach/CoachProvider.cs:12` | Client returns null by design. | Real offline/HTTP Unity coach clients absent. Null coach handling is an integration concern; P1 renders supplied advice and labels AI text. |
| `services/coach-proxy/app/main.py` | FastAPI scaffold has health endpoint, fixture-backed mock response and 503 for unconfigured real mode; backend tests are present. | Backend has code beyond interfaces, but it is P3-owned and does not unblock Unity UI/XR. Backend tests were not run as part of this P1 audit. |
| `docs/STATUS.md:14–16` | Device/runtime/version matrix TBD. | Hand support cannot be researched for a specific device yet; headset, comfort and performance checks remain unrun. |

## Integration decisions to settle before affected work

1. **One event route:** new input adapters and XRI callbacks must not each raise the same action. The acting interactor supplies the modality; interactables report through the existing bus. Only App forwards to runner, timestamps and persists.
2. **Recenter emitter:** P1 S1.6 gives the rig Recenter emission and S1.8 also mentions menu emission. Coordinate one publisher per completed recenter. This needs an explicit wiring design, not two independent implementations.
3. **L0 pause/resume:** these events can match pending mission steps. Pausing ordinary input must not prevent the resume report reaching the active runner. Frozen/paused timing and report order belong to the Integration Lead's controller; prepare that handoff.
4. **L0 reserved scene targets:** contract §1 fixes `system` and `calibration`; §2 requires each mission target to bind to one interactable. Request the contract owner's canonical scene representation for app-level events. Do not invent new IDs or duplicate the `system` target across several objects to bypass it.
5. **L1 grab → release → regrab → place:** the second grab is a neutral repeat under §4. Do not add duplicate event/target mission steps. A successful placement reports only `object_placed` on the socket; a wrong drop reports `object_released` on the tool.
6. **Dwell reset and UI phase control:** shared XR/UI cannot reference higher App assembly. Connect attempt reset, pause state and qualification-only Finish from App/level composition using events/public entry points. Do not add an XR→App dependency to observe phases.
7. **Debrief labels:** result errors include target IDs such as `power_bus_a`, which need not correspond to a completed mission step. Agree a readable label/fallback approach within P1; do not display only raw IDs or add unapproved mission schema fields.

These are design/contract handoffs, not claims that the stubbed app already exhibits the failures.

## What verification establishes

- `python3 scripts/validate_content.py` ran successfully: **11 reported files pass** (two missions and nine fixtures). The sample missions are L3 and L7; L0/L1 content is absent.
- The validator checks present mission keys/events/step uniqueness and basic values; validates event/result fixtures; replays the Python matching/scoring oracle including four scoring cases; checks coach fixture shapes. It does **not** inspect scenes, target bindings, controller callbacks, visual UI, Unity compilation, reachability or device behavior. A passing scan also does not require L0/L1 mission files to exist.
- `Tests/EditMode/ModulePhaseMachineTests.cs:9–30` defines **three Core FSM tests**. There are no XR/UI/L0/L1 tests or PlayMode test implementations. The Unity tests were inspected, not run.
- `.github/workflows/ci.yml:12–34` defines content validation and gitleaks jobs. Unity tests are a comment pending version/license setup. The content check passed locally; no claim is made about remote CI or a secret-scan run.
- Static inventory confirmed zero scenes/prefabs/metadata and absent project configuration. JSON inspection confirmed the current Core/XR/UI references and Core's `noEngineReferences: true`. This is not a compiler result.
- `docs/STATUS.md:5` and the validator's opening docstring still say contract v1.1; `CONTRACT.md:1` and the P1 prompt say **v1.2**. Treat the contract as authoritative and hand stale shared documentation/tool comments to Integration rather than editing them as P1.

## Next appropriate P1 action

The source review and S1 inventory required by **S0.2** are now documented. **S0 as a whole is not complete.** S0.1 still lacks verified LFS setup and pinned installed Unity; S0.3 awaits the canonical project/simulator; S0.4 awaits a selected headset and sourced capability research. We did not install/configure anything or start S1 during this analysis.

Once setup is supplied and the stage is authorized, use Sol for the consequential input/activation/socket/reset design, then bounded Terra tasks and Luna check preparation. Prioritize the shared XR handoff before level scenes. During S2, test in L4 and fix P1-owned prefab bugs within a day without editing the Core owner's scene. During S3, integrate L0/L1 with real progress and accessible UI. Finish S4 hardening and S5 media/README handoffs using the P1 prompt's stage reports and cut list.

The amended [P1 task plan](l0-l1-plan.md) includes these deliveries. Backend/shared-Core workers remain on demand for read-only dependency analysis; there is no P1 backend-development workstream.

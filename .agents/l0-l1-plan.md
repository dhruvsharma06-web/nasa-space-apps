# P1 task plan: shared XR/UI, Hub, L0 and L1

Planning baseline: **2026-11-14**. Deliverable for this request: agent framework and task plan only. The tasks below are proposed implementation work; none is marked implemented or tested by this document.

Implementation update: the user has since authorized safe P1 source implementation and isolated validation ahead of canonical setup, and selected **Unity 6.3 LTS** with macOS/Windows as desired platforms. The original planning baseline below is historical; consult [current implementation evidence](evidence/p1-implementation.md) for source and test progress. This exception does not authorize shared project/package changes, claiming stage completion, or skipping device acceptance.

Our role is **P1 — XR & Interaction Developer**. It owns shared XR interaction, input, all UI, the Hub, **L0 Safety & Calibration**, and **L1 Cadet Orientation**. It depends on the other three project owners rather than taking over their systems. Follow P1 stages in `docs/prompts/P1-xr-interaction.md`; start a stage only after the user authorizes it. The [P1 codebase analysis](p1-codebase-analysis.md) maps every S1 deliverable to current source and missing work.

Delivery order is **shared XR prefabs/rig/logger by end of week 1 → UI/pause/Hub by end of week 2 → L4 headset support in S2 → our L0/L1 scenes in S3**. Preserve P1's stage and step order; concurrency applies to small independent support tasks and must not skip a stage or its acceptance gate. The team has not recorded calendar dates, so these are relative windows.

## Current baseline and external gates

The repository contains the interaction bus/input interfaces and logging UI stubs, two sample missions for other levels, fixtures, and a content validator. There is no `Packages/` or `ProjectSettings/` project configuration, no shared rig/prefabs, no level template, and no L0/L1 mission/scene. `LevelController` only deserializes a mission; the real runner/scorer/progress integrations are pending. Unity version, primary headset, and device runtime are not pinned in `docs/STATUS.md`.

| Gate | Needed from | Evidence that opens it |
|---|---|---|
| G0: reproducible Unity project | Human Integration Lead | Exact Unity/package versions, project imports cleanly, simulator installed and usable. |
| G1: phase/controller/template integration | Human Integration Lead | Template available; real Practice/Qualification event forwarding, pause-aware timing, result/debrief and retry flow verified. |
| G2: deterministic scoring and progress | Core Logic owner | Real runner/scorer/progress store satisfy contract and fixtures; Hub can load saves/unlock available levels. |
| G3: primary headset selected | Team / Integration Lead | Headset model, OS/runtime and controller route recorded. This opens capability research and rig development; hand reliability is an output of DEVICE-01, not a prerequisite of G3. |
| G4: L0 reserved target clarification | Human Integration Lead / contract owner | Clarify how `system` and `calibration` satisfy scene-target uniqueness without conflicting with app-level emission. Keep current contract until resolved. |

Sol must settle these designs before dispatching affected implementation:

- **L0 event capture:** pause/resume/recenter are matchable mission steps. Record `paused` and `resumed` once during an active attempt even though ordinary interactions are suppressed while paused. Preserve nondecreasing timestamps and exclude paused duration. This behavior belongs in the other owner's `LevelController`; prepare a handoff rather than editing it.
- **Recenter ownership:** both the rig and pause menu descriptions mention raising `recentered`. Agree on one emitter per completed recenter; do not allow menu plus rig to double-report.
- **Reserved targets:** `CONTRACT.md` requires app events on `system`, calibration on `calibration`, and one interactable per mission target ID. Ask the contract owner for the canonical scene binding. JSON validation cannot resolve this ambiguity.
- **L1 release then placement:** use `grab → release → grab → place`. The second grab on the same tool is a neutral repeat under the matching rule, not a second identical mission step. A successful socket placement emits `object_placed` on the socket ID, with no additional `object_released` event.

## Work breakdown

All rows begin **planned**. `After` identifies prerequisites, not completed work. Before dispatch, Sol expands each write lane to an exact file list including metadata. Each row can become several small tasks; avoid delegating an entire subsystem at once.

| ID / stage | Accountable role | Bounded deliverable and write lane | After | Acceptance evidence |
|---|---|---|---|---|
| SETUP-01 / S0 | Sol + tester | Inspect checkout/LFS readiness and pinned project; create `XR/Dev/XR_Sandbox.unity` when available. Do not create our own Unity project. | G0 for Editor work | Clean import; simulator can point, poke and grab; report unverified setup instead of claiming S0 complete. |
| DESIGN-01 / S0 | Sol | Inventory every S1 class/prefab (S0.2); agree event routes, recenter emitter, modality lookup and UI wiring. Inspect chosen headset's OpenXR/XR Hands support and cite its source once G3 opens (S0.4). Framework notes only. | Contract review; G3 for device research | Written interface decisions and owner handoffs; full delivery inventory; sourced hand-support answer when device known; no contract edits. |
| XR-01 / S1 | Terra XR | `InputModalityTag` and per-interactor modality handling in `XR/Input/`. | SETUP-01, DESIGN-01 | Correct controller/simulator modality; invalid tags detected. |
| XR-02 / S1 | Sol + Terra XR | Sol designs activation/debounce behavior; helper implements bounded `ControlTarget` changes and Button/Toggle variants in `XR/`. | XR-01 | One allowed event per activation, 0.3 s cooldown, same-frame feedback. |
| XR-03 / S1 | Terra XR | Separate Dial and Lever tasks in `XR/`; exact prefab/script lists per task. | XR-02 | Trigger detent/threshold fires once; reset and repeated movement checked. |
| XR-04 / S1 | Sol + Terra XR | Sol designs grab/socket lifecycle; helper implements small tool/socket changes in `XR/`. | XR-01 | Correct object/socket IDs, matching-only sockets, one placement event, out-of-reach return after 3 s. |
| XR-05 / S1 | Terra XR | Locatable target and console logger as separate tasks in `XR/`. | XR-01 | Dwell ≥1 s fires once per attempt; reset works; subscription cleanup verified. |
| XR-06 / S1 | Sol + Terra XR | Rig composition and recenter in `XR/Prefabs/`, one scene/prefab writer. | XR-02, XR-04, DESIGN-01, G3 for hands | Controller ray/poke/grab; one recenter report; hands behind a runtime capability gate. Final supported-hands decision awaits DEVICE-01 evidence; it does not block the controller rig. |
| UI-01 / S1 | Terra frontend | InstructionPanel, PhaseBanner and AlarmIndicator as separate tasks, each with a prefab in `UI/Prefabs/`. | SETUP-01, DESIGN-01 | Public signatures preserved; high-contrast text readable at 1 m; captions, visible states, alarm ≤3 flashes/s. |
| UI-02 / S1 | Terra frontend | DebriefPanel and prefab in `UI/`; add currently missing `onRetry` / `onBackToHub` UnityEvents. | UI-01 | Fixture-driven PASS/RETRY, completed/omitted/sequence/wrong-action feedback in plain language, advisory AI label and both callable events. |
| UI-03 / S1 | Sol + Terra frontend | Sol designs pause event wiring; helper builds PauseMenu/prefab in `UI/`. | UI-01, DESIGN-01 | Controller menu and reachable world-space pause button; six required UnityEvents; Resume/Recenter/Restart/Exit/qualification-only Finish; one event per pause/resume/recenter. |
| UI-04 / S1 | Terra frontend + Luna tester | `XR/Dev/UI_Preview.unity` and initial eight-card `Scenes/Hub.unity`; separate serialized tasks. Dev scenes remain outside Build Settings. | UI-02, UI-03 | Preview loads both session-result fixtures via repo-relative `Path.Combine` plus L7 mission JSON; `power_bus_a` and omissions read clearly. L0–L7 locked/unlocked/Coming soon cards; scene-name loading. Fresh `ProgressState` temporary until G2. |
| QA-01 / S1 | Luna tester + Sol | One of each prefab in sandbox, test evidence, targeted tests and `XR/Prefabs/README.md` usage list. Prepare two reviewable handoff bundles: prefabs/rig/logger by week 1, UI/pause/Hub by week 2. | Relevant XR/UI task | Exact event tuples and one report/action; zero errors; fixture preview and Hub checked; prefab usage documented. Commits and two PRs require user authorization. |
| CONTENT-00 / S2 | Luna content | Draft `l0-safety-v1.json` after Sol approves event/target map; request wording review from Content owner. | DESIGN-01, G4 | Four critical safety steps; valid JSON; wording/style and owner clarification recorded. |
| DEVICE-01 / S2 | Sol + Luna tester | Validate shared prefabs on headset inside Core-owned L4 with Integration Lead; fix only our prefab/script bugs, responding within a day. Do not edit L4 scene. | QA-01, G3; G1/G2 and Core's L4 for real slice | L4 headset flow uses our prefabs with real debrief; seated reach/hit/feedback/readability results. If supported, 10 pinch-select, poke and grab trials per applicable prefab, record counts; any action below 9/10 keeps hands disabled. |
| CONTENT-01 / S3 | Luna content | Draft `l1-orientation-v1.json` from approved target map. | DESIGN-01, XR-04 | At least five steps; unique event/target pairs; content validator and wording review. |
| L0-01 / S3 | Sol + Terra level support | Sol integrates L0 copied template, calibration panel and mission into `Levels/L0_Safety/`. Helpers get only small level-local tasks. | CONTENT-00, XR-06, UI-01, UI-02, UI-03, G1, G2, DEVICE-01 | Full phase flow, matching safety events, correct debrief, retry/reset. |
| L1-01 / S3 | Sol + Terra level support | Sol integrates L1 tutorial using shared prefabs in `Levels/L1_Orientation/`. Disjoint from L0 if delegated. | CONTENT-01, XR-02, XR-04, XR-05, XR-06, UI-01, UI-02, UI-03, G1, G2, DEVICE-01 | Select/activate/grab/release/place trace and debrief agree; neutral second grab accepted. |
| HUB-01 / S3 | Terra frontend | Replace temporary Hub state with real progress using existing Core interface. Owned Hub scene/explicit local helper only. | UI-04, G2, L0-01, L1-01 | Passing L0 unlocks L1; persistence survives restart; unavailable levels show Coming soon. |
| REVIEW-01 / each change | Independent Terra reviewer | Read-only review of one completed task diff. | Task in review | Findings resolved; contract, scope, event lifetime, asset metadata and test evidence checked by Sol. |
| ACCEPT-01 / S3 | Sol + Luna tester | Run L0 → L1 → Hub with controllers on headset; report evidence. | L0-01, L1-01, HUB-01, REVIEW-01 | S3 matrix rows pass; distinguish real vs stubbed; attach P1 stage report. S4 hardening rows remain planned. |
| HARDEN-01 / S4 | Sol + assigned helpers | Tracking-loss auto-pause/message, hands-loss controller continuity; double press, two-object grabs, out-of-reach drops, pause mid-grab, recenter mid-level and rapid-click failures. Profile our scripts/canvases; polish existing feedback/labels. | S3 accepted; S4 authorized | No critical/major break-it failures; no per-frame allocations or every-frame canvas rebuilds; zero errors for 10-minute headset session; known limits recorded. |
| FREEZE-01 / S5 | Sol + Luna tester | Bug fixes only; L0 → L1 → Hub twice on team `rc-1`; screenshots/clips of levels and UI; draft exact `Interactables & UI` README section for Integration Lead's handoff. | S4 accepted; S5 authorized | Two recorded successful runs, media handoff, per-prefab usage section and honest known limits. Root README edited only with owner/user authorization. |

Core and backend specialists are summoned for concrete dependency questions. They inspect contracts and prepare owner handoffs; shared Core, App and coach/backend implementations are not part of this backlog. L2–L7 scene writes are outside scope. P1 still supports the L4 real slice by testing and fixing its own shared prefabs in cooperation with Core and Integration.

## Suggested execution waves

- **Now, planning only:** contract/design review, architecture notes, acceptance protocols. This request completes here.
- **S0 after authorization:** SETUP-01 and DESIGN-01; report missing project/device gates rather than fabricate them.
- **S1 independent lanes:** one Terra XR helper, one Terra UI helper, and one Luna test-preparation helper. Within a lane, respect dependency order. Sol designs important logic and integrates. Rotate a free slot to code review as tasks finish.
- **S2:** device checks and the L0 content draft can proceed independently once their gates open.
- **S3:** L0 and L1 level work may proceed concurrently only with distinct exact asset/script files and fixed shared prefabs. A third helper prepares acceptance checks or reviews. Serialize Hub changes and final integration.
- **S4/S5:** prioritize observed failures; no new feature expansion after freeze.

## Proposed mission event maps

These maps are design inputs, not new mission files. Sol finalizes scene labels and IDs before content/scene workers start. `system`/`calibration` remain subject to G4's scene-binding clarification.

| L0 order | Expected event / target | Critical |
|---|---|---|
| 1 | `calibration_completed` / `calibration` | Yes |
| 2 | `recentered` / `system` | Yes |
| 3 | `paused` / `system` | Yes |
| 4 | `resumed` / `system` | Yes |

| L1 order | Expected event / proposed target | Critical |
|---|---|---|
| 1 | `target_located` / `orientation_target_panel` | No |
| 2 | `target_selected` / `orientation_target_panel` | Yes |
| 3 | `control_activated` / `orientation_start_button` | Yes |
| 4 | `object_grabbed` / `orientation_practice_tool` | Yes |
| 5 | `object_released` / `orientation_practice_tool` | Yes |
| 6 | `object_placed` / `orientation_tool_socket` | Yes |

L1's successful physical trace includes another `object_grabbed` after step 5 and before step 6. It is recorded as neutral and does not get a duplicate mission step. The optional locate step can be skipped under the contract; selection remains controller-accessible. The `orientation_target_panel` has one scene object supporting both locate and select, not two objects with the same ID.

## Acceptance matrix

Record build/revision, environment, input route, steps, expected/actual events, outcome, and evidence for every executed case. A cell below is a planned check, not a result. S3 acceptance includes the first eight rows and the S3-labelled comfort/controller checks in row nine. The S4-labelled tracking and reliability checks in the last two rows must pass before final acceptance, without blocking the start of S4.

| Case | Editor / simulator check | Headset acceptance |
|---|---|---|
| L0 safety sequence | Four expected event/target pairs advance in order. Recenter and pause/resume are neutral only when unmatched, not when they match the pending step. | Calibration is usable seated; all safety controls can be found and used with controllers. |
| Pause/resume | One report per action; normal interactions suppressed while paused; resume still recorded; no clock jump from paused duration; current step preserved. | Pause mid-grab, recenter, resume and exit remain comfortable and reachable. |
| Recenter | Menu/rig produces one report; no duplicate advancement. | Actual origin/view resets correctly; targets remain in reach. |
| L1 select and activate | Correct semantics and modality; rapid double press respects 0.3 s cooldown; no duplicate report. | Ray/poke feedback and target hit reliability work at seated reach. |
| L1 tool/socket | Grab → release → neutral grab → place completes; object IDs used for grab/release, socket ID for place; wrong object releases without placing. | Drop/regrab/placement understandable with controllers; out-of-reach tool returns home after 3 s. |
| Reset and retry | Dwell latch, cooldown, tool/socket and event subscriptions reset appropriately; fresh attempt and no stale reports. | Two complete L0/L1 runs work without restarting the app. |
| UI and debrief | Hint behavior respects phase; fixtures show mistakes/omissions plainly; advisory label; alarm caption works muted and ≤3 flashes/s. | Readable at 1 m; colour never sole signal; captions for audio. |
| Hub/progress | Pass L0 unlocks L1; failure leaves it locked; restart preserves progress; absent scene is Coming soon. | L0 → L1 → Hub journey completes on installed build. |
| Comfort / S3; tracking hardening / S4 | S3: targets within 0.7 m horizontally, 0.7–1.5 m high, minimum 6 cm; no forced movement. S4: tracking-loss recovery. | S3: validate reach/comfort and controller-only operation. S4: tracking-loss auto-pause; controllers continue when hands unavailable. |
| Reliability hardening / S4 | Test repeats, wrong targets, two-object grabs, pause mid-action and subscription cleanup. | Zero console errors during a 10-minute session; performance evidence captured with Integration Lead. |

Run `python3 scripts/validate_content.py` after mission changes. Run meaningful Unity tests in compatible owned test assemblies and request external-owner PlayMode integration tests where needed. Scene wiring/target uniqueness, actual compilation, and headset acceptance require their own evidence.

Final acceptance requires the real runner, scorer, progress, phase controller and debrief, with controller-only completion on the selected headset. A sandbox, mock or validator pass is intermediate evidence. Dates for implementation remain relative to the team's stage schedule until the Integration Lead records calendar dates.

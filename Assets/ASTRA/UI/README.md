# P1 shared UI

The UI displays supplied mission state and assessment data. It never runs the mission, computes a score, changes unlocks, writes progress, or changes `Time.timeScale`.

## Components and prefabs

| Component / prefab | Composition contract |
|---|---|
| `InstructionPanel` | Bind distinct instruction/hint `UnityEngine.UI.Text` fields. Existing ShowInstruction/ShowHint/ClearHint methods update literal text; clearing a hint does not hide the panel. |
| `AlarmIndicator` | Bind a persistent caption and a separate light Graphic. SetAlarm controls the warning; flashing uses unscaled time and is limited to 3 flashes/s. Zero means a steady light. A blank active caption falls back to ALARM ACTIVE. |
| `PhaseBanner` | Call Show from the phase-change notification. It renders the five contract phases without driving transitions. |
| `DebriefPanel` | Bind four distinct Text labels. Show renders the supplied PASS/RETRY, completed/skipped/missed steps, sequence errors, incorrect targets and time. It clears previous advice. ShowCoachText labels AI advice exactly “AI coach (advisory)”. |

World-space prefabs are in `Prefabs/`: InstructionPanel, PhaseBanner, DebriefPanel, AlarmIndicator, PauseMenu and Certificate. **ASTRA → P1 → Rebuild P1 assets and Hub scene** regenerates them in place.
- **Transparent-backed.** The level factory scales each panel to its physical width and adds the dark-glass backplate: PhaseBanner 0.34 m, InstructionPanel 0.42 m, DebriefPanel 0.46 m, AlarmIndicator 0.28 m. Font sizes are chosen from those widths so body text is ≥ 12 mm and titles ≥ 18 mm cap height on the phone; `UiPrefabBuilderTests` checks this.
- **Palette** (`Theme`): accent `#409CFF`, success `#30D158`, warning `#FFB340`, error `#FF6961`, white text on dark glass.
- **Every button is gaze-selectable** (`Gaze/GazeButton`: a BoxCollider sized to the RectTransform; tap or dwell invokes `onClick`).
- **PhaseBanner** slides and fades in over 280 ms. Nothing moves the world.
- **DebriefPanel** shows PASSED / NOT PASSED, then the score reveal:
  1. the score counts 0 → N over 1.2 s;
  2. the medal appears with a soft steady glow;
  3. breakdown lines slide in one by one;
  4. coach advice follows.

  It shows NEW BEST when the score beats the stored best. When passed, **Next level** is the primary button (**Graduate** on L7, which opens the Certificate). When not passed, **Retry** is primary and says what to fix ("RETRY: FOCUS ON STEP 3"). `onNextLevel` and `onGraduate` are UnityEvents for the Integration Lead to wire. Scores come from `Progress/ScoreReports.Source`, P2's `ResultRating`/`ScoreBreakdown` adapter; until then a clearly marked placeholder is used. The panel never changes the result or progress.
- **Certificate**: "ASTRA ACADEMY: CADET QUALIFIED", callsign, date, academy score, 8 level medals, the fictional ASTRA STATION crest and "Fictional training, not an official certification".
- **AudioCues** (ScriptableObject): 14 self-made CC0 clips in `Assets/ASTRA/Audio` (see its CREDITS.md). The team lead hooks playback into the factory and LevelController.

Rich-text parsing is disabled. The prefabs use Unity's built-in font, so there are no extra font imports. Generated with the scripts in `Editor/`; **Unity 6.3 import and phone legibility are not verified on a device**.

`ASTRA → P1 → Create shared UI prefabs` creates only missing prefabs and preserves authored adjustments. `UiPrefabBuilder.BuildInDirectory` supports test output only under the owned UI tree.

## Pause and lifecycle wiring

`PauseMenu` must remain outside the child `menuRoot` that it hides. `finishButton` must be inside that root. Configure it while inactive or assign its serialized references before play.

- Connect onPause/onResume to the App attempt clock; UI does not implement or freeze that clock.
- Connect onRestart/onExit/onFinish to App's restart, Hub and attempt-completion handlers.
- Feed SetPhase from phase changes. Finish is available only in Qualification and can be requested once per attempt. Leaving the active attempt clears the overlay without inventing a resumed action.
- Controller/tracked-pointer adapters call TryPause/TryResume/TryToggle with the actual acting interactor. Parameterless bindings use the explicitly configured controller/simulator tag; they cannot infer which tracked pointer acted.
- The recenter service implements `Astra.XR.Input.IRecenterHandler`. It completes the actual recenter and publishes the sole `recentered`/`system` report using the acting interactor. Missing/refused services produce no success report. **onRecenter is a completion notification; do not wire it to request another recenter or publish another report.** The real rig/service is still pending.
- ResetForAttempt clears menu state without producing learner events. Visibility changes triggered by activation callbacks are queued to avoid nested Unity hierarchy activation and stale callbacks from a previous attempt.

App must record paused/resumed/recentered system reports during active attempts even while ordinary interaction input is suppressed. L0 can require those reports as critical steps. In particular, resume must reach the runner before normal input is accepted again, with paused duration excluded from the attempt timestamp.

## Hub source

`Hub/LevelCatalog` contains the eight exact scene routes from CONTRACT.md §2. `LevelCard` displays lock, availability and pass state. A locked unavailable scene shows both states; all unavailable scenes remain non-interactable.

`HubController` loads a supplied IProgressStore and presents it without saving or mutating progress. Until the real store is supplied, it uses the contract's fresh L0-only state and identifies the preview as unsaved. App/Core composition can call UseProgressStore when the real implementation is available. Scene availability defaults to Application.CanStreamedLevelBeLoaded, and scene loading defaults to SceneManager.LoadScene. Selection is guarded against repeated/reentrant loading and disabled-card callbacks.

The Hub scene itself is not yet created. Real progress persistence/unlocking, shared XR UI input and L0/L1 scene integration remain dependencies; source tests use injected availability/loading delegates and a progress store to verify presentation without changing Build Settings or Core.

## Verification

Tests live under separate `Tests/PlayMode` and `Tests/EditMode` assemblies. They cover supplied fixture results, visible text/state, persistent prefab bindings, pause lifecycle/event semantics, and Hub eligibility/store boundaries. See `.agents/evidence/p1-implementation.md` and machine-readable evidence at the repository root for the latest run, environment and remaining requirements.

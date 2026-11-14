# P1 shared UI

The UI displays supplied mission state and assessment data. It never runs the mission, computes a score, changes unlocks, writes progress, or changes `Time.timeScale`.

## Components and prefabs

| Component / prefab | Composition contract |
|---|---|
| `InstructionPanel` | Bind distinct instruction/hint `UnityEngine.UI.Text` fields. Existing ShowInstruction/ShowHint/ClearHint methods update literal text; clearing a hint does not hide the panel. |
| `AlarmIndicator` | Bind a persistent caption and a separate light Graphic. SetAlarm controls the warning; flashing uses unscaled time and is limited to 3 flashes/s. Zero means a steady light. A blank active caption falls back to ALARM ACTIVE. |
| `PhaseBanner` | Call Show from the phase-change notification. It renders the five contract phases without driving transitions. |
| `DebriefPanel` | Bind four distinct Text labels. Show renders the supplied PASS/RETRY, completed/skipped/missed steps, sequence errors, incorrect targets and time. It clears previous advice. ShowCoachText labels AI advice exactly “AI coach (advisory)”. |

Four world-space prefabs are in `Prefabs/`. The debrief has clipped vertical scroll areas for details and advice, plus 8 cm-high RETRY/HUB buttons bound to RequestRetry/RequestBackToHub. Text is high contrast and rich-text parsing is disabled. These assets were generated and round-tripped with the installed Unity 6000.6.5f1; **Unity 6.3 LTS import and headset readability remain unverified**.

The UI assembly references `UnityEngine.UI`. The Integration Lead must provide the canonical project's UGUI package/version; this work does not add or upgrade project packages. The isolated validation project used bundled UGUI 2.6.0. The prefabs use a built-in font, avoiding separate font/resource imports.

Use `ASTRA → P1 → Create shared UI prefabs` to create missing assets in a Unity project. `UiPrefabBuilder.BuildInDirectory` supports comparison/test output only under the owned UI tree. Generation preserves existing prefabs and their authored adjustments; it does not silently overwrite them or change project settings.

The prefabs have a standard GraphicRaycaster. Actual tracked-controller/hand UI interaction still requires the selected XR UI input module and tracked-device raycaster integration. Place panels and controls within the contract's seated reach limits; do not treat prefab dimensions as headset acceptance.

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

# CONTRACT v1.2 — ASTRA Academy

**Owner:** Integration Lead. Nobody else edits this file. To change it, open an issue titled `CONTRACT CHANGE REQUEST` (see §11).
**Rule:** every boundary between owners is coded against this file from day one. Mocks and stubs return these exact shapes.
**If this file and any other document disagree, this file wins.**

---

## 0. Conventions
- JSON, UTF-8. Field names are case-sensitive and **camelCase**. C# classes that are serialized use **public camelCase fields** so `JsonUtility` maps them 1:1.
- `timestampMs`: milliseconds since the current attempt started (Practice or Qualification), integer ≥ 0, never decreasing within one attempt. Measured by the LevelController (§8), never by Core.
- IDs: `stepId`/`targetId` are lowercase `snake_case`. `missionId` = `l<level>-<name>-v<major>` and equals the mission file name without `.json` (e.g. `l3-cabin-v1`).
- Unknown fields are rejected by the validator (`scripts/validate_content.py`).
- No personal data in any payload or log: no names, emails, raw camera, raw voice (NFR-SEC-01). `sessionId` is a random GUID string.
- Assemblies (asmdefs) and allowed references, lower never references higher:
  `ASTRA.Core` (no Unity) ← `ASTRA.XR` ← `ASTRA.UI` ← `ASTRA.App`; `ASTRA.Coach` → `ASTRA.Core`; `ASTRA.App` → `ASTRA.Coach`.
  Level scripts in `Assets/ASTRA/Levels/**` have no asmdef (they compile into the default assembly and may use all of the above). Test asmdefs reference the assemblies they test. Package assemblies (e.g. `Unity.XR.Interaction.Toolkit`, `Unity.TextMeshPro`) are added to an asmdef by that asmdef's owner.
- String constants live in `Astra.Core.Assessment` (`EventNames`, `InputModality`, `EventResults`, `SystemTargets`). Use the constants and never retype the strings.

## 1. SessionEvent: one recorded action
C# `Astra.Core.Assessment.SessionEvent` · Created by the mission runner (§4) · Consumed by scorer, log, exporter, coach.

```json
{
  "sessionId": "string, required",
  "missionId": "string, required, equals the running mission",
  "timestampMs": "integer >= 0, required",
  "event": "EventNames value, required",
  "targetId": "string, required, non-empty",
  "input": "InputModality value, required",
  "result": "EventResults value, required (stamped by the runner)"
}
```
- `event`: `calibration_completed | target_located | target_selected | control_activated | checklist_opened | object_grabbed | object_released | object_placed | status_reported | paused | resumed | recentered`
- `input`: `controller_ray | controller_grip | hand_pinch | hand_grab | simulator`
- `result`: `correct | incorrect | neutral`
- App-level events (`paused`, `resumed`, `recentered`) use `targetId: "system"`. Calibration uses `targetId: "calibration"`.

Fixtures: `fixtures/session-events.ok.json`, `.empty.json`, `.error.json` (invalid on purpose; must be rejected).

## 2. Mission definition: one file per level
Location `Assets/ASTRA/Content/Missions/<missionId>.json` · C# `Astra.Core.Mission.MissionDefinition` / `MissionStep`

```json
{
  "missionId": "l3-cabin-v1",
  "level": 3,
  "version": "1.0.0",
  "title": "string",
  "fictional": true,
  "steps": [
    { "stepId": "string", "order": 1, "instruction": "string (shown + subtitled)",
      "hint": "string (shown in Practice after a mistake)", "expectedEvent": "EventNames value",
      "targetId": "string", "critical": true }
  ]
}
```
Rules (enforced by the validator):
- `level` is 0–7 and unique across files; `missionId` starts with `l<level>-` and equals the file name.
- `order` is unique and ≥ 1; `stepId` is unique; no two steps have the same `expectedEvent` + `targetId`.
- At least one `critical` step. `fictional` must be `true` (PRD §10).
- Every `targetId` in a mission must exist on exactly one interactable in that level's scene (§8).

| Level | missionId | Scene | Owner |
|---|---|---|---|
| L0 Safety & Calibration | `l0-safety-v1` | `L0_Safety` | XR & Interaction |
| L1 Cadet Orientation | `l1-orientation-v1` | `L1_Orientation` | XR & Interaction |
| L2 Equipment Handling | `l2-equipment-v1` | `L2_Equipment` | Content & AI Coach |
| L3 Cabin Familiarization | `l3-cabin-v1` | `L3_Cabin` | Content & AI Coach |
| L4 Emergency Response | `l4-emergency-v1` | `L4_Emergency` | Core Logic |
| L5 EVA & Tethering | `l5-eva-v1` | `L5_EVA` | Integration Lead |
| L6 Robotics Arm | `l6-robotics-v1` | `L6_Robotics` | Integration Lead |
| L7 Integrated Mission | `l7-restore-power-v1` | `L7_Finale` | Core Logic |

## 3. Interaction events: XR → LevelController
C# `Astra.XR.Interactables.InteractionEvents` (static bus) and `InteractionReport { EventName, TargetId, Input }`.

- Every interactable and menu calls `InteractionEvents.Raise(eventName, targetId, input)` using `EventNames` / `InputModality` constants.
- Only the LevelController (§8) turns reports into SessionEvents. Interactables never score anything.
- Subscribers unsubscribe in `OnDisable`.

| Semantic action | Raised event | When |
|---|---|---|
| Select | `target_selected` | Ray/poke/pinch selects a selectable target |
| Activate | `control_activated` | Button pressed, toggle flipped, dial reaches its detent, breaker pushed |
| Grab | `object_grabbed` | Grab starts |
| Release | `object_placed` if released into a matching socket, else `object_released` | Grab ends |
| Recenter | `recentered` (`targetId: "system"`) | Recenter completes |
| (gaze/point dwell ≥ 1.0 s) | `target_located` | Optional "find it" steps |
| (UI) | `checklist_opened`, `status_reported`, `calibration_completed`, `paused`, `resumed` | Matching UI action |

`targetId` for grabbing: `object_grabbed` / `object_released` use the **object's** id (e.g. `multimeter_tool`); `object_placed` uses the **socket's** id (e.g. `tool_bay_slot_1`). A socket accepts only its matching object, so a wrong object dropped near a socket produces `object_released`, not `object_placed`.

`input` = the modality that performed the action: ray → `controller_ray`, controller grip → `controller_grip`, hand pinch/poke → `hand_pinch`, hand grab → `hand_grab`, XR Device Simulator → `simulator`.

## 4. Mission runner: live step tracking
C# `Astra.Core.Mission.IMissionRunner` (implemented by Core Logic as `MissionRunner`).

```csharp
StepFeedback Report(string eventName, string targetId, string input, long timestampMs);
MissionStep CurrentStep { get; }   // null when complete
bool IsComplete { get; }
IReadOnlyList<SessionEvent> Events { get; }   // with result stamped
```
`Report` throws `ArgumentException` if `eventName`/`input` are not contract values, `targetId` is empty, or `timestampMs` is negative or earlier than the previous event.

**Step-matching rule.** The runner (§4) and the scorer (§5) use this exact rule, so live feedback and the final score always agree. Steps are sorted by `order`; `p` = index of the first pending step. For each event:
1. Find the first step `k ≥ p` with the same `expectedEvent` **and** `targetId`.
   - `k == p` → **Correct**: step done, `p = k + 1`.
   - `k > p` and every step from `p` to `k-1` is non-critical → **Correct**: those steps are *skipped*, step `k` done, `p = k + 1`.
   - `k > p` and some step from `p` to `k-1` is critical → **OutOfOrder**: record a sequence error for step `k`; no advance.
2. No such `k`, but the event matches an already-completed step (`index < p`) → **Ignored** (repeat).
3. No match, and `event` is one of `target_located, object_grabbed, object_released, paused, resumed, recentered` → **Ignored**.
4. Anything else → **Incorrect**: record `targetId` as an incorrect action.

Stamped `result`: Correct → `correct`; Incorrect and OutOfOrder → `incorrect`; Ignored → `neutral`.
`StepFeedback.Hint` = hint of the step that is pending *after* processing (null when complete).

## 5. Scoring: deterministic
C# `Astra.Core.Assessment.IMissionScorer.Score(MissionDefinition mission, string sessionId, IReadOnlyList<SessionEvent> trace) → SessionResult`

```json
{
  "sessionId": "string", "missionId": "string", "passed": true,
  "completedSteps": ["stepId in completion order"],
  "skippedSteps": ["non-critical stepIds never done, in step order"],
  "omittedCriticalSteps": ["critical stepIds never done, in step order"],
  "sequenceErrors": ["stepId per OutOfOrder event, in event order"],
  "incorrectActions": ["targetId per Incorrect event, in event order"],
  "completionTimeMs": "timestampMs of the last event, 0 if no events"
}
```
- Validates every event first (§1 rules + `missionId` matches + non-decreasing time). Throws `ArgumentException` on the first invalid event.
- Recomputes outcomes with the §4 rule; it never trusts the stored `result` field.
- `skippedSteps` = steps skipped during matching, then any remaining non-critical steps. `omittedCriticalSteps` = remaining critical steps.
- `passed` = `omittedCriticalSteps` is empty. Speed and mistakes never override a missed critical step, and mistakes alone do not fail an attempt (they appear in the debrief).
- Pure function: no clock, randomness, I/O or Unity APIs. Same input → identical output (NFR-QUAL-01). AI output never enters (FR-AI-04).
- Only the **Qualification** attempt is scored. Practice uses a runner for hints only.

Oracle fixtures (all verified by `scripts/validate_content.py` against a reference implementation of this section):
`fixtures/session-result.ok.json`, `fixtures/session-result.empty.json`, `fixtures/scoring-cases.json` (4 cases: skipped optional steps, out of order then fixed, critical step omitted, repeats/neutral/wrong).

## 6. Progress: local save
C# `Astra.Core.Progress.ProgressState`, `LevelRecord`, `IProgressStore` (Core Logic implements `FileProgressStore` + `ProgressRules`).
- Constructor: `FileProgressStore(string directory, Func<ProgressState, string> toJson, Func<string, ProgressState> fromJson)`. Callers pass `JsonUtility.ToJson` / `JsonUtility.FromJson<ProgressState>`, so Core never references Unity.
- File: `<Application.persistentDataPath>/progress.json`. The directory is passed in by the caller (LevelController, Hub).
- `Load()` never throws: missing → fresh state (only level 0 unlocked); corrupt → rename to `progress.json.bak`, return fresh state.
- `Save()` writes `progress.json.tmp`, deletes `progress.json` if present, then moves the tmp file into place. `Load()` uses `progress.json.tmp` if `progress.json` is missing (no lost progress on a crash, AT-06).
- `ProgressRules.RecordAttempt(state, level, missionId, result, availableLevels)`: increments `attempts`; if `result.passed`, sets `passed`, updates `bestTimeMs` (lowest passing time) and unlocks the next level number in `availableLevels` (levels whose scene is in Build Settings).

## 7. UI components (XR & Interaction provides; others call)
Namespace `Astra.UI`, folder `Assets/ASTRA/UI`. Signatures are fixed; stubs already exist.

| Component | Methods |
|---|---|
| `InstructionPanel` | `ShowInstruction(string text)`, `ShowHint(string text)`, `ClearHint()` |
| `AlarmIndicator` | `SetAlarm(bool on, string caption)`: flashing light + caption, works with sound off |
| `PhaseBanner` | `Show(ModulePhase phase)` |
| `DebriefPanel` | `Show(SessionResult result, MissionDefinition mission)`, `ShowCoachText(string text, bool isAi)`: AI text labelled "AI coach (advisory)"; UnityEvents `onRetry`, `onBackToHub` |
| `PauseMenu` | UnityEvents `onPause, onResume, onRecenter, onRestart, onExit, onFinish` (`onFinish` = "Finish attempt" button, shown only in Qualification); also raises `paused`/`resumed`/`recentered` on the bus |
| Hub (scene `Hub`) | Lists L0–L7; locked/unlocked from `ProgressState`; "Coming soon" if the scene is not in Build Settings; loads scenes by the names in §2 |

## 8. Level scenes and the LevelController
- `Assets/ASTRA/Levels/_Template/LevelTemplate.unity` (Integration Lead) is the starting point: copy it into your level folder and rename it to your scene name.
- Folder `Assets/ASTRA/Levels/L<n>_<Name>/` holds the level's scene (named as in §2), level-only scripts and prefabs. **Only the level owner edits it.**
- Each level scene contains: `AstraXRRig` prefab (XR & Interaction; until it exists use the XR Interaction Toolkit starter XR Origin), one `LevelController` with the mission JSON assigned, the UI prefabs (§7), and the interactables.
- Comfort: seated/stationary. All targets within 0.7 m horizontally of the rig origin, 0.7–1.5 m above the floor, nothing behind the cadet. Minimum interactable size 6 cm. No artificial locomotion (NFR-UX-01).

`Astra.App.LevelController` (Integration Lead) public surface:
```csharp
event Action<ModulePhase> PhaseChanged;
event Action<SessionEvent, StepFeedback> EventRecorded;
event Action<SessionResult> QualificationScored;
MissionDefinition Mission { get; }
ModulePhase Phase { get; }
```
Behaviour:
1. Loads the mission (`JsonUtility`), new `sessionId` (GUID) per attempt.
2. Phases follow `ModulePhaseMachine`, and each phase change calls `PhaseBanner.Show`:
   - **Briefing**: title + first instruction for `briefingSeconds` (serialized, default 6).
   - **Demonstration**: each step's instruction for `demoSecondsPerStep` (default 4).
   - **Practice**: fresh runner; on Incorrect/OutOfOrder → `ShowHint(feedback.Hint)`; on Correct → `ClearHint()` + next instruction; when complete → Qualification after 3 s.
   - **Qualification**: fresh runner, new `sessionId`, no hints; ends when `IsComplete` or `PauseMenu.onFinish`.
   - **Debrief**: score → `QualificationScored` → `DebriefPanel.Show` → coach (§9) → progress save (§6).
3. Wiring: `DebriefPanel.onRetry` → Practice; `onBackToHub` / `PauseMenu.onExit` → load `Hub`; `PauseMenu.onRestart` → back to Briefing; `onPause` / `onResume` pause/resume the attempt clock.
4. Interactions are forwarded to the runner only in Practice and Qualification; `timestampMs` = ms since that attempt started (unscaled time, excluding paused time).
5. Writes the Qualification trace to `<persistentDataPath>/SessionLogs/<sessionId>.json` (local only, git-ignored).
6. Level scripts may subscribe to the events above (e.g. the power-panel lights) but never call the runner themselves.

## 9. Coach: advisory only (Content & AI Coach)
Unity side, namespace `Astra.Integrations.Coach`, assembly `ASTRA.Coach` (`Assets/ASTRA/Integrations/Coach`): `ICoachClient.RequestDebrief(CoachRequest, MissionDefinition, Action<CoachResponse>)`.
- `OfflineCoach`: deterministic, no network. Builds advice from `SessionResult` + step `hint`s (FR-AI-01).
- `HttpCoachClient`: calls the proxy with an 8 s timeout. On any error, timeout or invalid response → falls back to `OfflineCoach`.
- `onDone` is called exactly once and never with null. The client never throws.
- `CoachProvider` (MonoBehaviour, stub exists) exposes `ICoachClient Client`. The LevelController has a serialized `CoachProvider` field; if the field or `Client` is null, it skips coach text. In Debrief it calls `DebriefPanel.ShowCoachText(response.summary + hints, isAi: response.modelVersion does not start with "offline")`. `OfflineCoach` sets `modelVersion = "offline-1"`.

Proxy API: `POST /api/v1/coach/debrief` · max body 256 KB · max 500 events · retry safe.
Request:
```json
{ "sessionId": "string", "missionId": "string", "result": "SessionResult (§5)", "events": ["SessionEvent (§1)"] }
```
Response 200: `fixtures/coach-debrief.ok.json`. `advisory` is always `true`; `summary` ≤ 600 chars; at most 3 `hints`, each ≤ 200 chars.
Empty (valid, not an error): `summary: null`, `hints: []`, `reason: "NO_EVENTS"`, as in `fixtures/coach-debrief.empty.json`.
`USE_MOCK_AI=true` → the proxy returns the ok/empty fixture shape without calling Gemini.
The proxy holds `GEMINI_API_KEY`; the Unity app never does (NFR-SEC-02). Coach output never changes scoring or simulation (FR-SIM-04, FR-AI-04).

## 10. Error shape (every HTTP endpoint)
```json
{ "error": { "code": "UPSTREAM_UNAVAILABLE", "message": "Readable message", "details": [] } }
```
Codes: `INVALID_INPUT 400 | NOT_FOUND 404 | PAYLOAD_TOO_LARGE 413 | RATE_LIMITED 429 | UPSTREAM_UNAVAILABLE 503 | INTERNAL 500`. (`INVALID_INPUT` is also used with 405 for a wrong HTTP method.) Fixture: `fixtures/coach-debrief.error.json`.

## 11. Changing this contract
Open an issue `CONTRACT CHANGE REQUEST` with: section, old → new, why, who is affected. Keep coding against the current version until it's approved.
Adding an optional field is a minor bump (v1.2). Renaming, removing, or changing a field's meaning or type is a major bump (v2.0): every consumer is updated in the same merge.

## Changelog
- **v1.2** (2026-11-14): added error code `NOT_FOUND 404` (§10). Minor, additive.
- **v1.1** (2026-11-14): 8 levels with mission IDs/owners; `level` + step `hint`; interaction bus (§3); runner + step-matching rule (§4); scoring rule + `skippedSteps` + oracle fixtures (§5); progress (§6); UI API (§7); level scene + LevelController (§8); coach client + `modelVersion` (§9). Finale renamed `l7-restore-power-v1`.
- **v1.0** (2026-11-14): initial.

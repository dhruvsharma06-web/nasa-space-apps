# P1 XR interaction foundations

These components implement contract event behavior without taking over mission timing, scoring or progress. Device bindings, the shared rig, prefabs and sandbox scene still require integration with the team's canonical Unity/XRI project. Do not treat these source components as completed device support.

## Input and controls

Put `InputModalityTag` on every acting interactor or its parent. Set the actual controller ray/grip or hand pinch/grab modality using `InputModality` constants; set its simulator override when the simulator drives that route. `TryResolve` rejects missing tags and inactive/disabled actors. There is no guessed fallback modality.

| Component | Adapter entry point | Behavior |
|---|---|---|
| `ButtonTarget` | `TryActivate(actingInteractor)` | One configured control event with a 0.3 s unscaled cooldown. |
| `ToggleTarget` | `TryActivate(actingInteractor)` | Flips state once for an accepted action; cooldown prevents a second flip. |
| `DialTarget` | `SetAngle(localDegrees, actingInteractor)` | Snaps to configured detents and reports entry to the trigger. A one-shot request during cooldown remains pending until eligible; moving away cancels it. Direct activation cannot invent a trigger angle or steal another actor's request. |
| `LeverTarget` | `SetPull(normalizedPull, actingInteractor)` | Reports the rising activation threshold; must fall below its reset threshold to rearm. |
| `LocatableTarget` | `BeginDwell(actor)` / `EndDwell(actor)` | Requires 1 s continuous dwell; reports once per attempt. The adapter must end dwell on hover exit. |

Configure IDs/events on the inactive object before enabling it. Scene/prefab authoring normally sets serialized fields. Programmatic construction in the Editor can invoke `OnValidate` immediately when a component is added, before `Configure` runs; tests explicitly assert those expected validation messages. `Awake` disables misconfigured components with a contextual error.

Control events are limited to the five `ControlTarget.IsControlEvent` values. Emission uses the existing `InteractionEvents` bus. Optional feedback renderer colors and hover/pressed UnityEvents update synchronously; prefab composition must provide accessible labels and visible feedback, not colour alone. Renderer material properties are applied without instantiating a material per action.

Call `ResetForAttempt()` from composition at each Practice/Qualification reset. It restores control state, cooldown and dwell latches without producing mission events. Shared XR cannot reference App to discover phases; App/level composition supplies those connections. Reset and activation notifications are guarded against reentrant mutation during synchronous callbacks.

## Tools and sockets

- `GrabbableTool.BeginGrab(actor)` records one `object_grabbed` using the tool ID. A second holder is rejected.
- `EndGrab(actor, optionalSocket)` commits one terminal outcome: `object_placed` with a matching available socket ID, otherwise `object_released` with the tool ID. A second release is rejected.
- `PlacementSocket.TryPlace(tool, actor)` returns true only for a matching successful placement. A false result can still mean a rejected drop was handled as `object_released`; do not publish an additional fallback release.
- A socket has one occupant, accepts only its configured tool ID, and releases its references when disabled. Grabbing an occupied tool detaches it cleanly.
- An out-of-reach unheld tool returns home after 3 unscaled seconds without an extra learner action. Assign `reachOrigin` to the rig's reach anchor in the prefab; until assigned, distance is measured from the tool's captured home pose. Home position follows its original parent if the rig moves.
- Interrupted tracking/actor loss cancels a held grab once using its last validated input route. Adapters may call `CancelGrab()` explicitly; disabling/destroying an actor is also detected on Update. Attempt reset and component teardown clear state without inventing learner actions.

For L1, grab → release → grab → place is the physical sequence. The runner treats the second grab as a neutral repeat; do not add duplicate event/target pairs to the mission.

`ConsoleInteractionLogger` subscribes only while enabled and prints each report. Use it until the real LevelController is integrated; do not turn it into a scorer.

## Phone gaze rig (`Phone/`), the device route for the Irusu phone headset

See [Phone/README.md](Phone/README.md). It covers `IGazeTarget`, `GazeInteractor` (tap, dwell, long-press recenter), the always-on-top reticle, the gyro magic-window fallback, the Cardboard proposal and how the factory adds the rig in one call.

## Rig and input routes (`Rig/`, `Prefabs/AstraXRRig.prefab`): Vision Aura / controller route

`AstraXRRig` is the seated rig: origin on the floor, `TrackingSpace` at 1.2 m seated eye height, `Head` camera. Every route feeds the shared `RayInteractor`, which calls the interactable adapter entry points above with the actor that actually acted. No route scores, timestamps or changes mission state.

| Route | Component | Actions and reported modality |
|---|---|---|
| Headset hand aim | `HandAimInput` | Uses `UnityEngine.XR.InputDevices` (Vision Aura “Vision Aura Hand Aim”, else any tracked controller). Pinch/trigger selects/activates and clicks world UI (`hand_pinch`, or `controller_ray` for a controller); fist/grip grabs (`hand_grab` / `controller_grip`); menu opens pause. Tracking loss cancels a grab once. |
| Desktop window | `DesktopMouseInput` | Active only while no headset is tracking; reports `simulator`. Click selects/activates; hold on a tool to grab, release to drop/place; scroll changes held distance; right-drag turns the seated view; ESC/P pauses. UI uses the scene EventSystem. |
| Head | `HeadPoseDriver` | Applies the tracked center-eye pose locally under `TrackingSpace`; restores the desktop view when tracking stops. |
| Recenter | `RigRecenterService` | The single `recentered`/`system` publisher: `XRInputSubsystem.TryRecenter()` with a headset, desktop view reset otherwise. |

`UiActionActor` lets a tracked pointer that invokes a parameterless UI binding (PauseMenu buttons) report its own modality. `PassthroughHidden` marks the virtual room hidden in passthrough AR.

`VisionAura/` (assembly `ASTRA.XR.VisionAura`) compiles **only when the `com.visionaura.xr` package is installed** (`ASTRA_VISION_AURA` version define). It starts the Mac-hosted iPhone headset session at launch, reads AR/VR mode from the build manifest, and shows a small status box in the desktop window. The canonical project without that package is unaffected.

## Verification and remaining work

The source tests live in `Tests/PlayMode/` under a separate test assembly. The isolated validation run uses Unity 6000.6.5f1 on macOS with Unity Test Framework 1.8.0; see `.agents/evidence/p1-implementation.md` at the repository root for results and limits. This version is a validation environment, not a project pin.

Still required: real controller/XRI adapters, rig and hand capability integration, correctly sized/labeled prefabs, same-frame rendered feedback checks, the XR sandbox, UI/pause/Hub, canonical-project tests, Windows build verification and headset testing. Maintain controllers as the baseline until the actual device route is selected and validated.

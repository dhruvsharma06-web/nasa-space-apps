# L4 Emergency Response: scene build spec

**Owner:** Core Logic · **Mission:** [`l4-emergency-v1`](../../Content/Missions/l4-emergency-v1.json) · **Scene:** `L4_Emergency.unity` (in this folder)

The trainee clears a fictional power-bus fault on the **POWER panel**, with hints in Practice. This page lists everything the scene needs, so it can be built in one pass once the team Unity project, the level template and P1's adapters are in place.

## Scripts in this folder
| Script | What it does |
|---|---|
| `PowerPanelView.cs` | Draws the panel from `Core/Simulation/PowerPanelModel`: the state lights and labels, the STATUS text and the caution alarm. Shared with L7. |
| `LevelAttemptReset.cs` | Calls `ResetForAttempt()` on the panel's controls at Briefing, Practice and Qualification, so the dial and lever start in place every attempt. Shared with L7. |

## Layout (seated; CONTRACT.md §8)
Coordinates are metres from the XR rig origin: x is right, y is up from the floor, z is forward. Every target is within 0.7 m horizontally and 0.7–1.5 m high, nothing is behind the trainee, and every interactable is at least 6 cm. Each position matches what the hints say.

| Object (label in CAPS) | P1 component | `targetId` | Event | Position (x, y, z) | Hint that refers to it |
|---|---|---|---|---|---|
| POWER panel (the board itself) | `LocatableTarget` | `power_panel` | `target_located` (1 s dwell) | (0, 1.15, 0.55), about 0.6 × 0.45 m | "straight ahead at chest height" |
| CAUTION light + caption | UI `AlarmIndicator` prefab | (none) | (none) | (−0.20, 1.34, 0.52) | "the flashing CAUTION light" |
| ACK button | `ButtonTarget` | `caution_ack_button` | `control_activated` | (−0.20, 1.22, 0.52) | "ACK under the flashing CAUTION light" |
| CHECKLIST tab (display above the panel) | `ButtonTarget` with eventName `checklist_opened` | `power_fault_checklist` | `checklist_opened` | (0.20, 1.42, 0.55) | "CHECKLIST tab on the display above" |
| BUS A display (wrong choice) | `ButtonTarget` with eventName `target_selected` | `power_bus_a_display` | `target_selected` | (−0.12, 1.08, 0.52) | (not in the mission; a wrong pick appears in the debrief) |
| BUS B display + FAULT light | `ButtonTarget` with eventName `target_selected` | `power_bus_b_display` | `target_selected` | (0.12, 1.08, 0.52) | "the one whose FAULT light is lit" |
| BUS B dial (NORMAL / ISOLATE) | `DialTarget`: detents `{0, 90}`, `triggerDetent = 1` | `power_bus_b_dial` | `control_activated` on reaching ISOLATE | (0.12, 0.97, 0.52) | "BUS B dial below the bus display" |
| CROSS-TIE lever (OPEN / CLOSED) | `LeverTarget`: activation 0.8, reset 0.2 | `crosstie_lever` | `control_activated` when pulled down | (0.30, 1.00, 0.50) | "on the right of the POWER panel down to CLOSED" |
| STATUS display (text) | UI `Text` fed by `PowerPanelView.onStatusText` | (none) | (none) | (0, 0.90, 0.52) | "STATUS display reads RESTORED" |
| REPORT button | `ButtonTarget` with eventName `status_reported` | `power_report_button` | `status_reported` | (0, 0.82, 0.52) | "REPORT is the button below it" |

Every mission `targetId` must exist on **exactly one** object (CONTRACT.md §2). `power_bus_a_display` is the only extra one. Label text must match the instructions exactly (style guide §5), and every state light needs a text label next to it, never colour alone.

## Wiring
1. **Start from the template:** copy `Levels/_Template/LevelTemplate.unity` (Integration Lead) to `L4_Emergency.unity` in this folder.
2. **LevelController:** assign `missionJson = l4-emergency-v1`. Keep the template's UI prefabs (InstructionPanel, PhaseBanner, DebriefPanel, PauseMenu) and the `CoachProvider`.
3. **PowerPanelView** (on the panel root):
   - `Level Controller` = the scene's LevelController; `Alarm` = the AlarmIndicator above.
   - `Isolate Target Id = power_bus_b_dial`, `Cross Tie Target Id = crosstie_lever`, `Ack Target Id = caution_ack_button`. The defaults are L7's, so change all three.
   - `Show When Faulted`: the BUS B FAULT light + "FAULT" label.
   - `Show When Isolated`: the BUS B "ISOLATED" label.
   - `Show When Restored`: the STATUS "RESTORED" light.
   - `Show When Nominal`: the "NORMAL" labels.
   - `On Status Text`: connect to the STATUS display's `Text.text`.
   - The caution caption is `CAUTION — POWER: BUS B FAULT` (style guide §4).
4. **LevelAttemptReset** (on the panel root): `Level Controller` = the scene's LevelController; leave `Include Children` on, so every control under the panel is reset.
5. **Interactors:** each one in the rig needs P1's `InputModalityTag`, and each control needs P1's XRI adapter, which calls `TryActivate`, `SetAngle` or `SetPull`.
6. **Build Settings:** add the scene as `L4_Emergency` (Integration Lead).

## Done when
- **In the Editor (XR Device Simulator):** Briefing → Demonstration → Practice (hints appear after mistakes) → Qualification → Debrief.
- **On the headset (Rayyan), two runs:**

  | Run | What the trainee does | Debrief must show |
  |---|---|---|
  | Clean | All 7 steps in order | **PASS**, no mistakes, STATUS `RESTORED` |
  | Wrong bus | Select BUS A first, then do everything correctly | **PASS**, `incorrectActions: power_bus_a_display` |
  | (optional) Lever too early | Pull CROSS-TIE before isolating BUS B | **PASS**, `sequenceErrors: close_crosstie`; the panel stays FAULT until the dial is turned |

`Tests/EditMode/L4MissionTests.cs` already plays these runs through the real logic, so the headset debrief must match them.

# L0 Safety & Calibration: level spec

**Owner:** P1 XR & Interaction · **Mission:** [`l0-safety-v1`](../../Content/Missions/l0-safety-v1.json) · **Layout:** [`l0-layout.json`](../../Content/Layouts/l0-layout.json) · **Scene:** `L0_Safety.unity`, generated here by the level factory (**ASTRA → Build all level scenes from layouts**) · **View:** `L0SafetyView.cs` (attached automatically)

L0 teaches the controls in under 2 minutes. Look at a panel, tap a button, flip a switch, turn a dial, then pick up a tool and place it. Every hint teaches the mechanic, not only the location.

## Steps
| # | Instruction | Target (`targetId`) | Event | Mechanic taught |
|---|---|---|---|---|
| 1 | Look at the SAFETY placard. | `safety_placard_panel` (panel, `target: true`) | `target_located` | Gaze dwell until the ring fills (1 s). Not critical. |
| 2 | Tap READY. | `ready_button` | `control_activated` | Ring appears, then tap |
| 3 | Flip the TEST SWITCH. | `test_switch_toggle` | `control_activated` | One tap = one flip (label shows ON/OFF) |
| 4 | Turn TEST DIAL to 2. | `test_dial` (positions 1, 2, 3) | `control_activated` | One tap = one dial mark |
| 5 | Pick up the CHECK TOOL. | `check_tool` | `object_grabbed` | Tap a tool to hold it |
| 6 | Place the CHECK TOOL in the TOOL BAY. | `tool_bay_slot` (socket, accepts `check_tool`) | `object_placed` | Tap the matching bay while holding |

Decoy: **ALARM TEST** (`alarm_test_button`, below READY) teaches that the label matters. Tapping it is recorded as a wrong control.

## Layout (seat eye at (0, 1.2, 0), metres; the validator checks reach)
| Object | Position | Size | Direction |
|---|---|---|---|
| SAFETY placard panel (READY, TEST SWITCH, TEST DIAL, ALARM TEST, STATUS light) | (0.00, 1.02, 0.60) | 0.48 × 0.32 | straight ahead |
| TOOL STOWAGE panel (TOOL BAY socket) | (0.45, 1.02, 0.45) | 0.28 × 0.24 | 45° right |
| CHECK TOOL | (−0.42, 1.02, 0.42) | factory tool | 45° left, below the instruction column |

The panel zone (z 0.45–0.62, y 0.95–1.45) holds only these panels. The factory's left UI column (x ≈ −0.56, y 1.27–1.6) and the debrief area are clear.

## View script
`L0SafetyView` turns the **STATUS** light (`ready_light`) green after READY. It also turns **dwell-select on for L0 only**: looking at a control for 1.5 s selects it, so first-time cadets can finish without the tap. The Hub setting applies everywhere else.

## Done when
- `python3 scripts/validate_content.py` prints **OK** (checked).
- The factory builds `L0_Safety.unity` (Integration Lead run).
- `FirstTimeUserPlaythroughTests` passes with L0 included.
- Screenshots (`AstraShotCapture`) show no overlap.

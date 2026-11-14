# L1 Cadet Orientation: level spec

**Owner:** P1 XR & Interaction · **Mission:** [`l1-orientation-v1`](../../Content/Missions/l1-orientation-v1.json) · **Layout:** [`l1-layout.json`](../../Content/Layouts/l1-layout.json) · **Scene:** `L1_Orientation.unity`, generated here by the level factory · **View:** `L1OrientationView.cs` (attached automatically)

L1 teaches the cabin layout. The cadet turns their head to find four panels spread left, front and right, then opens the CHECKLIST.

## Steps
| # | Instruction | Target | Event |
|---|---|---|---|
| 1 | Find the COMMS panel. | `comms_panel` (far left) | `target_located` |
| 2 | Find the POWER panel. | `power_panel` (ahead, slightly left) | `target_located` |
| 3 | Find the LIFE SUPPORT panel. | `life_support_panel` (ahead, slightly right) | `target_located` |
| 4 | Find the STOWAGE panel. | `stowage_panel` (far right) | `target_located` |
| 5 | Open the CHECKLIST tab. | `orientation_checklist` (on CREW DISPLAY, above) | `checklist_opened` |

All steps are critical. Locating means a 1 s gaze dwell (CONTRACT §3), and the ring fills while you look. The order is a natural left-to-right sweep. Looking at a later panel for a full second before an earlier one is recorded as an out-of-order step.

Decoy: **VOLUME** dial on COMMS (`comms_volume_dial`). Turning it is recorded as a wrong control.

## Layout (seat eye at (0, 1.2, 0), metres)
| Panel | Position | Yaw from forward | Distance |
|---|---|---|---|
| COMMS | (−0.62, 1.00, 0.12) | −79° | 0.63 m |
| POWER | (−0.17, 1.02, 0.60) | −16° | 0.62 m |
| LIFE SUPPORT | (0.27, 1.02, 0.58) | +25° | 0.64 m |
| STOWAGE | (0.62, 1.00, 0.12) | +79° | 0.63 m |
| CREW DISPLAY (CHECKLIST tab) | (0.05, 1.42, 0.60) | +5° | 0.60 m |

Every target is within ±100° of forward and ≤ 0.7 m away. COMMS and STOWAGE sit below and outside the factory's left UI column (yaw ≈ −45°, y 1.27–1.6).

## View script
`L1OrientationView` lights each panel's **FOUND** light green as it is located, and resets the lights at each attempt.

## Done when
- The validator prints **OK** (checked).
- The factory builds `L1_Orientation.unity`.
- `FirstTimeUserPlaythroughTests` passes with L1 included.
- Screenshots show no overlap.

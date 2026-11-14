# STATUS

_Updated 2026-11-14 (night). The team lead updates this file at every checkpoint._

- **main:** `042ff30`. Unity 6.3 LTS project, level factory, LevelController, browser guide, L2/L3/L5/L6 content, coach proxy.
- **Unity:** 6000.3.28f1 (6.3 LTS). URP 17.3.0, Input System 1.20.1, XRI 3.3.2, XR Management 4.6.1, Test Framework 1.6.0.
- **Verified in the Unity editor (batch mode):** EditMode 231/231, PlayMode 55/55 (incl. `FirstTimeUserPlaythroughTests`: every level completable by aiming and clicking from the seat).
- **Verified in the browser (WebGL, desktop Chrome):** L2, L3, L5, L6 playable Briefing → Debrief with the guide.
- **Content:** `scripts/validate_content.py` OK; level-sim scoring tests 29/29.
- **Not verified on a phone yet.** No Android build exists yet.
- **Missing:** phone gaze rig + APK, Hub scene, L0/L1 content, L4/L7 layouts, progress wiring, coach inside Unity, Next level flow, score 0–100 + rating, instructor dashboard, public web link, CI.
- **Sprint plan:** checkpoints C1 03:00, C2 10:00, C3 14:00, freeze 17:00 (see `docs/TASKS.md`).

## Device test matrix
| Device | OS | Build | FPS | Input | Comfort notes | Tested by |
|---|---|---|---|---|---|---|
| Desktop Chrome (Windows 11) | — | WebGL 2026-11-14 | not measured | mouse + click | — | team lead |
| Irusu Ultra + Android phone | _TBD_ | _not built yet_ | | gaze + tap | | |

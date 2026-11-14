# STATUS

_Updated 2026-11-14. The Integration lead updates this file at every sync._

- **Unity project:** created 2026-11-14 on Unity **6000.6.9f1** (Unity 6.6, Supported; chosen for testing, LTS 6.3 to be decided before the demo). Everyone must install exactly this version.
- **Packages pinned:** URP 17.6.0, Input System 1.20.1, XR Interaction Toolkit 3.6.1, XR Plug-in Management 4.7.0, Test Framework 1.8.0 (see `Packages/manifest.json`). Phone VR (Irusu Ultra) plugin: Google Cardboard, pending the Android module.
- **main:** repository skeleton + contract stubs
- **Last sync passed:** none
- **Milestone:** M0 (platform spike), not started
- **Works for real:** content validator with reference scoring oracle; contract fixtures + 4 scoring cases
- **Still mocked:** everything in Unity (stubs only; C# not yet compiled, no Unity project, no headset build)
- **Verified in Unity:** zero compile errors/warnings in Assets/ASTRA; EditMode tests 3/3 passed (batch mode)
- **Known limits:** Unity EditMode tests are not in CI yet (needs a Unity license secret)
- **Next:** each person starts Stage 0 of their prompt in `docs/prompts/`; Integration Lead creates the Unity project (days 1–2)

## Device test matrix
| Headset | OS / runtime | Unity | OpenXR plugin | XRI | Hands | Controllers | FPS | Notes |
|---|---|---|---|---|---|---|---|---|
| _TBD_ | | | | | | | | |

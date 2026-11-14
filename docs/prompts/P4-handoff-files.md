# Integration Lead (P4): files, work done and work to do

**Date:** 2026-11-14 · **Branch:** `feat/content-ai-coach` (Content & AI Coach developer's machine)
**Status:** everything below is **local only, not committed or pushed yet**. It needs the team's approval first.
Read together with: `docs/prompts/P4-integration-lead.md` (Master Prompt) and `docs/prompts/P4-stage-update.md` (Stage Update).

---

## ✅ Done for P4: files and what they are

### Unity project setup (new; currently Unity 6000.6.9f1, moving to 6.3 LTS 6000.3.28f1)
| Path | What |
|---|---|
| `Packages/` (`manifest.json`, `packages-lock.json`) | Package list: URP, Input System, XR Interaction Toolkit, XR Plug-in Management, Test Framework, pinned |
| `ProjectSettings/` | All Unity project settings (from the URP template) |
| `Assets/Settings/` | URP render pipeline settings |
| `Assets/XRI/` | XR Interaction Toolkit settings |
| `Assets/InputSystem_Actions.inputactions` | Default input actions |
| `Assets/TextMesh Pro/` | TextMeshPro fonts and shaders (shared with P1's UI) |
| `*.meta` files across `Assets/` | Unity's IDs for every asset. **Must be committed once, by P4, before anyone else commits theirs** |

### Integration code (`Assets/ASTRA/App/`)
| Path | What |
|---|---|
| `Assets/ASTRA/App/LevelController.cs` *(edited; it was a stub)* | The full level flow: phases, timing, pause-aware clock, hints, scoring, debrief, coach call, session log. Switches to P2's real runner/scorer automatically (reflection) |
| `Assets/ASTRA/App/MockMission.cs` | Stand-in runner and scorer until P2's `vyom` is merged (clearly labelled) |
| `Assets/ASTRA/App/DesktopTest/` (`DesktopRig.cs`, `TestTarget.cs`, `DesktopHud.cs`, `ASTRA.DesktopTest.asmdef`) | Browser/desktop test tools: seated mouse-look, clickable test controls, test HUD labelled "NOT HEADSET EVIDENCE" |
| `Assets/ASTRA/App/Editor/` (`AstraSceneBuilder.cs`, `ASTRA.App.Editor.asmdef`) | Generates the L3 scene from code and makes the WebGL build (menu **ASTRA/…**; batch `-executeMethod Astra.App.EditorTools.AstraSceneBuilder.BuildWebGLBatch`) |

### Shared files P4 owns (edited)
| Path | Change |
|---|---|
| `CONTRACT.md` | v1.2 → **v1.3** (coach real-AI rules, Ask endpoint, input checks, rate limit) |
| `scripts/validate_content.py` | Checks the new Ask fixtures |
| `fixtures/coach-ask.ok.json`, `fixtures/coach-ask.error.json` | New test data |
| `.gitignore` | Added `*.slnx` |
| `docs/STATUS.md` | Unity version, pinned packages, test results |
| `docs/prompts/P4-stage-update.md` | Stage Update for P4's AI |

### Already on `main` (nothing to do)
`Assets/ASTRA/App/ASTRA.App.asmdef`, `Assets/ASTRA/UI/ASTRA.UI.asmdef`, `Assets/ASTRA/Integrations/Coach/ASTRA.Coach.asmdef` and the contract stubs (committed in "all prompts and docs").

### Content & AI Coach files made on P4's behalf (L3 owner: Content & AI Coach)
`Assets/ASTRA/Levels/L3_Cabin/`: `L3_Cabin.unity`, `L3CabinView.cs`, `L3_Cabin_Volume.asset`, `Materials/`

### Build output (git-ignored, never committed)
`Builds/WebGL/`: browser test build, served with `python -m http.server 8095` from that folder → http://127.0.0.1:8095

### Verified so far
- 0 compile errors, 0 warnings in `Assets/ASTRA` (Unity batch mode)
- EditMode tests 3/3 passed in Unity
- First WebGL build ran in a real browser (L3 cabin, test HUD, stand-in runner)
- A rebuild with TextMeshPro labels and post-processing is in progress

---

## ⏳ Still to do for P4

| # | Task | Where |
|---|---|---|
| 1 | Install **Unity 6000.3.28f1** (+ Android Build Support with OpenJDK + SDK & NDK, + Web Build Support). Then **move the project to 6.3**: regenerate `Packages/` and `ProjectSettings/` from the 6.3 URP template, re-pin packages to the 6.3-bundled versions, keep every `.meta` ID, delete `Library/`, re-import, compile, run the tests | `Packages/`, `ProjectSettings/`, `docs/STATUS.md` |
| 2 | Review all the "done" files above (contract shapes, assembly boundaries, no `?.` on serialized Unity fields, no secrets), then agree the **first commit** with the team | — |
| 3 | Merge in order: **(1) project setup → (2) P2 `vyom` → (3) `feat/content-ai-coach` → (4) P1**. CI green each time; tag `sync-0` after (1)+(2) | GitHub |
| 4 | Protect `main` (pull requests + CI required); add collaborators (with consent); write the real calendar dates | GitHub, `docs/STATUS.md` |
| 5 | **Phone-VR test:** add Google's Cardboard XR Plugin (`googlevr/cardboard-xr-plugin`; check Unity 6.3 support first and pin the version), switch to Android, build L3 to a phone, test it in the Irusu Ultra, and record phone, Android version, FPS and comfort | `Packages/manifest.json`, `docs/STATUS.md` |
| 6 | After `vyom` merges: confirm the browser build shows **"Core runner"** (not "stand-in"), then run the **2D-vs-3D parity check** (the same clicks in `tools/level-sim` and the WebGL build give identical `SessionResult`s) | `Builds/WebGL`, `tools/level-sim/` |
| 7 | **Level template** scene for everyone to copy (rig, LevelController, UI slots, CoachProvider) | `Assets/ASTRA/Levels/_Template/LevelTemplate.unity` |
| 8 | **PlayMode smoke test** (Master Prompt S1.5) | `Assets/ASTRA/Tests/PlayMode/` |
| 9 | Unity tests in CI via GameCI (if a licence secret is available), otherwise "run locally before merge" | `.github/workflows/ci.yml` |
| 10 | Draft **PRD Rev E** (phone VR: Cardboard, 3DoF, gaze + tap) and **contract v1.4** (`gaze` input), get the team's OK, and update the fixtures and validator in the same PR | `docs/PRD.md`, `CONTRACT.md`, `fixtures/` |
| 11 | Rebuild the browser test build after every merge | `Builds/WebGL` |

---

## ⚠️ Warnings
- **`.meta` file clash risk:** P2's `vyom` branch has no `.meta` files, which is fine. If P1 or P2 commit `.meta` files generated on their own PCs for the same shared files, the IDs will clash. P4 commits the `.meta` files first (task 2).
- **One Unity project only:** nobody else should push their own `Packages/` or `ProjectSettings/`.
- **Secrets:** the Gemini key lives only in `services/coach-proxy/.env` (git-ignored) on the Content dev's machine. Never put it in chat, code or commits.

## Team status
- **P2 Core Logic:** pushed `vyom` (runner, step matcher, scorer, power simulation, progress save, EditMode tests). Only their folders; not yet merged or run in the shared project.
- **P1 XR & Interaction:** working; nothing pushed yet (`rayyan` is only a copy of `feat/content-ai-coach`).
- **Device:** Irusu Ultra, a phone-based VR box (Android phone, 3DoF, no controllers).

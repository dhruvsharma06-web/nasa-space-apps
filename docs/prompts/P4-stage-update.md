STAGE UPDATE for Integration Lead — 2026-11-14
Applies to: docs/prompts/P4-integration-lead.md (your Master Prompt). Paste this to your AI after the Master Prompt.
Contract: now v1.3 (see CONTRACT.md changelog). PRD: Rev D.1 (Rev E pending, see section D).

While you were away, the Content & AI Coach developer did part of your Stage 0–1 so the team was not blocked.
Do not redo these steps. Review them, take ownership, and continue from section C.

## A. Important: where this work is
All items in section B are on the Content & AI Coach developer's machine on branch `feat/content-ai-coach`.
They are **NOT committed or pushed yet** (the team agreed: no commit/push/PR without approval).
Your first job is to agree with them on when it is committed, and to review it before it reaches `main`.

## B. Already done for you (verify, don't redo)
| Your step | Status | Where / evidence |
|---|---|---|
| S0.2–S0.3 Create Unity project (URP), install packages | Done on Unity 6000.6.9f1, **moving to 6.3 LTS (6000.3.28f1)**: the team decided to standardise on 6.3 LTS | `Packages/manifest.json`, `ProjectSettings/`, `Assets/Settings/`, `Assets/XRI/`, `Assets/InputSystem_Actions.inputactions` |
| Packages pinned | URP, Input System, XR Interaction Toolkit, XR Plug-in Management, Test Framework, pinned to the versions bundled with the editor. **Re-pin for 6.3** (the 6.6 versions differ) | `docs/STATUS.md` |
| S0.4 Compile + EditMode tests | 0 compile errors, 0 warnings in `Assets/ASTRA`; EditMode tests 3/3 passed (batch mode) | batch log |
| S1.2 `MockMissionRunner` + mock scorer | Done (simplified rule, clearly labelled "stand-in") | `Assets/ASTRA/App/MockMission.cs` |
| S1.3 `LevelController` (CONTRACT §8) | Implemented: phases + timings, pause-aware clock, interaction forwarding, practice hints, qualification, debrief, coach call, session log. **Switches to Core's real `MissionRunner`/`MissionScorer` automatically** (reflection) when they exist. Additive read-only helpers: `Runner`, `LastResult`, `IsPaused`, `UsingMockRunner`, `AttemptTimeMs`, event `DemonstrationStep`; public commands `Skip/SkipToPractice/Finish/Retry/Restart/Pause/Resume/Recenter/BackToHub` | `Assets/ASTRA/App/LevelController.cs` |
| Desktop/WebGL test tools (not product UI) | Mouse-look seated rig, crosshair click, temporary `TestTarget` interactables, IMGUI test HUD labelled "NOT HEADSET EVIDENCE" | `Assets/ASTRA/App/DesktopTest/` (asmdef `ASTRA.DesktopTest`) |
| Scene generation + WebGL build | Editor script generates the L3 Cabin greybox from the mission hints and builds WebGL. Menu **ASTRA/…**; batch: `-buildTarget WebGL -executeMethod Astra.App.EditorTools.AstraSceneBuilder.BuildWebGLBatch` → `Builds/WebGL` (git-ignored). Uses TextMeshPro labels + URP post-processing | `Assets/ASTRA/App/Editor/` |
| Assemblies | `ASTRA.UI` and `ASTRA.App` asmdefs added (Core ← XR ← UI ← App; Coach → Core) so PlayMode tests can reference App | CONTRACT §0 |
| Repo hygiene | `*.slnx` git-ignored; `Builds/`, `Library/`, `UserSettings/`, `*.csproj` already ignored | `.gitignore` |
| Contract | v1.2 (`NOT_FOUND`), v1.3 (coach real-AI rules, Ask endpoint, input checks, rate limit) + fixtures + validator | `CONTRACT.md`, `fixtures/`, `scripts/validate_content.py` |
| Browser 2D simulator (content/scoring) | Reference JS scoring + offline coach, 29/29 tests, used to check content before Unity | `tools/level-sim/` |

Team status you need to know:
- **Core Logic (P2)** pushed branch `vyom`: MissionRunner, StepMatcher, MissionScorer, PowerBusSimulation, FileProgressStore, ProgressRules + EditMode tests. Only their folders. Not yet merged or run inside the shared project.
- **XR & Interaction (P1)** is still working; nothing pushed yet. Branch `rayyan` is just a copy of `feat/content-ai-coach`, with no new work.
- **Headset:** the demo device is an **Irusu Ultra, a phone-based VR box** (3DoF, no controllers). See section D.

## C. Your remaining steps (replace your S0–S1 with these)
**STAGE 0 — Setup & Contract (finish)**
- S0.A Install **Unity 6000.3.28f1** with Android Build Support (+ OpenJDK + SDK & NDK) and Web Build Support. Everyone uses exactly this version.
- S0.B With the Content & AI Coach dev: finish the **6.3 migration** of the project. Regenerate `Packages/` + `ProjectSettings/` from the 6.3 URP template, re-pin packages to the 6.3-bundled versions, keep `Assets/ASTRA` and all `.meta` GUIDs, delete `Library/`, re-import, then: 0 compile errors, EditMode tests green. Update `docs/STATUS.md`.
- S0.C Review the section-B work file by file (use the `astra-reviewer` checklist: contract shapes, assembly boundaries, no Unity `?.` on serialized fields, no secrets). Then agree the first commit with the team.
- S0.D Merge order (each step with approval, CI green): **(1)** Unity project setup → `main`; **(2)** P2 `vyom`; **(3)** `feat/content-ai-coach` (content, coach, simulators); **(4)** P1 when ready. Tag `sync-0` after (1)+(2) compile and test green on `main`.
- S0.E GitHub: protect `main` (PRs + CI required), add collaborators (with consent), write the real calendar dates into `docs/STATUS.md`.

**STAGE 1 — Build alone → Sync 1 (first combine)**
- S1.A **Phone VR spike (replaces the standalone-headset M0):** add Google's Cardboard XR Plugin for Unity (official repo `googlevr/cardboard-xr-plugin`; check that it supports Unity 6.3 before adding it, and pin the version), switch the platform to Android, build the L3 scene to an Android phone, view it in the Irusu Ultra. Record phone model, Android version, FPS, comfort and tracking notes in the `docs/STATUS.md` device matrix.
- S1.B After P2's `vyom` merges: confirm `LevelController.UsingMockRunner == false` in the WebGL build. The test HUD shows "Core runner". Then run the parity check: the same click sequence in the 2D simulator (`tools/level-sim`) and the WebGL build must give identical `SessionResult`s (ignoring ids/timings).
- S1.C Level template: generalise `AstraSceneBuilder` into `Levels/_Template/LevelTemplate.unity` (rig, LevelController, UI slots, CoachProvider) so P1/P2/P3 copy it, as your Master Prompt S1.4 says.
- S1.D PlayMode smoke test (Master Prompt S1.5), now using the generated L3 scene or the template.
- S1.E CI: add GameCI Unity tests if a licence secret is available; otherwise document "run locally before merge".
- S1.F Keep `Builds/WebGL` working as the team's **browser test build** (P1–P3 test there before the phone). Rebuild after each merge; serve with `python -m http.server 8095` from `Builds/WebGL`.
- Sync 1 check: a level plays Briefing → Debrief in the browser **with the real Core runner**, and the parity check passes.

**STAGE 2 onward:** unchanged from your Master Prompt, except: "headset" now means **Android phone in the Irusu Ultra** (section D), and the WebGL build stays as the shared browser test path.

## D. Phone-VR change you own (decide with the team, then document)
The Irusu Ultra is a phone VR box, so:
- **PRD Rev E:** replace the "standalone OpenXR headset" assumptions (G-04, G-05, §8.2, §8.4, FR-XR-03 hands, AT-09) with: Android phone + Cardboard XR plugin, 3DoF head tracking, gaze reticle + tap (screen tap or headset button / Bluetooth remote).
- **CONTRACT v1.4 (minor, additive):** add input modality `gaze` (gaze reticle + tap). Interaction events, scoring, missions and coach are unchanged.
- **XR & Interaction (P1) impact:** a gaze interactor and a reticle instead of hands/controllers; bigger targets (aim with the head); L2 grabbing becomes gaze-select tool → gaze-select socket.
- Comfort: seated, no locomotion (unchanged). Keep the 6 cm minimum; consider larger targets for head aiming.
Draft both, get the team's OK, bump versions, and update fixtures/validator in the same PR.

## E. Current shared services (for testing)
- Coach proxy (Gemini `gemini-3.5-flash-lite`, about 1 s; falls back to the offline coach on any error): `services/coach-proxy`. Its key lives only in the Content dev's git-ignored `.env`; never ask for it in chat.
- 2D simulator: `python tools/level-sim/server.py` → http://127.0.0.1:8090
- WebGL build: `Builds/WebGL` → http://127.0.0.1:8095

Acknowledge with one line, then start at S0.A.

# ASTRA 3D Test Environment: Build Guide (three.js + Blender)

**Goal:** a realistic, live 3D test environment in the browser, where you sit in a fictional ASTRA Station cabin and perform Level 2 and Level 3 exactly as a junior professional would, with the real phases, scoring, hints and coach.

**You build it. This guide tells you what, in which order, and how to check each part.**

> It is a **test environment**, not the game. It proves content, layout, procedure and feel early. Final evidence is still the Unity build on the headset. The Blender assets you make here are reused in Unity, so none of the work is wasted.

---

## 0. Ground rules (read once)
1. **Reuse the proven logic. Never rewrite it.** Import `tools/level-sim/scoring.js` (runner + scorer) and `tools/level-sim/coach.js` (offline coach). They are tested against every fixture (29/29). Your 3D code only produces interaction events.
2. **The contract is the law.** Event names, targetIds, phases and the coach API come from `CONTRACT.md` (v1.2). In this sim, `InteractionBus` plays the role of Unity's `InteractionEvents`, and `LevelController` mirrors §8.
3. **Layout must match the hints.** `l3-cabin-v1.json` says CABIN CONTROL is *on your right at chest height*, COMMS is *to the left of CABIN CONTROL*, and the CHECKLIST display is *above the panels*. Build exactly that, or change the hint first (style guide review).
4. **Fictional and professional.** The vehicle is the fictional ASTRA Station. No agency logos or real procedures on any texture or placard (style guide §7).
5. **Git:** work on `feat/content-ai-coach` (or a new `feat/content-3d-sim` branch). Nothing is committed or pushed without your approval. `.blend` and `.glb` files go through Git LFS (already configured).

---

## 1. What "real feel" means here (the acceptance bar)
Treat this as the definition of done for realism. Every item must be true.

**Seated cockpit ergonomics**
- Eye height 1.20 m above the floor and seat facing the main panels. All interactables within 0.7 m horizontally of the seat and 0.7–1.5 m high, **at least 6 cm** across (CONTRACT §8).
- Nothing behind you; no camera motion you didn't cause; Recenter always available.

**Hardware that behaves like hardware**
- Buttons travel 3–4 mm with a click sound, and spring back.
- Toggles snap between two positions with a guard or collar.
- Dials have detents (click per position) and only trigger at their target position.
- Levers need a deliberate pull past a threshold.
- Indicator LEDs are emissive and dim when off.

**Microgravity cues (fictional but believable)**
- Released tools **drift**, slowly and damped, instead of falling. A tool that isn't stowed floats away, which is exactly why L2 teaches stowage.
- Loose cable ties and dust particles drift slowly. No falling objects anywhere.
- Handrails, foot restraints, velcro patches, fabric stowage bags and cable bundles on the walls.

**Light and surface**
- 4000–5000 K panel LEDs plus a cool ambient fill. Baked ambient occlusion so corners read correctly.
- PBR materials with wear: scuffed edges, fingerprints near switches, slightly dirty roughness.
- Placards and labels in CAPS that match the mission text **exactly** (`CABIN LIGHTS`, `AIR FAN`, `COMMS CHECK`).

**Sound (every sound has a caption)**
- Constant fan hum, relay clicks, a comms static burst on COMMS CHECK, and a caution tone.
- A caption for every sound (style guide §4); an alarm is never audio-only.

**Professional procedure**
- An in-world **checklist tablet** shows the current step. A **caution and warning annunciator** shows alarms (`CAUTION — POWER: BUS B FAULT`).
- A **comms panel** gives READY feedback. The debrief reads like a post-task review, not a game score.

---

## 2. Features that must exist (no compromises)
| Area | Must support | Source |
|---|---|---|
| Interaction events | `calibration_completed, target_located` (gaze/ray dwell 1.0 s), `target_selected, control_activated, checklist_opened, status_reported, object_grabbed, object_released, object_placed` (socket id), `paused, resumed, recentered` (`targetId: "system"`) | CONTRACT §1, §3 |
| Input modalities | Desktop mouse ray → `simulator`; WebXR controller ray → `controller_ray`; controller grip → `controller_grip`; hand pinch/poke → `hand_pinch`; hand grab → `hand_grab` | CONTRACT §3 |
| Phases | Briefing (`briefingSeconds` 6) → Demonstration (`demoSecondsPerStep` 4, highlight each target) → Practice (hints after mistakes) → Qualification (new session, no hints, Finish button) → Debrief; Retry → Practice; Restart → Briefing | CONTRACT §8, ModulePhaseMachine |
| Timing | `timestampMs` from attempt start, excluding paused time | CONTRACT §0, §8 |
| UI in the world | Instruction/checklist tablet, hint line, alarm annunciator (≤ 3 flashes/s), phase banner, debrief board, pause menu (Resume, Recenter, Restart, Exit, Finish) | CONTRACT §7 |
| Scoring | `score()` from `scoring.js` on the Qualification trace only | CONTRACT §5 |
| Coach | Offline coach always; AI coach via the proxy, labelled "AI coach (advisory)"; falls back to offline on any error | CONTRACT §9 |
| Session log | Download the Qualification trace + result as JSON | CONTRACT §8 |
| Accessibility | Captions, colour never the only cue, `prefers-reduced-motion` respected, readable text at 1 m | Style guide §4, PRD NFR-ACC-01 |
| Honesty | A permanent "TEST ENVIRONMENT · NOT HEADSET EVIDENCE" tag in the corner | — |

---

## 3. Tools to install (one time)
1. **Blender** (current LTS) with its built-in glTF 2.0 exporter.
2. **Node.js** is installed (v24), but `npm` scripts can't find `node` on this PC. **Fix your PATH first:**
   Windows Settings → System → About → Advanced system settings → Environment Variables → *Path* (user) → **Add** `C:\nodejs\node-v24.21.0-win-x64` → OK. Open a **new** terminal and check `node --version` and `npm --version`.
3. **gltf-transform CLI** for compression: `npx @gltf-transform/cli --help`. KTX2 texture compression also needs **KTX-Software** (`toktx`) on PATH.
4. A modern Chromium browser (Edge or Chrome). For headset testing: the headset's browser with WebXR (see §9).

---

## 4. Folder layout to create
```
art-source/                      .blend files (OUTSIDE Assets/, so Unity never tries to import .blend)
  astra_cabin.blend
  astra_props.blend
Assets/ASTRA/Art/Models/         exported .glb (shared with Unity later)
  l3_cabin.glb
  l2_equipment_bay.glb
tools/level-sim-3d/              the three.js test environment (Vite project)
  index.html
  vite.config.js
  src/
    main.js                      renderer, scene, loop, resize, stats
    world/loadLevel.js           load .glb, find nodes by targetId, attach behaviours from extras
    interact/bus.js              InteractionBus.raise(event, targetId, input)  (= Unity InteractionEvents)
    interact/desktop.js          pointer lock, crosshair ray, click, dwell (target_located)
    interact/xr.js               WebXR controllers + hands → same bus
    interactables/*.js           Button, Toggle, Dial, Lever, Panel, Checklist, Tool, Socket, Display
    game/levelController.js      phases, timers, runner, scoring, coach, session log (= CONTRACT §8)
    ui/*.js                      tablet, annunciator, phase banner, debrief board, pause menu
    audio/*.js                   ambience, sfx, captions
  public/audio/                  .ogg sounds (credit any licensed sounds in README)
```

---

## 5. Blender conventions (the contract between your art and the code)
Get these right and the loader needs no per-level code.

**Scale and orientation**
- Units are metric with 1 unit = 1 m. Model with Z up; the glTF exporter converts to Y up (keep **+Y Up** checked).
- **Apply all transforms** (Ctrl+A → All Transforms) before export.

**Names**
- Every interactable object is named **exactly its `targetId`**: `cabin_lights_toggle`, `air_fan_dial`, `comms_check_button`, `cabin_control_panel`.
- One object per `targetId`, no duplicates.
- Child named `<targetId>__hit`: an invisible, simple box **larger** than the visual part (forgiving target, ≥ 6 cm). Rays hit this, not the detailed mesh.
- Moving part: a child named `<targetId>__part` (switch lever, dial knob, button cap). **Put its origin on the pivot**, the hinge or rotation axis, so code rotates or moves only this part.
- Seat: an Empty named `spawn_seat` at eye position (1.20 m), facing the panels.
- Annunciator lights: `light_<name>` meshes with an emissive material.

**Custom Properties → glTF extras** (Object Properties → Custom Properties; export with *Include → Custom Properties* checked)
| Property | Values | Used by |
|---|---|---|
| `astra_type` | `button, toggle, dial, lever, panel, checklist, display, tool, socket, decor` | loader picks the behaviour |
| `astra_axis` | `x`, `y` or `z` (local axis of `__part`) | toggle, dial, lever |
| `astra_angles` | e.g. `"-35,35"` (toggle), `"-60,-20,20,60"` (dial detents, degrees) | toggle, dial |
| `astra_trigger` | detent index or position that fires the event, e.g. `1` | dial, lever |
| `astra_travel` | press depth in metres, e.g. `0.004` | button |
| `astra_accepts` | the tool `targetId` this socket accepts | socket |
| `astra_label` | text on the placard, CAPS, identical to the mission text | label/placard |

The **event** a control raises comes from the mission step that uses its `targetId` (as in the 2D simulator), so art never hard-codes events.

**Realism budgets (so it runs at 72–90 fps in a headset browser and 60 fps on a laptop)**
- Whole cabin ≤ 300k triangles; ≤ 100 draw calls (join static decor meshes per material).
- Textures 2K max, with a texture atlas for small props. Texel density is consistent; labels get the most.
- Lighting is **baked** (Cycles → bake AO/lightmap to a second UV set), plus one real-time light for highlights.
- Export as `.glb`, then compress: `npx @gltf-transform/cli optimize in.glb out.glb --compress meshopt --texture-compress ktx2`.

---

## 6. Build order: milestones with checks
Do them in order. Each has a check that must pass before you move on.

### M1: Project skeleton (½ day)
1. Fix PATH (§3). In `tools/level-sim-3d/`: `npm create vite@latest . -- --template vanilla`, then `npm install three troika-three-text`. **Pin exact versions** in `package.json`.
2. `vite.config.js`: allow importing the shared logic and proxy the coach (no CORS changes needed):
   ```js
   export default {
     server: {
       fs: { allow: [".."] },
       proxy: { "/api/coach": { target: "http://127.0.0.1:8787", rewrite: p => p.replace(/^\/api\/coach/, "/api/v1/coach/debrief") } },
     },
   };
   ```
3. `main.js`: WebGLRenderer (sRGB output, ACES tone mapping, shadows off for now), PerspectiveCamera (FOV 70) at `spawn_seat`, a test cube, `PointerLockControls` for mouse look, and a stats panel.
4. **Check:** `npm run dev` → you can look around with the mouse at 60 fps, and clicking a cube logs `control_activated test_button simulator`.

### M2: Greybox cabin in Blender (1 day)
Block out the cabin with simple boxes at **correct scale**: seat, front panels, CABIN CONTROL on your right at chest height, COMMS to its left, the display with a CHECKLIST tab above, handrails, and stowage.
- **Check:** export → load in the browser → from the seat, every L3 target is reachable by the §1 rules, and every hint's position words are true.

### M3: Interactables pipeline (1–2 days)
1. `loadLevel.js`: traverse the glTF, and for each node whose name is a `targetId` in the mission, attach the behaviour from `userData.astra_type` (glTF extras land in `userData`). Use the `__hit` child for raycasts and animate the `__part` child.
2. Behaviours: Button (travel + spring), Toggle (snap between angles), Dial (detents; fires only at `astra_trigger`), Lever (threshold), Panel (select/locate), Checklist (opens the tablet tab), Tool and Socket (see M6).
3. `desktop.js`: crosshair ray; click = select/activate; hold 1.0 s on a target = `target_located` (once per attempt); hover highlight within the same frame. Raise everything through `InteractionBus` with `input: "simulator"`.
4. **Write a glb check script** (`tools/level-sim-3d/check-glb.mjs`): read the `.glb` JSON chunk with Node, list node names, and fail if any mission `targetId` is missing or duplicated, or if any interactable lacks `__hit` or `astra_type`.
- **Check:** every L3 control raises exactly one correct event per action (log it), and the glb check passes.

### M4: Level flow and UI (2 days)
1. `levelController.js`: implement CONTRACT §8 exactly, using `MissionRunner` and `score` from `../level-sim/scoring.js` and `offlineCoach` from `../level-sim/coach.js`.
2. In-world UI with **troika-three-text** (crisp text): checklist tablet (instruction + hint line), phase banner, annunciator, debrief board (verdict, missed steps, out-of-order, wrong controls, offline coach, AI coach labelled advisory), and pause menu.
3. Coach: `POST /api/coach` (Vite proxy) with `{sessionId, missionId, result, events}`. On any error, show the offline coach only.
- **Check (parity test):** perform the same click sequence in the 2D simulator and in 3D. The downloaded `SessionResult` JSON must be **identical** apart from `sessionId` and timings.

### M5: Realism pass (3–5 days, the "feel")
Detail, texture, light and sound to meet §1. Recommended order: hard-surface detail → UVs → PBR materials and wear → placards → bake AO/lightmap → HDRI reflections → emissive LEDs → audio and captions. Optional post effects on desktop only (bloom on LEDs); keep them **off in WebXR**.
- **Check:** the §1 checklist passes, and frame time stays ≤ 16.6 ms on your laptop.

### M6: Level 2 equipment bay (2–3 days)
Tools (`*_tool`) and sockets (`*_socket_N`) behave like this:
- `object_grabbed` on grab.
- Release inside the matching socket → snaps in, raises `object_placed` with the **socket's** id, and the latch toggle becomes active.
- Release anywhere else → `object_released`, and the tool **drifts** (damped velocity, slow spin). Recovering it is part of the task.
- Sockets reject the wrong tool (it bounces out gently). That is `object_released`, not a placement.
- Write `l2-equipment-v1.json` first (see your prompt S1.1); the layout follows its hints.
- **Check:** the L2 mission is playable end to end, and the parity test (M4) passes for L2.

### M7: Live in a headset with WebXR (optional, 1–2 days)
1. Add three.js `XRButton` with `optionalFeatures: ["hand-tracking"]`; controllers via `XRControllerModelFactory`, hands via `XRHandModelFactory`.
2. Map input to modalities: trigger ray → `controller_ray`, grip → `controller_grip`, pinch/poke → `hand_pinch`, hand grab → `hand_grab`.
3. WebXR needs a secure context: either `adb reverse tcp:5173 tcp:5173` (Android-based headsets, then open `http://localhost:5173` on the headset), or HTTPS via `@vitejs/plugin-basic-ssl`.
- **Check:** the full L3 run works with controllers in the headset at a steady frame rate. Label results as **"WebXR test"**; they're still not Unity evidence.

### M8: Quality gate (1 day)
- Automated end-to-end run (like the 2D sim's: headless browser, scripted clicks, deliberate mistakes, assert the debrief).
- glb check passes for every level. Parity tests pass. Zero console errors in a 10-minute session.
- Style review: placards and captions match the mission text and the style guide.

---

## 7. Common mistakes to avoid
- Rotating the whole object instead of `__part` (the label spins with it). Fix the pivot in Blender.
- Unapplied scale, so the hit box ends up 100× too big. Ctrl+A before export.
- Forgetting *Include → Custom Properties*, so `userData` is empty and nothing is interactive.
- Placard text that differs from the mission text by one character. The glb check should compare `astra_label` to the CAPS words in the instruction.
- Post-processing and shadows on in WebXR, which drops the frame rate. Keep them desktop-only.
- Writing a second scoring rule in the 3D code. Always import `scoring.js`.
- Putting `.blend` files inside `Assets/`. Unity will try to import them.

---

## 8. Definition of done
- [ ] L3 and L2 playable end to end in 3D on desktop (and in WebXR if M7 is done)
- [ ] Every §2 feature present; the §1 realism checklist passes
- [ ] glb check, parity tests (2D = 3D results) and e2e run all pass
- [ ] Frame time ≤ 16.6 ms on desktop (≤ 13.9 ms in WebXR at 72 Hz)
- [ ] All text matches the mission files and the style guide; everything fictional, no logos
- [ ] Reviewed by `astra-reviewer`; committed only after your approval

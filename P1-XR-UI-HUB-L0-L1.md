# P1: XR & Interaction (phone VR rig, Hub, L0, L1, in-world UI, art + audio)

> Read `handoff/00-TEAM-BRIEF.md` first. Branches: `feat/p1-phone-hub` (rig + Hub + UI) and `feat/p1-l0-l1`.
> **You own the most important risk: the app must work on the Irusu phone headset.** Do task 1 first.

---

## Task 1: Phone VR gaze rig (start now; C1 editor prototype, C2 on the phone)

**Goal:** on an Android phone in the Irusu Ultra, the trainee:
- sees the cabin in stereo with head tracking,
- aims with a centre reticle,
- taps the screen (or the headset button) to use the control under the reticle.

**1a. Platform (propose first, the team lead approves; this is the only time you touch `Packages/`/`ProjectSettings/`)**
- Install the **Google Cardboard XR Plugin for Unity** from its git URL (googlevr/cardboard-xr-plugin).
  **First, read its README and confirm it supports Unity 6.3.** Note the minimum Android API, the
  graphics API and the scripting backend it needs.
- XR Plug-in Management → Android → enable Cardboard. Settings:
  - IL2CPP, ARM64, landscape left.
  - Graphics API as the plugin README says.
  - URP asset for Android = `Assets/Settings/Mobile_RPAsset.asset`.
- **Fallback if the plugin fails on 6.3 (decide by 02:00, don't sink the night):** a "magic window"
  rig: a single camera driven by the gyro (Input System `AttitudeSensor`), with an optional split-
  screen stereo of 2 cameras with an IPD offset. Gaze + tap stay identical. Tell the team lead immediately.

**1b. Interface contract (put it in `Assets/ASTRA/XR/Input/IGazeTarget.cs`)**
```csharp
namespace Astra.XR.Input
{
    /// Anything the gaze reticle can point at and use.
    public interface IGazeTarget
    {
        string TargetId { get; }
        bool IsGazeEnabled { get; }          // false while a held tool is in hand, etc.
        void SetGazeHover(bool on);          // highlight on/off
        void ActivateFromGaze();             // must raise the contract event with InputModality.Gaze
    }
}
```
The team lead makes the level controls (`TestTarget`, which becomes `LevelTarget`) implement it.
**You** make UI buttons implement it (`GazeButton`, below).

**1c. `Assets/ASTRA/XR/Phone/GazeInteractor.cs` + prefab `Assets/ASTRA/XR/Phone/GazeRig.prefab`**

*Pointing and hover:*
- Each frame, raycast from `Camera.main` forward (max 3 m).
- Take the first collider and use `GetComponentInParent<IGazeTarget>()` on it.
- Call hover enter/exit only when the target changes.

*Selecting (all three must work):*
- **Tap:** `Touchscreen.current.primaryTouch.press.wasPressedThisFrame`. Also the Cardboard trigger,
  which arrives as a screen touch.
- **Dwell (accessibility):** select after looking at a target for 1.5 s (configurable). Show a radial
  fill. On by default in L0, a toggle elsewhere.
- **Editor simulation:** mouse click + mouse look, so you can test without a phone.

*Reticle:*
- A small dot ~1.5 m ahead that sits on the hit point (scaled to keep the same angular size).
- It grows into a ring on a hoverable target and shows the radial fill during dwell.
- It is always rendered on top and never clipped.

*Hover label:*
- Show the control's label under the reticle (e.g. `CABIN LIGHTS`), like the browser crosshair label.

*Rest of the rig:*
- **Recenter:** a long press (≥ 1 s) recenters yaw. Also call `IRecenterHandler` if present.
- **Comfort:** the camera never moves except by the head. No vignette flashes, nothing strobing.

*Tests (`XR/Tests/PlayMode/GazeInteractorTests.cs`):*
- hover enter/exit fires once,
- dwell selects after the set time,
- tap selects,
- a target with `IsGazeEnabled=false` is ignored,
- nothing throws with no target.

**1d. Hand-off to the team lead:** the prefab path + a 10-line README section "How the factory should
add the rig". The team lead adds it to every generated level.

**Done when:**
- An APK on the Irusu shows L3 in stereo.
- The reticle hovers and taps controls, and a full L3 run reaches Debrief **on the phone**.
- You recorded fps and comfort notes in your STAGE REPORT.

---

## Task 2: Hub scene (C2)

`Assets/ASTRA/Scenes/Hub.unity` using your existing `HubController`, `LevelCard` and `LevelCatalog`.

**Layout:**
- 8 level cards in a gentle arc at 0.9–1.1 m, readable on the phone, all reachable by gaze.
- Each card shows: number, title, one-line goal, a lock icon, the best result, and a rating
  (Gold/Silver/Bronze from P2's `ResultRating`).

**The level-select experience has to feel premium:**
- Each card has a preview image (use the factory's screenshot tool output, e.g. `L3_Cabin_front.png`
  cropped to 16:9), the skill it teaches, its difficulty (1–3 dots), the time (~2–4 min) and the best
  score + medal.
- On gaze-hover the card lifts 2 cm toward the user, its edge glows, and its details expand. These are
  200 ms eased animations: the card moves, never the camera.
- Locked cards are dimmed with a lock and "Pass L<N-1> to unlock".
- The next recommended level pulses softly ("Continue: L3 Cabin").
- A header shows the callsign (typed once in the browser, or the default "CADET"), the total academy
  score and the medals earned. Behind the cards, the Earth view from the station window.

**Unlocking:**
- L0 is always unlocked.
- Passing level N unlocks N+1, using P2's progress API.
- A **"Demo: unlock all"** card or button for judges. This is mandatory: judges must reach every level.

**Buttons and polish:**
- Selecting a card loads its scene.
- Every UI button is gaze-selectable via a `GazeButton : MonoBehaviour, IGazeTarget` that invokes
  `Button.onClick`. It needs a BoxCollider sized to the RectTransform.
- Add a title, the fictional "ASTRA STATION" crest (our own, no agency logos), a short "How to play"
  card, and a settings card (dwell on/off).

**Integration:**
- The factory automatically puts `Assets/ASTRA/Scenes/Hub.unity` first in the build if it exists.
- The levels call `BackToHub()`, which loads the scene named `Hub`.

**Test:** a PlayMode test that loads Hub, checks 8 cards exist, the locked ones can't be selected,
and "unlock all" unlocks them.

---

## Task 3: L0 Safety & Calibration + L1 Orientation (C1 data, C2 playable)

Write the mission + layout JSON (brief §5 rules). The team lead reviews the wording. Use **only** event
names from `CONTRACT.md §3`.

- **L0 Safety & Calibration (teaches the controls, ≤ 2 min):**
  1. Look at the SAFETY placard (panel `target: true`, `target_located`).
  2. Tap READY.
  3. Flip TEST SWITCH.
  4. Turn TEST DIAL to 2.
  5. Grab the CHECK TOOL and place it in TOOL BAY.

  Use 1 decoy. Every hint teaches the mechanic ("Look at the switch until the ring appears, then tap.").
- **L1 Cadet Orientation (learn the cabin):** locate COMMS, POWER, LIFE SUPPORT and STOWAGE panels
  around the seat (`target_located`), then open the CHECKLIST tab. Spread the panels left, front and
  right so the user looks around. Every target is still within ±100° of forward and ≤ 0.7 m away.
- Optional view scripts `L0SafetyView.cs` / `L1OrientationView.cs` (auto-attached by name), e.g.
  a light that turns green when calibration is done.

**Done when:**
- The validator is OK.
- The factory builds both scenes.
- `FirstTimeUserPlaythroughTests` passes with L0 and L1 included.
- Your screenshots show no overlap.

---

## Task 4: In-world UI polish (C3)

Your prefabs: `InstructionPanel`, `PhaseBanner`, `DebriefPanel`, `AlarmIndicator`, `PauseMenu`. The
factory places them:
- PhaseBanner (−0.56, 1.56, 0.56), 0.34 m wide,
- InstructionPanel (−0.56, 1.37, 0.56), 0.42 m wide,
- DebriefPanel (0, 1.30, 0.44), 0.46 m wide, shown only in Debrief,
- AlarmIndicator (0.62, 1.58, 0.60).

The factory adds a dark-glass backplate with a glowing edge. Keep your prefabs transparent-backed.

- **Legibility on the phone:** body text ≥ 12 mm tall at 0.55 m; titles ≥ 18 mm. Use high contrast:
  white or near-white on dark glass. No thin fonts.
- **Look:** match the browser guide: rounded dark glass, accent blue `#409CFF`, success green
  `#30D158`, warning amber `#FFB340`, error `#FF6961`.
- **Debrief:**
  - The big result (PASSED / NOT PASSED + Gold/Silver/Bronze).
  - Time, steps completed, missed critical steps, out-of-order steps, wrong controls, coach text.
  - Gaze buttons **Retry**, **Next level**, **Hub**. Add a `onNextLevel` UnityEvent; the team lead wires it.
- **Motion:** phase banner slide/fade ≤ 300 ms; checklist tick animation; no motion that moves the
  world. AlarmIndicator flashes ≤ 3 Hz.
- **Score reveal (the most watched 5 seconds of every level):**
  1. The score counts up 0 → N over 1.2 s.
  2. Then the rating medal appears (Gold/Silver/Bronze, a soft glow, no flashing).
  3. Then the breakdown lines from P2's `ScoreBreakdown` (e.g. "Wrong control: AUX HEATER −8") slide
     in one by one.
  4. Then the coach advice.
  Show "NEW BEST" if the score beats the stored best.
- **Next level flow:**
  - **Next level** is the primary, highlighted button when passed.
  - **Retry** is the primary button when not passed. It says what to fix ("Retry: focus on step 3").
  - On the last level the primary button is **Graduate**, which opens the certificate.
- **Graduation certificate** (`UI/Prefabs/Certificate.prefab`): "ASTRA ACADEMY: CADET QUALIFIED",
  the callsign, the date, the total academy score, 8 level medals, our own fictional crest, and the
  line "Fictional training, not an official certification". It must look good in the video.
- **Pause menu** (Resume, Recenter, Restart, Hub) must be gaze-selectable. The team lead wires it to
  `LevelController.Pause/Resume/Recenter/Restart/BackToHub`.

---

## Task 5: Audio + art (after tasks 1–3 work; this is what makes it feel premium)

**Audio** (`Assets/ASTRA/Audio/`), CC0 or self-made only (e.g. Kenney.nl packs are CC0), all listed
in `CREDITS.md`:
- Create the asset `AudioCues.asset` from a ScriptableObject `Astra.UI.AudioCues` with AudioClip fields:
  `hover, select, toggle, button, dialTick, grab, place, correct, wrong, phaseChange, alarm,
  success, fail, ambientHum`.
- The team lead hooks playback into the factory + LevelController. Keep the clips short, normalised
  to −14 LUFS, and the hum looped and quiet.

**Art (optional, if you or the team lead have time):** Blender hero props exported to
`Assets/ASTRA/Art/Prefabs/`. The factory will use them if present:
- `Cabin_Dressing.prefab`: extra cosmetic set dressing placed at world origin (cables, velcro
  patches, stowage bags, handrails, laptop, labels). **No colliders.** Keep the window, the panel zones
  (z 0.45–0.62, y 0.95–1.45) and the left UI column clear (brief §5).
- `Tool_<targetId>.prefab`: the visual for a tool. The factory keeps its own root and collider.
- Budget on the phone: whole cabin ≤ 150k triangles, ≤ 60 materials, textures ≤ 1024².
- Use the mobile-friendly URP Lit, baked AO in the textures, and no realtime shadows from dressing.

---

## Your checkpoints

| Checkpoint | Deliverable |
|---|---|
| C1 (03:00) | IGazeTarget + GazeInteractor working in the editor with mouse simulation; L0/L1 JSON validated; plugin go/no-go decided |
| C2 (10:00) | APK on the Irusu running L3; Hub scene in the browser build; L0/L1 playable |
| C3 (14:00) | All levels on the phone via gaze; Hub on the phone; UI legibility pass; audio cues delivered |
| Freeze (17:00) | Only bug fixes |

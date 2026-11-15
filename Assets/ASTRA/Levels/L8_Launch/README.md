# L8 Launch Day: Ride to Orbit

**Owner:** Core Logic · **Mission:** [`l8-launch-v1`](../../Content/Missions/l8-launch-v1.json) · **Layout:** [`l8-layout.json`](../../Content/Layouts/l8-layout.json) · **Scene:** `L8_Launch.unity` (generated)

The cinematic finale. The trainee flies the ship from the launch pad at sunrise to orbit, and puts out a cabin fire on
the way. Graduation follows: the certificate now comes after L8 (`DebriefPanel.LastLevel = 8`), and L7's "Next level"
leads straight into it.

## The flight (`L8LaunchView.cs`)

Every completed step drives the next beat. The flight only goes as far as the steps allow, and it catches up quickly
when the trainee is ahead.

| Step (control) | What happens outside the windshield and in the cabin |
|---|---|
| Pad (Briefing) | A sunrise sky with clouds, the red launch tower and lightning masts; the pad vents steam |
| 1 HARNESS · 2 ENGINE ARM · 3 GUIDANCE | The flight computer's go/no-go calls, clicks and a steam burst |
| 4 LAUNCH | A 10 s countdown with beeps. Ignition at T-6: the real launch roar, smoke rolling past the glass, an orange engine glow and vibration. Liftoff at T-0: the tower drops away |
| Climb | Through the cloud deck, with condensation streaks rushing past at MAX-Q; the sun rises into the windshield and the sky deepens toward black |
| 5 THROTTLE 70% | Through MAX-Q. BOOSTER SEP lights amber at booster burnout |
| 6 STAGE | A staging jolt, a moment of silence, debris falling away, then the second stage at a higher pitch |
| **Fire!** (3 s into the second-stage burn) | An electrical fire on AUX POWER, below the FLIGHT panel: flames, sparks and smoke rising to the ceiling. The klaxon blares, the cabin pulses red (1 Hz) and the flight computer shouts. The climb holds |
| 7 FIRE EXT · 8 FIRE | Grab the red CO2 extinguisher from its bracket by your left knee (it glides into your hand), then tap the FIRE. You spray it from your hand: it aims at the flames and a CO2 cloud from its nozzle smothers them. The alarm dies away, the extinguisher goes back on its bracket and the climb resumes |
| 9 TOWER JETT | The escape tower streaks away on a smoke trail |
| ORBIT light | Black sky, stars, and the Earth's curve rising into the windshield |
| 10 CUTOFF | Silence and weightlessness: a pen, a checklist card and a washer float up off the panel |
| 11 REPORT | "Orbit confirmed" |

**The extinguisher:** the level factory builds every tool as the same generic orange tool. At runtime L8 reshapes it
into a CO2 extinguisher: a red cylinder with a white band, a black valve head and handle, a hose and a nozzle. The red
body stays the tool's highlight, so gaze, hover and the Practice glow still work.

**Also on screen:**
- **Caption strip:** a "flight computer" caption typed out along the bottom of the windshield (red during the fire),
  with live telemetry (T± clock, altitude, velocity, stage or ALARM).
- **Decoys:** ABORT (seat panel) and CABIN VENT (comms panel).

**Where it resets:** every new phase starts on the pad: the clouds and steam restart and the fire is cleared. The
Debrief keeps the orbit view.

**The windshield:** at runtime, L8 resizes the factory's front-wall pieces into a three-pane windshield (x ±0.95 m,
1.30–2.12 m). It also moves the placards below the glass. Other levels are untouched.

**Scoring note:** BOOSTER SEP and ORBIT are cues. They don't gate the controls. Pressing ahead of the cues is
accepted, and the flight catches up. By the flight's own timings, a trainee who waits for each cue should need about a minute, inside par (11 steps × 8 s = 88 s). That figure hasn't been timed in a real run yet.

## Rules it keeps
- **Comfort:**
  - the camera never moves, and nor does the cabin; only the world outside the glass does;
  - the "shake" is sound, vibration (Android) and millimetre rattles on decorative props only;
  - nothing flashes faster than 3 Hz (the alarm pulses at 1 Hz, and the fire light flickers smoothly).
- **Not yet tried in a headset:** the windshield is about ±43° wide, and the streaks and clouds move fast outside it.
- **Scoring:** visual and audio only; it never touches scoring, the mission or the progress rules.
- **Builds:** everything visual is built in code from shaders every build has: `Sprites/Default` (in the
  always-included list) and clones of URP Lit materials already in the scene.
- **Audio:**
  - `Resources/L8/launch_roar.ogg` (273 KB) is NASA's "Discovery STS-131: Sound of Launch", trimmed into a seamless
    loop. NASA audio is generally not subject to copyright in the US (NASA Images and Media Usage Guidelines); it's
    credited in `CREDITS.md`;
  - the repo's `.gitattributes` sends `.ogg` through Git LFS, as it does the Hub's `L8_preview.png`;
  - the beeps, clicks, thump, whoosh, klaxon and CO2 hiss are generated in code.

## Tests
`Tests/PlayMode/LaunchLevelPlayModeTests.cs`:
- **The full flight:**
  - the pad, ignition, liftoff (the tower drops), the climb and booster sep;
  - staging, then **the fire** (flames, ALARM in telemetry), then the extinguisher grab and spray: it is sprayed from the cadet's hand, CO2 comes out of the nozzle, the flames and the alarm light go out, and it goes back on its bracket;
  - the jettison, the Earth near orbit and cutoff (COAST);
  - Qualification back on the pad, with the clouds and steam restarted and no fire.
- **The Demonstration's first launch, then a fast clean qualification:** passes with Gold, and the flight catches up
  to orbit in the debrief.
- **Window shots:** saved to `Builds/Shots/L8_*.png`, plus shots of the fire. `L8_Launch_front.png` becomes the Hub
  card preview (`ASTRA → P1 → Build P1 assets and Hub scene`).

`FirstTimeUserPlaythroughTests` covers L8 like every other level. All of this is Editor PlayMode evidence, not phone
evidence.

## After the PR merges
The hosted coach (Render) checks `missionId` against its own copy of the mission files. Until it's redeployed from
`main`, it doesn't know `l8-launch-v1`, so L8's debrief uses the offline coach. Redeploy it: Render → the service →
**Manual Deploy → Deploy latest commit**.

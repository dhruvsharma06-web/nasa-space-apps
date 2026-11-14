# ASTRA Academy: Content Style Guide

**Owner:** Content & AI Coach Developer · **Applies to:** every mission file (`instruction`, `hint`), alarm captions, subtitles, panel labels, debrief and coach text.
Every mission file is reviewed against the checklist at the end before it merges.

## 1. Audience
**Junior professionals who are learning:** early-career engineers, technicians and trainees. They are technically literate and comfortable with procedures and checklists, but new to spacecraft operations.

So the text is:
- **Professional, never childish.** No "Great job, cadet!" cheerleading, no emojis, no exclamation marks in procedures.
- **Procedural.** It reads like a real operations checklist: short imperative steps, controls named exactly as labelled.
- **Teaching.** Every domain term is explained the first time a level uses it (see §6), and hints say *why* a step matters, not only *where* the control is.

## 2. Instructions (`instruction`)
- One action per instruction. Imperative mood, verb first. **≤ 15 words.**
- Name controls **exactly as printed on the panel, in CAPS**: `Set AIR FAN to LOW.`
- Target state in CAPS too: `ON`, `OFF`, `LOW`, `ISOLATE`.
- End with a full stop. No "please", no "now", no "try to".
- No colour-only or position-only references in the instruction. Position belongs in the hint.

| Good | Bad | Why |
|---|---|---|
| `Set CABIN LIGHTS to ON.` | `Turn on the lights please!` | Exact label + state; professional tone |
| `Press COMMS CHECK.` | `Click the green button.` | Colour-only; "click" is not a VR action |
| `Select the faulted power bus.` | `Find and fix the bus problem.` | One action; precise verb |

**Verbs:** `Locate` (find it), `Select` (point and choose), `Press` (button), `Set … to …` (toggle, dial, lever), `Open` (checklist or display), `Grab`, `Place … in …`, `Secure` (latch), `Acknowledge` (alarm), `Report`.

## 3. Hints (`hint`)
Shown in Practice after a mistake. **≤ 25 words, at most 2 sentences.**
1. **Where:** location + label (`top-left of the CABIN CONTROL panel`).
2. **What to check:** the visible confirmation (`the READY light turns green and shows READY`).
3. **Why (optional, one short clause):** the operational reason (`isolating the bus stops the fault spreading`).

- Never just repeat the instruction.
- Always pair a colour with a label or shape: `the green READY light`, never `the green light`.
- Address the trainee as "you". Present tense.

## 4. Alarms, captions and subtitles
- Every sound has a caption; every alarm has a visual indicator (light + caption). Nothing is audio-only.
- Format: `CAUTION — <SYSTEM>: <condition>` or `WARNING — <SYSTEM>: <condition>`, e.g. `CAUTION — POWER: BUS B FAULT`.
  - **CAUTION** = needs attention soon, not immediately dangerous. **WARNING** = immediate action needed. Don't use WARNING for minor issues.
- Subtitles for spoken lines: ≤ 2 lines, ≤ 42 characters per line, speaker label first (`MISSION CONTROL: Comms check received.`).
- Caption flashing: at most 3 flashes per second.

## 5. Panel labels and IDs
- **Panel labels:** CAPS, ≤ 12 characters per word group, identical in the scene, the instruction and the hint (`AIR FAN`, `COMMS CHECK`, `CROSS-TIE`).
- **`targetId`:** `snake_case`, `<system>_<thing>_<type>`, with `<type>` one of `button`, `toggle`, `dial`, `lever`, `panel`, `socket`, `tool`, `checklist`, `display`. E.g. `cabin_lights_toggle`, `air_fan_dial`, `tool_bay_socket_1`. Pattern: `^[a-z0-9_]{1,64}$`.
- **`stepId`:** short `verb_object`, `snake_case`: `lights_on`, `isolate_bus`.
- One `targetId` = exactly one interactable in the level scene.

## 6. Terms explained on first use (glossary)
Explain a term in the hint or instruction the first time it appears in a level. Keep the explanation to one clause.

| Term | Plain explanation |
|---|---|
| Bus (power bus) | a distribution line that feeds power to a group of systems |
| Isolate | disconnect a faulted part so the fault can't spread |
| Cross-tie | a connection that lets a healthy bus feed the systems of an isolated one |
| Acknowledge (ACK) | confirm you have seen an alarm; it silences the alarm but does not fix the fault |
| Nominal | operating normally, as expected |
| Comms | communications with mission control |
| Comms check | a test call confirming the voice link works |
| Caution / Warning | alert levels: caution = attend soon; warning = act now |
| Checklist | the step-by-step procedure for a task, so nothing is missed |
| Tether | a safety line that keeps you attached to the vehicle |

## 7. Content integrity (non-negotiable)
- Everything is **fictional**. The vehicle is the fictional **ASTRA Station**. Every mission file has `"fictional": true`.
- No real or export-controlled spacecraft procedures, specifications or data. If something looks real, make it generic.
- No NASA (or any agency) names, logos or insignia used in a way that implies endorsement.
- Never call completion a qualification or certification. Use "level complete", "passed", "debrief".
- Coach and AI text is advisory and labelled as such. It never invents procedures that are not in the mission file.

## 8. Critical vs optional steps
- **Critical** (`"critical": true`): changes a system state that matters for safety or task success, or is the confirmation that ends the procedure. Missing one fails the attempt.
- **Optional** (`"critical": false`): orientation help (locate, open a checklist). Skipping it never fails the attempt.
- Every mission has at least one critical step. Usually the final report/confirmation step is critical.

## 9. Units and numbers
SI units with symbols and a space (`21 °C`, `101 kPa`, `3 m`). Numbers as digits. Times as `mm:ss` on displays.

## 10. Mission file review checklist
Copy this into the PR description and tick each box.

- [ ] `python scripts/validate_content.py` prints `OK`
- [ ] `"fictional": true`; no real procedures, agency branding or certification language
- [ ] Every instruction: one action, verb first, ≤ 15 words, control named in CAPS exactly as labelled
- [ ] Every hint: ≤ 25 words, says where + what to check (+ why), never repeats the instruction
- [ ] No colour-only references; no audio-only cues
- [ ] Every domain term explained on first use in this level (§6)
- [ ] `targetId`/`stepId` follow §5; every `targetId` exists exactly once in the scene
- [ ] Critical/optional marked per §8; at least one critical step
- [ ] Alarm captions follow §4
- [ ] Tone: professional, no exclamation marks, no emojis

# Team split — 4 people, 8 levels

## The 8 levels
| Level | Name | What the cadet does | Priority |
|---|---|---|---|
| L0 | Safety & Calibration | Recenter, check tracking, learn pause/exit | P0 (MVP) |
| L1 | Cadet Orientation | Point, select, grab, release, place | P0 (MVP) |
| L2 | Equipment Handling | Move tools, place and secure equipment | P1 |
| L3 | Cabin Familiarization | Find labelled controls, press buttons/toggles | P0 (MVP) |
| L4 | Emergency Response | Read caution, fix a simulated power fault with a checklist | P0 (MVP) |
| L5 | EVA & Tethering | Clip/unclip a virtual tether, use a tool outside | P2 (stretch) |
| L6 | Robotics Arm | Move a robotic arm to grab a target | P2 (stretch) |
| L7 | Integrated Mission (finale) | "Restore Station Power": panel and tool tasks together, then debrief | P0 (MVP) |

Every level runs the same loop: **Briefing → Demo → Practice → Qualification → Debrief**.
Every level reports the same `SessionEvent` (see `CONTRACT.md`), so scoring works the same way everywhere.

## Who does what
Each person owns **one system** (shared code everyone uses) and **two levels** (built from those systems).

| | Person 1: XR & Interaction | Person 2: Core Logic | Person 3: Content & AI Coach | Person 4: Integration Lead |
|---|---|---|---|---|
| **System** | Hand/controller input, grab/press/point, hub menu, pause/recenter UI | Level state machine, scoring, event log, power-system sim, save progress | Mission JSON, checklists, hints, subtitles, offline guidance, Gemini coach proxy (P1) | Unity project setup, OpenXR + headset builds, CI, testing, demo and pitch |
| **Levels** | L0 Safety, L1 Orientation | L4 Emergency, L7 Finale | L2 Equipment, L3 Cabin | L5 EVA, L6 Robotics (stretch) |
| **Owns folders** | `Assets/ASTRA/XR/`, `Assets/ASTRA/UI/`, `Assets/ASTRA/Scenes/Hub*`, L0/L1 scenes | `Assets/ASTRA/Core/`, `Assets/ASTRA/Tests/`, L4/L7 scenes | `Assets/ASTRA/Content/`, `Assets/ASTRA/Integrations/Coach/`, `services/coach-proxy/`, L2/L3 scenes | `Packages/`, `ProjectSettings/`, `.github/`, `scripts/`, `fixtures/`, `docs/`, `CONTRACT.md`, L5/L6 scenes |
| **Branch** | `feat/xr-*` | `feat/core-*` | `feat/content-*` | `feat/int-*` |
| **Week 1–2 (M0)** | Grab a cube with controllers and hands on the headset | Phase FSM + scorer passing tests against `fixtures/` | Write `restore-power-v1` and the L1/L3 mission JSON | Create the Unity project, pin packages, first headset build |

## What to give each person
Everyone gets: the repo link, `docs/PRD.md`, `CONTRACT.md`, `docs/TASKS.md`, this file.
Then, per person:

- **Person 1 (XR):** PRD §7.2 (FR-XR-01…08) and §8.3–8.4, `Assets/ASTRA/XR/Input/ISemanticInputSource.cs`. Needs the **headset** first.
- **Person 2 (Core):** PRD §6, §7.3, §7.4, NFR-QUAL-01, `Assets/ASTRA/Core/**`, `fixtures/session-events.*.json`.
- **Person 3 (Content & AI):** PRD §6.1, §7.5, §10 (content safety), `Assets/ASTRA/Content/Missions/restore-power-v1.json`, `fixtures/coach-debrief.*.json`, `.env.example`. Needs a Gemini key only for P1.
- **Person 4 (Integration):** PRD §8 and §11–14, `.github/workflows/ci.yml`, `docs/STATUS.md`, the M0 checklist in `docs/TASKS.md`. Needs **Unity 6 LTS installed**, headset developer mode, and GitHub admin on the repo.

## Hand-offs (who waits on whom)
1. Person 4 creates the Unity project, then everyone can open it.
2. Person 1's input events and Person 2's scoring are the base every level is built on. Until they are ready, the others build against `fixtures/` and the interfaces in `CONTRACT.md`. Nobody waits.
3. Person 3's mission JSON drives Person 2's FSM; the format is fixed in `CONTRACT.md` §2.
4. Only Person 4 edits `CONTRACT.md`. Ask for changes; don't edit it yourself.

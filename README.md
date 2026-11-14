# ASTRA Academy

An astronaut-inspired XR training simulator built in **Unity 6 LTS** with **OpenXR**, for standalone headsets.
Cadets work through 8 levels that cover calibration, orientation, equipment handling, cabin familiarization, emergency response, EVA, robotics and a final integrated mission. Every action is logged and scored deterministically, then explained in a debrief.

> Educational prototype. This is **not** astronaut certification or an operational flight simulator. All procedures are fictional. Not affiliated with or endorsed by NASA.

## Status
Pre-M0: repository skeleton only. See [docs/STATUS.md](docs/STATUS.md).

## Repository layout
```
Assets/ASTRA/
  Core/          mission phase FSM, simulation, assessment (pure C#, no engine refs)
  XR/            OpenXR input adapters -> semantic actions, interactables
  Scenes/        hub + one scene per level
  Content/       mission definitions (validated JSON)
  UI/            hub, HUD, debrief, subtitles
  Integrations/  optional coach + operator clients
  Tests/         EditMode tests (event-trace replays)
services/coach-proxy/   optional Gemini proxy (holds the key, advisory only)
fixtures/               contract fixtures: ok / empty / error
scripts/                tooling (content validator)
docs/                   PRD, tasks, status, decisions, team split
CONTRACT.md             every boundary between owners
```

## Getting started
1. Install **Unity 6 LTS** and **Git LFS** (`git lfs install`).
2. Clone, then open the repo root in Unity Hub. The Unity project itself is created during M0; see the checklist in [docs/TASKS.md](docs/TASKS.md).
3. Validate content: `python scripts/validate_content.py`
4. Coach proxy (optional): copy `.env.example` to `.env`; keep `USE_MOCK_AI=true` unless you have a key.

## Docs
- [PRD](docs/PRD.md) · [Contract](CONTRACT.md) · [Tasks](docs/TASKS.md) · [Team split](docs/TEAM.md) · [Decisions](docs/DECISIONS.md) · [Contributing](CONTRIBUTING.md)

## Tech stack
Unity 6 LTS · OpenXR Plugin · XR Interaction Toolkit · Input System · XR Hands (where supported) · URP · optional Gemini via proxy

## Team
_To be filled in at kickoff._

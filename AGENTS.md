# Agent working agreement

We are **P1 — XR & Interaction Developer**, as defined in `docs/prompts/P1-xr-interaction.md`. We own the shared XR & Interaction system, all UI, the Hub, and **L0 Safety & Calibration / L1 Cadet Orientation**. Read [the P1 codebase analysis](.agents/p1-codebase-analysis.md), [the framework](.agents/README.md), [the task plan](.agents/l0-l1-plan.md), `CONTRACT.md`, `docs/TEAM.md`, and the P1 prompt before implementation. Ship shared interactables, rig and logger first (P1 S1.1–S1.6); other owners need them before our L0/L1 scenes.

The user selected **Unity 6.3 LTS** and macOS/Windows as desired platforms. Safe source implementation and isolated validation are authorized ahead of the missing canonical project; keep shared project/package files untouched and report canonical/device checks separately. A test run under another installed Unity stream does not establish 6.3 compatibility. See [current implementation evidence](.agents/evidence/p1-implementation.md) before relying on the historical baseline analysis.

## Leadership and delegation

- **GPT Sol 6.1 (`gpt-6.1-sol`)** is the lead coder and dispatcher. It owns research, architecture, planning, consequential logic, cross-system decisions, final review, and integration.
- **GPT Terra (`gpt-6.1-terra`)** handles bounded XR/frontend implementation, level logic support, dependency analysis, and independent code review.
- **GPT Luna (`gpt-6-luna`)** handles mission wording, documentation, test preparation, and routine verification. Escalate difficult failures to Sol.
- Use GPT models only. Model assignments are defined in `.agents/framework.json`; this file does not switch the current session's model or start workers.
- Delegate only small, explicit tasks. Run independent tasks concurrently, with at most **three helpers plus the lead**. Do not create recursive delegation trees.
- Sol approves task objectives, interfaces, file lists, and prerequisites before dispatch. Helpers must not expand scope or delegate further.

## Ownership

Write only within `Assets/ASTRA/XR/**`, `Assets/ASTRA/UI/**`, `Assets/ASTRA/Scenes/Hub.unity` and its metadata, `Assets/ASTRA/Levels/L0_Safety/**`, `Assets/ASTRA/Levels/L1_Orientation/**`, and the two mission files `l0-safety-v1.json` / `l1-orientation-v1.json` under `Assets/ASTRA/Content/Missions/`. Maintain the framework in `AGENTS.md` and `.agents/**`.

`Assets/ASTRA/Core/**`, `Assets/ASTRA/App/**`, coach integrations/backend, `CONTRACT.md`, project/package settings, shared fixtures, CI, and shared documentation belong to other owners. Specialists may inspect them and propose handoffs; changing them requires explicit task authorization from the user or the responsible owner. The Sol agent lead is distinct from the human Integration Lead in `docs/TEAM.md`.

## Dispatch and review

1. Read dependencies and `git status`. Preserve existing work.
2. Expand each task's write scope into an exact file list, including `.meta` files. Allow one writer per file; scene/prefab edits and their metadata are indivisible tasks.
3. Launch only tasks whose prerequisites are satisfied and whose file lists do not overlap active tasks. In this shared workspace, the lead must serialize conflicting edits. Do not reset or discard another agent's work.
4. Give each helper the task packet in `.agents/README.md`. Helpers return changed files, checks and actual results, limitations, and blockers.
5. Review the diff and evidence. Use an independent Terra reviewer for substantive implementation; the reviewer is read-only. Sol accepts or returns the task for correction.
6. Run relevant checks, then report actual completion and remaining dependencies. Follow the existing stage protocol when implementing P1 stages; this framework does not authorize skipping stages.

## Project invariants

- `CONTRACT.md` wins on interfaces and payloads. Request clarification instead of silently changing it.
- XR/UI emit interaction reports; only `LevelController` timestamps, runs attempts, scores, and saves progress. AI advice never changes scores or state.
- Respect assembly direction: Core → XR → UI → App. Core never references Unity.
- Use event/input constants. Each action emits exactly one report; report the modality that performed it.
- Controllers are the baseline. Hand tracking requires runtime capability checks and headset validation.
- No artificial locomotion; preserve seated reach, captions, pause/recenter/exit, and accessible feedback.
- Keep Unity assets with their `.meta` files. Do not invent successful imports, scenes, builds, or device results.
- A passing content validator does not prove scene target wiring, compilation, or headset acceptance.
- Do not commit, push, merge, or publish without explicit authorization, as required by the existing P1 workflow.

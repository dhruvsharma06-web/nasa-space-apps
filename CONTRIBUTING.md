# Contributing

## Ownership
Each person owns folders (see [docs/TEAM.md](docs/TEAM.md)). Edit only your own folders.
Shared files have one editor, the Integration lead: `CONTRACT.md`, `Packages/manifest.json`, `ProjectSettings/`, `.github/`, `.env.example`.

## Git workflow
- `main` is protected and must always open and run. Work on short branches: `feat/<area>-<thing>` (e.g. `feat/xr-grab`).
- Small commits: `feat(core): critical-step omission check [F9]`. Pull `main` into your branch before opening a PR.
- Open a PR as soon as one coherent piece works. CI must be green. One review before merge.
- Never commit `.env`, keys, learner session logs, or large binaries outside Git LFS.
- If a merge breaks `main`, revert first and fix on a branch.
- Tags: `sync-0` … `sync-4`, `rc-1`, `final`.

## Contract changes
Do not edit `CONTRACT.md` yourself. Open an issue titled `CONTRACT CHANGE REQUEST` with: boundary, old → new, why, who is affected.
Adding an optional field is a minor bump (v1.1). Renaming, removing or changing a field's meaning is a major bump (v2.0), and every consumer is updated in the same merge.

## Done means
Merged to `main`, uses the real adapter (not a hard-coded value), matches the current contract, tests pass, and **verified on the headset** (the Editor simulator alone does not count).

## Content safety
All scenarios are fictional and labelled. No real or export-controlled spacecraft data. No NASA branding implying endorsement. See PRD §10.

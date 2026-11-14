# DECISIONS

Format: `D<n>: decision (why, date)`. Assumptions use `A<n>`.

- **D1:** Unity project lives at the repo root (`Assets/`, `Packages/`, `ProjectSettings/`); non-Unity work goes in `services/`, `scripts/`, `fixtures/`, `docs/`. _Why: Unity expects its root layout; ownership stays per folder._ (2026-11-14)
- **D2:** Mission core (`Assets/ASTRA/Core`) has `noEngineReferences: true`. _Why: scoring must be deterministic and unit-testable without a headset (NFR-QUAL-01)._ (2026-11-14)
- **D3:** Mission content is validated JSON for now; ScriptableObjects can wrap it later. _Why: CI can validate it without Unity._ (2026-11-14)
- **D4:** Gemini goes only through `services/coach-proxy`; the client never holds a key. _Why: NFR-SEC-02._ (2026-11-14)
- **D5:** Binary art and audio go through Git LFS (`.gitattributes`). (2026-11-14)

## Open (PRD §13)
- **A1:** Primary headset: _TBD at M0_
- **A2:** Hand tracking is a target, not mandatory; controllers are the guaranteed path
- **A3:** Gemini is optional (P1); the demo must work without it

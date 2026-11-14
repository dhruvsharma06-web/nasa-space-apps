# CONTRACT v1.0 — ASTRA Academy

**Owner:** Integration lead. Change it only through a contract change request (see `CONTRIBUTING.md`).
**Rule:** every boundary between owners is coded against this file. Mocks return these exact shapes.

## Conventions
- Format: JSON, UTF-8. Field names are as written below; they are case-sensitive.
- Times: `timestampMs` is milliseconds since the session started (monotonic, ≥ 0). Wall-clock times use ISO 8601 UTC.
- IDs: lowercase `snake_case` for `targetId`/`stepId`; `kebab-case` + version suffix for `missionId` (e.g. `restore-power-v1`).
- Unknown fields: rejected by validators.
- Data: local-only by default. No names, emails, raw camera or voice data in any payload (NFR-SEC-01).

---

## 1. Session event (XR / mission runtime → Assessment log)
Producer: `Assets/ASTRA/XR`, `Assets/ASTRA/Core/Mission` · Consumer: `Assets/ASTRA/Core/Assessment`, exporter, coach proxy

```json
{
  "sessionId": "string, required",
  "missionId": "string, required, must match a mission definition",
  "timestampMs": "integer >= 0, required",
  "event": "enum, required",
  "targetId": "string, required (may be a step target or a wrong target)",
  "input": "enum, required",
  "result": "enum, required"
}
```
- `event`: `calibration_completed | target_located | target_selected | control_activated | checklist_opened | object_grabbed | object_released | object_placed | status_reported | paused | resumed | recentered`
- `input`: `controller_ray | controller_grip | hand_pinch | hand_grab | simulator`
- `result`: `correct | incorrect | neutral`

C# type: `Astra.Core.Assessment.SessionEvent`
Fixtures: `fixtures/session-events.ok.json`, `.empty.json`, `.error.json` (invalid on purpose: must be rejected)

## 2. Mission definition (Content → Mission core)
Location: `Assets/ASTRA/Content/Missions/*.json` · C# type: `Astra.Core.Mission.MissionDefinition`

```json
{
  "missionId": "string",
  "version": "semver string",
  "title": "string",
  "fictional": true,
  "steps": [
    { "stepId": "string", "order": "integer >= 1, unique", "instruction": "string",
      "expectedEvent": "session event enum", "targetId": "string", "critical": "boolean" }
  ]
}
```
`fictional` must be `true` until SME review (PRD §10).

## 3. Semantic input (XR adapters → Mission core)
C# boundary: `Astra.XR.Input.ISemanticInputSource`
Actions: `Select | Grab | Release | Activate | Recenter` (FR-XR-04). Mission logic never reads raw OpenXR / XR Hands input.

## 4. Scoring (Assessment, deterministic)
C# boundary: `Astra.Core.Assessment.IMissionScorer.Score(mission, trace) -> SessionResult`
- Same mission + same trace → identical `SessionResult` (NFR-QUAL-01).
- `Passed` is `false` if any `critical` step is omitted, whatever the completion time (PRD §6).
- AI output never feeds into scoring (FR-AI-04).

## 5. Coach proxy API (Unity app → `services/coach-proxy`) — P1, optional
`POST /api/v1/coach/debrief` · Timeout: 8 s · Max body: 256 KB · Retry: safe (no side effects)

Request:
```json
{ "sessionId": "string", "missionId": "string", "result": "SessionResult (section 4)", "events": ["SessionEvent (section 1)"] }
```
Response 200: see `fixtures/coach-debrief.ok.json`. `advisory` is always `true`.
Empty (valid, not an error): `summary: null`, `reason: "NO_EVENTS"` — `fixtures/coach-debrief.empty.json`.

Error shape (every endpoint):
```json
{ "error": { "code": "UPSTREAM_UNAVAILABLE", "message": "Readable message", "details": [] } }
```
Codes: `INVALID_INPUT 400 | PAYLOAD_TOO_LARGE 413 | RATE_LIMITED 429 | UPSTREAM_UNAVAILABLE 503 | INTERNAL 500`
On any error, the app shows the scripted offline debrief (FR-AI-01).

The proxy holds the Gemini key; the Unity client never does (NFR-SEC-02). The proxy cannot change simulation state (FR-SIM-04).

---

## Changelog
- v1.0 (2026-11-14): initial

# coach-proxy (P1, optional)

Small backend that holds the Gemini key and turns **structured session facts** into an advisory debrief.
Implements `CONTRACT.md` §9 and §10. Stack: Python 3.11, FastAPI, Pydantic v2.

Rules (from the PRD):
- The Unity client never holds the API key (NFR-SEC-02).
- Input is the deterministic `SessionResult` + event trace only. No free-text user data, no camera/voice.
- Output is advisory text. It cannot change simulation state or scoring (FR-SIM-04, FR-AI-04).
- `USE_MOCK_AI=true` returns `fixtures/coach-debrief.ok.json` so the app works without Gemini (FR-AI-01).

## Run locally

Windows (PowerShell), from `services/coach-proxy`:
```
python -m venv .venv
.venv\Scripts\python.exe -m pip install -r requirements.txt
.venv\Scripts\python.exe -m uvicorn app.main:app --port 8787
```

macOS / Linux:
```
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
.venv/bin/uvicorn app.main:app --port 8787
```

Optional config in env vars or `services/coach-proxy/.env` (see repo-root `.env.example`). Env vars override `.env`.

| Setting | Default | Meaning |
|---|---|---|
| `USE_MOCK_AI` | `true` | `true` returns fixture-shaped answers, no Gemini call |
| `GEMINI_API_KEY` | none | Held only by the proxy. Never logged or returned |
| `GEMINI_MODEL` | none | Pinned model id (currently `gemini-3.5-flash-lite`) |
| `AI_TIMEOUT_MS` | `6000` | Upstream timeout, no SDK retries |
| `RATE_LIMIT_PER_MIN` | `10` | Per client IP, shared by both coach endpoints |
| `MISSIONS_DIR` | `Assets/ASTRA/Content/Missions` | Server copy of the mission files |

## Endpoints (CONTRACT v1.3)

- `GET /health`
- `POST /api/v1/coach/debrief` (max 256 KB, max 500 events): advisory debrief. `summary` is a server-written verdict ("Passed." / "Not passed.") plus the AI's advice, at most 600 chars; at most 3 hints of at most 200 chars. Empty `events` returns the `NO_EVENTS` shape without calling the AI.
- `POST /api/v1/coach/ask`: Practice-only question about the current step. Answers come only from the mission's own steps, at most 400 chars. Other phases return `400 INVALID_INPUT`.

Checks (both endpoints): `missionId` must be a known mission; event and result `missionId` and the result `sessionId` must match the request; ids match `^[a-z0-9_]{1,64}$` (`sessionId`: `^[A-Za-z0-9_-]{1,64}$`); questions are 1-300 chars after trimming. Violations return `400 INVALID_INPUT`. More than 10 requests per minute per IP returns `429 RATE_LIMITED` with `Retry-After`. AI failure or invalid output (after one retry) returns `503 UPSTREAM_UNAVAILABLE`; the client falls back to the offline coach.

## Real AI mode

1. Put `GEMINI_API_KEY` and `GEMINI_MODEL` in `services/coach-proxy/.env` (git-ignored) or the process environment.
2. Set `USE_MOCK_AI=false` and restart. Without a key or model the endpoints return `503 UPSTREAM_UNAVAILABLE` ("AI coach is not configured.").
3. Set `USE_MOCK_AI=true` to switch back to the mock.

The AI never decides pass/fail and never changes scoring. The debrief prompt contains only the server's mission data, the scored result and mistake lists, never client free text. In Ask, the trainee's question is passed as JSON-escaped data inside tags, with the instructions in a separate system instruction. Logs contain only model, latency and outcome, never questions, payloads or the key.

Consent: before the first Ask, the Unity client must show "Your question is sent to Google Gemini. Do not include personal information."

Cost: each call is one short request to a flash-lite model (a few hundred input tokens, at most 512 output tokens), so a demo session costs a fraction of a cent. The per-IP rate limit and the 300-char question cap bound abuse.

## Test

Windows: `.venv\Scripts\python.exe -m pytest -q`
macOS / Linux: `.venv/bin/python -m pytest -q`

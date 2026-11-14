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

Optional config in env vars or `services/coach-proxy/.env` (see repo-root `.env.example`).
`USE_MOCK_AI` defaults to true. With `false`, the endpoint returns 503 until the Gemini stage lands.

Endpoints: `GET /health`, `POST /api/v1/coach/debrief` (max 256 KB, max 500 events).

## Test

Windows: `.venv\Scripts\python.exe -m pytest -q`
macOS / Linux: `.venv/bin/python -m pytest -q`

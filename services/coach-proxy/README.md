# coach-proxy (P1, optional)

Small backend that holds the Gemini key and turns **structured session facts** into an advisory debrief.
Implements `CONTRACT.md` §5. Stack not chosen yet — decide at kickoff.

Rules (from the PRD):
- The Unity client never holds the API key (NFR-SEC-02).
- Input is the deterministic `SessionResult` + event trace only. No free-text user data, no camera/voice.
- Output is advisory text. It cannot change simulation state or scoring (FR-SIM-04, FR-AI-04).
- `USE_MOCK_AI=true` returns `fixtures/coach-debrief.ok.json` so the app works without Gemini (FR-AI-01).

# Level simulator (browser)

Plays any mission file in a browser: Briefing → Demonstration → Practice (hints) → Qualification (scored) → Debrief.
Use it to test **content, step order, scoring and the coach** before the Unity project exists.

> **Browser simulator, not headset evidence.** It does not test VR interaction, reach, tracking or comfort.

## Run
From the repo root:
```
python tools/level-sim/server.py          # http://127.0.0.1:8090
```
Optional, for the AI coach panel: start the coach proxy (`services/coach-proxy`, port 8787). Without it, the debrief shows "unavailable" and the offline coach still works.

## What is in here
| File | Purpose |
|---|---|
| `scoring.js` | Reference implementation of CONTRACT.md §4–§5 (step matching, runner, scorer). Same rules as `scripts/validate_content.py`; the C# Core must match it. |
| `coach.js` | Reference offline coach (CONTRACT.md §9). The Unity `OfflineCoach` must produce identical text. |
| `index.html` | The simulator UI. The control panel is generated from the mission's `targetId`s plus three decoy controls for testing mistakes. |
| `server.py` | Stdlib server: serves the page, lists missions, forwards coach requests to the proxy. |
| `test_scoring.mjs` | Proves `scoring.js` and `coach.js` against every fixture. |

## Test
```
node --test tools/level-sim/test_scoring.mjs
```

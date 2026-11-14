# Instructor dashboard

An offline page that turns ASTRA Academy session logs into a cohort view for instructors. Open `index.html` in any
browser: no server, no build step, no network. Open `index.html#sample` to start with the sample data loaded.

## What it shows

- **Cohort overview:** attempts, trainees, pass rate, average score and time, medals, and a row per level.
- **Where trainees struggle:** the most-missed critical steps, the most out-of-order steps and the most-pressed wrong
  controls, each with the mission's instruction text (for wrong controls: the step the trainee was on).
- **Attempts:** pick one to see every event on a time axis (correct, out of order, wrong control, no effect) and its
  score breakdown.
- **Export CSV** of the attempts shown (filters apply). Cells that start with `=`, `+`, `-` or `@` are escaped, so a
  spreadsheet never runs them as formulas.

## Input

Drag in session log files (`.json`), the ones `LevelController` writes to `persistentDataPath/SessionLogs/`:
`{ sessionId, missionId, result, events }` (CONTRACT §1 and §5), plus an optional free-text `callsign`. A file may also
hold an array of logs. The same `sessionId` is counted once. Nothing else is read, and nothing leaves the computer.

Scores and medals follow [docs/SCORING.md](../../docs/SCORING.md), the same rules as `ResultRating` in Core.

## Files

| File | What |
|---|---|
| `index.html`, `style.css`, `app.js` | The page |
| `aggregate.js` | Pure functions (loading, scoring, replay, aggregation, CSV); used by the page and by the tests |
| `data/missions.js` | Mission titles and steps, generated from `Assets/ASTRA/Content/Missions` |
| `sample/*.json`, `sample/sample-data.js` | 24 fictional attempts by 6 fictional callsigns |
| `make-sample.mjs` | Regenerates `data/` and `sample/`: `node tools/instructor-dashboard/make-sample.mjs` |
| `test.mjs` | Checks: `node tools/instructor-dashboard/test.mjs` |

The sample attempts are played through the reference runner and scorer in `tools/level-sim/scoring.js` (which
`tools/level-sim/test_scoring.mjs` proves equal to the C# scorer), so each result is exactly what the app would save.
Rerun `make-sample.mjs` after changing a mission file. `test.mjs` checks the dashboard against its own copy of the
missions, so mission text edits never break it. It fails only if a mission's step structure (ids, order, critical,
event, target, all fixed by CONTRACT §2) no longer matches the source; changed wording only prints a note.

`package.json` marks this folder as CommonJS so `aggregate.js` loads in Node; `npm test` and `npm run sample` work
too, but nothing needs installing.

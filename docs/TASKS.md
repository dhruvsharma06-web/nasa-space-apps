# TASKS (final sprint)

Checkpoints: **C1** 03:00 · **C2** 10:00 · **C3** 14:00 · **Freeze** 17:00 · submit at deadline −2 h.
Owners: P1 XR & Interaction · P2 Core · P3 Content, AI coach & team lead · P4 Levels L5/L6, QA & ship.

| ID | Task | Owner | Due | Acceptance check |
|---|---|---|---|---|
| S1 | Phone gaze rig (`IGazeTarget`, `GazeInteractor`, reticle, tap + dwell, recenter) | P1 | C1 editor / C2 phone | Full L3 run on the Irusu phone via gaze + tap |
| S2 | Android build pipeline (`ASTRA → Build Android APK`) | P3 | C2 | APK installs and starts on the phone |
| S3 | Hub scene: level cards, previews, locks, academy score, "Demo: unlock all" | P1 | C2 | PlayMode test: 8 cards, locks respected, unlock-all works |
| S4 | L0 Safety & Calibration + L1 Orientation (mission + layout) | P1 | C1 data / C2 play | Validator OK; playthrough test passes |
| S5 | L4 Emergency + L7 Finale layouts, views, alarm + lights drama | P2 | C1 data / C2 play | Playthrough test passes; trace-replay tests |
| S6 | `ProgressService` + browser persistence | P2 | C2 | Pass L2, reload page, L3 still unlocked |
| S7 | Score 0–100 + Gold/Silver/Bronze (`ResultRating`, `docs/SCORING.md`) | P2 | C2 | Rule tests; perfect run = 100 Gold |
| S8 | Next level / Retry / Hub flow, pause menu, progress wiring, graduation certificate | P3 + P1 (UI) | C2 | Playthrough test continues into the next level |
| S9 | Debrief score reveal + breakdown + medal | P1 | C3 | Matches `ScoreBreakdown`; readable on the phone |
| S10 | Coach in Unity: offline coach, HTTP client with fallback, Ask (Practice only), debrief advice | P3 | C3 | Works with the network off; never changes the score |
| S11 | Cinematic visuals: NASA Blue Marble Earth + clouds + atmosphere, star sky, sun flare, cabin dressing, mobile tier | P3 + P1 (art) | C3 | Screenshots reviewed; ≥ 60 fps browser, ≥ 50 fps phone |
| S12 | Audio cues | P1 (clips) + P3 (hooks) | C3 | Every action has a sound; credits listed |
| S13 | Instructor dashboard (offline HTML, sample data, CSV) + session log download | P2 + P3 | C3 | Loads sample data; aggregation tests |
| S14 | L5/L6 text + view scripts | P4 | C2 | Validator OK; indicators react |
| S15 | CI (validator, scoring tests, proxy tests) | P4 | C2 | Green on a PR |
| S16 | Public web link (GitHub Pages) + APK release | P4 + P3 | C3 | Incognito test passes |
| S17 | QA passes + first-timer test | P4 | C2, C3, 16:00 | `docs/QA.md` bug table, no open S1/S2 |
| S18 | README, CREDITS, SUBMISSION, video, slides, trailer | P4 + P3 | 20:00 | Submitted; every link opens |

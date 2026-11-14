# QA: browser build

Owner: P4. Passes: **#1 at C2 (10:00)**, **#2 at C3 (14:00)**, **final at 16:00**.
After each pass, post the bug table in the team chat and tag each bug's owner.

Rules:
- Test the build link the team lead sends (local `http://127.0.0.1:8095` or the GitHub Pages link).
  Write down **which one**, plus the build commit shown on the landing page or the HUD.
- Browser results are **not** phone-headset results. Phone checks go in `docs/STATUS.md` (device matrix).
- Only write "pass" for what you actually did. Anything not tried is "not run".

---

## 1. Smoke script (do it for **every** level)

| # | Step | Expected |
|---|---|---|
| 1 | Welcome → **Start** | Briefing card shows the mission goal; nothing overlaps |
| 2 | Watch the **demonstration** | Controls light up in order; the camera never moves by itself |
| 3 | **Practice**: make **one deliberate mistake** (use a decoy or the wrong control) | Mistake feedback (sound + visual); a **hint** appears; the checklist doesn't tick |
| 3b | Press **Q** (Ask) during Practice | The coach answers about the current step (AI, or the offline answer if the AI is off). Consent text shown before the first question |
| 4 | **Qualification**: finish with no help | No glow or hints; every control still reachable from the seat |
| 5 | **Debrief** | Score 0–100 + medal appear; the breakdown matches what you did (your mistake is named); coach advice shown |
| 6 | **Next level** | Loads the next level; no hitch longer than ~1 s |

Then, once:
- [ ] **Hub**: locked levels can't be started; unlocked ones can.
- [ ] **"Demo: unlock all"** unlocks every card.
- [ ] Pass a level → **reload the page** → the next level is still unlocked (progress saved).
- [ ] After **L7**: the graduation certificate appears.
- [ ] **Pause** (P) and **Recenter** (R) work in every phase; Retry and Hub are reachable from the Debrief.

## 2. Also check (every pass)

- [ ] Text is readable at normal window size (no squinting, no clipping).
- [ ] Nothing overlaps, floats in mid-air or clips into a panel; no magenta (missing-shader) surfaces.
- [ ] **No flashing faster than 3 per second** (alarms included).
- [ ] Sound levels are balanced: no cue much louder than the others, and none silent.
- [ ] **You can never get stuck**: every screen has a way back or forward.
- [ ] Frame rate feels smooth (target 60 fps on a laptop). If not, note the level and what was on screen.
- [ ] Every action gives feedback: a sound, a visual state change and a checklist update.
- [ ] Footer/landing says "Fictional training, not affiliated with NASA"; no NASA logo anywhere.

## 3. First-timer test (very important)

Give the browser build to someone who has **never seen it**. Say nothing, and don't point.
Write down **every moment they hesitate or ask a question**. Each one is an **S2** bug.

| Time | Level / screen | What they did or asked | Likely cause |
|---|---|---|---|
| | | | |

Tester (first name or "tester A", no other personal details), browser, date: ____

## 4. Severity

| Level | Meaning | Example |
|---|---|---|
| **S1 blocker** | A level or the demo can't be finished, crash, wrong score | Control unreachable; debrief score differs from what happened |
| **S2 major** | A first-timer gets confused or stuck for a while; a clear feature is broken | Hint never appears; Next level button missing |
| **S3 minor** | Noticeable but doesn't block or confuse | Label slightly clipped; sound a bit loud |
| **S4 cosmetic** | Polish | Alignment off by a few pixels |

Release rule (TASKS S17): **no open S1/S2** at the freeze (17:00).

## 5. Bug table

Owners: P1 XR/UI/Hub/L0/L1/art/audio · P2 Core/L4/L7/progress/score/dashboard · P3 factory/LevelController/coach/L2/L3/build · P4 L5/L6/web page/README.

| ID | level | browser | steps to reproduce | expected | actual | severity | owner | status |
|---|---|---|---|---|---|---|---|---|
| QA-001 | | | | | | | | open |

## 6. Pass log

| Pass | When | Build (link + commit) | Browsers | Levels covered | Result |
|---|---|---|---|---|---|
| Pre-check | 2026-11-14 night | `main` @ `204da01` (content only, no build) | — | L2–L7 mission text | Every instruction ≤ 15 words and every hint ≤ 25 words (style guide). `validate_content.py` OK. **No build tested yet.** |
| #1 (C2) | | | Chrome, Edge | | not run |
| #2 (C3) | | | Chrome, Edge, incognito, phone browser | | not run |
| Final (16:00) | | | | | not run |

### Live-link test (C3, GitHub Pages)

| Browser | Cold cache (incognito)? | Time to playable | Works? | Notes |
|---|---|---|---|---|
| Chrome (laptop) | yes | | | |
| Edge (laptop) | yes | | | |
| Phone browser | yes | | | If it's too heavy, the landing page already says "On phones, use the APK" |

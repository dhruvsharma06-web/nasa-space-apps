# Demo runbook

Read this before any demo, judging session or recording. The checks take about 5 minutes.

## Links

| What | Link |
|---|---|
| Play in the browser | https://dhruvsharma06-web.github.io/nasa-space-apps/ (live once the `gh-pages` branch is pushed and GitHub Pages is on) |
| Android APK | https://github.com/dhruvsharma06-web/nasa-space-apps/releases/latest |
| AI coach server, health check | https://nasa-space-apps-s7jq.onrender.com/health |

## 1. Wake the AI coach (5 minutes before)

The coach server runs on Render's free plan:
- It **goes to sleep after about 15 minutes** without requests.
- The first request after that takes **about a minute** (Render says "50 seconds or more").

The app only **waits 3 seconds** for the coach, once, **when a level starts**. If the server is asleep, that level quietly uses the
**offline coach**. Nothing breaks and scores never change, but the AI advice doesn't appear.

Before the demo:
1. Open https://nasa-space-apps-s7jq.onrender.com/health in any browser.
2. Wait until the page shows `{"status":"ok"}`. The first load can take about a minute; if it times out, reload it.
3. Only then start the app or load a level. A level that started while the server was asleep stays offline; load the level
   again to pick up the AI coach.
4. If more than 15 minutes pass without anyone using the coach (for example between judging slots), repeat step 1.

## 2. Check that the AI coach is on

**In the app:**
- **Debrief:** the advice heading reads **"AI coach (advisory)"**. With the offline coach, it just reads **"Coach"**.
- **Browser, Practice phase:** in the Flight Instructor chat (press **Q**), each answer is tagged **AI** (green) or **Offline** (orange).

**From a terminal** (repo root, Python 3.10+):
```
python services/coach-proxy/deploy/smoke_test.py https://nasa-space-apps-s7jq.onrender.com --origin https://dhruvsharma06-web.github.io
```
Expect 4 `PASS` lines showing `model=gemini-3.5-flash-lite`, then `All checks passed.` The test uses 2 AI calls.

## 3. If the coach doesn't work

The demo still works: the app falls back to the offline coach on its own. Fix the coach after the demo.

| Symptom | Likely cause | Fix |
|---|---|---|
| The health page still doesn't load after 2 minutes | Service suspended, or the last deploy failed | Render dashboard → the service → Events and Logs |
| Smoke test: `debrief` or `ask` fails with HTTP 503 | Gemini key missing, wrong, revoked or out of quota | Render → the service → Environment → `GEMINI_API_KEY`; check the key in Google AI Studio |
| HTTP 429 | More than 10 coach requests per minute from one network | Wait a minute |
| Browser shows **Offline** while the phone shows **AI** | The page isn't served from `https://dhruvsharma06-web.github.io` (e.g. `localhost`) | Expected: the server only accepts that site (CORS) |

**The Gemini key lives only in Render → Environment.** Never put it in a file, commit, chat or screenshot. If it leaks, revoke it
in Google AI Studio first, then paste a new one into Render.

## 4. Device checklist

- **Phone:**
  - charged, with the latest APK installed;
  - notifications silenced and brightness up.
- **Hub → Settings → "Demo: unlock all"**, so every level can be opened.
- **Phone controls:**
  - look at a control until its ring grows, then tap the screen (or the headset button);
  - **hold the tap for 1 s to recenter**;
  - tools: tap to pick up, then tap the bay to place.
- **Browser controls:**
  - mouse to look and click;
  - **R** recenter, **P** pause, **Q** ask the instructor (Practice only).
- **Good levels to show:**
  - **L0 Safety:** the first-time flow;
  - **L4 Emergency:** the power fault; a clean run should score 100, Gold.
  - **L8 Launch Day:** the cinematic finale, from the launch pad to orbit. Turn the phone volume up and **tell the
    viewer to look up at the window**: the launch tower drops away at liftoff, then come the clouds and staging.
    Then a cabin fire breaks out (alarm, red light): grab the red FIRE EXT by your left knee and tap the FIRE. Then
    comes the Earth's curve.
- **After a new level or mission ships**, redeploy the hosted coach (Render → the service → Manual Deploy → Deploy
  latest commit). It only accepts missions it has a copy of, and until it's redeployed the new level's debrief uses the
  offline coach.
- **Known limit:** the phone build runs at **30 fps** (Unity's default cap on Android; measured on a Nothing Phone A059 on
  2026-11-15). Don't claim more. 60 fps would need `Application.targetFrameRate = 60` in the phone tier of
  `Assets/ASTRA/App/PlatformQuality.cs` and a check on the phone.

## 5. Changing the coach server

- **Where the address lives:** the app reads it from `CoachProxyUrl` in `Assets/ASTRA/App/Editor/AstraSceneBuilder.cs`. The
  scene builder bakes it into every level.
- **After changing it, rebuild both:**
  - the APK: `ASTRA → Build Android APK (phone)`;
  - the web build: `ASTRA → Build WebGL test build`.

  Both menus rebuild the scenes first.
- **To use a local proxy instead:** set `http://127.0.0.1:8787`. Deployment steps for a new server are in
  [services/coach-proxy/deploy/README.md](../services/coach-proxy/deploy/README.md).

## 6. After the hackathon

- **Shut it down:** in Render, the service → Settings → Suspend. Then revoke the Gemini key in Google AI Studio.
- **Access:** the Render service and the Gemini key belong to P2. Ask P2 for dashboard access or a new key.

# Deploying the coach proxy (Render, free plan)

Goal: the public web build (GitHub Pages) can call the AI coach. If it isn't running by **12:00**, stop:
the web build uses the offline coach, which is fine.

**Why Render:** the free web-service plan needs no credit card, deploys from GitHub, and reads its
settings from [`render.yaml`](render.yaml). (Cloud Run needs a billing account; Fly.io needs a card.)

**Free-plan limits:**
- The service **sleeps after ~15 minutes idle**, and the first request then takes about a minute.
  The app's coach call times out sooner and shows the offline coach. Before a demo or judging
  session, open `https://<service>.onrender.com/health` once to wake it.
- The rate limit is in memory: it resets on restart and isn't shared between instances (the free
  plan runs one).

## Settings (all in `render.yaml`)

| Variable | Value | Notes |
|---|---|---|
| `USE_MOCK_AI` | `false` | Real AI mode. Without a key, the coach endpoints return 503 and the app falls back to the offline coach |
| `GEMINI_API_KEY` | **set by the team lead in the Render dashboard only** | `sync: false`: never in a file, chat, commit or screenshot |
| `GEMINI_MODEL` | `gemini-3.5-flash-lite` | Pinned model id from the proxy README |
| `COACH_ALLOWED_ORIGIN` | `https://dhruvsharma06-web.github.io` | CORS: only our GitHub Pages site (an origin has no path) |
| `RATE_LIMIT_PER_MIN` | `10` | Built-in per-IP limit; keep it on |
| `AI_TIMEOUT_MS` | `6000` | Upstream timeout |
| `PYTHON_VERSION` | `3.12.8` | Same major version as CI |

The start command adds `--proxy-headers --forwarded-allow-ips="*"` so the per-IP rate limit sees
each user's address behind Render's load balancer. Without it, every user shares one 10/min bucket.
The trade-off: a client could send a fake `X-Forwarded-For` to dodge the limit. That's acceptable
for a demo with a flash-lite model and a 300-character question cap.

> **CORS and localhost:** the proxy accepts exactly **one** origin today (`COACH_ALLOWED_ORIGIN`).
> To also allow `http://localhost:8095` / `http://127.0.0.1:8095` for browser testing, the proxy needs
> a small change in `app/main.py`/`app/settings.py`: split the value on commas. That code is owned
> by the team lead (P3); see the P4 stage report. Command-line tests (`smoke_test.py`, `curl`) don't
> need CORS.

## Steps

1. **Test locally in mock mode first** (from the repo root, after installing the proxy, see `../README.md`):
   ```bash
   cd services/coach-proxy
   USE_MOCK_AI=true COACH_ALLOWED_ORIGIN=https://dhruvsharma06-web.github.io .venv/bin/uvicorn app.main:app --port 8787 --proxy-headers
   # second terminal, repo root:
   python services/coach-proxy/deploy/smoke_test.py http://127.0.0.1:8787 --origin https://dhruvsharma06-web.github.io
   ```
   Windows PowerShell: `$env:USE_MOCK_AI="true"; $env:COACH_ALLOWED_ORIGIN="https://dhruvsharma06-web.github.io"; .venv\Scripts\python.exe -m uvicorn app.main:app --port 8787 --proxy-headers`
2. `render.yaml` deploys from **`main`**, so this folder must be merged to `main` first.
3. Sign in at <https://dashboard.render.com> with the GitHub account that can see the repo, and give
   Render access to `dhruvsharma06-web/nasa-space-apps` only.
4. **New → Blueprint** → pick the repo → set **Blueprint Path** to
   `services/coach-proxy/deploy/render.yaml` → **Apply**.
   - Render asks for `GEMINI_API_KEY`. Leave it **empty** for the first deploy, so the service comes
     up keyless and we can test it before the key exists.
   - *If the Blueprint Path option isn't offered:* **New → Web Service** → the repo, branch `main`,
     Root Directory empty, Runtime Python 3, Build Command and Start Command copied from
     `render.yaml`, Instance Type **Free**, Health Check Path `/health`, then add each env var from
     the table above.
5. Wait for "Live". Copy the URL (`https://astra-coach-proxy.onrender.com` or similar).
6. **Hosted test, mock mode:** in Render → Environment, set `USE_MOCK_AI=true` → Save (it redeploys), then:
   ```bash
   python services/coach-proxy/deploy/smoke_test.py https://<service>.onrender.com --origin https://dhruvsharma06-web.github.io
   ```
   Expect 4× PASS with `model=coach-mock-0`.
7. **Real AI:** the **team lead** pastes the key into `GEMINI_API_KEY` in Render → Environment, and sets
   `USE_MOCK_AI=false` → Save. Run the same smoke test. Expect 4× PASS with
   `model=gemini-3.5-flash-lite` and real advice text.
   - `debrief`/`ask` FAIL with HTTP 503 means the key/model is missing or wrong. The app still works
     with the offline coach. Check Render → Logs (they never contain the key or the questions).
8. Give the URL to the team lead to set as the coach URL in the web build. Write it in `docs/STATUS.md`
   (team lead) and in the README.

## Rotating or removing the key
- Leaked? **Revoke it in Google AI Studio first**, then paste a new one in Render. Deleting it isn't enough.
- After the hackathon: suspend the service in Render (Settings → Suspend) and revoke the key.

"""Smoke-test a running coach proxy (local or hosted). Python 3.10+, standard library only.

    python services/coach-proxy/deploy/smoke_test.py https://<your-service>.onrender.com
    python services/coach-proxy/deploy/smoke_test.py http://127.0.0.1:8787 --origin https://dhruvsharma06-web.github.io

Checks:
  1. GET  /health                 -> 200 {"status": "ok"}
  2. POST /api/v1/coach/debrief   -> 200 with a non-empty "summary" (uses fixtures/session-*.ok.json)
  3. POST /api/v1/coach/ask       -> 200 with a non-empty "answer"  (Practice, l7 mission, first step)
  4. CORS (only with --origin): the preflight answers with Access-Control-Allow-Origin = that origin

Uses 2 coach calls, well inside the 10/min rate limit. Prints the modelVersion so you can see
whether the proxy is in mock mode ("coach-mock-0") or real AI mode. Sends no personal data.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
FIXTURES = REPO / "fixtures"
MISSIONS = REPO / "Assets" / "ASTRA" / "Content" / "Missions"


def call(method: str, url: str, body: dict | None = None, headers: dict | None = None, timeout: float = 90.0):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    if data is not None:
        req.add_header("Content-Type", "application/json")
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    t0 = time.monotonic()
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            raw = r.read().decode() or "null"
            return r.status, dict(r.headers), json.loads(raw) if raw.strip().startswith(("{", "[")) else raw, time.monotonic() - t0
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            payload = json.loads(raw)
        except ValueError:
            payload = raw
        return e.code, dict(e.headers), payload, time.monotonic() - t0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("base_url", help="e.g. https://astra-coach-proxy.onrender.com")
    ap.add_argument("--origin", help="Browser origin to check CORS for, e.g. https://<owner>.github.io")
    args = ap.parse_args()
    base = args.base_url.rstrip("/")
    failures = 0

    def report(name: str, ok: bool, detail: str) -> None:
        nonlocal failures
        failures += 0 if ok else 1
        print(f"{'PASS' if ok else 'FAIL'}  {name}: {detail}")

    # 1. Health (a sleeping free-tier instance can take ~1 minute to wake up).
    status, _, body, dt = call("GET", f"{base}/health")
    report("health", status == 200 and isinstance(body, dict) and body.get("status") == "ok",
           f"HTTP {status} in {dt:.1f}s -> {body}")

    # 2. Debrief with the contract's ok fixtures.
    events = json.loads((FIXTURES / "session-events.ok.json").read_text(encoding="utf-8"))
    result = json.loads((FIXTURES / "session-result.ok.json").read_text(encoding="utf-8"))
    debrief = {"sessionId": result["sessionId"], "missionId": result["missionId"], "result": result, "events": events}
    status, _, body, dt = call("POST", f"{base}/api/v1/coach/debrief", debrief)
    ok = status == 200 and isinstance(body, dict) and bool(body.get("summary"))
    detail = (f"HTTP {status} in {dt:.1f}s, model={body.get('modelVersion')}, summary={body.get('summary')!r}"
              if isinstance(body, dict) else f"HTTP {status} -> {body}")
    report("debrief", ok, detail)

    # 3. Ask about the first step of the same mission, in Practice.
    mission = json.loads((MISSIONS / f"{result['missionId']}.json").read_text(encoding="utf-8"))
    first = sorted(mission["steps"], key=lambda s: s["order"])[0]
    ask = {"sessionId": result["sessionId"], "missionId": mission["missionId"], "phase": "Practice",
           "currentStepId": first["stepId"], "question": "Where is this control and what should I check?"}
    status, _, body, dt = call("POST", f"{base}/api/v1/coach/ask", ask)
    ok = status == 200 and isinstance(body, dict) and bool(body.get("answer"))
    detail = (f"HTTP {status} in {dt:.1f}s, model={body.get('modelVersion')}, answer={body.get('answer')!r}"
              if isinstance(body, dict) and "answer" in body else f"HTTP {status} -> {body}")
    report("ask", ok, detail)

    # 4. CORS preflight, as a browser on the given origin would send it.
    if args.origin:
        status, headers, _, dt = call("OPTIONS", f"{base}/api/v1/coach/debrief", headers={
            "Origin": args.origin,
            "Access-Control-Request-Method": "POST",
            "Access-Control-Request-Headers": "content-type",
        })
        allowed = {k.lower(): v for k, v in headers.items()}.get("access-control-allow-origin")
        report("cors", status == 200 and allowed == args.origin,
               f"HTTP {status}, Access-Control-Allow-Origin={allowed!r} (wanted {args.origin!r})")

    if failures:
        print(f"\n{failures} check(s) failed. HTTP 503 UPSTREAM_UNAVAILABLE means real AI mode without a working "
              "key/model: the app falls back to the offline coach.")
    else:
        print("\nAll checks passed.")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())

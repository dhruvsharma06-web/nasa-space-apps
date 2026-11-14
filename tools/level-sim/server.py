#!/usr/bin/env python3
"""ASTRA Academy browser level simulator (local dev tool, standard library only).

Run from the repo root:  python tools/level-sim/server.py            (port 8090)
Open:                    http://127.0.0.1:8090
Optional coach proxy:    services/coach-proxy on http://127.0.0.1:8787 (set COACH_PROXY_URL to change)

This tests content, step flow, scoring and the coach in a browser. It is NOT headset evidence.
"""
from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
MISSIONS = REPO / "Assets" / "ASTRA" / "Content" / "Missions"
COACH_URL = os.environ.get("COACH_PROXY_URL", "http://127.0.0.1:8787").rstrip("/")
MAX_BODY = 262_144  # same limit as the coach proxy (CONTRACT.md §9)

STATIC = {  # whitelist: nothing else on disk is served
    "/": ("index.html", "text/html; charset=utf-8"),
    "/index.html": ("index.html", "text/html; charset=utf-8"),
    "/scoring.js": ("scoring.js", "text/javascript; charset=utf-8"),
    "/coach.js": ("coach.js", "text/javascript; charset=utf-8"),
}


def error_body(code: str, message: str) -> dict:
    return {"error": {"code": code, "message": message, "details": []}}


def list_missions() -> list[dict]:
    out = []
    for path in sorted(MISSIONS.glob("*.json")):
        try:
            out.append({"file": path.name, "mission": json.loads(path.read_text(encoding="utf-8"))})
        except (OSError, ValueError) as exc:
            out.append({"file": path.name, "error": f"could not read: {exc.__class__.__name__}"})
    return out


def forward_to_coach(body: bytes) -> tuple[int, dict]:
    req = urllib.request.Request(f"{COACH_URL}/api/v1/coach/debrief", data=body,
                                 headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=8) as r:
            return r.status, json.loads(r.read() or b"{}")
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read() or b"{}")
        except ValueError:
            return e.code, error_body("UPSTREAM_UNAVAILABLE", "Coach returned a non-JSON error.")
    except (urllib.error.URLError, OSError, ValueError):
        return 503, error_body("UPSTREAM_UNAVAILABLE", f"Coach proxy not reachable at {COACH_URL}.")


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):  # noqa: N802
        path = urlsplit(self.path).path
        if path in STATIC:
            name, ctype = STATIC[path]
            self.send(200, (HERE / name).read_bytes(), ctype)
        elif path == "/api/missions":
            self.send_json(200, {"missions": list_missions(), "coachUrl": COACH_URL})
        else:
            self.send_json(404, error_body("NOT_FOUND", "Not found."))

    def do_POST(self):  # noqa: N802
        if urlsplit(self.path).path != "/api/coach":
            self.send_json(404, error_body("NOT_FOUND", "Not found."))
            return
        try:
            length = int(self.headers.get("Content-Length") or 0)
        except ValueError:
            length = -1
        if length < 0 or length > MAX_BODY:
            self.send_json(413, error_body("PAYLOAD_TOO_LARGE", "Request body exceeds 256 KB."))
            return
        status, body = forward_to_coach(self.rfile.read(length))
        self.send_json(status, body)

    def send_json(self, code: int, obj: dict):
        self.send(code, json.dumps(obj).encode(), "application/json")

    def send(self, code: int, body: bytes, ctype: str):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *args):
        pass


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8090
    print(f"ASTRA level simulator on http://127.0.0.1:{port}  (coach: {COACH_URL})", flush=True)
    ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()

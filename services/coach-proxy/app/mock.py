import json
from pathlib import Path

from .models import CoachRequest


def _load(fixtures_dir: Path, name: str) -> dict:
    with open(fixtures_dir / name, encoding="utf-8") as f:
        return json.load(f)


def build_mock_response(request: CoachRequest, fixtures_dir: Path) -> dict:
    if not request.events:
        fx = _load(fixtures_dir, "coach-debrief.empty.json")
        return {
            "sessionId": request.sessionId,
            "missionId": request.missionId,
            "advisory": True,
            "summary": None,
            "hints": [],
            "reason": "NO_EVENTS",
            "modelVersion": fx["modelVersion"],
        }
    fx = _load(fixtures_dir, "coach-debrief.ok.json")
    return {
        "sessionId": request.sessionId,
        "missionId": request.missionId,
        "advisory": True,
        "summary": fx["summary"],
        "hints": list(fx["hints"]),
        "modelVersion": fx["modelVersion"],
    }

import json
from pathlib import Path


class MissionStore:
    """Server-side copy of the mission files, loaded once."""

    def __init__(self, missions_dir: Path):
        self._missions: dict[str, dict] = {}
        for path in sorted(Path(missions_dir).glob("*.json")):
            with open(path, encoding="utf-8") as f:
                mission = json.load(f)
            mission_id = mission.get("missionId")
            if isinstance(mission_id, str):
                self._missions[mission_id] = mission

    def get(self, mission_id: str) -> dict | None:
        return self._missions.get(mission_id)

    def __len__(self) -> int:
        return len(self._missions)


def find_step(mission: dict, step_id: str) -> dict | None:
    for step in mission.get("steps", []):
        if step.get("stepId") == step_id:
            return step
    return None

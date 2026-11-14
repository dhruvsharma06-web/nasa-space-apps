from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, StrictInt

NonEmptyStr = Annotated[str, Field(min_length=1)]

EventName = Literal[
    "calibration_completed",
    "target_located",
    "target_selected",
    "control_activated",
    "checklist_opened",
    "object_grabbed",
    "object_released",
    "object_placed",
    "status_reported",
    "paused",
    "resumed",
    "recentered",
]
InputModality = Literal[
    "controller_ray", "controller_grip", "hand_pinch", "hand_grab", "simulator"
]
EventResult = Literal["correct", "incorrect", "neutral"]


class _Strict(BaseModel):
    model_config = ConfigDict(extra="forbid")


class SessionEvent(_Strict):
    sessionId: NonEmptyStr
    missionId: NonEmptyStr
    timestampMs: Annotated[StrictInt, Field(ge=0)]
    event: EventName
    targetId: NonEmptyStr
    input: InputModality
    result: EventResult


class SessionResult(_Strict):
    sessionId: str
    missionId: str
    passed: bool
    completedSteps: list[str]
    skippedSteps: list[str]
    omittedCriticalSteps: list[str]
    sequenceErrors: list[str]
    incorrectActions: list[str]
    completionTimeMs: Annotated[StrictInt, Field(ge=0)]


class CoachRequest(_Strict):
    sessionId: NonEmptyStr
    missionId: NonEmptyStr
    result: SessionResult
    events: Annotated[list[SessionEvent], Field(max_length=500)]

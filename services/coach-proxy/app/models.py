from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, StrictInt, StringConstraints

NonEmptyStr = Annotated[str, Field(min_length=1)]
IdStr = Annotated[str, Field(pattern=r"^[a-z0-9_]{1,64}$")]
SessionIdStr = Annotated[str, Field(pattern=r"^[A-Za-z0-9_-]{1,64}$")]
Question = Annotated[str, StringConstraints(strip_whitespace=True, min_length=1, max_length=300)]
Phase = Literal["Briefing", "Demonstration", "Practice", "Qualification", "Debrief"]

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
    "controller_ray", "controller_grip", "hand_pinch", "hand_grab", "simulator", "gaze"
]
EventResult = Literal["correct", "incorrect", "neutral"]


class _Strict(BaseModel):
    model_config = ConfigDict(extra="forbid")


class SessionEvent(_Strict):
    sessionId: SessionIdStr
    missionId: NonEmptyStr
    timestampMs: Annotated[StrictInt, Field(ge=0)]
    event: EventName
    targetId: IdStr
    input: InputModality
    result: EventResult


class SessionResult(_Strict):
    sessionId: SessionIdStr
    missionId: str
    passed: bool
    completedSteps: list[IdStr]
    skippedSteps: list[IdStr]
    omittedCriticalSteps: list[IdStr]
    sequenceErrors: list[IdStr]
    incorrectActions: list[IdStr]
    completionTimeMs: Annotated[StrictInt, Field(ge=0)]


class CoachRequest(_Strict):
    sessionId: SessionIdStr
    missionId: NonEmptyStr
    result: SessionResult
    events: Annotated[list[SessionEvent], Field(max_length=500)]


class AskRequest(_Strict):
    sessionId: SessionIdStr
    missionId: NonEmptyStr
    phase: Phase
    currentStepId: IdStr | None
    question: Question

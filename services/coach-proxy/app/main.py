from typing import Callable

from fastapi import Depends, FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware

from .ask import offline_answer, screen
from .ai import AIUnavailable, CoachAI, GeminiCoach, OutputRejected
from .errors import error_response, install_error_handling
from .missions import MissionStore, find_step
from .mock import build_mock_response
from .models import AskRequest, CoachRequest
from .ratelimit import RateLimited, RateLimiter, client_key
from .settings import Settings

SUMMARY_MAX = 600
HINT_MAX = 200
HINT_COUNT = 3
ANSWER_MAX = 400


def cut_words(text: str, limit: int) -> str:
    """Trim to at most `limit` chars, cutting at a word boundary."""
    text = text.strip()
    if len(text) <= limit:
        return text
    head = text[:limit]
    if not text[limit].isspace():
        space = head.rfind(" ")
        if space > 0:
            head = head[:space]
    return head.rstrip()


def compose_debrief(req: CoachRequest, advice: str, hints: list[str], model: str) -> dict:
    prefix = "Passed. " if req.result.passed else "Not passed. "
    summary = prefix + cut_words(advice, SUMMARY_MAX - len(prefix))
    out_hints = []
    for h in hints:
        h = cut_words(h, HINT_MAX)
        if h:
            out_hints.append(h)
    return {
        "sessionId": req.sessionId,
        "missionId": req.missionId,
        "advisory": True,
        "summary": summary,
        "hints": out_hints[:HINT_COUNT],
        "modelVersion": model,
    }


def _invalid(field: str, reason: str, message: str = "Request validation failed."):
    return error_response(400, "INVALID_INPUT", message, [{"field": field, "reason": reason}])


def create_app(
    settings: Settings | None = None,
    ai: CoachAI | None = None,
    clock: Callable[[], float] | None = None,
) -> FastAPI:
    settings = settings or Settings()
    app = FastAPI(title="ASTRA coach proxy", version="1.3.0")

    install_error_handling(app)

    missions = MissionStore(settings.MISSIONS_DIR)
    limiter = RateLimiter(settings.RATE_LIMIT_PER_MIN, 60.0, clock)

    if ai is None and not settings.USE_MOCK_AI:
        key = settings.GEMINI_API_KEY.get_secret_value() if settings.GEMINI_API_KEY else ""
        if key and settings.GEMINI_MODEL:
            ai = GeminiCoach(key, settings.GEMINI_MODEL, settings.AI_TIMEOUT_MS)

    @app.exception_handler(RateLimited)
    async def _rate_limited(request: Request, exc: RateLimited):
        resp = error_response(
            429, "RATE_LIMITED", "Too many coach requests. Try again in a minute."
        )
        resp.headers["Retry-After"] = str(exc.retry_after)
        return resp

    def rate_limit(request: Request) -> None:
        limiter.check(client_key(request))

    if settings.COACH_ALLOWED_ORIGIN:
        app.add_middleware(
            CORSMiddleware,
            allow_origins=[settings.COACH_ALLOWED_ORIGIN],
            allow_methods=["POST", "GET"],
            allow_headers=["Content-Type"],
        )

    @app.get("/health")
    def health() -> dict:
        return {"status": "ok"}

    def unavailable(message: str = "The AI coach is unavailable. Your scripted debrief is shown instead."):
        return error_response(503, "UPSTREAM_UNAVAILABLE", message)

    @app.post("/api/v1/coach/debrief", dependencies=[Depends(rate_limit)])
    def debrief(request: CoachRequest):
        mission = missions.get(request.missionId)
        if mission is None:
            return _invalid("missionId", "unknown mission")
        if request.result.missionId != request.missionId:
            return _invalid("result.missionId", "must match missionId")
        if request.result.sessionId != request.sessionId:
            return _invalid("result.sessionId", "must match sessionId")
        for i, ev in enumerate(request.events):
            if ev.missionId != request.missionId:
                return _invalid(f"events.{i}.missionId", "must match missionId")

        if settings.USE_MOCK_AI:
            return build_mock_response(request, settings.FIXTURES_DIR)
        if not request.events:
            # Real mode, nothing to explain: same NO_EVENTS shape, but never labelled as mock.
            return {**build_mock_response(request, settings.FIXTURES_DIR), "modelVersion": settings.GEMINI_MODEL}

        if ai is None:
            return unavailable("AI coach is not configured.")
        try:
            advice, hints = ai.debrief(mission, request)
        except OutputRejected:
            # empty/off-topic model text: deterministic offline debrief, labelled as such
            return build_mock_response(request, settings.FIXTURES_DIR)
        except AIUnavailable:
            return unavailable()
        return compose_debrief(request, advice, hints, settings.GEMINI_MODEL)

    @app.post("/api/v1/coach/ask", dependencies=[Depends(rate_limit)])
    def ask(request: AskRequest):
        mission = missions.get(request.missionId)
        if mission is None:
            return _invalid("missionId", "unknown mission")
        if request.phase != "Practice":
            return _invalid(
                "phase", "must be Practice", "Ask the coach is only available during Practice."
            )
        step = None
        if request.currentStepId is not None:
            step = find_step(mission, request.currentStepId)
            if step is None:
                return _invalid("currentStepId", "not a step of this mission")

        refusal = screen(request.question)
        if refusal is not None:
            answer, model = refusal, "coach-mock-0"
        elif settings.USE_MOCK_AI:
            answer = offline_answer(mission, step, request.question)
            model = "coach-mock-0"
        else:
            if ai is None:
                return unavailable("AI coach is not configured.")
            try:
                answer = cut_words(ai.ask(mission, step, request.question), ANSWER_MAX)
                model = settings.GEMINI_MODEL
            except OutputRejected:
                answer = offline_answer(mission, step, request.question)
                model = "coach-mock-0"
            except AIUnavailable:
                return unavailable()
        return {
            "sessionId": request.sessionId,
            "missionId": request.missionId,
            "advisory": True,
            "answer": answer,
            "stepId": request.currentStepId,
            "modelVersion": model,
        }

    return app


app = create_app()

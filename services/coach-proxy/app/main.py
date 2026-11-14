from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from .errors import error_response, install_error_handling
from .mock import build_mock_response
from .models import CoachRequest
from .settings import Settings


def create_app(settings: Settings | None = None) -> FastAPI:
    settings = settings or Settings()
    app = FastAPI(title="ASTRA coach proxy", version="1.0.0")

    install_error_handling(app)

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

    @app.post("/api/v1/coach/debrief")
    def debrief(request: CoachRequest):
        if settings.USE_MOCK_AI:
            return build_mock_response(request, settings.FIXTURES_DIR)
        return error_response(
            503, "UPSTREAM_UNAVAILABLE", "AI coach is not configured yet."
        )

    return app


app = create_app()

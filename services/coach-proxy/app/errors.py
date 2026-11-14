import logging
from typing import Any

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

MAX_BODY_BYTES = 262144

logger = logging.getLogger("coach-proxy")


def error_body(code: str, message: str, details: list[dict[str, Any]] | None = None) -> dict:
    return {"error": {"code": code, "message": message, "details": details or []}}


def error_response(
    status: int, code: str, message: str, details: list[dict[str, Any]] | None = None
) -> JSONResponse:
    return JSONResponse(status_code=status, content=error_body(code, message, details))


def _field(loc: tuple) -> str:
    parts = [str(p) for p in loc if p != "body"]
    return ".".join(parts) if parts else "body"


async def _validation_handler(request: Request, exc: RequestValidationError):
    details = [
        {"field": _field(tuple(e.get("loc", ()))), "reason": str(e.get("msg", "invalid"))}
        for e in exc.errors()
    ]
    return error_response(400, "INVALID_INPUT", "Request validation failed.", details)


async def _http_handler(request: Request, exc: StarletteHTTPException):
    if exc.status_code == 404:
        return error_response(404, "NOT_FOUND", "Not found.")
    if exc.status_code == 405:
        return error_response(405, "INVALID_INPUT", "Method not allowed.")
    if exc.status_code >= 500:
        return error_response(500, "INTERNAL", "Internal server error.")
    return error_response(exc.status_code, "INVALID_INPUT", "Request rejected.")


async def _unhandled_handler(request: Request, exc: Exception):
    logger.error("Unhandled error on %s", request.url.path, exc_info=exc)
    return error_response(500, "INTERNAL", "Internal server error.")


class BodyLimitMiddleware:
    """Pure ASGI middleware: rejects oversized bodies before JSON parsing."""

    def __init__(self, app, max_bytes: int = MAX_BODY_BYTES):
        self.app = app
        self.max_bytes = max_bytes

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return

        too_large = error_response(
            413, "PAYLOAD_TOO_LARGE", "Request body exceeds 256 KB."
        )

        for name, value in scope.get("headers", []):
            if name == b"content-length":
                try:
                    if int(value) > self.max_bytes:
                        await too_large(scope, receive, send)
                        return
                except ValueError:
                    pass

        chunks: list[bytes] = []
        total = 0
        more = True
        while more:
            message = await receive()
            if message["type"] == "http.disconnect":
                return
            body = message.get("body", b"")
            total += len(body)
            if total > self.max_bytes:
                await too_large(scope, receive, send)
                return
            chunks.append(body)
            more = message.get("more_body", False)

        replay = [b"".join(chunks)]
        sent = False

        async def replay_receive():
            nonlocal sent
            if not sent:
                sent = True
                return {"type": "http.request", "body": replay[0], "more_body": False}
            return await receive()

        await self.app(scope, replay_receive, send)


def install_error_handling(app: FastAPI) -> None:
    app.add_exception_handler(RequestValidationError, _validation_handler)
    app.add_exception_handler(StarletteHTTPException, _http_handler)
    app.add_exception_handler(Exception, _unhandled_handler)
    app.add_middleware(BodyLimitMiddleware)

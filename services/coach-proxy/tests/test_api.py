import json
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.main import create_app
from app.settings import Settings

FIXTURES = Path(__file__).resolve().parents[3] / "fixtures"
URL = "/api/v1/coach/debrief"


def load(name):
    return json.loads((FIXTURES / name).read_text(encoding="utf-8"))


@pytest.fixture
def client():
    return TestClient(create_app(Settings(USE_MOCK_AI=True)), raise_server_exceptions=False)


@pytest.fixture
def payload():
    result = load("session-result.ok.json")
    events = load("session-events.ok.json")
    return {
        "sessionId": result["sessionId"],
        "missionId": result["missionId"],
        "result": result,
        "events": events,
    }


def assert_error_shape(body, code):
    assert set(body.keys()) == {"error"}
    assert set(body["error"].keys()) == {"code", "message", "details"}
    assert body["error"]["code"] == code
    assert isinstance(body["error"]["details"], list)


def test_health(client):
    r = client.get("/health")
    assert r.status_code == 200
    assert r.json() == {"status": "ok"}


def test_ok(client, payload):
    r = client.post(URL, json=payload)
    assert r.status_code == 200
    body = r.json()
    assert set(body.keys()) == set(load("coach-debrief.ok.json").keys())
    assert body["sessionId"] == payload["sessionId"]
    assert body["missionId"] == payload["missionId"]
    assert body["advisory"] is True


def test_session_id_echoed(client, payload):
    payload["sessionId"] = "other-session-xyz"
    r = client.post(URL, json=payload)
    assert r.status_code == 200
    assert r.json()["sessionId"] == "other-session-xyz"


def test_empty_events(client, payload):
    payload["events"] = []
    r = client.post(URL, json=payload)
    assert r.status_code == 200
    body = r.json()
    assert set(body.keys()) == set(load("coach-debrief.empty.json").keys())
    assert body["reason"] == "NO_EVENTS"
    assert body["summary"] is None
    assert body["hints"] == []
    assert body["advisory"] is True


def test_unknown_top_level_field(client, payload):
    payload["extra"] = 1
    r = client.post(URL, json=payload)
    assert r.status_code == 400
    assert_error_shape(r.json(), "INVALID_INPUT")


def test_bad_enum(client, payload):
    payload["events"][0]["event"] = "teleport"
    r = client.post(URL, json=payload)
    assert r.status_code == 400
    body = r.json()
    assert_error_shape(body, "INVALID_INPUT")
    assert any("event" in d["field"] for d in body["error"]["details"])


def test_negative_timestamp(client, payload):
    payload["events"][0]["timestampMs"] = -1
    r = client.post(URL, json=payload)
    assert r.status_code == 400
    assert_error_shape(r.json(), "INVALID_INPUT")


def test_bool_timestamp_rejected(client, payload):
    payload["events"][0]["timestampMs"] = True
    assert client.post(URL, json=payload).status_code == 400


def test_too_many_events(client, payload):
    payload["events"] = [payload["events"][0]] * 501
    r = client.post(URL, json=payload)
    assert r.status_code == 400
    assert_error_shape(r.json(), "INVALID_INPUT")


def test_payload_too_large(client, payload):
    payload["junk"] = "x" * 300_000
    r = client.post(URL, json=payload)
    assert r.status_code == 413
    assert_error_shape(r.json(), "PAYLOAD_TOO_LARGE")


def test_payload_too_large_no_content_length(client):
    def gen():
        yield b"x" * 200_000
        yield b"x" * 100_000

    r = client.post(URL, content=gen(), headers={"Content-Type": "application/json"})
    assert r.status_code == 413
    assert_error_shape(r.json(), "PAYLOAD_TOO_LARGE")


def test_real_mode_unavailable(payload):
    c = TestClient(create_app(Settings(USE_MOCK_AI=False)))
    r = c.post(URL, json=payload)
    assert r.status_code == 503
    body = r.json()
    assert_error_shape(body, "UPSTREAM_UNAVAILABLE")
    assert body["error"]["message"] == "AI coach is not configured yet."


def test_internal_error_shape(payload):
    app = create_app(Settings(USE_MOCK_AI=True, FIXTURES_DIR=Path("/nonexistent")))
    c = TestClient(app, raise_server_exceptions=False)
    r = c.post(URL, json=payload)
    assert r.status_code == 500
    assert_error_shape(r.json(), "INTERNAL")


def test_malformed_json_error_shape(client):
    r = client.post(URL, content=b"{not json", headers={"Content-Type": "application/json"})
    assert r.status_code == 400
    assert_error_shape(r.json(), "INVALID_INPUT")


def test_unknown_route_returns_not_found_contract_shape():
    """CONTRACT v1.2 §10: unknown routes return 404 NOT_FOUND in the standard error shape."""
    from fastapi.testclient import TestClient
    from app.main import create_app

    response = TestClient(create_app()).get("/api/v1/does-not-exist")
    assert response.status_code == 404
    body = response.json()
    assert set(body) == {"error"}
    assert set(body["error"]) == {"code", "message", "details"}
    assert body["error"]["code"] == "NOT_FOUND"

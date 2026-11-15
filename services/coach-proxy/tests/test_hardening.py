import json
from types import SimpleNamespace

import pytest
from fastapi.testclient import TestClient
from google.genai import types

from app.ai import (
    ASK_SYSTEM,
    ASK_WORD_CAP,
    DEBRIEF_SYSTEM,
    DEBRIEF_WORD_CAP,
    GeminiCoach,
    OutputRejected,
    build_ask_contents,
    build_debrief_contents,
    cap_words,
    strip_markdown,
)
from app.main import create_app
from app.models import CoachRequest
from app.settings import Settings
from tests.test_v13 import ASK, DEBRIEF, MODEL, FakeAI, ask_payload, load, payload  # noqa: F401

MISSION = {
    "title": "Cabin air",
    "steps": [
        {
            "stepId": "fan_low",
            "order": 1,
            "instruction": "Set the AIR FAN dial to LOW",
            "hint": "The AIR FAN dial is on the cabin panel",
            "critical": True,
            "secret": "must not leak",
        }
    ],
}


def resp(text):
    return SimpleNamespace(text=text, candidates=[SimpleNamespace(finish_reason=types.FinishReason.STOP)])


class FakeClient:
    def __init__(self, responses):
        self.responses, self.calls = list(responses), []
        self.models = SimpleNamespace(generate_content=self._gen)

    def _gen(self, **kwargs):
        self.calls.append(kwargs)
        return self.responses.pop(0)


def coach(*texts):
    return GeminiCoach("k", MODEL, 6000, client=FakeClient([resp(t) for t in texts])), None


def test_strip_markdown():
    raw = "## Title\n- **Good** work with `AIR FAN`\n* next\n1. [link](http://x) tip \U0001f680"
    out = strip_markdown(raw)
    for ch in "*#`[]":
        assert ch not in out
    assert "\n" not in out and "  " not in out
    assert out == "Title Good work with AIR FAN next link tip"


def test_cap_words_sentence_boundary():
    text = "One two three. Four five six. Seven eight nine ten."
    assert cap_words(text, 7) == "One two three. Four five six."
    assert cap_words(text, 100) == text
    assert cap_words("a b c d e f", 3) == "a b c."


def test_debrief_word_cap_and_cleanup(payload):
    advice = "**" + "Good work on the fan. " * 30 + "**"
    good = json.dumps({"advice": advice, "hints": ["- tip one", "- tip two"]})
    out = GeminiCoach("k", MODEL, 6000, client=FakeClient([resp(good)])).debrief(
        MISSION, CoachRequest(**payload)
    )
    assert "*" not in out[0]
    assert len(out[0].split()) + sum(len(h.split()) for h in out[1]) <= DEBRIEF_WORD_CAP


def test_ask_word_cap():
    good = json.dumps({"answer": "Turn the AIR FAN dial now. " * 30})
    out = GeminiCoach("k", MODEL, 6000, client=FakeClient([resp(good)])).ask(MISSION, MISSION["steps"][0], "q")
    assert 0 < len(out.split()) <= ASK_WORD_CAP


def test_empty_after_cleaning_rejected(payload):
    bad = json.dumps({"advice": "**", "hints": []})
    c = GeminiCoach("k", MODEL, 6000, client=FakeClient([resp(bad), resp(bad)]))
    with pytest.raises(OutputRejected):
        c.debrief(MISSION, CoachRequest(**payload))


def test_off_topic_rejected():
    bad = json.dumps({"answer": "Pasta is best cooked al dente with salted water."})
    c = GeminiCoach("k", MODEL, 6000, client=FakeClient([resp(bad), resp(bad)]))
    with pytest.raises(OutputRejected):
        c.ask(MISSION, MISSION["steps"][0], "q")


def real_client(ai):
    s = Settings(USE_MOCK_AI=False, GEMINI_MODEL=MODEL, GEMINI_API_KEY=None)
    return TestClient(create_app(s, ai=ai), raise_server_exceptions=False)


def test_debrief_fallback_on_rejected_output(payload):
    r = real_client(FakeAI(exc=OutputRejected())).post(DEBRIEF, json=payload)
    assert r.status_code == 200
    body = r.json()
    assert body["advisory"] is True and body["summary"] and body["modelVersion"] == "coach-mock-0"


def test_ask_fallback_on_rejected_output(ask_payload):
    r = real_client(FakeAI(exc=OutputRejected())).post(ASK, json=ask_payload)
    assert r.status_code == 200
    body = r.json()
    assert body["answer"] and body["modelVersion"] == "coach-mock-0"


def test_system_prompts_rules():
    for p in (DEBRIEF_SYSTEM, ASK_SYSTEM):
        low = p.lower()
        assert "advisory only" in low and "fictional astra station" in low
        assert "never state, change or estimate a score" in low
        assert "only the mission steps" in low
        assert "nasa" in low and "no emojis" in low and "no markdown" in low
    assert "60 words" in DEBRIEF_SYSTEM and "40 words" in ASK_SYSTEM


def test_model_input_contains_only_step_fields(payload):
    req = CoachRequest(**payload)
    for text in (build_debrief_contents(MISSION, req), build_ask_contents(MISSION, MISSION["steps"][0], "Where?")):
        assert "must not leak" not in text
        assert "secret" not in text
        assert "instruction" in text and "hint" in text and "critical" in text

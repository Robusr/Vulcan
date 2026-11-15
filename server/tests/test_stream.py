import json

import pytest

from app import app, llm_client, rate_limiter
from config import Config
from utils.exceptions import APIConnectionError

VALID_CHUNKS = [
    '{"feature_type":"extrude",',
    '"name":"Cylinder","params":{"plane":"前视基准面",'
    '"shape":"circle","diameter":50,"depth":100}}',
]


@pytest.fixture(autouse=True)
def reset_state(monkeypatch):
    app.config["TESTING"] = True
    rate_limiter.reset()
    monkeypatch.setattr(Config, "API_TOKEN", None)
    monkeypatch.setattr(rate_limiter, "limit", 60)
    yield
    rate_limiter.reset()


@pytest.fixture
def client():
    return app.test_client()


def read_events(response):
    events = []
    for block in response.data.decode("utf-8").split("\n\n"):
        block = block.strip()
        if block.startswith("data:"):
            events.append(json.loads(block[len("data:") :].strip()))
    return events


def test_stream_returns_deltas_and_result(client, monkeypatch):
    def fake_stream(prompt, **kwargs):
        yield from VALID_CHUNKS

    monkeypatch.setattr(llm_client, "stream_model", fake_stream)
    response = client.post("/api/v1/generate/stream", json={"prompt": "a cylinder"})

    assert response.status_code == 200
    assert response.mimetype == "text/event-stream"
    events = read_events(response)
    assert [event["type"] for event in events] == ["delta", "delta", "result"]
    assert events[-1]["data"]["feature_type"] == "extrude"


def test_stream_reports_connection_error(client, monkeypatch):
    def fake_stream(prompt, **kwargs):
        raise APIConnectionError("provider down")
        yield ""  # pragma: no cover

    monkeypatch.setattr(llm_client, "stream_model", fake_stream)
    response = client.post("/api/v1/generate/stream", json={"prompt": "a plate"})

    events = read_events(response)
    assert events[-1]["type"] == "error"
    assert events[-1]["status"] == 503


def test_stream_rejects_invalid_tree(client, monkeypatch):
    def fake_stream(prompt, **kwargs):
        yield '{"foo": 1}'

    monkeypatch.setattr(llm_client, "stream_model", fake_stream)
    response = client.post("/api/v1/generate/stream", json={"prompt": "a plate"})

    events = read_events(response)
    assert events[-1]["type"] == "error"
    assert events[-1]["status"] == 400


def test_stream_forwards_provider_and_language(client, monkeypatch):
    captured = {}

    def fake_stream(prompt, **kwargs):
        captured.update(kwargs)
        yield from VALID_CHUNKS

    monkeypatch.setattr(llm_client, "stream_model", fake_stream)
    response = client.post(
        "/api/v1/generate/stream",
        json={"prompt": "a plate", "language": "en"},
        headers={"X-LLM-Provider": "anthropic"},
    )

    assert response.status_code == 200
    assert captured["provider"] == "anthropic"
    assert captured["language"] == "en"


def test_unknown_provider_rejected(client):
    response = client.post(
        "/api/v1/generate",
        json={"prompt": "a plate"},
        headers={"X-LLM-Provider": "gemini"},
    )
    assert response.status_code == 400
    assert "服务商" in response.get_json()["message"]

import pytest

from app import app, llm_client, rate_limiter
from config import Config
from utils.exceptions import APIConnectionError, LLMResponseError


VALID_TREE = {
    "feature_type": "extrude",
    "name": "Cylinder",
    "params": {
        "plane": "前视基准面",
        "shape": "circle",
        "diameter": 50,
        "depth": 100,
    },
}


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


def test_health(client):
    response = client.get("/health")
    assert response.status_code == 200
    assert response.get_json()["status"] == "ok"


def test_missing_prompt(client):
    response = client.post("/api/v1/generate", json={})
    assert response.status_code == 400
    assert response.get_json()["status"] == "error"


def test_empty_prompt(client):
    response = client.post("/api/v1/generate", json={"prompt": "   "})
    assert response.status_code == 400


def test_non_string_prompt(client):
    response = client.post("/api/v1/generate", json={"prompt": 123})
    assert response.status_code == 400


def test_malformed_json(client):
    response = client.post(
        "/api/v1/generate",
        data="not json",
        content_type="application/json",
    )
    assert response.status_code == 400


def test_prompt_too_long(client, monkeypatch):
    monkeypatch.setattr(Config, "MAX_PROMPT_LENGTH", 5)
    response = client.post("/api/v1/generate", json={"prompt": "longer than five"})
    assert response.status_code == 400


def test_success(client, monkeypatch):
    monkeypatch.setattr(llm_client, "call_model", lambda prompt: VALID_TREE)
    response = client.post("/api/v1/generate", json={"prompt": "a cylinder"})
    assert response.status_code == 200
    assert response.get_json()["feature_type"] == "extrude"


def test_multi_feature_envelope(client, monkeypatch):
    monkeypatch.setattr(
        llm_client,
        "call_model",
        lambda prompt: {"model_name": "Plate", "features": [VALID_TREE]},
    )
    response = client.post("/api/v1/generate", json={"prompt": "a plate"})
    assert response.status_code == 200
    assert response.get_json()["features"]


def test_invalid_envelope(client, monkeypatch):
    monkeypatch.setattr(llm_client, "call_model", lambda prompt: {"foo": "bar"})
    response = client.post("/api/v1/generate", json={"prompt": "a plate"})
    assert response.status_code == 400


def test_llm_connection_error(client, monkeypatch):
    def fail(prompt):
        raise APIConnectionError("provider down")

    monkeypatch.setattr(llm_client, "call_model", fail)
    response = client.post("/api/v1/generate", json={"prompt": "a plate"})
    assert response.status_code == 503


def test_llm_response_error(client, monkeypatch):
    def fail(prompt):
        raise LLMResponseError("bad json")

    monkeypatch.setattr(llm_client, "call_model", fail)
    response = client.post("/api/v1/generate", json={"prompt": "a plate"})
    assert response.status_code == 500


def test_invalid_feature_params(client, monkeypatch):
    monkeypatch.setattr(
        llm_client, "call_model", lambda prompt: {"feature_type": "fillet", "params": {}}
    )
    response = client.post("/api/v1/generate", json={"prompt": "fillet"})
    assert response.status_code == 400
    assert "参数无效" in response.get_json()["message"]


def test_auth_required(client, monkeypatch):
    monkeypatch.setattr(Config, "API_TOKEN", "secret-token")
    response = client.post("/api/v1/generate", json={"prompt": "a plate"})
    assert response.status_code == 401

    monkeypatch.setattr(llm_client, "call_model", lambda prompt: VALID_TREE)
    response = client.post(
        "/api/v1/generate",
        json={"prompt": "a plate"},
        headers={"Authorization": "Bearer secret-token"},
    )
    assert response.status_code == 200


def test_rate_limit(client, monkeypatch):
    monkeypatch.setattr(llm_client, "call_model", lambda prompt: VALID_TREE)
    monkeypatch.setattr(rate_limiter, "limit", 1)
    rate_limiter.reset()

    first = client.post("/api/v1/generate", json={"prompt": "one"})
    second = client.post("/api/v1/generate", json={"prompt": "two"})

    assert first.status_code == 200
    assert second.status_code == 429

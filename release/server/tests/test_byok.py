import pytest
import requests_mock

from app import app, llm_client, rate_limiter
from config import Config
from services.llm_client import LLMClient
from utils.exceptions import APIConnectionError

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

SERVER_BASE = "https://server.example.test/v1"
SERVER_CHAT_URL = SERVER_BASE + "/chat/completions"
USER_BASE = "https://user.example.test/v1"
USER_CHAT_URL = USER_BASE + "/chat/completions"


def payload(content='{"feature_type":"extrude"}'):
    return {"choices": [{"message": {"content": content}}]}


@pytest.fixture(autouse=True)
def reset_state(monkeypatch):
    app.config["TESTING"] = True
    rate_limiter.reset()
    monkeypatch.setattr(Config, "API_TOKEN", None)
    monkeypatch.setattr(rate_limiter, "limit", 60)
    monkeypatch.setattr(Config, "ALLOW_CUSTOM_LLM_BASE_URL", True)
    yield
    rate_limiter.reset()


@pytest.fixture
def client():
    return app.test_client()


def test_app_forwards_byok_headers(client, monkeypatch):
    captured = {}

    def fake_call(prompt, api_key=None, base_url=None, model=None):
        captured.update(
            prompt=prompt,
            api_key=api_key,
            base_url=base_url,
            model=model,
        )
        return VALID_TREE

    monkeypatch.setattr(llm_client, "call_model", fake_call)
    response = client.post(
        "/api/v1/generate",
        json={"prompt": "a plate"},
        headers={
            "X-LLM-API-Key": "user-key",
            "X-LLM-Base-URL": USER_BASE,
            "X-LLM-Model": "user-model",
        },
    )
    assert response.status_code == 200
    assert captured == {
        "prompt": "a plate",
        "api_key": "user-key",
        "base_url": USER_BASE,
        "model": "user-model",
    }


def test_app_without_byok_headers_passes_none(client, monkeypatch):
    captured = {}

    def fake_call(prompt, api_key=None, base_url=None, model=None):
        captured.update(api_key=api_key, base_url=base_url, model=model)
        return VALID_TREE

    monkeypatch.setattr(llm_client, "call_model", fake_call)
    response = client.post("/api/v1/generate", json={"prompt": "a plate"})
    assert response.status_code == 200
    assert captured == {"api_key": None, "base_url": None, "model": None}


def make_client(monkeypatch, allow_custom=True):
    monkeypatch.setattr(Config, "QINIU_API_KEY", "server-key")
    monkeypatch.setattr(Config, "QINIU_BASE_URL", SERVER_BASE)
    monkeypatch.setattr(Config, "QINIU_MODEL", "server-model")
    monkeypatch.setattr(Config, "LLM_TIMEOUT", 5)
    monkeypatch.setattr(Config, "ALLOW_CUSTOM_LLM_BASE_URL", allow_custom)
    monkeypatch.setattr(Config, "MAX_LLM_API_KEY_LENGTH", 512)
    monkeypatch.setattr(Config, "MAX_LLM_BASE_URL_LENGTH", 512)
    return LLMClient()


def test_llm_client_uses_byok_overrides(monkeypatch):
    client = make_client(monkeypatch)
    with requests_mock.Mocker() as mock:
        mock.post(USER_CHAT_URL, json=payload())
        result = client.call_model(
            "a plate",
            api_key="user-key",
            base_url=USER_BASE,
            model="user-model",
        )

    assert result == {"feature_type": "extrude"}
    assert mock.last_request.headers["Authorization"] == "Bearer user-key"
    assert mock.last_request.json()["model"] == "user-model"


def test_llm_client_ignores_custom_base_url_when_disabled(monkeypatch):
    client = make_client(monkeypatch, allow_custom=False)
    with requests_mock.Mocker() as mock:
        mock.post(SERVER_CHAT_URL, json=payload())
        client.call_model(
            "a plate",
            api_key="user-key",
            base_url=USER_BASE,
            model="user-model",
        )

    assert mock.last_request.url.startswith(SERVER_BASE)
    assert mock.last_request.headers["Authorization"] == "Bearer user-key"


def test_llm_client_rejects_invalid_base_url_scheme(monkeypatch):
    client = make_client(monkeypatch)
    with pytest.raises(APIConnectionError):
        client.call_model("a plate", api_key="user-key", base_url="ftp://example.test/v1")


def test_llm_client_rejects_oversized_key(monkeypatch):
    client = make_client(monkeypatch)
    monkeypatch.setattr(Config, "MAX_LLM_API_KEY_LENGTH", 5)
    with pytest.raises(APIConnectionError):
        client.call_model("a plate", api_key="abcdef", base_url=USER_BASE)

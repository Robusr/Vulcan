import pytest
import requests
import requests_mock

from config import Config
from services.llm_client import LLMClient
from utils.exceptions import APIConnectionError, LLMResponseError

BASE_URL = "https://llm.example.test/v1"
CHAT_URL = BASE_URL + "/chat/completions"


def payload(content):
    return {"choices": [{"message": {"content": content}}]}


@pytest.fixture
def client(monkeypatch):
    monkeypatch.setattr(Config, "QINIU_API_KEY", "test-key")
    monkeypatch.setattr(Config, "QINIU_BASE_URL", BASE_URL)
    monkeypatch.setattr(Config, "QINIU_MODEL", "test-model")
    monkeypatch.setattr(Config, "LLM_TIMEOUT", 5)
    return LLMClient()


def test_success(client):
    with requests_mock.Mocker() as mock:
        mock.post(CHAT_URL, json=payload('{"feature_type":"extrude"}'))
        assert client.call_model("a cylinder") == {"feature_type": "extrude"}


def test_markdown_fence_cleanup(client):
    with requests_mock.Mocker() as mock:
        mock.post(CHAT_URL, json=payload('```json\n{"feature_type":"cut"}\n```'))
        assert client.call_model("a cut")["feature_type"] == "cut"


def test_prose_around_json_cleanup(client):
    with requests_mock.Mocker() as mock:
        mock.post(CHAT_URL, json=payload('Here is the tree: {"feature_type":"revolve"} done.'))
        assert client.call_model("a revolve")["feature_type"] == "revolve"


def test_timeout_maps_to_connection_error(client):
    with requests_mock.Mocker() as mock:
        mock.post(CHAT_URL, exc=requests.exceptions.Timeout)
        with pytest.raises(APIConnectionError):
            client.call_model("slow")


def test_connection_error_maps_to_connection_error(client):
    with requests_mock.Mocker() as mock:
        mock.post(CHAT_URL, exc=requests.exceptions.ConnectionError)
        with pytest.raises(APIConnectionError):
            client.call_model("offline")


def test_http_error_maps_to_connection_error(client):
    with requests_mock.Mocker() as mock:
        mock.post(CHAT_URL, status_code=500)
        with pytest.raises(APIConnectionError):
            client.call_model("broken provider")


def test_empty_choices_raises_response_error(client):
    with requests_mock.Mocker() as mock:
        mock.post(CHAT_URL, json={"choices": []})
        with pytest.raises(LLMResponseError):
            client.call_model("empty")


def test_invalid_json_raises_response_error(client):
    with requests_mock.Mocker() as mock:
        mock.post(CHAT_URL, json=payload("not json"))
        with pytest.raises(LLMResponseError):
            client.call_model("bad json")


def test_non_json_http_body_raises_response_error(client):
    with requests_mock.Mocker() as mock:
        mock.post(CHAT_URL, text="<html>gateway error</html>")
        with pytest.raises(LLMResponseError):
            client.call_model("bad body")


def test_missing_configuration_raises_connection_error(monkeypatch):
    monkeypatch.setattr(Config, "QINIU_API_KEY", None)
    with pytest.raises(APIConnectionError):
        LLMClient().call_model("no key")

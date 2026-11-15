from services.providers import (
    ANTHROPIC,
    OPENAI,
    get_provider,
    normalize_provider,
    supported_providers,
)


def test_supported_providers():
    assert supported_providers() == ["anthropic", "openai"]


def test_normalize_provider_aliases():
    assert normalize_provider("openai") == OPENAI
    assert normalize_provider("OpenAI-Compatible") == OPENAI
    assert normalize_provider("claude") == ANTHROPIC
    assert normalize_provider(None) == OPENAI


def test_unknown_provider_returns_none():
    assert normalize_provider("gemini") is None
    assert get_provider("gemini") is None


def test_openai_endpoint_headers_and_payload():
    adapter = get_provider(OPENAI)
    assert adapter.endpoint("https://api.example.test/v1") == (
        "https://api.example.test/v1/chat/completions"
    )
    headers = adapter.headers("key")
    assert headers["Authorization"] == "Bearer key"

    body = adapter.payload("m", "system", "user", stream=False)
    assert body["messages"][0] == {"role": "system", "content": "system"}
    assert body["messages"][1] == {"role": "user", "content": "user"}
    assert "stream" not in body
    assert adapter.payload("m", "s", "u", stream=True)["stream"] is True


def test_openai_extract_and_stream():
    adapter = get_provider(OPENAI)
    assert adapter.extract_text({"choices": [{"message": {"content": " hello "}}]}) == "hello"

    lines = [
        'data: {"choices":[{"delta":{"content":"{\\"a\\":"}}]}',
        'data: {"choices":[{"delta":{"content":"1}"}}]}',
        "data: [DONE]",
    ]
    assert "".join(adapter.iter_stream_text(lines)) == '{"a":1}'


def test_anthropic_endpoint_headers_and_payload():
    adapter = get_provider(ANTHROPIC)
    assert adapter.endpoint("https://api.anthropic.com/v1") == (
        "https://api.anthropic.com/v1/messages"
    )
    headers = adapter.headers("key")
    assert headers["x-api-key"] == "key"
    assert headers["anthropic-version"] == "2023-06-01"

    body = adapter.payload("claude-opus-5-5", "system", "user", stream=False)
    assert body["system"] == "system"
    assert body["messages"] == [{"role": "user", "content": "user"}]
    assert "temperature" not in body
    assert "top_p" not in body


def test_anthropic_extract_and_stream():
    adapter = get_provider(ANTHROPIC)
    text = adapter.extract_text(
        {
            "content": [
                {"type": "thinking", "thinking": ""},
                {"type": "text", "text": '{"a":'},
                {"type": "text", "text": "1}"},
            ]
        }
    )
    assert text == '{"a":1}'

    lines = [
        'data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"{\\"a\\":"}}',
        'data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"1}"}}',
        'data: {"type":"message_stop"}',
    ]
    assert "".join(adapter.iter_stream_text(lines)) == '{"a":1}'

# Provider adapters for the Vulcan server.
# Each adapter owns the wire shape for one provider family; the LLM client
# keeps the request validation, retry mapping, and JSON cleanup in one place.
# The project stays request-based and provider-neutral: no provider SDKs.

import json

from utils.exceptions import APIConnectionError, LLMResponseError

OPENAI = "openai"
ANTHROPIC = "anthropic"
PROVIDER_NAMES = {OPENAI, ANTHROPIC}
PROVIDER_ALIASES = {
    "openai": OPENAI,
    "openai-compatible": OPENAI,
    "compatible": OPENAI,
    "azure": OPENAI,
    "deepseek": OPENAI,
    "qwen": OPENAI,
    "ollama": OPENAI,
    "vllm": OPENAI,
    "anthropic": ANTHROPIC,
    "claude": ANTHROPIC,
}


def normalize_provider(value, default=OPENAI):
    if value is None or not str(value).strip():
        return default
    return PROVIDER_ALIASES.get(str(value).strip().lower())


class ChatProvider:
    name = OPENAI

    def endpoint(self, base_url):
        raise NotImplementedError

    def headers(self, api_key):
        raise NotImplementedError

    def payload(self, model, system_prompt, user_input, stream):
        raise NotImplementedError

    def extract_text(self, body):
        raise NotImplementedError

    def iter_stream_text(self, lines):
        raise NotImplementedError


class OpenAICompatibleProvider(ChatProvider):
    name = OPENAI

    def endpoint(self, base_url):
        return f"{base_url}/chat/completions"

    def headers(self, api_key):
        return {
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        }

    def payload(self, model, system_prompt, user_input, stream):
        body = {
            "model": model,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_input},
            ],
            "temperature": 0.05,
            "max_tokens": 2048,
            "top_p": 0.95,
            "frequency_penalty": 0,
            "presence_penalty": 0,
        }
        if stream:
            body["stream"] = True
        return body

    def extract_text(self, body):
        choices = body.get("choices") if isinstance(body, dict) else None
        if not choices:
            raise LLMResponseError("云端模型返回空内容")
        message = choices[0].get("message") or {}
        content = message.get("content")
        if not isinstance(content, str) or not content.strip():
            raise LLMResponseError("云端模型返回空内容")
        return content.strip()

    def iter_stream_text(self, lines):
        for raw in lines:
            if not raw:
                continue
            line = raw.decode("utf-8", "replace") if isinstance(raw, bytes) else raw
            line = line.strip()
            if not line.startswith("data:"):
                continue
            data = line[len("data:") :].strip()
            if data == "[DONE]":
                return
            try:
                event = json.loads(data)
            except json.JSONDecodeError:
                continue
            choices = event.get("choices") or []
            if not choices:
                continue
            delta = choices[0].get("delta") or {}
            text = delta.get("content")
            if isinstance(text, str) and text:
                yield text


class AnthropicProvider(ChatProvider):
    name = ANTHROPIC
    API_VERSION = "2023-06-01"

    def endpoint(self, base_url):
        return f"{base_url}/messages"

    def headers(self, api_key):
        return {
            "x-api-key": api_key,
            "anthropic-version": self.API_VERSION,
            "Content-Type": "application/json",
        }

    def payload(self, model, system_prompt, user_input, stream):
        # Current Claude models reject temperature and top_p; send only the
        # parameters every Messages API version accepts.
        body = {
            "model": model,
            "max_tokens": 2048,
            "system": system_prompt,
            "messages": [{"role": "user", "content": user_input}],
        }
        if stream:
            body["stream"] = True
        return body

    def extract_text(self, body):
        content = body.get("content") if isinstance(body, dict) else None
        if not isinstance(content, list):
            raise LLMResponseError("云端模型返回空内容")
        parts = [
            block.get("text", "")
            for block in content
            if isinstance(block, dict) and block.get("type") == "text"
        ]
        text = "".join(parts).strip()
        if not text:
            raise LLMResponseError("云端模型返回空内容")
        return text

    def iter_stream_text(self, lines):
        for raw in lines:
            if not raw:
                continue
            line = raw.decode("utf-8", "replace") if isinstance(raw, bytes) else raw
            line = line.strip()
            if not line.startswith("data:"):
                continue
            data = line[len("data:") :].strip()
            if not data:
                continue
            try:
                event = json.loads(data)
            except json.JSONDecodeError:
                continue
            event_type = event.get("type")
            if event_type == "content_block_delta":
                delta = event.get("delta") or {}
                if delta.get("type") == "text_delta":
                    text = delta.get("text")
                    if isinstance(text, str) and text:
                        yield text
            elif event_type == "error":
                error = event.get("error") or {}
                raise APIConnectionError(
                    "云端服务返回错误: " + str(error.get("message", "unknown"))
                )


_PROVIDERS = {
    OPENAI: OpenAICompatibleProvider(),
    ANTHROPIC: AnthropicProvider(),
}


def get_provider(name, default=OPENAI):
    key = normalize_provider(name, default)
    if key is None:
        return None
    return _PROVIDERS.get(key)


def supported_providers():
    return sorted(PROVIDER_NAMES)

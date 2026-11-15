# Robusr Mar.27th
# LLM client for OpenAI-compatible and Anthropic Messages endpoints.
import json
import re

import requests

from config import Config
from prompts import get_system_prompt
from services.language import detect_language
from services.providers import get_provider
from utils.exceptions import APIConnectionError, LLMResponseError
from utils.logger import setup_logger

logger = setup_logger(__name__)


def parse_feature_content(content):
    """Parse a feature tree from model text, tolerating fences and prose."""
    try:
        return json.loads(content)
    except json.JSONDecodeError:
        logger.warning("LLM 返回带代码块或额外文本，尝试清理")
        cleaned = re.sub(r"^```json\s*", "", content, flags=re.MULTILINE)
        cleaned = re.sub(r"\s*```$", "", cleaned, flags=re.MULTILINE)
        start = cleaned.find("{")
        end = cleaned.rfind("}")
        if start != -1 and end != -1 and end > start:
            try:
                return json.loads(cleaned[start : end + 1])
            except json.JSONDecodeError as exc:
                raise LLMResponseError("无法解析模型返回的JSON格式") from exc
        raise LLMResponseError("无法解析模型返回的JSON格式") from None


class LLMClient:
    def __init__(self):
        self.api_key = Config.QINIU_API_KEY
        self.base_url = Config.QINIU_BASE_URL
        self.model = Config.QINIU_MODEL

    def _resolve(self, api_key, base_url, model, language, provider, user_input):
        effective_provider = provider or getattr(Config, "LLM_PROVIDER", "openai")
        adapter = get_provider(effective_provider)
        if adapter is None:
            raise APIConnectionError(f"不支持的服务商: {effective_provider}")

        effective_api_key = (api_key or "").strip() or (self.api_key or "").strip()
        requested_base_url = (base_url or "").strip()
        if requested_base_url and not Config.ALLOW_CUSTOM_LLM_BASE_URL:
            logger.warning("客户端自定义 LLM Base URL 已被服务端配置禁用")
            requested_base_url = ""
        effective_base_url = (requested_base_url or self.base_url or "").rstrip("/")
        effective_model = (model or "").strip() or self.model
        effective_language = language or detect_language(user_input)
        system_prompt = get_system_prompt(effective_language)

        if not effective_api_key or not effective_base_url:
            raise APIConnectionError(
                "未配置 LLM API Key 或 Base URL，请在插件设置中填写或配置服务端环境变量"
            )
        if len(effective_api_key) > Config.MAX_LLM_API_KEY_LENGTH:
            raise APIConnectionError("LLM API Key 过长")
        if len(effective_base_url) > Config.MAX_LLM_BASE_URL_LENGTH:
            raise APIConnectionError("LLM Base URL 过长")
        if not effective_base_url.startswith(("http://", "https://")):
            raise APIConnectionError("LLM Base URL 必须以 http:// 或 https:// 开头")

        return {
            "adapter": adapter,
            "api_key": effective_api_key,
            "base_url": effective_base_url,
            "model": effective_model,
            "language": effective_language,
            "system_prompt": system_prompt,
        }

    def call_model(
        self,
        user_input,
        api_key=None,
        base_url=None,
        model=None,
        language=None,
        provider=None,
    ):
        """Call the provider and return a parsed JSON feature tree."""
        resolved = self._resolve(api_key, base_url, model, language, provider, user_input)
        adapter = resolved["adapter"]
        url = adapter.endpoint(resolved["base_url"])
        payload = adapter.payload(
            resolved["model"], resolved["system_prompt"], user_input, stream=False
        )

        logger.info(
            "正在请求 LLM，provider=%s，model=%s，language=%s，prompt 长度=%d",
            adapter.name,
            resolved["model"],
            resolved["language"],
            len(user_input),
        )
        try:
            response = requests.post(
                url,
                headers=adapter.headers(resolved["api_key"]),
                json=payload,
                timeout=Config.LLM_TIMEOUT,
            )
            response.raise_for_status()
        except requests.exceptions.Timeout as exc:
            logger.error("LLM 请求超时")
            raise APIConnectionError("云端模型响应超时，请稍后重试") from exc
        except requests.exceptions.ConnectionError as exc:
            logger.error("网络连接失败")
            raise APIConnectionError("无法连接到云端服务器") from exc
        except requests.exceptions.HTTPError as exc:
            status = exc.response.status_code if exc.response is not None else "unknown"
            logger.error("LLM 服务返回 HTTP %s", status)
            raise APIConnectionError(f"云端服务返回 HTTP {status}") from exc

        try:
            result = response.json()
        except ValueError as exc:
            raise LLMResponseError("云端返回的不是 JSON") from exc

        content = adapter.extract_text(result)
        logger.debug("LLM 返回原始内容: %s", content)
        return parse_feature_content(content)

    def stream_model(
        self,
        user_input,
        api_key=None,
        base_url=None,
        model=None,
        language=None,
        provider=None,
    ):
        """Yield text deltas from the provider for the same request."""
        resolved = self._resolve(api_key, base_url, model, language, provider, user_input)
        adapter = resolved["adapter"]
        url = adapter.endpoint(resolved["base_url"])
        payload = adapter.payload(
            resolved["model"], resolved["system_prompt"], user_input, stream=True
        )

        logger.info(
            "正在流式请求 LLM，provider=%s，model=%s，language=%s，prompt 长度=%d",
            adapter.name,
            resolved["model"],
            resolved["language"],
            len(user_input),
        )
        try:
            response = requests.post(
                url,
                headers=adapter.headers(resolved["api_key"]),
                json=payload,
                timeout=Config.LLM_TIMEOUT,
                stream=True,
            )
            response.raise_for_status()
        except requests.exceptions.Timeout as exc:
            raise APIConnectionError("云端模型响应超时，请稍后重试") from exc
        except requests.exceptions.ConnectionError as exc:
            raise APIConnectionError("无法连接到云端服务器") from exc
        except requests.exceptions.HTTPError as exc:
            status = exc.response.status_code if exc.response is not None else "unknown"
            raise APIConnectionError(f"云端服务返回 HTTP {status}") from exc

        with response:
            yield from adapter.iter_stream_text(response.iter_lines())

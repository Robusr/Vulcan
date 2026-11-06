# Robusr Mar.27th
# LLM client for any OpenAI-compatible /chat/completions endpoint.
import json
import re

import requests

from config import Config
from prompts.system_prompt import SYSTEM_PROMPT
from utils.exceptions import APIConnectionError, LLMResponseError
from utils.logger import setup_logger

logger = setup_logger(__name__)


class LLMClient:
    def __init__(self):
        self.api_key = Config.QINIU_API_KEY
        self.base_url = Config.QINIU_BASE_URL
        self.model = Config.QINIU_MODEL
        self.system_prompt = SYSTEM_PROMPT

    def _headers(self):
        return {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json",
        }

    def call_model(self, user_input: str) -> dict:
        """Call the provider and return a parsed JSON feature tree."""
        if not self.api_key or not self.base_url:
            raise APIConnectionError(
                "未配置 LLM API Key 或 Base URL，请检查 QINIU_API_KEY 和 QINIU_BASE_URL"
            )

        url = f"{self.base_url}/chat/completions"
        payload = {
            "model": self.model,
            "messages": [
                {"role": "system", "content": self.system_prompt},
                {"role": "user", "content": user_input},
            ],
            "temperature": 0.05,
            "max_tokens": 2048,
            "top_p": 0.95,
            "frequency_penalty": 0,
            "presence_penalty": 0,
        }

        logger.info("正在请求 LLM，prompt 长度=%d", len(user_input))
        try:
            response = requests.post(
                url,
                headers=self._headers(),
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

        if not result.get("choices"):
            raise LLMResponseError("云端模型返回空内容")

        content = result["choices"][0]["message"]["content"].strip()
        logger.debug("LLM 返回原始内容: %s", content)

        # Tolerate markdown fences and prose around the JSON object.
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
            raise LLMResponseError("无法解析模型返回的JSON格式")

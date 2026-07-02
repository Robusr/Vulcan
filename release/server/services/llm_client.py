# Robusr Mar.27th
# LLM大模型调用管理
import requests
import json
import re
from config import Config
from prompts.system_prompt import SYSTEM_PROMPT
from utils.logger import setup_logger
from utils.exceptions import APIConnectionError, LLMResponseError

logger = setup_logger(__name__)


class LLMClient:
    def __init__(self):
        self.api_key = Config.QINIU_API_KEY
        self.base_url = Config.QINIU_BASE_URL
        self.model = Config.QINIU_MODEL
        self.headers = {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json"
        }

        self.system_prompt = SYSTEM_PROMPT

    def call_model(self, user_input: str) -> dict:
        """
        调用七牛云 API 并返回解析后的标准JSON
        """
        url = f"{self.base_url}/chat/completions"

        payload = {
            "model": self.model,
            "messages": [
                {"role": "system", "content": self.system_prompt},
                {"role": "user", "content": user_input}
            ],
            "temperature": 0.05,  # 极低随机性
            "max_tokens": 2048,  # 支持多特征长输出
            "top_p": 0.95,
            "frequency_penalty": 0,
            "presence_penalty": 0
        }

        try:
            logger.info(f"正在请求 LLM，用户输入: {user_input}")
            response = requests.post(
                url,
                headers=self.headers,
                json=payload,
                timeout=Config.LLM_TIMEOUT
            )
            response.raise_for_status()  # 捕获HTTP错误

            result = response.json()
            # 校验返回结构
            if not result.get("choices") or len(result["choices"]) == 0:
                raise LLMResponseError("云端模型返回空内容")

            content = result['choices'][0]['message']['content'].strip()
            logger.info(f"LLM返回原始内容: {content}")

            # 多层容错JSON解析
            try:
                # 1. 直接解析
                return json.loads(content)
            except json.JSONDecodeError:
                # 2. 去除markdown代码块包裹
                logger.warning("LLM返回带代码块，尝试清理")
                content = re.sub(r'^```json\s*', '', content, flags=re.MULTILINE)
                content = re.sub(r'\s*```$', '', content, flags=re.MULTILINE)
                # 3. 提取首尾{}之间的内容
                start = content.find('{')
                end = content.rfind('}')
                if start != -1 and end != -1:
                    json_str = content[start:end + 1].strip()
                    return json.loads(json_str)
                raise LLMResponseError("无法解析模型返回的JSON格式")

        except requests.exceptions.Timeout:
            logger.error("LLM 请求超时")
            raise APIConnectionError("云端模型响应超时，请稍后重试")
        except requests.exceptions.ConnectionError:
            logger.error("网络连接失败")
            raise APIConnectionError("无法连接到云端服务器")
        except json.JSONDecodeError as e:
            logger.error(f"JSON解析失败: {str(e)}，原始内容: {content}")
            raise LLMResponseError("云端返回格式错误，无法解析建模参数")
        except Exception as e:
            logger.error(f"LLM 调用未知错误: {str(e)}")
            raise LLMResponseError(f"云端处理错误: {str(e)}")

import requests
import json
from config import Config
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

        # System Prompt: 定义 AI 的行为，强制输出 JSON
        self.system_prompt = """
        你是一个专业的 SolidWorks AI 建模助手 (代号 Vulcan)。
        你的任务是将用户的自然语言需求转换为严格的 JSON 格式参数。

        输出规则：
        1. 只输出 JSON，不要任何 Markdown 标记或解释性文字。
        2. JSON 结构必须包含以下根字段：
           - "feature_type": 字符串，值只能是 "extrude"(拉伸), "revolve"(旋转), "hole"(孔), "sketch"(草图) 之一。
           - "params": 对象，包含具体的建模数值。

        3. "params" 通用字段约定：
           - 单位默认 mm。
           - "plane": 基准面 ("Front", "Top", "Right")。
           - "height"/"depth"/"diameter": 数值类型。

        示例输入："在前视基准面上画一个100x50的矩形，拉伸20mm"
        示例输出：{"feature_type": "extrude", "params": {"plane": "Front", "shape": "rectangle", "length": 100, "width": 50, "depth": 20}}
        """

    def call_model(self, user_input: str) -> dict:
        """
        调用七牛云 API 并返回解析后的 JSON
        """
        url = f"{self.base_url}/chat/completions"

        payload = {
            "model": self.model,
            "messages": [
                {"role": "system", "content": self.system_prompt},
                {"role": "user", "content": user_input}
            ],
            "temperature": 0.1,  # 降低随机性，确保输出格式稳定
            "max_tokens": 1024
        }

        try:
            logger.info(f"正在请求 LLM，Input: {user_input}")
            response = requests.post(
                url,
                headers=self.headers,
                json=payload,
                timeout=Config.LLM_TIMEOUT
            )
            response.raise_for_status()  # 检查 HTTP 错误

            result = response.json()
            content = result['choices'][0]['message']['content'].strip()

            # 尝试解析 JSON
            try:
                return json.loads(content)
            except json.JSONDecodeError:
                # 有时候 LLM 可能会多输出废话，尝试提取 JSON 部分
                logger.warning(f"LLM 返回非纯 JSON，尝试提取: {content}")
                # 简单的提取逻辑：找第一个 { 和最后一个 }
                start = content.find('{')
                end = content.rfind('}')
                if start != -1 and end != -1:
                    json_str = content[start:end + 1]
                    return json.loads(json_str)
                raise LLMResponseError("无法解析模型返回的参数格式")

        except requests.exceptions.Timeout:
            logger.error("LLM 请求超时")
            raise APIConnectionError("云端模型响应超时，请稍后重试")
        except requests.exceptions.ConnectionError:
            logger.error("网络连接失败")
            raise APIConnectionError("无法连接到云端服务器")
        except Exception as e:
            logger.error(f"LLM 调用未知错误: {str(e)}")
            raise LLMResponseError(f"云端处理错误: {str(e)}")
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
        你是一个专业的SolidWorks CAD建模助手。请根据用户的自然语言描述，生成结构化的JSON建模指令，严格匹配SolidWorks API的参数要求。

            ## 输出格式要求
            严格输出JSON格式，不要包含任何其他文字说明。
            
            ## JSON结构
            {
              "feature_type": "extrude", // 特征类型：extrude(拉伸), revolve(旋转), cut(切除), fillet(圆角)等
              "params": {
                // 通用参数
                "plane": "Front", // 基准面：Front/Top/Right 或 前视基准面/上视基准面/右视基准面
                "shape": "circle", // 草图形状：circle(圆), rectangle(矩形)
                
                // 圆参数
                "diameter": 50, // 圆形直径（mm），shape为circle时必填
                
                // 矩形参数
                "length": 100, // 矩形长度（mm），shape为rectangle时必填
                "width": 50, // 矩形宽度（mm），shape为rectangle时必填
                
                // 拉伸参数
                "depth": 100, // 拉伸深度（mm），必填
                "draft_angle": 0, // 拔模角度（度），默认0
                "draft_outward": false, // 是否向外拔模，默认false
                
                // 后续可扩展：旋转特征的axis, angle等
              }
            }
            
            ## 示例
            用户输入：在前视基准面拉伸一个直径50，高度100圆柱体
            输出：
            {
              "feature_type": "extrude",
              "params": {
                "plane": "Front",
                "shape": "circle",
                "diameter": 50,
                "depth": 100,
                "draft_angle": 0,
                "draft_outward": false
              }
            }
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
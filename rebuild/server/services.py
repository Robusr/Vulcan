import requests
import json
import re
from config import Config


class AIService:
    @staticmethod
    def natural_language_to_params(user_prompt: str) -> dict:
        """调用七牛云大模型将自然语言转换为建模参数"""

        system_prompt = """
        你是一个专业的SolidWorks参数解析器。请将用户的自然语言需求转换为严格的JSON格式。
        输出JSON schema:
        {
            "operation": "create_extrude" | "create_revolve" | "create_hole",
            "parameters": {
                "sketch_shape": "rectangle" | "circle" | "polygon",
                "dimensions": {"length": 100, "width": 50, "diameter": 20},
                "extrude_depth": 50,
                "position": {"x": 0, "y": 0, "z": 0}
            }
        }
        只返回JSON，不要任何其他文字。
        """

        try:
            headers = {
                "Authorization": f"Bearer {Config.OPENAI_API_KEY}",
                "Content-Type": "application/json"
            }

            payload = {
                "model": "gpt-oss-120b",
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": user_prompt}
                ],
                "temperature": 0.1
            }

            response = requests.post(
                Config.OPENAI_BASE_URL,
                headers=headers,
                json=payload,
                timeout=30
            )
            response.raise_for_status()

            # 提取并清理AI返回的JSON
            ai_response = response.json()["choices"][0]["message"]["content"]
            json_match = re.search(r'\{[\s\S]*\}', ai_response)
            if not json_match:
                raise ValueError("AI返回格式无效")

            return json.loads(json_match.group())

        except requests.exceptions.RequestException as e:
            raise Exception(f"网络请求失败: {str(e)}")
        except Exception as e:
            raise Exception(f"参数解析失败: {str(e)}")
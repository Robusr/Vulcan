import requests
import os
from dotenv import load_dotenv
from pathlib import Path

# 加载配置
env_path = Path(__file__).parent.parent / '.env'
load_dotenv(dotenv_path=env_path)


class CloudAgentClient:
    def __init__(self):
        self.base_url = os.getenv("AGENT_SERVER_URL", "http://127.0.0.1:8000")

    def generate_sw_code(self, user_prompt: str) -> dict:
        """请求云端生成代码"""
        try:
            resp = requests.post(
                f"{self.base_url}/api/v1/agent/generate",
                json={"user_prompt": user_prompt},
                timeout=60
            )
            resp.raise_for_status()
            return resp.json()
        except Exception as e:
            return {"success": False, "error_msg": f"网络错误: {str(e)}"}
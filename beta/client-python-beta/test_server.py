import requests
import os
from dotenv import load_dotenv
from pathlib import Path

env_path = Path(__file__).parent / '.env'
load_dotenv(dotenv_path=env_path)

BASE_URL = os.getenv("AGENT_SERVER_URL", "http://127.0.0.1:8000")


def test_health():
    print(f"正在连接服务器: {BASE_URL} ...")
    try:
        res = requests.get(f"{BASE_URL}/health", timeout=5)
        if res.status_code == 200:
            print(f"✅ 服务器连接正常! 当前模型: {res.json()['model']}")
            return True
    except Exception as e:
        print(f"❌ 服务器连接失败: {e}")
        return False


def test_cloud_agent():
    if not test_health():
        return

    print("\n" + "=" * 30)
    user_input = input("请输入建模需求 (例如: 画一个方块): ")

    print("\n🤖 正在调用 AI 思考...")
    payload = {"user_prompt": user_input}
    try:
        response = requests.post(f"{BASE_URL}/api/v1/agent/generate", json=payload, timeout=60)

        if response.status_code == 200:
            data = response.json()
            if data['success']:
                print("\n✅ 成功!")
                print(f"\n💡 AI 思考过程:\n{data['thought']}")
                print(f"\n💻 生成的代码:\n```python\n{data['generated_code']}\n```")
            else:
                print(f"\n❌ AI 报错: {data['error_msg']}")
        else:
            print(f"\n❌ HTTP 错误: {response.status_code}")
    except Exception as e:
        print(f"\n❌ 请求异常: {e}")


if __name__ == "__main__":
    test_cloud_agent()

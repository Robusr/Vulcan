import os
from openai import AsyncOpenAI
import json
from dotenv import load_dotenv

load_dotenv()

client = AsyncOpenAI(
    api_key=os.getenv("OPENAI_API_KEY"),
    base_url=os.getenv("OPENAI_BASE_URL")
)


async def call_llm_for_code(prompt: str) -> dict:
    try:
        response = await client.chat.completions.create(
            model=os.getenv("MODEL_NAME"),
            messages=[{"role": "user", "content": prompt}],
            response_format={"type": "json_object"}
        )
        content = response.choices[0].message.content

        # 【关键调试】打印一下大模型的原始输出
        print("\n[云端调试] 大模型原始输出:", content)

        if not content:
            return {"error": "Empty response"}

        parsed = json.loads(content)

        # 确保字段存在
        return {
            "thought": parsed.get("thought", "无思路"),
            "code": parsed.get("code", "")
        }

    except Exception as e:
        print(f"[云端调试] 报错: {e}")
        return {"error": str(e)}
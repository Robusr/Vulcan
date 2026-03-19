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
        return json.loads(content) if content else {"error": "Empty response"}
    except Exception as e:
        return {"error": str(e)}
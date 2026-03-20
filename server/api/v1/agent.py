from fastapi import APIRouter
from server.models.schemas import UserRequest, CodeGenerationResponse
from server.core.llm_client import call_llm_for_code
from server.core.prompt_manager import get_full_prompt
import json

router = APIRouter()


@router.post("/generate")
async def generate_code(request: UserRequest):
    full_prompt = get_full_prompt(request.user_prompt)
    llm_response = await call_llm_for_code(full_prompt)

    if "error" in llm_response:
        return {"success": False, "error_msg": llm_response["error"]}

    # 新的返回结构
    return {
        "success": True,
        "thought": llm_response.get("thought"),
        "operations": llm_response.get("operations", []),  # 这里是操作列表
        "generated_code": None  # 保留字段兼容旧版
    }
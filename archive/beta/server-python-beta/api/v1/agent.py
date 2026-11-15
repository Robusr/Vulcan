# Robusr Mar. 19th
# 大模型API接口初始化

from fastapi import APIRouter
from models.schemas import UserRequest, CodeGenerationResponse
from core.llm_client import call_llm_for_code
from core.prompt_manager import get_full_prompt

router = APIRouter()


@router.post("/generate", response_model=CodeGenerationResponse)
async def generate_code(request: UserRequest):
    full_prompt = get_full_prompt(request.user_prompt)
    llm_response = await call_llm_for_code(full_prompt)

    if "error" in llm_response:
        return CodeGenerationResponse(success=False, error_msg=llm_response["error"])

    return CodeGenerationResponse(
        success=True,
        thought=llm_response.get("thought"),
        generated_code=llm_response.get("code")
    )
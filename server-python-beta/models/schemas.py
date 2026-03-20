# Robusr Mar. 19th
# 大模型输出数据配置

from pydantic import BaseModel
from typing import Optional

class UserRequest(BaseModel):
    user_prompt: str
    context: Optional[str] = None

class CodeGenerationResponse(BaseModel):
    success: bool
    generated_code: Optional[str] = None
    thought: Optional[str] = None
    error_msg: Optional[str] = None
class VulcanBaseError(Exception):
    """基础异常类"""
    status_code = 500
    def __init__(self, message):
        self.message = message
        super().__init__(self.message)

class APIConnectionError(VulcanBaseError):
    """网络连接错误"""
    status_code = 502

class LLMResponseError(VulcanBaseError):
    """LLM 响应错误"""
    status_code = 503

class ParamValidationError(VulcanBaseError):
    """参数验证错误"""
    status_code = 400
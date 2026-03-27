# Robusr Mar.24th
# 返回数据方法检查器

from utils.exceptions import ParamValidationError
from utils.logger import setup_logger

logger = setup_logger(__name__)


def validate_and_enhance(raw_params: dict) -> dict:
    """
    验证参数合法性，并补充默认值
    """
    if not raw_params:
        raise ParamValidationError("未收到任何参数")

    # 检查必填字段
    required_fields = ["feature_type", "params"]
    for field in required_fields:
        if field not in raw_params:
            raise ParamValidationError(f"缺少必要字段: {field}")

    # 标准化特征类型 (转小写)
    feature_type = raw_params["feature_type"].lower()
    valid_types = ["extrude", "revolve", "hole", "sketch"]
    if feature_type not in valid_types:
        raise ParamValidationError(f"不支持的特征类型: {feature_type}")

    params = raw_params["params"]

    # 补充默认基准面
    if "plane" not in params:
        params["plane"] = "Front"
        logger.info("未指定基准面，默认使用 Front Plane")

    # 确保数值类型为 number
    # 防止 LLM 返回字符串
    for key in params:
        if isinstance(params[key], str) and params[key].replace('.', '', 1).isdigit():
            params[key] = float(params[key]) if '.' in params[key] else int(params[key])

    # 返回封装好的安全数据
    return {
        "feature_type": feature_type,
        "params": params,
        "version": "1.0"  # 协议版本，方便后续兼容
    }
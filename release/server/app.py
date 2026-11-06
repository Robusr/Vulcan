# Robusr Mar.27th
# Flask server entry point.
import hmac

from flask import Flask, jsonify, request
from werkzeug.exceptions import RequestEntityTooLarge

from config import Config
from services.feature_validator import validate_feature_tree
from services.llm_client import LLMClient
from utils.exceptions import APIConnectionError, LLMResponseError
from utils.logger import setup_logger
from utils.rate_limit import RateLimiter

logger = setup_logger(__name__)
app = Flask(__name__)
app.config["MAX_CONTENT_LENGTH"] = 64 * 1024

llm_client = LLMClient()
rate_limiter = RateLimiter(Config.RATE_LIMIT_PER_MINUTE, 60)


def _error(message, status):
    return jsonify({"message": message, "status": "error"}), status


@app.before_request
def enforce_access_controls():
    if request.path == "/health":
        return None

    if Config.API_TOKEN:
        expected = f"Bearer {Config.API_TOKEN}"
        provided = request.headers.get("Authorization", "")
        if not hmac.compare_digest(provided, expected):
            return _error("未授权", 401)

    if request.path == "/api/v1/generate":
        client_key = request.remote_addr or "unknown"
        if rate_limiter.is_limited(client_key):
            return _error("请求过于频繁，请稍后重试", 429)

    return None


@app.errorhandler(RequestEntityTooLarge)
def handle_large_request(_):
    return _error("请求体过大", 413)


@app.route("/health", methods=["GET"])
def health_check():
    return jsonify({"status": "ok", "model": Config.QINIU_MODEL}), 200


@app.route("/api/v1/generate", methods=["POST"])
def generate_model():
    try:
        # 1. Parse and validate the request body.
        request_data = request.get_json(silent=True)
        if not isinstance(request_data, dict) or "prompt" not in request_data:
            return _error("缺少必要字段: prompt", 400)

        raw_prompt = request_data.get("prompt")
        if not isinstance(raw_prompt, str):
            return _error("prompt必须是字符串", 400)

        user_prompt = raw_prompt.strip()
        if not user_prompt:
            return _error("prompt不能为空", 400)
        if len(user_prompt) > Config.MAX_PROMPT_LENGTH:
            return _error(
                f"prompt长度不能超过{Config.MAX_PROMPT_LENGTH}个字符", 400
            )

        # 2. Ask the LLM for a feature tree.
        model_data = llm_client.call_model(user_prompt)
        if not isinstance(model_data, dict):
            return _error("模型返回格式错误", 400)

        # 3. Accept either a single-feature or a multi-feature envelope.
        is_valid_single = model_data.get("feature_type") is not None
        features = model_data.get("features")
        is_valid_multi = isinstance(features, list) and len(features) > 0

        if not (is_valid_single or is_valid_multi):
            return _error("模型返回格式错误，缺少有效特征信息", 400)

        validation_errors = validate_feature_tree(model_data)
        if validation_errors:
            return _error("模型返回参数无效: " + "; ".join(validation_errors), 400)

        return jsonify(model_data), 200

    except APIConnectionError as exc:
        logger.error("API连接错误: %s", exc)
        return _error(exc.message, exc.status_code)
    except LLMResponseError as exc:
        logger.error("LLM响应错误: %s", exc)
        return _error(exc.message, exc.status_code)
    except Exception:
        logger.exception("服务未知错误")
        return _error("服务内部错误", 500)


if __name__ == "__main__":
    if not Config.API_TOKEN:
        logger.warning(
            "VULCAN_API_TOKEN 未设置，服务端未启用鉴权。不要将服务暴露到公网。"
        )
    app.run(host=Config.HOST, port=Config.PORT, debug=Config.DEBUG)

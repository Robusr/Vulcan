# Robusr Mar.27th
# Flask server entry point.
import hmac
import json

from flask import Flask, Response, jsonify, request
from werkzeug.exceptions import RequestEntityTooLarge
from werkzeug.middleware.proxy_fix import ProxyFix

from config import Config
from services.feature_validator import validate_feature_tree
from services.language import normalize_language
from services.llm_client import LLMClient, parse_feature_content
from services.providers import normalize_provider
from utils.exceptions import APIConnectionError, LLMResponseError
from utils.logger import setup_logger
from utils.rate_limit import RateLimiter

logger = setup_logger(__name__)
app = Flask(__name__)
app.config["MAX_CONTENT_LENGTH"] = 64 * 1024
if Config.TRUST_PROXY:
    app.wsgi_app = ProxyFix(app.wsgi_app, x_for=1, x_proto=1, x_host=1)

llm_client = LLMClient()
rate_limiter = RateLimiter(Config.RATE_LIMIT_PER_MINUTE, 60, Config.RATE_LIMIT_MAX_KEYS)


def _error(message, status):
    return jsonify({"message": message, "status": "error"}), status


def _sse(payload):
    return "data: " + json.dumps(payload, ensure_ascii=False) + "\n\n"


def _parse_generate_request():
    """Return (prompt, language, error_response)."""
    request_data = request.get_json(silent=True)
    if not isinstance(request_data, dict) or "prompt" not in request_data:
        return None, None, _error("缺少必要字段: prompt", 400)

    raw_prompt = request_data.get("prompt")
    if not isinstance(raw_prompt, str):
        return None, None, _error("prompt必须是字符串", 400)

    user_prompt = raw_prompt.strip()
    if not user_prompt:
        return None, None, _error("prompt不能为空", 400)
    if len(user_prompt) > Config.MAX_PROMPT_LENGTH:
        return (
            None,
            None,
            _error(f"prompt长度不能超过{Config.MAX_PROMPT_LENGTH}个字符", 400),
        )

    raw_language = request_data.get("language")
    language = normalize_language(raw_language)
    if raw_language is not None and language is None:
        return None, None, _error("language 仅支持 zh、en、fr", 400)

    return user_prompt, language, None


def _request_provider():
    """Return (provider, error_response)."""
    raw = request.headers.get("X-LLM-Provider")
    if not raw:
        return None, None
    provider = normalize_provider(raw)
    if provider is None:
        return None, _error("不支持的服务商，可选 openai 或 anthropic", 400)
    return provider, None


def _request_options(language):
    return {
        "api_key": request.headers.get("X-LLM-API-Key"),
        "base_url": request.headers.get("X-LLM-Base-URL"),
        "model": request.headers.get("X-LLM-Model"),
        "language": language,
    }


def _model_error(model_data):
    if not isinstance(model_data, dict):
        return "模型返回格式错误"

    is_valid_single = model_data.get("feature_type") is not None
    features = model_data.get("features")
    is_valid_multi = isinstance(features, list) and len(features) > 0

    if not (is_valid_single or is_valid_multi):
        return "模型返回格式错误，缺少有效特征信息"

    validation_errors = validate_feature_tree(model_data)
    if validation_errors:
        return "模型返回参数无效: " + "; ".join(validation_errors)

    return None


@app.before_request
def enforce_access_controls():
    if request.path == "/health":
        return None

    if Config.API_TOKEN:
        expected = f"Bearer {Config.API_TOKEN}"
        provided = request.headers.get("Authorization", "")
        if not hmac.compare_digest(provided, expected):
            return _error("未授权", 401)

    if request.path in {"/api/v1/generate", "/api/v1/generate/stream"}:
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
        user_prompt, language, error_response = _parse_generate_request()
        if error_response:
            return error_response

        provider, provider_error = _request_provider()
        if provider_error:
            return provider_error

        options = _request_options(language)
        model_data = llm_client.call_model(
            user_prompt,
            api_key=options["api_key"],
            base_url=options["base_url"],
            model=options["model"],
            language=language,
            provider=provider,
        )

        error_message = _model_error(model_data)
        if error_message:
            return _error(error_message, 400)

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


@app.route("/api/v1/generate/stream", methods=["POST"])
def generate_model_stream():
    user_prompt, language, error_response = _parse_generate_request()
    if error_response:
        return error_response

    provider, provider_error = _request_provider()
    if provider_error:
        return provider_error

    options = _request_options(language)

    def event_stream():
        chunks = []
        try:
            for text in llm_client.stream_model(
                user_prompt,
                api_key=options["api_key"],
                base_url=options["base_url"],
                model=options["model"],
                language=language,
                provider=provider,
            ):
                chunks.append(text)
                yield _sse({"type": "delta", "text": text})

            model_data = parse_feature_content("".join(chunks))
            error_message = _model_error(model_data)
            if error_message:
                yield _sse({"type": "error", "message": error_message, "status": 400})
                return

            yield _sse({"type": "result", "data": model_data})
        except APIConnectionError as exc:
            logger.error("流式 API 连接错误: %s", exc)
            yield _sse({"type": "error", "message": exc.message, "status": exc.status_code})
        except LLMResponseError as exc:
            logger.error("流式 LLM 响应错误: %s", exc)
            yield _sse({"type": "error", "message": exc.message, "status": exc.status_code})
        except Exception:
            logger.exception("流式服务未知错误")
            yield _sse({"type": "error", "message": "服务内部错误", "status": 500})

    return Response(
        event_stream(),
        mimetype="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


if __name__ == "__main__":
    if not Config.API_TOKEN:
        logger.warning("VULCAN_API_TOKEN 未设置，服务端未启用鉴权。不要将服务暴露到公网。")
    app.run(host=Config.HOST, port=Config.PORT, debug=Config.DEBUG)

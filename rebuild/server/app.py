from flask import Flask, request, jsonify
from config import Config
from services import AIService
from utils import validate_json, APIError

app = Flask(__name__)


# 全局错误处理
@app.errorhandler(APIError)
def handle_api_error(error):
    return jsonify({"success": False, "error": error.message}), error.status_code


@app.errorhandler(Exception)
def handle_generic_error(error):
    return jsonify({"success": False, "error": "服务器内部错误"}), 500


# API路由
@app.route('/api/health', methods=['GET'])
def health_check():
    return jsonify({"status": "healthy", "service": "Vulcan AI Backend"})


@app.route('/api/generate-params', methods=['POST'])
@validate_json
def generate_params():
    data = request.get_json()
    user_prompt = data.get("prompt", "")

    if not user_prompt:
        raise APIError("缺少prompt参数", 400)

    # 调用AI服务
    model_params = AIService.natural_language_to_params(user_prompt)

    return jsonify({
        "success": True,
        "data": model_params
    })


if __name__ == '__main__':
    app.run(host=Config.HOST, port=Config.PORT, debug=Config.DEBUG)
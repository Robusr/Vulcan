from flask import Flask, request, jsonify
from config import Config
from utils.logger import setup_logger
from utils.exceptions import VulcanBaseError
from services.llm_client import LLMClient
from services.param_parser import validate_and_enhance

app = Flask(__name__)
logger = setup_logger()

# 初始化服务
llm_client = LLMClient()


# --- API 路由 ---

@app.route('/health', methods=['GET'])
def health_check():
    """健康检查接口"""
    return jsonify({"status": "ok", "service": "Vulcan AI Core"})


@app.route('/api/v1/generate', methods=['POST'])
def generate_model():
    """
    核心生成接口
    请求体: { "prompt": "画一个立方体" }
    响应体: { "status": "success", "data": {...} }
    """
    try:
        # 1. 获取请求数据
        data = request.get_json()
        if not data or 'prompt' not in data:
            return jsonify({"status": "error", "message": "缺少 'prompt' 字段"}), 400

        user_prompt = data['prompt']

        # 2. 调用 LLM
        raw_json = llm_client.call_model(user_prompt)

        # 3. 验证与优化
        final_params = validate_and_enhance(raw_json)

        # 4. 返回成功响应
        logger.info("参数生成成功")
        return jsonify({
            "status": "success",
            "data": final_params
        })

    except VulcanBaseError as e:
        # 处理已知的业务异常
        logger.error(f"业务错误: {e.message}")
        return jsonify({"status": "error", "message": e.message}), e.status_code
    except Exception as e:
        # 处理未知异常
        logger.critical(f"系统未捕获异常: {str(e)}", exc_info=True)
        return jsonify({"status": "error", "message": "服务器内部未知错误"}), 500


# --- 启动入口 ---
if __name__ == '__main__':
    logger.info(f"Starting Vulcan Server on port {Config.PORT}...")
    app.run(host='0.0.0.0', port=Config.PORT, debug=Config.DEBUG)
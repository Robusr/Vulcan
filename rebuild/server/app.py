# Robusr Mar.27th
# Flask服务器入口
from flask import Flask, request, jsonify
from services.llm_client import LLMClient
from utils.logger import setup_logger
from utils.exceptions import APIConnectionError, LLMResponseError

logger = setup_logger(__name__)
app = Flask(__name__)
llm_client = LLMClient()


@app.route('/api/v1/generate', methods=['POST'])
def generate_model():
    try:
        # 1. 解析请求体
        request_data = request.get_json()
        if not request_data or 'prompt' not in request_data:
            return jsonify({
                "message": "缺少必要字段: prompt",
                "status": "error"
            }), 400

        user_prompt = request_data['prompt'].strip()
        if not user_prompt:
            return jsonify({
                "message": "prompt不能为空",
                "status": "error"
            }), 400

        # 2. 调用LLM获取建模参数
        model_data = llm_client.call_model(user_prompt)

        # 3. 兼容校验：支持单特征/多特征两种结构
        is_valid_single = model_data.get("feature_type") is not None
        is_valid_multi = model_data.get("features") is not None and isinstance(model_data.get("features"), list)

        if not (is_valid_single or is_valid_multi):
            return jsonify({
                "message": "模型返回格式错误，缺少有效特征信息",
                "status": "error"
            }), 400

        # 4. 校验通过，返回结果给前端
        return jsonify(model_data), 200

    except APIConnectionError as e:
        logger.error(f"API连接错误: {str(e)}")
        return jsonify({
            "message": str(e),
            "status": "error"
        }), 503
    except LLMResponseError as e:
        logger.error(f"LLM响应错误: {str(e)}")
        return jsonify({
            "message": str(e),
            "status": "error"
        }), 500
    except Exception as e:
        logger.error(f"服务未知错误: {str(e)}")
        return jsonify({
            "message": f"服务内部错误: {str(e)}",
            "status": "error"
        }), 500


if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
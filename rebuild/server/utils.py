from functools import wraps
from flask import request, jsonify

def validate_json(f):
    @wraps(f)
    def decorated(*args, **kwargs):
        if not request.is_json:
            return jsonify({"success": False, "error": "请求必须是JSON格式"}), 400
        return f(*args, **kwargs)
    return decorated

class APIError(Exception):
    def __init__(self, message, status_code=500):
        self.message = message
        self.status_code = status_code
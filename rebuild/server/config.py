# Robusr Mar.24th
# LLM调用入口定义

import os
from dotenv import load_dotenv

# 加载 .env 文件
load_dotenv()


class Config:
    # Flask 基础配置
    DEBUG = os.getenv('FLASK_ENV') == 'development'
    PORT = int(os.getenv('FLASK_RUN_PORT', 5000))

    # LLM 配置
    QINIU_API_KEY = os.getenv('QINIU_API_KEY')
    QINIU_BASE_URL = os.getenv('QINIU_BASE_URL')
    QINIU_MODEL = os.getenv('QINIU_MODEL', 'gpt-oss-120b')

    # 请求超时设置 (秒)
    LLM_TIMEOUT = 30
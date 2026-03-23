import os
from dotenv import load_dotenv

load_dotenv()


class Config:

    OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
    OPENAI_BASE_URL = os.getenv("OPENAI_BASE_URL")

    # 服务器配置
    HOST = "0.0.0.0"
    PORT = 5000
    DEBUG = True
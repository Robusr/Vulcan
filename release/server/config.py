# Robusr Mar.24th
# Server configuration loaded from environment variables.

import os

from dotenv import load_dotenv

load_dotenv()


def _int_env(name: str, default: int) -> int:
    raw = os.getenv(name)
    if raw is None or raw == "":
        return default
    try:
        return int(raw)
    except ValueError as exc:
        raise ValueError(f"Environment variable {name} must be an integer") from exc


class Config:
    # Flask/server binding
    DEBUG = os.getenv("FLASK_ENV", "production").strip().lower() == "development"
    HOST = os.getenv("HOST", "127.0.0.1")
    PORT = _int_env("PORT", _int_env("FLASK_RUN_PORT", 5000))

    # Access control
    API_TOKEN = os.getenv("VULCAN_API_TOKEN")
    RATE_LIMIT_PER_MINUTE = _int_env("VULCAN_RATE_LIMIT_PER_MINUTE", 60)
    MAX_PROMPT_LENGTH = _int_env("MAX_PROMPT_LENGTH", 4000)

    # Logging
    LOG_LEVEL = os.getenv("LOG_LEVEL", "INFO").upper()
    LOG_FILE = os.getenv("LOG_FILE", "vulcan_server.log")

    # LLM provider (QINIU_* is historical naming; any OpenAI-compatible endpoint works)
    QINIU_API_KEY = os.getenv("QINIU_API_KEY")
    QINIU_BASE_URL = (os.getenv("QINIU_BASE_URL") or "").rstrip("/")
    QINIU_MODEL = os.getenv("QINIU_MODEL", "gpt-oss-120b")
    LLM_TIMEOUT = _int_env("LLM_TIMEOUT", 30)

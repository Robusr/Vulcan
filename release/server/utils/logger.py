# Robusr Mar.24th
# Server logger with console output and rotating file output.

import logging
import sys
from logging.handlers import RotatingFileHandler

from config import Config


def setup_logger(name="vulcan_server"):
    logger = logging.getLogger(name)
    if logger.handlers:
        return logger

    logger.setLevel(getattr(logging, Config.LOG_LEVEL, logging.INFO))
    logger.propagate = False

    formatter = logging.Formatter(
        "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    )

    console = logging.StreamHandler(sys.stdout)
    console.setFormatter(formatter)
    logger.addHandler(console)

    try:
        file_handler = RotatingFileHandler(
            Config.LOG_FILE,
            maxBytes=5_000_000,
            backupCount=3,
            encoding="utf-8",
        )
        file_handler.setFormatter(formatter)
        logger.addHandler(file_handler)
    except OSError as exc:
        logger.warning("Cannot write log file %s: %s", Config.LOG_FILE, exc)

    return logger

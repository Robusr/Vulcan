# Robusr Mar.24th
# 服务器日志生成器

import logging
import sys

def setup_logger(name='vulcan_server'):
    logger = logging.getLogger(name)
    if logger.handlers:
        return logger  # 避免重复添加 handler

    logger.setLevel(logging.INFO)
    formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')

    # 控制台输出
    ch = logging.StreamHandler(sys.stdout)
    ch.setFormatter(formatter)
    logger.addHandler(ch)

    # 文件输出
    fh = logging.FileHandler('vulcan_server.log', encoding='utf-8')
    fh.setFormatter(formatter)
    logger.addHandler(fh)

    return logger
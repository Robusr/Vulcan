# client-python-beta/main.py
import sys
from pathlib import Path

# 确保能导入 sw_agent 包
sys.path.insert(0, str(Path(__file__).parent))

from sw_agent_ui import main

if __name__ == "__main__":
    main()
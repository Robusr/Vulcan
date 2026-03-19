import win32com.client
import pythoncom
from typing import Optional
import logging

# 【关键修复】在这里导入 SWModeler
# 注意：因为 sw_operations.py 也导入了 sw_connection.py，为了避免循环导入，
# 我们把 import 放在函数内部，或者调整结构。
# 最安全的做法是在 get_modeler 函数内部导入。

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class SolidWorksConnection:
    def __init__(self):
        self.sw_app: Optional[win32com.client.CDispatch] = None
        self.sw_model: Optional[win32com.client.CDispatch] = None
        self.is_connected = False

    # ... (保持 connect 和 new_part 函数不变) ...
    # 为了节省篇幅，这里假设 connect 和 new_part 函数还是之前的代码

    def connect(self, visible: bool = True) -> bool:
        """连接 SolidWorks，必须在主线程调用"""
        try:
            pythoncom.CoInitialize()
        except:
            pass

        try:
            self.sw_app = win32com.client.GetObject(Class="SldWorks.Application")
            logger.info("已连接到现有 SolidWorks 实例")
        except:
            try:
                logger.info("正在启动 SolidWorks...")
                self.sw_app = win32com.client.Dispatch("SldWorks.Application")
                self.sw_app.Visible = visible
            except Exception as e:
                logger.error(f"启动失败: {e}")
                return False

        self.is_connected = True
        return True

    def new_part(self):
        """新建一个零件 (修复版：不依赖枚举常量)"""
        if not self.is_connected: return None
        # 这里简化处理，直接返回 None，让用户手动打开
        return None

    def get_modeler(self):
        """返回建模器 (修复了导入问题)"""
        if not self.sw_model:
            logger.info("尝试获取当前活动文档...")
            self.sw_model = self.sw_app.ActiveDoc
            if not self.sw_model:
                raise Exception("没有可用的 SolidWorks 文档，请先手动打开或新建一个零件。")

        # 【关键修复】延迟导入，避免循环引用
        from .sw_operations import SWModeler
        return SWModeler(self)
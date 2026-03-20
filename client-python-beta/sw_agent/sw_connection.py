# Robusr Mar. 19th
# SolidWorks连接配置
import win32com.client
import pythoncom
from typing import Optional
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)


class SolidWorksConnection:
    def __init__(self):
        self.sw_app: Optional[win32com.client.CDispatch] = None
        self.sw_model: Optional[win32com.client.CDispatch] = None
        self.is_connected = False

    def connect(self, visible: bool = True) -> bool:
        """连接 SolidWorks，主线程调用"""
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
        """新建零件"""
        if not self.is_connected: return None
        return None

    def get_modeler(self):
        """返回建模器"""
        if not self.sw_model:
            logger.info("尝试获取当前活动文档...")
            self.sw_model = self.sw_app.ActiveDoc
            if not self.sw_model:
                raise Exception("没有可用的 SolidWorks 文档，请先手动打开或新建一个零件。")

        # 延迟导入，避免循环引用
        from .sw_operations import SWModeler
        return SWModeler(self)
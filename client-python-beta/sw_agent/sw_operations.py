# Robusr Mar. 19th
# SolidWorks操作封装

import win32com.client
import logging

logger = logging.getLogger(__name__)


class SWModeler:
    def __init__(self, sw_conn):
        self.sw_conn = sw_conn
        self.sw_app = sw_conn.sw_app
        self.sw_model = sw_conn.sw_model

    def _clear_selection(self):
        """内部方法：清除选择"""
        if self.sw_model:
            self.sw_model.ClearSelection2(True)

    def _select_entity(self, name, type_name):
        """内部方法：选择实体"""
        try:
            return self.sw_model.Extension.SelectByID2(name, type_name, 0, 0, 0, False, 0, None, 0)
        except:
            try:
                return self.sw_model.SelectByID(name, type_name, 0, 0, 0)
            except:
                return False

    # 基准面与草图控制
    def create_front_sketch(self):
        self._clear_selection()
        if self._select_entity("前视基准面", "PLANE") or self._select_entity("Front Plane", "PLANE"):
            self.sw_model.SketchManager.InsertSketch(True)
            logger.info("✅ 进入前视草图")

    def create_top_sketch(self):
        self._clear_selection()
        if self._select_entity("上视基准面", "PLANE") or self._select_entity("Top Plane", "PLANE"):
            self.sw_model.SketchManager.InsertSketch(True)
            logger.info("✅ 进入上视草图")

    def create_right_sketch(self):
        self._clear_selection()
        if self._select_entity("右视基准面", "PLANE") or self._select_entity("Right Plane", "PLANE"):
            self.sw_model.SketchManager.InsertSketch(True)
            logger.info("✅ 进入右视草图")

    def exit_sketch(self):
        self.sw_model.SketchManager.InsertSketch(True)
        logger.info("✅ 退出草图")

    # 草图绘制 (增强版)
    def draw_rectangle(self, x1, y1, x2, y2):
        """画矩形: (x1,y1)左下角, (x2,y2)右上角 (mm)"""
        sk = self.sw_model.SketchManager
        x1_m, y1_m, x2_m, y2_m = x1 / 1000, y1 / 1000, x2 / 1000, y2 / 1000
        sk.CreateCornerRectangle(x1_m, y1_m, 0, x2_m, y2_m, 0)
        logger.info(f"✅ 画矩形 ({x1},{y1})-({x2},{y2})")

    def draw_circle(self, x, y, radius):
        """画圆: 中心(x,y), 半径 (mm)"""
        sk = self.sw_model.SketchManager
        x_m, y_m, r_m = x / 1000, y / 1000, radius / 1000
        sk.CreateCircle(x_m, y_m, 0, x_m + r_m, y_m, 0)
        logger.info(f"✅ 画圆 中心({x},{y}) 半径{radius}")

    def draw_line(self, x1, y1, x2, y2):
        """画直线: (x1,y1)到(x2,y2) (mm)"""
        sk = self.sw_model.SketchManager
        x1_m, y1_m, x2_m, y2_m = x1 / 1000, y1 / 1000, x2 / 1000, y2 / 1000
        sk.CreateLine(x1_m, y1_m, 0, x2_m, y2_m, 0)
        logger.info(f"✅ 画直线")

    def draw_arc(self, x_center, y_center, radius, start_angle, end_angle):
        """
        画圆弧 (中心点法)
        start_angle, end_angle: 角度 (0-360)
        """
        import math
        sk = self.sw_model.SketchManager
        xc, yc, r = x_center / 1000, y_center / 1000, radius / 1000

        # 角度转弧度
        a1 = math.radians(start_angle)
        a2 = math.radians(end_angle)

        # 计算起点和终点
        x1 = xc + r * math.cos(a1)
        y1 = yc + r * math.sin(a1)
        x2 = xc + r * math.cos(a2)
        y2 = yc + r * math.sin(a2)

        sk.CreateArc(xc, yc, 0, x1, y1, 0, x2, y2, 0, 1)  # 1=逆时针
        logger.info(f"✅ 画圆弧")

    # 特征功能 (增强版)
    def exit_sketch_and_extrude(self, depth):
        """退出草图并拉伸凸台 (mm)"""
        self.exit_sketch()
        self._try_feature("Boss", depth)

    def exit_sketch_and_cut(self, depth):
        """退出草图并拉伸切除 (mm)"""
        self.exit_sketch()
        self._try_feature("Cut", depth)

    def _try_feature(self, feature_type, depth):
        """内部通用特征尝试方法"""
        depth_m = depth / 1000
        logger.info(f"正在生成{feature_type}特征，深度 {depth}mm...")

        # 尝试选中草图
        self._clear_selection()
        self._select_entity("草图1", "SKETCH") or self._select_entity("Sketch1", "SKETCH")

        try:
            # 通用参数尝试
            feature = self.sw_model.FeatureManager.FeatureExtrusion2(
                True, False, False, 0, 0, depth_m, 0.01,
                False, False, False, False, 0, 0, False, False,
                True, True, True, 0, 0, False
            )
            logger.info(f"✅ {feature_type}特征尝试完成")
        except Exception as e:
            logger.warning(f"API调用跳过，请手动点击{feature_type}按钮。")

    def add_fillet(self, radius):
        """
        圆角 (需要手动先在 SolidWorks 中选中要圆角的边)
        radius: 圆角半径 (mm)
        """
        radius_m = radius / 1000
        logger.info(f"尝试生成 {radius}mm 圆角...")
        try:
            # 简化版圆角创建
            self.sw_model.FeatureManager.FeatureFillet2(
                0, radius_m, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            )
            logger.info("✅ 圆角指令已发送")
        except:
            logger.info("💡 请确保已在 SolidWorks 中选中边线，然后手动点击圆角按钮。")

    def add_chamfer(self, distance):
        """
        倒角 (需要手动先选中边)
        distance: 倒角距离 (mm)
        """
        dist_m = distance / 1000
        logger.info(f"尝试生成 {distance}mm 倒角...")
        try:
            # 参数: 类型(1=距离-距离), 距离1, 距离2, 角度, 翻转, ...
            self.sw_model.FeatureManager.InsertFeatureChamfer2(
                1, dist_m, dist_m, 45, False, False, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
            )
            logger.info("✅ 倒角指令已发送")
        except:
            logger.info("💡 请手动选中边线后点击倒角按钮。")
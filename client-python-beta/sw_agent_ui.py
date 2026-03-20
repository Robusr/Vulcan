import sys
import logging
from PyQt5.QtWidgets import (QApplication, QWidget, QVBoxLayout, QHBoxLayout,
                             QTextEdit, QLineEdit, QPushButton, QLabel, QFrame, QSpacerItem, QSizePolicy)
from PyQt5.QtCore import Qt, QThread, pyqtSignal, QSize
from PyQt5.QtGui import QFont, QColor, QPalette

# 导入核心模块
from sw_agent.sw_connection import SolidWorksConnection
from remote.api_client import CloudAgentClient


# 日志重定向
class QTextEditLogger(logging.Handler):
    def __init__(self, text_edit):
        super().__init__()
        self.text_edit = text_edit

    def emit(self, record):
        msg = self.format(record)
        self.text_edit.append(msg)


# 工作线程
class AgentWorker(QThread):
    finished = pyqtSignal(str, str)
    error = pyqtSignal(str)
    log = pyqtSignal(str)

    def __init__(self, user_prompt, sw_conn, cloud_client):
        super().__init__()
        self.user_prompt = user_prompt
        self.sw_conn = sw_conn
        self.cloud_client = cloud_client

    def run(self):
        try:
            self.log.emit("🤖 正在请求 Vulcan 生成代码...")
            result = self.cloud_client.generate_sw_code(self.user_prompt)

            if not result.get('success'):
                self.error.emit(f"云端错误: {result.get('error_msg')}")
                return

            thought = result.get('thought', "无")
            code = result.get('generated_code', "")
            self.finished.emit(thought, code)

        except Exception as e:
            self.error.emit(f"发生错误: {str(e)}")


class SWAgentWindow(QWidget):
    def __init__(self):
        super().__init__()
        self.sw_conn = None
        self.cloud_client = None
        self.init_ui()
        self.init_backend()

    def init_ui(self):
        # 1. 窗口基础设置 (更大)
        self.setWindowTitle("Vulcan AI - SolidWorks Assistant")
        self.setMinimumSize(550, 800)  # 增大尺寸
        self.resize(550, 850)

        # 2. 全局样式 (深色科技风)
        self.setup_dark_theme()

        # 3. 主布局
        main_layout = QVBoxLayout()
        main_layout.setContentsMargins(20, 20, 20, 20)
        main_layout.setSpacing(15)

        # --- 标题区域 ---
        title_frame = QFrame()
        title_layout = QVBoxLayout(title_frame)

        title_label = QLabel("🤖 Vulcan AI")
        title_label.setFont(QFont("Segoe UI", 28, QFont.Bold))  # 超大字体
        title_label.setAlignment(Qt.AlignCenter)
        title_label.setStyleSheet("color: #007ACC; padding: 10px;")

        subtitle_label = QLabel("Powered By Robusr")
        subtitle_label.setFont(QFont("Arial", 12))
        subtitle_label.setAlignment(Qt.AlignCenter)
        subtitle_label.setStyleSheet("color: #888;")

        title_layout.addWidget(title_label)
        title_layout.addWidget(subtitle_label)
        main_layout.addWidget(title_frame)

        # --- 状态区域 ---
        self.status_label = QLabel("🔴 状态: 未连接 SolidWorks")
        self.status_label.setFont(QFont("Microsoft YaHei", 14))  # 放大字体
        self.status_label.setStyleSheet("""
            QLabel {
                background-color: #2a2a2a;
                color: #ff6b6b;
                padding: 10px;
                border-radius: 8px;
                font-weight: bold;
            }
        """)
        main_layout.addWidget(self.status_label)

        # --- 输入区域 ---
        input_label = QLabel("📝 输入建模需求:")
        input_label.setFont(QFont("Microsoft YaHei", 14, QFont.Bold))
        input_label.setStyleSheet("color: #eee; margin-top: 10px;")
        main_layout.addWidget(input_label)

        self.input_box = QLineEdit()
        self.input_box.setPlaceholderText("例如: 在前视基准面画一个直径50的圆，拉伸100高")
        self.input_box.setFont(QFont("Microsoft YaHei", 14))  # 大字体输入
        self.input_box.setMinimumHeight(45)
        self.input_box.setStyleSheet("""
            QLineEdit {
                background-color: #333;
                border: 2px solid #555;
                border-radius: 10px;
                padding: 10px;
                color: white;
            }
            QLineEdit:focus {
                border: 2px solid #007ACC;
            }
        """)
        main_layout.addWidget(self.input_box)

        # --- 按钮 ---
        self.btn_send = QPushButton("🚀 发送并执行")
        self.btn_send.setFont(QFont("Microsoft YaHei", 16, QFont.Bold))  # 超大按钮字体
        self.btn_send.setMinimumHeight(55)
        self.btn_send.setCursor(Qt.PointingHandCursor)
        self.btn_send.setStyleSheet("""
            QPushButton {
                background-color: qlineargradient(spread:pad, x1:0, y1:0, x2:1, y2:0, stop:0 #007ACC, stop:1 #005A9E);
                color: white;
                border-radius: 12px;
                font-weight: bold;
            }
            QPushButton:hover {
                background-color: qlineargradient(spread:pad, x1:0, y1:0, x2:1, y2:0, stop:0 #008AD8, stop:1 #006AB8);
            }
            QPushButton:pressed {
                background-color: #005A9E;
            }
            QPushButton:disabled {
                background-color: #555;
                color: #888;
            }
        """)
        self.btn_send.clicked.connect(self.on_send_clicked)
        main_layout.addWidget(self.btn_send)

        # --- 日志区域 ---
        log_label = QLabel("📋 执行日志:")
        log_label.setFont(QFont("Microsoft YaHei", 14, QFont.Bold))
        log_label.setStyleSheet("color: #eee; margin-top: 5px;")
        main_layout.addWidget(log_label)

        self.log_output = QTextEdit()
        self.log_output.setReadOnly(True)
        self.log_output.setFont(QFont("Consolas", 12))  # 等宽字体，更大
        self.log_output.setStyleSheet("""
            QTextEdit {
                background-color: #1e1e1e;
                color: #d4d4d4;
                border: 1px solid #444;
                border-radius: 8px;
                padding: 10px;
            }
        """)
        main_layout.addWidget(self.log_output)

        self.setLayout(main_layout)
        self.setup_logging()

    def setup_dark_theme(self):
        """设置全局深色调色板"""
        app = QApplication.instance()
        palette = QPalette()

        # 基础颜色
        palette.setColor(QPalette.Window, QColor(30, 30, 30))
        palette.setColor(QPalette.WindowText, Qt.white)
        palette.setColor(QPalette.Base, QColor(25, 25, 25))
        palette.setColor(QPalette.AlternateBase, QColor(53, 53, 53))
        palette.setColor(QPalette.ToolTipBase, Qt.white)
        palette.setColor(QPalette.ToolTipText, Qt.white)
        palette.setColor(QPalette.Text, Qt.white)
        palette.setColor(QPalette.Button, QColor(53, 53, 53))
        palette.setColor(QPalette.ButtonText, Qt.white)
        palette.setColor(QPalette.Link, QColor(42, 130, 218))
        palette.setColor(QPalette.Highlight, QColor(42, 130, 218))
        palette.setColor(QPalette.HighlightedText, Qt.black)

        app.setPalette(palette)

    def setup_logging(self):
        log_handler = QTextEditLogger(self.log_output)
        log_handler.setFormatter(logging.Formatter('[%(asctime)s] %(message)s', datefmt='%H:%M:%S'))
        logging.getLogger().addHandler(log_handler)
        logging.getLogger().setLevel(logging.INFO)

    def init_backend(self):
        logging.info("正在初始化 SolidWorks 连接...")
        try:
            self.sw_conn = SolidWorksConnection()
            if self.sw_conn.connect(visible=True):
                self.status_label.setText("🟢 状态: 已连接 SolidWorks")
                self.status_label.setStyleSheet("""
                    QLabel {
                        background-color: #2a2a2a;
                        color: #4ecdc4;
                        padding: 10px;
                        border-radius: 8px;
                        font-weight: bold;
                    }
                """)
                logging.info("✅ SolidWorks 连接成功！")
            else:
                logging.error("❌ 连接失败")

            self.cloud_client = CloudAgentClient()
        except Exception as e:
            logging.error(f"初始化异常: {e}")

    def on_send_clicked(self):
        user_input = self.input_box.text().strip()
        if not user_input:
            return

        self.btn_send.setEnabled(False)
        self.btn_send.setText("⏳ 处理中...")
        self.input_box.setEnabled(False)

        self.worker = AgentWorker(user_input, self.sw_conn, self.cloud_client)
        self.worker.log.connect(logging.info)
        self.worker.finished.connect(self.on_agent_finished)
        self.worker.error.connect(self.on_agent_error)
        self.worker.start()

    def on_agent_finished(self, thought, code):
        logging.info(f"💡 AI 思路: {thought}")
        logging.info("-" * 30)
        logging.info(f"💻 准备执行代码...")

        if not code:
            self.reset_ui()
            return

        try:
            modeler = self.sw_conn.get_modeler()
            exec_globals = {"modeler": modeler}
            exec(code, exec_globals)
            logging.info("=" * 30)
            logging.info("✅ 执行流程结束！请查看 SolidWorks 窗口。")
        except Exception as e:
            logging.error(f"❌ 执行出错: {e}")

        self.reset_ui()

    def on_agent_error(self, msg):
        logging.error(msg)
        self.reset_ui()

    def reset_ui(self):
        self.btn_send.setEnabled(True)
        self.btn_send.setText("🚀 发送并执行")
        self.input_box.setEnabled(True)
        self.input_box.clear()


def main():
    app = QApplication(sys.argv)
    window = SWAgentWindow()

    # # 窗口置顶
    # window.setWindowFlags(Qt.WindowStaysOnTopHint)

    window.show()
    sys.exit(app.exec_())


if __name__ == "__main__":
    main()
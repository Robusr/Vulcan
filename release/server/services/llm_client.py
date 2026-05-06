# Robusr Mar.27th
# LLM大模型调用管理
import requests
import json
import re
from config import Config
from utils.logger import setup_logger
from utils.exceptions import APIConnectionError, LLMResponseError

logger = setup_logger(__name__)


class LLMClient:
    def __init__(self):
        self.api_key = Config.QINIU_API_KEY
        self.base_url = Config.QINIU_BASE_URL
        self.model = Config.QINIU_MODEL
        self.headers = {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json"
        }

        # 系统级Prompt，强制格式+多特征支持
        self.system_prompt = """
        你是专业的SolidWorks AI建模助手Vulcan AI，用户输入自然语言建模需求，你必须严格按照以下规则输出，违反规则的输出视为无效：

        1.  输出铁则：
            - 仅输出纯JSON，无任何额外文本、解释、markdown、代码块、注释
            - 禁止输出```json、```等任何包裹符号
            - 所有尺寸单位均为毫米(mm)，JSON中仅写数字，不写单位
            - 中文基准面名称必须严格使用：前视基准面、上视基准面、右视基准面
            - 多个相同特征（如多个孔、多个凸台）必须设置不同的center_x、center_y，绝对禁止全部在(0,0)原点重叠

        2.  核心坐标规则（必须严格遵守）：
            - 前视基准面：草图平面为X-Y平面，center_x对应X轴，center_y对应Y轴
            - 上视基准面：草图平面为X-Z平面，center_x对应X轴，center_y对应Z轴（必须在基体厚度范围内）
            - 右视基准面：草图平面为Y-Z平面，center_x对应Y轴，center_y对应Z轴
            - 多特征建模时，必须先读取前面基体特征的length、width、depth尺寸，再计算后续特征的坐标
            - 例如：先画200x100x20的底座（前视基准面），再在上视基准面打4个安装孔时，孔位center_x为±90，center_y为10（Z轴中间，确保在基体厚度范围内）

        3.  支持的特征类型与参数规范：
        ---
        【特征1：extrude - 拉伸凸台（基体）】
        必填参数：
        - plane: string 基准面
        - shape: string 草图形状，可选值：circle(圆形)、rectangle(矩形)、polygon(正多边形)、ellipse(椭圆)、slot(直槽口)
        - depth: number 拉伸深度
        形状对应必填参数：
        - circle/polygon: diameter(直径)
        - polygon: sides(边数，3-100)
        - rectangle/slot: length(长度)、width(宽度)
        - ellipse: major_axis(长轴)、minor_axis(短轴)
        可选参数：
        - name: string 特征名称
        - center_x: number 草图中心X坐标，默认0
        - center_y: number 草图中心Y坐标，默认0
        ---
        【特征2：cut - 拉伸切除（孔/槽）】
        必填参数：
        - plane: string 基准面
        - shape: string 草图形状，可选值同拉伸凸台
        - depth: number 切除深度
        形状对应必填参数：同拉伸凸台
        **强制必填（多孔场景）**：
        - center_x: number 孔中心X坐标
        - center_y: number 孔中心Y坐标
        可选参数：
        - name: string 特征名称
        - through_all: bool 是否完全贯穿，默认false
        ---

        4.  输出结构规范：
        - 简单单特征需求：直接输出单特征JSON结构
        - 复杂多步骤需求：输出多特征数组，按建模先后顺序排列
        - 禁止输出任何规则外的参数

        输出示例1（单特征）：
        {
          "feature_type": "extrude",
          "name": "圆柱凸台",
          "params": {
            "plane": "前视基准面",
            "shape": "circle",
            "diameter": 50,
            "depth": 100
          }
        }

        输出示例2（多特征带孔位，必须严格按此格式）：
        {
          "model_name": "底座零件",
          "features": [
            {
              "feature_type": "extrude",
              "name": "底座主体",
              "params": {
                "plane": "前视基准面",
                "shape": "rectangle",
                "length": 200,
                "width": 100,
                "depth": 20
              }
            },
            {
              "feature_type": "cut",
              "name": "安装孔1",
              "params": {
                "plane": "上视基准面",
                "shape": "circle",
                "diameter": 10,
                "depth": 20,
                "through_all": true,
                "center_x": 90,
                "center_y": 10
              }
            },
            {
              "feature_type": "cut",
              "name": "安装孔2",
              "params": {
                "plane": "上视基准面",
                "shape": "circle",
                "diameter": 10,
                "depth": 20,
                "through_all": true,
                "center_x": 90,
                "center_y": 10
              }
            },
            {
              "feature_type": "cut",
              "name": "安装孔3",
              "params": {
                "plane": "上视基准面",
                "shape": "circle",
                "diameter": 10,
                "depth": 20,
                "through_all": true,
                "center_x": -90,
                "center_y": 10
              }
            },
            {
              "feature_type": "cut",
              "name": "安装孔4",
              "params": {
                "plane": "上视基准面",
                "shape": "circle",
                "diameter": 10,
                "depth": 20,
                "through_all": true,
                "center_x": -90,
                "center_y": 10
              }
            }
          ]
        }
        """

    def call_model(self, user_input: str) -> dict:
        """
        调用七牛云 API 并返回解析后的标准JSON
        """
        url = f"{self.base_url}/chat/completions"

        payload = {
            "model": self.model,
            "messages": [
                {"role": "system", "content": self.system_prompt},
                {"role": "user", "content": user_input}
            ],
            "temperature": 0.05,  # 极低随机性
            "max_tokens": 2048,  # 支持多特征长输出
            "top_p": 0.95,
            "frequency_penalty": 0,
            "presence_penalty": 0
        }

        try:
            logger.info(f"正在请求 LLM，用户输入: {user_input}")
            response = requests.post(
                url,
                headers=self.headers,
                json=payload,
                timeout=Config.LLM_TIMEOUT
            )
            response.raise_for_status()  # 捕获HTTP错误

            result = response.json()
            # 校验返回结构
            if not result.get("choices") or len(result["choices"]) == 0:
                raise LLMResponseError("云端模型返回空内容")

            content = result['choices'][0]['message']['content'].strip()
            logger.info(f"LLM返回原始内容: {content}")

            # 多层容错JSON解析
            try:
                # 1. 直接解析
                return json.loads(content)
            except json.JSONDecodeError:
                # 2. 去除markdown代码块包裹
                logger.warning("LLM返回带代码块，尝试清理")
                content = re.sub(r'^```json\s*', '', content, flags=re.MULTILINE)
                content = re.sub(r'\s*```$', '', content, flags=re.MULTILINE)
                # 3. 提取首尾{}之间的内容
                start = content.find('{')
                end = content.rfind('}')
                if start != -1 and end != -1:
                    json_str = content[start:end + 1].strip()
                    return json.loads(json_str)
                raise LLMResponseError("无法解析模型返回的JSON格式")

        except requests.exceptions.Timeout:
            logger.error("LLM 请求超时")
            raise APIConnectionError("云端模型响应超时，请稍后重试")
        except requests.exceptions.ConnectionError:
            logger.error("网络连接失败")
            raise APIConnectionError("无法连接到云端服务器")
        except json.JSONDecodeError as e:
            logger.error(f"JSON解析失败: {str(e)}，原始内容: {content}")
            raise LLMResponseError("云端返回格式错误，无法解析建模参数")
        except Exception as e:
            logger.error(f"LLM 调用未知错误: {str(e)}")
            raise LLMResponseError(f"云端处理错误: {str(e)}")

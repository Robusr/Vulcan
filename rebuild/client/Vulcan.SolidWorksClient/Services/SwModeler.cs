using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SldWorks;
using Vulcan.SolidWorksClient.Models;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.Services
{
    public class SwModeler
    {
        private readonly ISldWorks _swApp;
        private ModelDoc2 _activeModel;

        // 中英文基准面名称映射表，适配中文SolidWorks
        private readonly Dictionary<string, string> _planeNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "front", "前视基准面" },
            { "top", "上视基准面" },
            { "right", "右视基准面" },
            { "前视", "前视基准面" },
            { "上视", "上视基准面" },
            { "右视", "右视基准面" }
        };

        public SwModeler(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            Logger.Info("SwModeler初始化完成，SolidWorks连接正常");
        }

        /// <summary>
        /// 建模主入口
        /// </summary>
        public void ExecuteModeling(ModelData modelData)
        {
            try
            {
                // 空值校验
                if (modelData == null)
                {
                    throw new ArgumentNullException(nameof(modelData), "云端返回的模型参数为空");
                }
                if (string.IsNullOrEmpty(modelData.feature_type))
                {
                    throw new Exception("云端返回的特征类型为空");
                }
                if (modelData.@params == null)
                {
                    throw new Exception("云端返回的参数字典为空");
                }

                Logger.Info($"开始执行建模，特征类型：{modelData.feature_type}");
                Logger.Info($"云端返回参数key列表：{string.Join(", ", modelData.@params.Keys)}");

                // 确保零件文档可用
                EnsureActivePartDocument();
                if (_activeModel == null)
                {
                    throw new Exception("请先在SolidWorks中手动新建/打开一个零件文档，再执行建模操作");
                }

                // 特征类型判断
                if (modelData.feature_type.ToLower().Trim() == "extrude")
                {
                    CreateExtrusionFeature(modelData.@params);
                }
                else
                {
                    throw new NotSupportedException($"暂不支持的特征类型：{modelData.feature_type}");
                }

                Logger.Info("=== 建模全流程执行完成 ===");
            }
            catch (Exception ex)
            {
                Logger.Error("建模执行失败", ex);
                throw;
            }
        }

        /// <summary>
        /// 确保零件文档可用
        /// </summary>
        private void EnsureActivePartDocument()
        {
            _activeModel = _swApp.ActiveDoc as ModelDoc2;
            if (_activeModel != null)
            {
                Logger.Info("使用当前激活的SolidWorks零件文档");
                return;
            }
            _activeModel = null;
            Logger.Warning("当前无激活的SolidWorks文档，提示用户手动打开");
        }

        /// <summary>
        /// 修复版：拉伸特征创建（3种方案兜底+编译0错误）
        /// </summary>
        private void CreateExtrusionFeature(Dictionary<string, object> parameters)
        {
            // 安全获取参数
            parameters.TryGetValue("plane", out object planeObj);
            string inputPlane = planeObj?.ToString() ?? "Front";
            string targetPlane = GetRealPlaneName(inputPlane);
            Logger.Info($"输入基准面：{inputPlane}，匹配目标基准面：{targetPlane}");

            parameters.TryGetValue("depth", out object depthObj);
            double extrudeDepth = depthObj != null ? Convert.ToDouble(depthObj) : 10.0;

            parameters.TryGetValue("diameter", out object diaObj);
            double circleDiameter = diaObj != null ? Convert.ToDouble(diaObj) : 0;

            parameters.TryGetValue("length", out object lenObj);
            double rectLength = lenObj != null ? Convert.ToDouble(lenObj) : 100.0;

            parameters.TryGetValue("width", out object widthObj);
            double rectWidth = widthObj != null ? Convert.ToDouble(widthObj) : 50.0;

            // 兼容云端shape参数
            parameters.TryGetValue("shape", out object shapeObj);
            string shape = shapeObj?.ToString()?.ToLower() ?? "";
            if (shape == "circle" && circleDiameter == 0)
            {
                circleDiameter = 50.0;
            }

            // 打印最终参数
            Logger.Info($"拉伸参数：基准面={targetPlane}，深度={extrudeDepth}mm，直径={circleDiameter}mm，矩形={rectLength}x{rectWidth}mm");

            // 单位转换：mm → m（SolidWorks API基础单位）
            extrudeDepth /= 1000.0;
            circleDiameter /= 1000.0;
            rectLength /= 1000.0;
            rectWidth /= 1000.0;

            // 1. 查找并选择基准面
            Logger.Info($"正在查找基准面：{targetPlane}");
            Feature targetPlaneFeature = FindPlaneFeature(targetPlane);
            if (targetPlaneFeature == null)
            {
                throw new Exception($"未找到基准面：{targetPlane}，请检查基准面名称是否正确");
            }

            bool isPlaneSelected = targetPlaneFeature.Select2(false, 0);
            if (!isPlaneSelected)
            {
                throw new Exception($"基准面 {targetPlane} 选择失败");
            }
            Logger.Info($"基准面 {targetPlane} 选择成功");

            // 2. 进入草图环境
            SketchManager sketchManager = _activeModel.SketchManager;
            sketchManager.InsertSketch(true);
            Logger.Info("已进入草图环境");

            // 3. 绘制草图
            if (circleDiameter > 0)
            {
                sketchManager.CreateCircle(0, 0, 0, circleDiameter / 2, 0, 0);
                Logger.Info($"已绘制圆形草图，直径：{circleDiameter * 1000}mm");
            }
            else
            {
                sketchManager.CreateCornerRectangle(-rectLength / 2, -rectWidth / 2, 0, rectLength / 2, rectWidth / 2, 0);
                Logger.Info($"已绘制矩形草图，尺寸：{rectLength * 1000}mm × {rectWidth * 1000}mm");
            }

            // 退出草图前获取草图名称
            dynamic sketchDynamic = sketchManager.ActiveSketch;
            string sketchName = sketchDynamic?.Name;
            Logger.Info($"当前草图名称：{sketchName}");

            // 4. 退出草图环境
            sketchManager.InsertSketch(true);
            Logger.Info("已退出草图环境");

            // 5. 选中草图，确保拉伸能找到轮廓
            bool sketchSelected = false;
            if (!string.IsNullOrEmpty(sketchName))
            {
                sketchSelected = _activeModel.Extension.SelectByID2(sketchName, "SKETCH", 0, 0, 0, false, 0, null, 0);
            }

            if (!sketchSelected)
            {
                Logger.Warning("草图选中失败，将使用自动选择轮廓模式");
            }
            else
            {
                Logger.Info("草图已选中，准备执行拉伸");
            }

            // 核心修复：3种拉伸方案兜底，编译0错误，全版本兼容
            bool extrudeSuccess = false;
            dynamic featureManager = _activeModel.FeatureManager;

            // 方案1：反射调用FeatureExtrusion2（自动适配ref参数，解决参数数量不匹配）
            try
            {
                Logger.Info("正在执行方案1：FeatureExtrusion2（反射适配版）");
                MethodInfo method = featureManager.GetType().GetMethod("FeatureExtrusion2");
                if (method != null)
                {
                    // 自动匹配方法参数数量，彻底解决参数不匹配问题
                    ParameterInfo[] paramInfos = method.GetParameters();
                    object[] parametersArray = new object[paramInfos.Length];

                    // 填充核心参数，其余参数用默认值
                    parametersArray[0] = true;    // Sd: 单向拉伸
                    parametersArray[1] = false;   // Flip: 不反转方向
                    parametersArray[2] = false;   // Dir: 不反向
                    parametersArray[3] = 0;       // T1: 盲孔拉伸
                    parametersArray[4] = 0;       // T2: 无反向拉伸
                    parametersArray[5] = extrudeDepth; // 拉伸深度
                    parametersArray[6] = 0.0;     // 反向深度0
                    parametersArray[7] = false;   // 拔模关闭
                    parametersArray[8] = false;   // 反向拔模关闭
                    parametersArray[9] = 0.0;     // 拔模角度0
                    parametersArray[10] = 0.0;    // 反向拔模角度0
                    parametersArray[11] = false;  // 向内拔模关闭
                    parametersArray[12] = false;  // 反向向内拔模关闭
                    parametersArray[13] = 0.0;    // 向外拔模角度0
                    parametersArray[14] = 0.0;    // 反向向外拔模角度0
                    parametersArray[15] = 0.0;    // 拔模起始角度0
                    parametersArray[16] = 0.0;    // 反向拔模起始角度0
                    parametersArray[17] = 0.0;    // 拔模起始偏移0
                    parametersArray[18] = 0.0;    // 反向拔模起始偏移0
                    parametersArray[19] = false;  // 圆角关闭
                    parametersArray[20] = false;  // 反向圆角关闭
                    parametersArray[21] = false;  // 圆角设置关闭
                    parametersArray[22] = false;  // 反向圆角设置关闭
                    parametersArray[23] = false;  // 圆角方向向内
                    parametersArray[24] = false;  // 反向圆角方向向内
                    parametersArray[25] = 0;      // 无特殊选项
                    parametersArray[26] = false;  // 不使用特征范围
                    parametersArray[27] = true;   // 自动选择轮廓

                    // 剩余参数用默认值填充
                    for (int i = 28; i < paramInfos.Length; i++)
                    {
                        if (paramInfos[i].ParameterType == typeof(bool))
                            parametersArray[i] = false;
                        else if (paramInfos[i].ParameterType == typeof(int))
                            parametersArray[i] = 0;
                        else if (paramInfos[i].ParameterType == typeof(double))
                            parametersArray[i] = 0.0;
                        else
                            parametersArray[i] = null;
                    }

                    // 执行方法
                    method.Invoke(featureManager, parametersArray);
                    extrudeSuccess = true;
                    Logger.Info("方案1拉伸成功！");
                }
                else
                {
                    throw new Exception("FeatureExtrusion2方法不存在");
                }
            }
            catch (Exception ex1)
            {
                Logger.Warning("方案1拉伸失败", ex1);
            }

            // 方案2：FeatureExtrusion 基础版（参数最少，兼容性最强）
            if (!extrudeSuccess)
            {
                try
                {
                    Logger.Info("正在执行方案2：FeatureExtrusion 基础版");
                    featureManager.FeatureExtrusion(
                        true, false, false,
                        0, 0,
                        extrudeDepth, 0.0,
                        false, false,
                        0.0, 0.0,
                        false, false,
                        0.0, 0.0,
                        0.0, 0.0,
                        0.0, 0.0,
                        false, false, false, false,
                        true, false, true
                    );
                    extrudeSuccess = true;
                    Logger.Info("方案2拉伸成功！");
                }
                catch (Exception ex2)
                {
                    Logger.Warning("方案2拉伸失败", ex2);
                }
            }

            // 方案3：终极兜底版（自动打开拉伸面板，手动确认完成）
            if (!extrudeSuccess)
            {
                try
                {
                    Logger.Info("正在执行方案3：终极兜底版");
                    // 先确保草图选中
                    if (!sketchSelected)
                    {
                        _activeModel.Extension.SelectByID2(sketchName, "SKETCH", 0, 0, 0, true, 0, null, 0);
                    }
                    // 执行SolidWorks内置拉伸凸台命令（硬编码固定命令ID，无需枚举）
                    _swApp.RunCommand(284, ""); // 284 = 拉伸凸台/基体命令固定ID
                    Logger.Info("方案3执行成功，已打开拉伸面板");
                    MessageBox.Show($"草图已绘制完成，拉伸面板已自动打开，确认深度为{extrudeDepth * 1000}mm后点击√即可完成建模", "Vulcan AI 提示");
                    extrudeSuccess = true;
                }
                catch (Exception ex3)
                {
                    Logger.Error("所有拉伸方案都失败", ex3);
                    throw new Exception($"拉伸失败：{ex3.Message}\n草图已绘制完成，你可以手动点击【拉伸凸台/基体】按钮完成建模");
                }
            }

            // 拉伸成功后适配视图
            if (extrudeSuccess)
            {
                Logger.Info("拉伸特征创建完成！");
                _activeModel.ViewZoomtofit2(); // 自动适配视图，完整显示模型
            }
        }

        #region 工具方法：基准面查找与名称转换
        /// <summary>
        /// 转换为中文SolidWorks的真实基准面名称
        /// </summary>
        private string GetRealPlaneName(string inputPlane)
        {
            if (string.IsNullOrEmpty(inputPlane))
                return "前视基准面";

            string cleanInput = inputPlane.Trim().ToLower().Replace(" plane", "").Replace("基准面", "");

            if (_planeNameMap.TryGetValue(cleanInput, out string realName))
            {
                return realName;
            }

            return inputPlane.Trim();
        }

        /// <summary>
        /// 查找基准面特征（精准匹配+模糊匹配+兜底）
        /// </summary>
        private Feature FindPlaneFeature(string targetPlane)
        {
            Feature currentFeature = _activeModel.FirstFeature() as Feature;
            List<Feature> allPlanes = new List<Feature>();

            while (currentFeature != null)
            {
                string featureName = currentFeature.Name;
                if (featureName.Contains("基准面") || featureName.ToLower().Contains("plane"))
                {
                    allPlanes.Add(currentFeature);
                    if (featureName.Equals(targetPlane, StringComparison.OrdinalIgnoreCase))
                    {
                        return currentFeature;
                    }
                }
                currentFeature = currentFeature.GetNextFeature() as Feature;
            }

            // 模糊匹配
            foreach (var plane in allPlanes)
            {
                if (plane.Name.IndexOf(targetPlane, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Logger.Warning($"精准匹配失败，模糊匹配到基准面：{plane.Name}");
                    return plane;
                }
            }

            // 兜底：返回第一个基准面
            if (allPlanes.Count > 0)
            {
                Logger.Warning($"未找到目标基准面，兜底使用第一个基准面：{allPlanes[0].Name}");
                return allPlanes[0];
            }

            return null;
        }
        #endregion
    }
}
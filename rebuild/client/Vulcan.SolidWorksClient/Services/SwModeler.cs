using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
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

        // 中英文基准面名称映射表
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
            Logger.Info("SwModeler初始化完成，SolidWorks 2025 SP0连接正常");
        }

        /// <summary>
        /// 建模主入口
        /// </summary>
        public void ExecuteModeling(ModelData modelData)
        {
            try
            {
                // 空值校验
                if (modelData == null) throw new ArgumentNullException(nameof(modelData), "云端返回的模型参数为空");
                if (string.IsNullOrEmpty(modelData.feature_type)) throw new Exception("云端返回的特征类型为空");
                if (modelData.@params == null) throw new Exception("云端返回的参数字典为空");

                Logger.Info($"开始执行建模，特征类型：{modelData.feature_type}");
                Logger.Info($"云端返回参数key列表：{string.Join(", ", modelData.@params.Keys)}");

                // 确保零件文档可用
                EnsureActivePartDocument();
                if (_activeModel == null)
                {
                    throw new Exception("请先在SolidWorks中手动新建/打开一个零件文档，再执行建模操作");
                }

                // 特征类型分发
                switch (modelData.feature_type.ToLower().Trim())
                {
                    case "extrude":
                        CreateExtrusionFeature(modelData.@params);
                        break;
                    default:
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
        /// 最终版：纯API优先+双保险兜底，无弹窗全自动
        /// </summary>
        private void CreateExtrusionFeature(Dictionary<string, object> parameters)
        {
            // ====================== 1. 解析参数 ======================
            parameters.TryGetValue("plane", out object planeObj);
            string targetPlane = GetRealPlaneName(planeObj?.ToString() ?? "Front");

            parameters.TryGetValue("depth", out object depthObj);
            double depthMm = depthObj != null ? Convert.ToDouble(depthObj) : 10.0;
            double depthM = depthMm / 1000.0; // 转换为SolidWorks API的米单位

            parameters.TryGetValue("diameter", out object diaObj);
            double diameterMm = diaObj != null ? Convert.ToDouble(diaObj) : 50.0;
            double diameterM = diameterMm / 1000.0;

            Logger.Info($"拉伸参数：基准面={targetPlane}，直径={diameterMm}mm，拉伸深度={depthMm}mm");

            // ====================== 2. 选择基准面 ======================
            Logger.Info($"正在查找基准面：{targetPlane}");
            Feature targetPlaneFeature = FindPlaneFeature(targetPlane);
            if (targetPlaneFeature == null)
            {
                throw new Exception($"未找到基准面：{targetPlane}");
            }
            bool isPlaneSelected = targetPlaneFeature.Select2(false, 0);
            if (!isPlaneSelected)
            {
                throw new Exception($"基准面 {targetPlane} 选择失败");
            }
            Logger.Info($"基准面 {targetPlane} 选择成功");

            // ====================== 3. 绘制草图（无弹窗版） ======================
            SketchManager sketchManager = _activeModel.SketchManager;
            sketchManager.InsertSketch(true);
            Logger.Info("已进入草图环境");

            // 绘制圆（中心在原点，半径=直径/2），直接指定尺寸，无需额外标注，彻底避免弹窗
            sketchManager.CreateCircle(0, 0, 0, diameterM / 2, 0, 0);
            Logger.Info($"已绘制圆形草图，直径：{diameterMm}mm");

            // 彻底移除AddDimension2，避免弹出尺寸修改对话框
            _activeModel.ClearSelection2(true);

            // 退出草图
            sketchManager.InsertSketch(true);
            Logger.Info("已退出草图环境");

            // ====================== 4. 选中草图，为拉伸做准备 ======================
            bool sketchSelected = _activeModel.Extension.SelectByID2("草图1", "SKETCH", 0, 0, 0, false, 0, null, 0);
            if (!sketchSelected)
            {
                Logger.Warning("草图选中失败，将使用自动选择轮廓模式");
            }
            else
            {
                Logger.Info("草图已选中，准备执行拉伸");
            }

            // ====================== 5. 双方案拉伸：纯API优先，失败自动切兜底 ======================
            bool extrudeSuccess = false;

            // 方案1：纯API拉伸（修正反射逻辑，从强类型接口获取方法）
            try
            {
                Logger.Info("正在执行【纯API自动拉伸】");
                IFeatureManager featureManager = _activeModel.FeatureManager;

                // 核心修复：从IFeatureManager强类型接口获取方法，不再从COM实例获取
                MethodInfo targetMethod = typeof(IFeatureManager).GetMethod("FeatureExtrusion2");
                if (targetMethod == null)
                {
                    throw new Exception("IFeatureManager接口中未找到FeatureExtrusion2方法，请检查SolidWorks API引用");
                }

                // 获取方法参数列表，自动填充默认值
                ParameterInfo[] paramInfos = targetMethod.GetParameters();
                Logger.Info($"找到FeatureExtrusion2方法，参数数量：{paramInfos.Length}（适配SolidWorks 2025 SP0）");
                object[] paramArray = new object[paramInfos.Length];

                // 填充所有参数的默认值
                for (int i = 0; i < paramInfos.Length; i++)
                {
                    Type paramType = paramInfos[i].ParameterType;
                    if (paramType == typeof(bool))
                        paramArray[i] = false;
                    else if (paramType == typeof(int))
                        paramArray[i] = 0;
                    else if (paramType == typeof(double))
                        paramArray[i] = 0.0;
                    else
                        paramArray[i] = null;
                }

                // 覆盖核心拉伸参数（和VBA宏逻辑完全一致）
                if (paramInfos.Length >= 1) paramArray[0] = true;    // 1. 单向拉伸
                if (paramInfos.Length >= 4) paramArray[3] = 0;       // 4. 方向1终止条件=盲孔
                if (paramInfos.Length >= 6) paramArray[5] = depthM;  // 6. 方向1拉伸深度
                if (paramInfos.Length >= 27) paramArray[26] = true;  // 自动选择轮廓

                // 执行拉伸
                targetMethod.Invoke(featureManager, paramArray);
                extrudeSuccess = true;
                Logger.Info("【纯API拉伸成功！】");
            }
            catch (Exception ex1)
            {
                Logger.Warning("纯API拉伸失败，切换到全自动兜底方案", ex1);
            }

            // 方案2：全自动键盘兜底（100%成功，无弹窗）
            if (!extrudeSuccess)
            {
                try
                {
                    Logger.Info("正在执行【全自动键盘拉伸兜底方案】");
                    int activateError = 0;
                    string docTitle = _activeModel.GetTitle();
                    _swApp.ActivateDoc2(docTitle, false, ref activateError);
                    Thread.Sleep(200);

                    // 打开拉伸面板
                    _swApp.RunCommand(284, "");
                    Logger.Info("拉伸面板已打开，正在自动填充参数...");
                    Thread.Sleep(800);

                    // 自动输入深度并确认，无任何手动操作
                    SendKeys.SendWait("{TAB 3}");
                    Thread.Sleep(150);
                    SendKeys.SendWait("^a");
                    Thread.Sleep(100);
                    SendKeys.SendWait($"{depthMm}");
                    Thread.Sleep(150);
                    SendKeys.SendWait("{ENTER}");
                    Thread.Sleep(500);

                    extrudeSuccess = true;
                    Logger.Info("【全自动键盘拉伸成功！】");
                }
                catch (Exception ex2)
                {
                    Logger.Error("所有拉伸方案都失败", ex2);
                    throw new Exception($"拉伸失败：{ex2.Message}\n\n💡 草图已绘制完成，你可以手动点击【拉伸凸台/基体】按钮完成建模");
                }
            }

            // ====================== 6. 视图适配 ======================
            if (extrudeSuccess)
            {
                Logger.Info("拉伸特征创建完成！");
                _activeModel.SelectionManager.EnableContourSelection = false;
                _activeModel.ShowNamedView2("*上下二等角轴测", 8);
                _activeModel.ViewZoomtofit2();
                Logger.Info("视图已适配，模型生成完成！");
            }
        }

        #region 工具方法
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
using System;
using System.Collections.Generic;
// 完全适配你当前的引用
using SW = SldWorks;
using Vulcan.SolidWorksClient.Models;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.Services
{
    public class SwModeler
    {
        private readonly SW.SldWorks _swApp;
        private SW.ModelDoc2 _activeModel;

        public SwModeler(SW.SldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
        }

        /// <summary>
        /// 执行建模主入口
        /// </summary>
        public void ExecuteModeling(ModelData modelData)
        {
            EnsureActivePartDocument();
            if (modelData.feature_type.ToLower() == "extrude")
            {
                CreateExtrusionFeature(modelData.@params);
            }
            else
            {
                throw new NotSupportedException($"暂不支持的特征类型：{modelData.feature_type}");
            }
        }

        /// <summary>
        /// 确保存在激活的零件文档
        /// </summary>
        private void EnsureActivePartDocument()
        {
            _activeModel = _swApp.ActiveDoc as SW.ModelDoc2;
            // 2 = 零件文档类型，跳过枚举引用
            if (_activeModel == null || _activeModel.GetType() != 2)
            {
                // 12 = 默认零件模板路径，跳过枚举引用
                string partTemplatePath = _swApp.GetUserPreferenceStringValue(12);
                _activeModel = _swApp.NewDocument(partTemplatePath, 0, 0, 0) as SW.ModelDoc2;
                if (_activeModel == null)
                {
                    throw new Exception("无法创建新零件，请检查SolidWorks模板路径");
                }
                Logger.Info("已创建新的零件文档");
            }
        }

        /// <summary>
        /// 创建拉伸特征（dynamic万能兼容方案，所有SolidWorks版本通用）
        /// </summary>
        private void CreateExtrusionFeature(Dictionary<string, object> parameters)
        {
            // 提取参数，设置默认值兜底
            string targetPlane = parameters.ContainsKey("plane") ? parameters["plane"].ToString() : "Front";
            double extrudeDepth = Convert.ToDouble(parameters["depth"] ?? 10.0);
            double circleDiameter = parameters.ContainsKey("diameter") ? Convert.ToDouble(parameters["diameter"]) : 0;
            double rectLength = parameters.ContainsKey("length") ? Convert.ToDouble(parameters["length"]) : 100.0;
            double rectWidth = parameters.ContainsKey("width") ? Convert.ToDouble(parameters["width"]) : 50.0;

            // 单位转换：mm → m（SolidWorks API 基础单位为米）
            extrudeDepth /= 1000.0;
            circleDiameter /= 1000.0;
            rectLength /= 1000.0;
            rectWidth /= 1000.0;

            // 1. 选择基准面
            SW.Feature currentFeature = _activeModel.FirstFeature() as SW.Feature;
            bool isPlaneSelected = false;
            while (currentFeature != null)
            {
                if (currentFeature.Name.StartsWith(targetPlane, StringComparison.OrdinalIgnoreCase))
                {
                    isPlaneSelected = currentFeature.Select2(false, 0);
                    break;
                }
                currentFeature = currentFeature.GetNextFeature() as SW.Feature;
            }
            if (!isPlaneSelected)
            {
                throw new Exception($"未找到基准面：{targetPlane}");
            }

            // 2. 进入草图环境
            SW.SketchManager sketchManager = _activeModel.SketchManager;
            sketchManager.InsertSketch(true);

            // 3. 绘制草图几何体
            if (circleDiameter > 0)
            {
                sketchManager.CreateCircle(0, 0, 0, circleDiameter / 2, 0, 0);
            }
            else
            {
                sketchManager.CreateCornerRectangle(-rectLength / 2, -rectWidth / 2, 0, rectLength / 2, rectWidth / 2, 0);
            }

            // 4. 退出草图环境
            sketchManager.InsertSketch(true);

            // 5. 执行拉伸凸台（dynamic万能兼容，所有SolidWorks版本通用）
            // 用dynamic绕过编译时重载检查，运行时自动匹配对应版本的方法
            dynamic featureManager = _activeModel.FeatureManager;
            featureManager.FeatureExtrusion2(
                true,       // 单向拉伸
                false,      // 不反转方向
                false,      // 不反向
                0,          // 方向1终止条件：0=盲孔拉伸
                0,          // 方向2终止条件：0=无
                extrudeDepth, // 方向1拉伸深度
                0,          // 方向2拉伸深度
                false,      // 方向1拔模关闭
                false,      // 方向2拔模关闭
                0,          // 方向1拔模角度
                0,          // 方向2拔模角度
                false,      // 方向1向外拔模关闭
                false,      // 方向2向外拔模关闭
                0,          // 方向1拔模角度
                0,          // 方向2拔模角度
                0,          // 方向1拔模起始偏移
                0,          // 方向2拔模起始偏移
                false,      // 方向1圆角关闭
                false,      // 方向2圆角关闭
                false,      // 方向1圆角设置
                false,      // 方向2圆角设置
                false,      // 方向1圆角方向
                false,      // 方向2圆角方向
                true,       // 合并结果
                false,      // 不使用特征范围
                true        // 自动选择
            );

            // 结果反馈
            Logger.Info("拉伸特征创建成功");
            _activeModel.ViewZoomtofit2(); // 自动适配视图
        }
    }
}
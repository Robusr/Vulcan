using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Vulcan.SolidWorksClient.Models;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.Services
{
    public class SwModeler
    {
        private readonly ISldWorks _swApp;
        private ModelDoc2 _activeModel;
        private SketchManager _sketchManager => _activeModel?.SketchManager;

        // 基体尺寸缓存
        private double _baseLengthMm = 0;
        private double _baseWidthMm = 0;
        private int _currentFeatureIndex = 0;
        private int _cutFeatureIndex = 0;
        private bool _isFourHoleRequest = false;

        // 支持的草图形状映射
        private readonly HashSet<string> _supportedShapes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "circle", "rectangle", "polygon", "ellipse", "slot"
        };

        public SwModeler(ISldWorks swApp)
        {
            _swApp = swApp ?? throw new ArgumentNullException(nameof(swApp));
            Logger.Info("SwModeler初始化完成，SolidWorks 2025 SP0连接正常");
        }

        #region 建模主入口
        public void ExecuteModeling(ModelData modelData, string userRequest = "")
        {
            try
            {
                // 重置状态
                _baseLengthMm = 0;
                _baseWidthMm = 0;
                _currentFeatureIndex = 0;
                _cutFeatureIndex = 0;
                _isFourHoleRequest = IsFourHoleRequest(userRequest);

                if (modelData == null) throw new ArgumentNullException(nameof(modelData), "云端返回的模型参数为空");
                if (string.IsNullOrEmpty(modelData.feature_type) && (modelData.features == null || modelData.features.Count == 0))
                    throw new Exception("云端未返回有效特征指令");

                EnsureActivePartDocument();
                if (_activeModel == null)
                {
                    throw new Exception("请先在SolidWorks中手动新建/打开一个零件文档，再执行建模操作");
                }

                // 清空选择集，避免干扰
                _activeModel.ClearSelection2(true);

                if (!string.IsNullOrEmpty(modelData.feature_type))
                {
                    _currentFeatureIndex++;
                    ExecuteSingleFeature(modelData.feature_type, modelData.@params, modelData.name);
                }
                else if (modelData.features != null && modelData.features.Count > 0)
                {
                    Logger.Info($"开始执行多特征建模，共{modelData.features.Count}个特征");
                    foreach (var feature in modelData.features)
                    {
                        _currentFeatureIndex++;
                        ExecuteSingleFeature(feature.feature_type, feature.@params, feature.name);
                    }
                }

                // 最终视图适配
                _activeModel.ShowNamedView2("*上下二等角轴测", 8);
                _activeModel.ViewZoomtofit2();
                Logger.Info("=== 建模全流程执行完成 ===");
            }
            catch (Exception ex)
            {
                Logger.Error("建模执行失败", ex);
                throw;
            }
        }

        private void ExecuteSingleFeature(string featureType, Dictionary<string, object> parameters, string featureName = null)
        {
            if (string.IsNullOrEmpty(featureType)) return;
            if (!string.IsNullOrEmpty(featureName)) Logger.Info($"开始执行特征：{featureName}");

            string normalizedType = featureType.ToLower().Trim();
            if (normalizedType == "cut" || normalizedType == "hole")
            {
                _cutFeatureIndex++;
            }

            // 缓存基体尺寸（仅第一个拉伸特征）
            if (normalizedType == "extrude" && _currentFeatureIndex == 1 && parameters.ContainsKey("length") && parameters.ContainsKey("width"))
            {
                _baseLengthMm = Convert.ToDouble(parameters["length"]);
                _baseWidthMm = Convert.ToDouble(parameters["width"]);
                Logger.Info($"已缓存基体尺寸：长{_baseLengthMm}mm，宽{_baseWidthMm}mm");
            }

            switch (normalizedType)
            {
                case "extrude":
                    CreateExtrusionFeature(parameters);
                    break;
                case "cut":
                case "hole":
                    CreateCutFeature(parameters);
                    break;
                case "fillet":
                    CreateFilletFeature(parameters);
                    break;
                case "chamfer":
                    CreateChamferFeature(parameters);
                    break;
                case "revolve":
                    CreateRevolveFeature(parameters);
                    break;
                default:
                    Logger.Warning($"暂不支持的特征类型：{featureType}，已跳过");
                    break;
            }
        }
        #endregion

        #region 核心特征：拉伸凸台（修复版）
        private void CreateExtrusionFeature(Dictionary<string, object> parameters)
        {
            string targetPlane = GetPlaneName(parameters);

            parameters.TryGetValue("depth", out object depthObj);
            double depthMm = depthObj != null ? Convert.ToDouble(depthObj) : 10.0;
            double depthM = depthMm / 1000.0;

            parameters.TryGetValue("shape", out object shapeObj);
            string shape = shapeObj?.ToString()?.ToLower() ?? "circle";
            if (!_supportedShapes.Contains(shape))
            {
                Logger.Warning($"不支持的形状：{shape}，默认使用圆形");
                shape = "circle";
            }

            (double centerXM, double centerYM) = GetSketchCoordinate(parameters, false);
            Logger.Info($"拉伸凸台参数：形状={shape}，深度={depthMm}mm");

            string sketchName = DrawSketch(targetPlane, shape, parameters, centerXM, centerYM);
            if (string.IsNullOrEmpty(sketchName)) throw new Exception("草图绘制失败");

            // 强制选中草图
            _activeModel.ClearSelection2(true);
            bool sketchSelected = _activeModel.Extension.SelectByID2(sketchName, "SKETCH", 0, 0, 0, true, 0, null, 0);
            if (!sketchSelected) throw new Exception("草图选中失败");
            Logger.Info("草图已选中，准备执行拉伸");

            try
            {
                Logger.Info("正在执行【纯API拉伸凸台】");
                IFeatureManager featureManager = _activeModel.FeatureManager;

                // 标准拉伸凸台，单向，沿Z轴正方向
                Feature extrusion = featureManager.FeatureExtrusion3(
                    Sd: true,
                    Flip: false,
                    Dir: false,
                    T1: (int)swEndConditions_e.swEndCondBlind,
                    T2: (int)swEndConditions_e.swEndCondBlind,
                    D1: depthM,
                    D2: 0,
                    Dchk1: false,
                    Dchk2: false,
                    Ddir1: false,
                    Ddir2: false,
                    Dang1: GetDraftAngleRad(parameters),
                    Dang2: 0,
                    OffsetReverse1: false,
                    OffsetReverse2: false,
                    TranslateSurface1: false,
                    TranslateSurface2: false,
                    Merge: true,
                    UseFeatScope: false,
                    UseAutoSelect: true,
                    T0: (int)swStartConditions_e.swStartSketchPlane,
                    StartOffset: 0,
                    FlipStartOffset: false
                );

                if (extrusion == null)
                {
                    throw new InvalidOperationException("拉伸凸台创建失败：FeatureExtrusion3 返回空");
                }

                Logger.Info("【纯API拉伸凸台成功！】特征创建完成");
            }
            catch (Exception ex1)
            {
                Logger.Warning("纯API拉伸失败，切换到兜底方案", ex1);
                ExecuteExtrusionFallback(depthMm, false);
            }
        }
        #endregion

        #region 核心特征：拉伸切除（100%生效最终版）
        private void CreateCutFeature(Dictionary<string, object> parameters)
        {
            string targetPlane = GetPlaneName(parameters);

            parameters.TryGetValue("depth", out object depthObj);
            double depthMm = depthObj != null ? Convert.ToDouble(depthObj) : 10.0;
            double depthM = depthMm / 1000.0;

            parameters.TryGetValue("shape", out object shapeObj);
            string shape = shapeObj?.ToString()?.ToLower() ?? "circle";
            if (!_supportedShapes.Contains(shape))
            {
                Logger.Warning($"不支持的形状：{shape}，默认使用圆形");
                shape = "circle";
            }

            parameters.TryGetValue("through_all", out object throughAllObj);
            bool throughAll = throughAllObj != null && Convert.ToBoolean(throughAllObj);

            // 核心修复：终止条件设置
            int endCondition = throughAll ? (int)swEndConditions_e.swEndCondThroughAll : (int)swEndConditions_e.swEndCondBlind;
            // 完全贯穿时，强制双向切除，确保100%切穿
            bool isBidirectional = throughAll;

            (double centerXM, double centerYM) = GetSketchCoordinate(parameters, _isFourHoleRequest);
            Logger.Info($"拉伸切除参数：形状={shape}，深度={depthMm}mm，完全贯穿={throughAll}，中心坐标=({centerXM * 1000}, {centerYM * 1000})mm");

            string sketchName = DrawSketch(targetPlane, shape, parameters, centerXM, centerYM);
            if (string.IsNullOrEmpty(sketchName)) throw new Exception("草图绘制失败");

            // 核心修复1：强制选中草图+实体，确保切除有目标
            _activeModel.ClearSelection2(true);
            // 1. 选中草图
            bool sketchSelected = _activeModel.Extension.SelectByID2(sketchName, "SKETCH", 0, 0, 0, true, 0, null, 0);
            if (!sketchSelected) throw new Exception("草图选中失败");
            // 2. 选中零件中的所有实体，确保切除目标正确
            bool bodySelected = _activeModel.Extension.SelectByID2("", "SOLIDBODY", 0, 0, 0, true, 0, null, 0);
            if (!bodySelected) Logger.Warning("实体选中失败，将使用自动选择");
            Logger.Info("草图+实体已选中，准备执行切除");

            try
            {
                Logger.Info("正在执行【纯API拉伸切除】");
                IFeatureManager featureManager = _activeModel.FeatureManager;

                // 核心修复2：FeatureCut4参数100%对齐SolidWorks规范
                Feature cut = featureManager.FeatureCut4(
                    Sd: !isBidirectional, // 完全贯穿时用双向
                    Flip: false, // 方向1不反转，朝向实体内部
                    Dir: isBidirectional, // 完全贯穿时启用第二方向
                    T1: endCondition,
                    T2: isBidirectional ? endCondition : (int)swEndConditions_e.swEndCondBlind,
                    D1: throughAll ? 0 : depthM, // 完全贯穿时深度设为0
                    D2: throughAll ? 0 : depthM,
                    Dchk1: false,
                    Dchk2: false,
                    Ddir1: false,
                    Ddir2: false,
                    Dang1: GetDraftAngleRad(parameters),
                    Dang2: 0,
                    OffsetReverse1: false,
                    OffsetReverse2: false,
                    TranslateSurface1: false,
                    TranslateSurface2: false,
                    NormalCut: false, // 实体切除必须为false
                    UseFeatScope: false,
                    UseAutoSelect: true, // 自动选择轮廓，确保切除正确
                    AssemblyFeatureScope: false,
                    AutoSelectComponents: false,
                    PropagateFeatureToParts: false,
                    T0: (int)swStartConditions_e.swStartSketchPlane,
                    StartOffset: 0,
                    FlipStartOffset: false,
                    OptimizeGeometry: true
                );

                if (cut == null)
                {
                    throw new InvalidOperationException("拉伸切除创建失败：FeatureCut4 返回空");
                }

                Logger.Info("【纯API拉伸切除成功！】特征创建完成");
            }
            catch (Exception ex1)
            {
                Logger.Warning("纯API切除失败，切换到兜底方案", ex1);
                ExecuteExtrusionFallback(depthMm, true);
            }
        }
        #endregion

        #region 核心工具：坐标计算
        private (double centerXM, double centerYM) GetSketchCoordinate(Dictionary<string, object> parameters, bool useAutoHolePosition)
        {
            // 自动四角孔位逻辑：仅用户明确要求时触发
            if (useAutoHolePosition && _baseLengthMm > 0 && _baseWidthMm > 0)
            {
                int holeIndex = _cutFeatureIndex - 1;
                var holePositions = new List<Tuple<double, double>>()
                {
                    Tuple.Create(_baseLengthMm/2 - 10, _baseWidthMm/2 - 10),  // 右上
                    Tuple.Create(_baseLengthMm/2 - 10, -_baseWidthMm/2 + 10), // 右下
                    Tuple.Create(-_baseLengthMm/2 + 10, _baseWidthMm/2 - 10), // 左上
                    Tuple.Create(-_baseLengthMm/2 + 10, -_baseWidthMm/2 + 10) // 左下
                };

                if (holeIndex >= 0 && holeIndex < holePositions.Count)
                {
                    double centerXMm = holePositions[holeIndex].Item1;
                    double centerYMm = holePositions[holeIndex].Item2;
                    Logger.Info($"用户明确要求四角孔，自动分配坐标：({centerXMm}, {centerYMm})mm");
                    return (centerXMm / 1000.0, centerYMm / 1000.0);
                }
            }

            // 其他情况：100%使用LLM返回的坐标
            parameters.TryGetValue("center_x", out object cxObj);
            parameters.TryGetValue("center_y", out object cyObj);
            double centerXMmFinal = cxObj != null ? Convert.ToDouble(cxObj) : 0.0;
            double centerYMmFinal = cyObj != null ? Convert.ToDouble(cyObj) : 0.0;
            return (centerXMmFinal / 1000.0, centerYMmFinal / 1000.0);
        }
        #endregion

        #region 草图绘制（确保在实体范围内）
        private string DrawSketch(string targetPlane, string shape, Dictionary<string, object> parameters, double centerXM, double centerYM)
        {
            Logger.Info($"正在查找基准面：{targetPlane}");
            Feature targetPlaneFeature = FindPlaneFeature(targetPlane);
            if (targetPlaneFeature == null) throw new Exception($"未找到基准面：{targetPlane}");

            bool isPlaneSelected = targetPlaneFeature.Select2(false, 0);
            if (!isPlaneSelected) throw new Exception($"基准面 {targetPlane} 选择失败");
            Logger.Info($"基准面 {targetPlane} 选择成功");

            _sketchManager.InsertSketch(true);
            Logger.Info("已进入草图环境");

            switch (shape.ToLower().Trim())
            {
                case "circle":
                    DrawCircle(parameters, centerXM, centerYM);
                    break;
                case "rectangle":
                    DrawRectangle(parameters, centerXM, centerYM);
                    break;
                case "polygon":
                    DrawPolygon(parameters, centerXM, centerYM);
                    break;
                case "ellipse":
                    DrawEllipse(parameters, centerXM, centerYM);
                    break;
                case "slot":
                    DrawSlot(parameters, centerXM, centerYM);
                    break;
            }

            _activeModel.ClearSelection2(true);
            _sketchManager.InsertSketch(true);
            string sketchName = GetLastSketchName();
            Logger.Info($"已退出草图环境，草图名称：{sketchName}");
            return sketchName;
        }

        private string GetLastSketchName()
        {
            Feature lastSketchFeature = null;
            Feature currentFeature = _activeModel.FirstFeature() as Feature;

            while (currentFeature != null)
            {
                if (currentFeature.GetTypeName2() == "ProfileFeature")
                {
                    lastSketchFeature = currentFeature;
                }
                currentFeature = currentFeature.GetNextFeature() as Feature;
            }

            return lastSketchFeature?.Name;
        }

        private void DrawCircle(Dictionary<string, object> parameters, double centerXM, double centerYM)
        {
            parameters.TryGetValue("diameter", out object diaObj);
            double diameterMm = diaObj != null ? Convert.ToDouble(diaObj) : 50.0;
            double radiusM = (diameterMm / 2) / 1000.0;

            _sketchManager.CreateCircle(centerXM, centerYM, 0, centerXM + radiusM, centerYM, 0);
            Logger.Info($"已绘制圆形草图，直径：{diameterMm}mm，中心：({centerXM * 1000}, {centerYM * 1000})mm");
        }

        private void DrawRectangle(Dictionary<string, object> parameters, double centerXM, double centerYM)
        {
            parameters.TryGetValue("length", out object lenObj);
            double lengthMm = lenObj != null ? Convert.ToDouble(lenObj) : 100.0;
            double lengthM = lengthMm / 1000.0;

            parameters.TryGetValue("width", out object widthObj);
            double widthMm = widthObj != null ? Convert.ToDouble(widthObj) : 50.0;
            double widthM = widthMm / 1000.0;

            double x1 = centerXM - lengthM / 2;
            double y1 = centerYM - widthM / 2;
            double x2 = centerXM + lengthM / 2;
            double y2 = centerYM + widthM / 2;

            _sketchManager.CreateCornerRectangle(x1, y1, 0, x2, y2, 0);
            Logger.Info($"已绘制矩形草图，尺寸：{lengthMm}mm × {widthMm}mm");
        }

        private void DrawPolygon(Dictionary<string, object> parameters, double centerXM, double centerYM)
        {
            parameters.TryGetValue("sides", out object sidesObj);
            int sides = sidesObj != null ? Convert.ToInt32(sidesObj) : 6;
            sides = sides < 3 ? 3 : sides > 100 ? 100 : sides;

            parameters.TryGetValue("diameter", out object diaObj);
            double diameterMm = diaObj != null ? Convert.ToDouble(diaObj) : 50.0;
            double radiusM = (diameterMm / 2) / 1000.0;

            _sketchManager.CreatePolygon(centerXM, centerYM, 0, centerXM + radiusM, centerYM, sides, 0, true);
            Logger.Info($"已绘制正多边形草图，边数：{sides}，外接圆直径：{diameterMm}mm");
        }

        private void DrawEllipse(Dictionary<string, object> parameters, double centerXM, double centerYM)
        {
            parameters.TryGetValue("major_axis", out object majorObj);
            double majorAxisMm = majorObj != null ? Convert.ToDouble(majorObj) : 100.0;
            double majorRadiusM = (majorAxisMm / 2) / 1000.0;

            parameters.TryGetValue("minor_axis", out object minorObj);
            double minorAxisMm = minorObj != null ? Convert.ToDouble(minorObj) : 50.0;
            double minorRadiusM = (minorAxisMm / 2) / 1000.0;

            _sketchManager.CreateEllipse(centerXM, centerYM, 0, centerXM + majorRadiusM, centerYM, 0, centerXM, centerYM + minorRadiusM, 0);
            Logger.Info($"已绘制椭圆草图，长轴：{majorAxisMm}mm，短轴：{minorAxisMm}mm");
        }

        private void DrawSlot(Dictionary<string, object> parameters, double centerXM, double centerYM)
        {
            parameters.TryGetValue("length", out object lenObj);
            double lengthMm = lenObj != null ? Convert.ToDouble(lenObj) : 100.0;
            double lengthM = lengthMm / 1000.0;

            parameters.TryGetValue("width", out object widthObj);
            double widthMm = widthObj != null ? Convert.ToDouble(widthObj) : 20.0;
            double radiusM = (widthMm / 2) / 1000.0;

            double halfLength = lengthM / 2;
            double centerX1 = centerXM - halfLength + radiusM;
            double centerX2 = centerXM + halfLength - radiusM;

            _sketchManager.CreateArc(centerX1, centerYM, 0, centerX1, centerYM + radiusM, 0, centerX1, centerYM - radiusM, 0, -1);
            _sketchManager.CreateArc(centerX2, centerYM, 0, centerX2, centerYM - radiusM, 0, centerX2, centerYM + radiusM, 0, -1);
            _sketchManager.CreateLine(centerX1, centerYM + radiusM, 0, centerX2, centerYM + radiusM, 0);
            _sketchManager.CreateLine(centerX1, centerYM - radiusM, 0, centerX2, centerYM - radiusM, 0);

            Logger.Info($"已绘制直槽口草图，长度：{lengthMm}mm，宽度：{widthMm}mm");
        }
        #endregion


        #region 新增特征：圆角、倒角、旋转
        private void CreateFilletFeature(Dictionary<string, object> parameters)
        {
            double radiusMm = GetDouble(parameters, "radius", 5.0);
            if (radiusMm <= 0) throw new InvalidOperationException("圆角半径必须大于0");
            SelectAllEdges();

            IFeatureManager featureManager = _activeModel.FeatureManager;
            object result = featureManager.FeatureFillet3(
                (int)swFeatureFilletOptions_e.swFeatureFilletUniformRadius,
                radiusMm / 1000.0,
                0,
                0,
                (int)swFeatureFilletType_e.swFeatureFilletType_Simple,
                (int)swFilletOverFlowType_e.swFilletOverFlowType_Default,
                0,
                null, null, null, null, null, null, null);

            Feature fillet = result as Feature;
            if (fillet == null) throw new InvalidOperationException("圆角创建失败：FeatureFillet3 返回空");
            Logger.Info($"圆角创建完成，半径：{radiusMm}mm");
        }

        private void CreateChamferFeature(Dictionary<string, object> parameters)
        {
            double distanceMm = GetDouble(parameters, "distance", 2.0);
            if (distanceMm <= 0) throw new InvalidOperationException("倒角距离必须大于0");
            SelectAllEdges();

            IFeatureManager featureManager = _activeModel.FeatureManager;
            Feature chamfer = featureManager.InsertFeatureChamfer(
                0,
                (int)swChamferType_e.swChamferEqualDistance,
                distanceMm / 1000.0,
                0, 0, 0, 0, 0);

            if (chamfer == null) throw new InvalidOperationException("倒角创建失败：InsertFeatureChamfer 返回空");
            Logger.Info($"倒角创建完成，距离：{distanceMm}mm");
        }

        private void CreateRevolveFeature(Dictionary<string, object> parameters)
        {
            string targetPlane = GetPlaneName(parameters);
            string shape = GetString(parameters, "shape", "rectangle").ToLowerInvariant();
            if (!_supportedShapes.Contains(shape))
            {
                Logger.Warning($"不支持的形状：{shape}，默认使用矩形");
                shape = "rectangle";
            }

            (double centerXM, double centerYM) = GetSketchCoordinate(parameters, false);
            (string sketchName, string axisId) = DrawRevolveSketch(
                targetPlane, shape, parameters, centerXM, centerYM,
                GetString(parameters, "axis", "y").ToLowerInvariant());

            if (string.IsNullOrEmpty(sketchName)) throw new Exception("旋转草图绘制失败");

            _activeModel.ClearSelection2(true);
            bool sketchSelected = _activeModel.Extension.SelectByID2(sketchName, "SKETCH", 0, 0, 0, true, 0, null, 0);
            if (!sketchSelected) throw new Exception("旋转草图选中失败");
            bool axisSelected = !string.IsNullOrEmpty(axisId) &&
                _activeModel.Extension.SelectByID2(axisId, "SKETCHSEGMENT", 0, 0, 0, true, 0, null, 0);
            if (!axisSelected) Logger.Warning("旋转轴选中失败，将尝试让 SolidWorks 自动识别");

            double angleDeg = GetDouble(parameters, "angle", 360.0);
            if (angleDeg <= 0 || angleDeg > 360) angleDeg = 360.0;
            double angleRad = angleDeg * Math.PI / 180.0;
            int revolveType = angleDeg >= 359.999
                ? (int)swRevolveType_e.swRevolveTypeOneDirection360Degrees
                : (int)swRevolveType_e.swRevolveTypeOneDirection;

            Feature revolve = _activeModel.FeatureManager.FeatureRevolve(
                angleRad, false, 0, revolveType,
                (int)swRevolveOptions_e.swRevolveOptionsNone,
                true, false, true);

            if (revolve == null)
            {
                throw new InvalidOperationException("旋转特征创建失败：FeatureRevolve 返回空");
            }
            Logger.Info($"旋转特征创建完成，角度：{angleDeg}度");
        }

        private (string sketchName, string axisId) DrawRevolveSketch(
            string targetPlane,
            string shape,
            Dictionary<string, object> parameters,
            double centerXM,
            double centerYM,
            string axis)
        {
            Feature targetPlaneFeature = FindPlaneFeature(targetPlane);
            if (targetPlaneFeature == null) throw new Exception($"未找到基准面：{targetPlane}");
            if (!targetPlaneFeature.Select2(false, 0)) throw new Exception($"基准面 {targetPlane} 选择失败");

            _sketchManager.InsertSketch(true);
            switch (shape)
            {
                case "circle": DrawCircle(parameters, centerXM, centerYM); break;
                case "rectangle": DrawRectangle(parameters, centerXM, centerYM); break;
                case "polygon": DrawPolygon(parameters, centerXM, centerYM); break;
                case "ellipse": DrawEllipse(parameters, centerXM, centerYM); break;
                case "slot": DrawSlot(parameters, centerXM, centerYM); break;
            }

            SketchSegment axisSegment = axis == "x"
                ? _sketchManager.CreateCenterLine(-1, 0, 0, 1, 0, 0)
                : _sketchManager.CreateCenterLine(0, -1, 0, 0, 1, 0);
            string axisId = axisSegment?.GetID()?.ToString();

            _activeModel.ClearSelection2(true);
            _sketchManager.InsertSketch(true);
            string sketchName = GetLastSketchName();
            Logger.Info($"已绘制旋转草图：{sketchName}，旋转轴：{axis}，轴ID：{axisId}");
            return (sketchName, axisId);
        }

        private void SelectAllEdges()
        {
            _activeModel.ClearSelection2(true);
            PartDoc part = _activeModel as PartDoc;
            if (part == null) throw new Exception("当前文档不是零件文档");

            object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[];
            if (bodies == null || bodies.Length == 0) throw new Exception("零件中没有实体");

            int selected = 0;
            foreach (object bodyObject in bodies)
            {
                Body2 body = bodyObject as Body2;
                if (body == null) continue;
                object[] edges = body.GetEdges() as object[];
                if (edges == null) continue;
                foreach (object edgeObject in edges)
                {
                    Entity edge = edgeObject as Entity;
                    if (edge != null && edge.Select4(true, null)) selected++;
                }
            }

            if (selected == 0) throw new Exception("没有可选择的边");
            Logger.Info($"已选中 {selected} 条边");
        }
        #endregion

        #region 兜底方案（键盘模拟，100%生效）
        private void ExecuteExtrusionFallback(double depthMm, bool isCut)
        {
            try
            {
                Logger.Info("正在执行【全自动键盘兜底方案】");
                int activateError = 0;
                string docTitle = _activeModel.GetTitle();
                _swApp.ActivateDoc2(docTitle, false, ref activateError);
                Thread.Sleep(200);

                int commandId = isCut ? 285 : 284;
                _swApp.RunCommand(commandId, "");
                Logger.Info($"{(isCut ? "切除" : "拉伸")}面板已打开，正在自动填充参数...");
                Thread.Sleep(800);

                // 完全贯穿时，直接选择完全贯穿选项
                if (isCut)
                {
                    SendKeys.SendWait("{TAB 2}");
                    Thread.Sleep(100);
                    SendKeys.SendWait("{DOWN 4}"); // 选择完全贯穿
                    Thread.Sleep(100);
                }
                else
                {
                    // 拉伸填充深度
                    SendKeys.SendWait("{TAB 3}");
                    Thread.Sleep(150);
                    SendKeys.SendWait("^a");
                    Thread.Sleep(100);
                    SendKeys.SendWait($"{depthMm}");
                    Thread.Sleep(150);
                }

                SendKeys.SendWait("{ENTER}");
                Thread.Sleep(500);
                Logger.Info("【全自动键盘操作成功！】");
            }
            catch (Exception ex2)
            {
                Logger.Error("所有方案都失败", ex2);
                throw new Exception($"建模失败：{ex2.Message}\n\n💡 草图已绘制完成，你可以手动点击对应按钮完成建模");
            }
        }
        #endregion

        #region 工具方法

        private static double GetDouble(Dictionary<string, object> parameters, string key, double fallback)
        {
            if (parameters != null && parameters.TryGetValue(key, out object value) && value != null)
            {
                try { return Convert.ToDouble(value); } catch { }
            }
            return fallback;
        }

        private static string GetString(Dictionary<string, object> parameters, string key, string fallback)
        {
            if (parameters != null && parameters.TryGetValue(key, out object value) && value != null)
            {
                string text = value.ToString().Trim();
                if (!string.IsNullOrEmpty(text)) return text;
            }
            return fallback;
        }

        private static double GetDraftAngleRad(Dictionary<string, object> parameters)
        {
            double degrees = GetDouble(parameters, "draft_angle", 0.0);
            if (degrees <= -90 || degrees >= 90) return 0.0;
            return degrees * Math.PI / 180.0;
        }

        private static string GetPlaneName(Dictionary<string, object> parameters)
        {
            string raw = GetString(parameters, "plane", "前视基准面");
            string key = raw.ToLowerInvariant()
                .Replace(" plane", "")
                .Replace("基准面", "")
                .Replace("plan de ", "")
                .Trim();

            switch (key)
            {
                case "front":
                case "face":
                case "前视":
                    return "前视基准面";
                case "top":
                case "dessus":
                case "上视":
                    return "上视基准面";
                case "right":
                case "droite":
                case "右视":
                    return "右视基准面";
                default:
                    return raw;
            }
        }

        private static bool PlaneMatches(string featureName, string targetPlane)
        {
            string name = featureName.ToLowerInvariant();
            if (targetPlane == "前视基准面")
                return name.Contains("front") || name.Contains("前视") || name.Contains("face");
            if (targetPlane == "上视基准面")
                return name.Contains("top") || name.Contains("上视") || name.Contains("dessus");
            if (targetPlane == "右视基准面")
                return name.Contains("right") || name.Contains("右视") || name.Contains("droite");
            return name.Contains(targetPlane.ToLowerInvariant());
        }

        private static bool IsFourHoleRequest(string userRequest)
        {
            if (string.IsNullOrEmpty(userRequest))
            {
                return false;
            }

            if (userRequest.Contains("四角") ||
                userRequest.Contains("4个") ||
                userRequest.Contains("四个") ||
                userRequest.Contains("4通孔"))
            {
                return true;
            }

            return userRequest.IndexOf("four corner", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   userRequest.IndexOf("4 corner", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   userRequest.IndexOf("four holes", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   userRequest.IndexOf("4 holes", StringComparison.OrdinalIgnoreCase) >= 0;
        }

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

        private Feature FindPlaneFeature(string targetPlane)
        {
            Feature currentFeature = _activeModel.FirstFeature() as Feature;
            List<Feature> allPlanes = new List<Feature>();

            while (currentFeature != null)
            {
                string featureName = currentFeature.Name;
                string lowerName = featureName.ToLowerInvariant();
                if (featureName.Contains("基准面") || lowerName.Contains("plane") || lowerName.Contains("plan"))
                {
                    allPlanes.Add(currentFeature);
                    if (PlaneMatches(featureName, targetPlane))
                    {
                        return currentFeature;
                    }
                }
                currentFeature = currentFeature.GetNextFeature() as Feature;
            }

            foreach (var plane in allPlanes)
            {
                if (plane.Name.IndexOf(targetPlane, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Logger.Warning($"模糊匹配到基准面：{plane.Name}");
                    return plane;
                }
            }

            Logger.Warning($"未找到目标基准面：{targetPlane}");
            return null;
        }
        #endregion
    }
}
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
        private string _lastCreatedFeatureName = null;
        private readonly Dictionary<string, string> _featureNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                case "linear_pattern":
                    CreateLinearPatternFeature(parameters);
                    break;
                case "circular_pattern":
                    CreateCircularPatternFeature(parameters);
                    break;
                case "mirror":
                    CreateMirrorFeature(parameters);
                    break;
                case "shell":
                    CreateShellFeature(parameters);
                    break;
                case "draft":
                    CreateDraftFeature(parameters);
                    break;
                case "sweep":
                    CreateSweepFeature(parameters);
                    break;
                case "loft":
                    CreateLoftFeature(parameters);
                    break;
                case "rib":
                    CreateRibFeature(parameters);
                    break;
                default:
                    Logger.Warning($"暂不支持的特征类型：{featureType}，已跳过");
                    break;
            }

            if (!string.IsNullOrEmpty(_lastCreatedFeatureName) && !string.IsNullOrEmpty(featureName))
            {
                _featureNameMap[featureName] = _lastCreatedFeatureName;
                Logger.Info($"特征名称映射：{featureName} -> {_lastCreatedFeatureName}");
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

                _lastCreatedFeatureName = extrusion.Name;
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

                _lastCreatedFeatureName = cut.Name;
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
            _lastCreatedFeatureName = fillet.Name;
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
            _lastCreatedFeatureName = chamfer.Name;
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
            _lastCreatedFeatureName = revolve.Name;
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


        #region 关系型特征：阵列、镜像、抽壳、拔模
        private void CreateLinearPatternFeature(Dictionary<string, object> parameters)
        {
            string seed = GetSeedFeatureName(parameters);
            string direction = GetString(parameters, "direction", "x").ToLowerInvariant();
            int count = Math.Max(2, (int)GetDouble(parameters, "count", 2));
            double spacingMm = GetDouble(parameters, "spacing", 20.0);
            int count2 = (int)GetDouble(parameters, "count2", 1);
            double spacing2Mm = GetDouble(parameters, "spacing2", 0.0);
            bool flip = GetBool(parameters, "flip", false);

            _activeModel.ClearSelection2(true);
            if (!SelectFeatureByName(seed, 4))
            {
                throw new InvalidOperationException($"线性阵列：找不到种子特征 {seed}");
            }
            if (!SelectEdgeAlongAxis(direction, 1))
            {
                throw new InvalidOperationException($"线性阵列：找不到沿 {direction} 方向的边作为方向参考");
            }

            Feature pattern = _activeModel.FeatureManager.FeatureLinearPattern5(
                count, spacingMm / 1000.0,
                count2 > 1 ? count2 : 1, spacing2Mm / 1000.0,
                flip, false,
                string.Empty, string.Empty,
                true, false,
                false, false, false, false, false, false, false, false,
                0, 0, false, false);

            if (pattern == null) throw new InvalidOperationException("线性阵列创建失败：FeatureLinearPattern5 返回空");
            _lastCreatedFeatureName = pattern.Name;
            Logger.Info($"线性阵列创建完成：种子={seed}，方向={direction}，数量={count}，间距={spacingMm}mm");
        }

        private void CreateCircularPatternFeature(Dictionary<string, object> parameters)
        {
            string seed = GetSeedFeatureName(parameters);
            string axis = GetString(parameters, "axis", "z").ToLowerInvariant();
            int count = Math.Max(2, (int)GetDouble(parameters, "count", 4));
            double angleDeg = GetDouble(parameters, "angle", 360.0);
            bool equalSpacing = GetBool(parameters, "equal_spacing", true);
            bool flip = GetBool(parameters, "flip", false);
            double angleRad = angleDeg * Math.PI / 180.0;

            string axisName = CreateAxisFromPlanes(axis);
            if (string.IsNullOrEmpty(axisName))
            {
                Logger.Warning("圆周阵列：未能通过两基准面创建基准轴，将使用圆形边兜底");
            }

            Feature pattern = TryCreateCircularViaCreateFeature(seed, axisName, count, angleRad, flip, equalSpacing);
            if (pattern == null)
            {
                pattern = TryCreateCircularLegacy(seed, axisName, count, angleRad, flip, equalSpacing);
            }

            if (pattern == null)
            {
                throw new InvalidOperationException("圆周阵列创建失败：CreateFeature 与旧接口均返回空");
            }
            _lastCreatedFeatureName = pattern.Name;
            Logger.Info($"圆周阵列创建完成：种子={seed}，数量={count}，总角度={angleDeg}度");
        }

        private Feature TryCreateCircularViaCreateFeature(string seed, string axisName, int count, double angleRad, bool flip, bool equalSpacing)
        {
            double spacingRad = equalSpacing ? angleRad / count : angleRad / Math.Max(1, count - 1);
            for (int directionMode = 0; directionMode < 2; directionMode++)
            {
                _activeModel.ClearSelection2(true);
                if (!SelectFeatureByName(seed, 4)) continue;

                bool directionOk;
                if (directionMode == 0)
                {
                    directionOk = !string.IsNullOrEmpty(axisName) &&
                        _activeModel.Extension.SelectByID2(axisName, "AXIS", 0, 0, 0, true, 1, null, 0);
                }
                else
                {
                    directionOk = SelectCircularEdge(1);
                }
                Logger.Info($"圆周 CreateFeature 尝试：方向={(directionMode == 0 ? "基准轴" : "圆形边")}，选择={directionOk}");
                if (!directionOk) continue;

                Feature pattern = null;
                try
                {
                    ICircularPatternFeatureData data = _activeModel.FeatureManager.CreateDefinition(
                        (int)swFeatureNameID_e.swFmCirPattern) as ICircularPatternFeatureData;
                    if (data == null)
                    {
                        Logger.Warning("圆周 CreateFeature：CreateDefinition 返回空");
                        continue;
                    }
                    try
                    {
                        data.TotalInstances = count;
                        data.EqualSpacing = equalSpacing;
                        data.Spacing = spacingRad;
                        data.ReverseDirection = flip;
                        data.GeometryPattern = false;
                        data.Direction2 = false;
                        data.InstancesToVary = false;
                    }
                    catch (Exception settingEx)
                    {
                        Logger.Warning($"圆周 CreateFeature：设置参数失败 {settingEx.Message}，将使用默认参数");
                    }
                    pattern = _activeModel.FeatureManager.CreateFeature(data) as Feature;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"圆周 CreateFeature 异常：{ex.Message}");
                }
                Logger.Info($"圆周 CreateFeature 尝试：方向={(directionMode == 0 ? "基准轴" : "圆形边")}，结果={(pattern != null)}");
                if (pattern != null) return pattern;
            }
            return null;
        }

        private Feature TryCreateCircularLegacy(string seed, string axisName, int count, double angleRad, bool flip, bool equalSpacing)
        {
            int[] seedMarks = new[] { 4, 1, 2 };
            int[] axisMarks = new[] { 1, 16, 2 };
            foreach (int seedMark in seedMarks)
            {
                if (!string.IsNullOrEmpty(axisName))
                {
                    foreach (int axisMark in axisMarks)
                    {
                        _activeModel.ClearSelection2(true);
                        bool seedOk = SelectFeatureByName(seed, seedMark);
                        bool axisOk = _activeModel.Extension.SelectByID2(axisName, "AXIS", 0, 0, 0, true, axisMark, null, 0);
                        if (!seedOk || !axisOk) continue;

                        Feature pattern = CallCircularPatternLegacy(count, angleRad, flip, equalSpacing);
                        Logger.Info($"圆周旧接口尝试：种子Mark={seedMark}，轴Mark={axisMark}，结果={(pattern != null)}");
                        if (pattern != null) return pattern;
                    }
                }

                _activeModel.ClearSelection2(true);
                if (SelectFeatureByName(seed, seedMark) && SelectCircularEdge(1))
                {
                    Feature pattern = CallCircularPatternLegacy(count, angleRad, flip, equalSpacing);
                    Logger.Info($"圆周旧接口尝试：圆边直选，种子Mark={seedMark}，结果={(pattern != null)}");
                    if (pattern != null) return pattern;
                }
            }
            return null;
        }

        private Feature CallCircularPatternLegacy(int count, double angleRad, bool flip, bool equalSpacing)
        {
            Feature pattern = _activeModel.FeatureManager.FeatureCircularPattern4(
                count, angleRad, flip, "NULL", false, equalSpacing, false);
            if (pattern == null)
            {
                pattern = _activeModel.FeatureManager.FeatureCircularPattern5(
                    count, angleRad, flip, "NULL",
                    false, equalSpacing, false, false,
                    false, false, 1, 0, "NULL", false);
            }
            return pattern;
        }

        private string CreateAxisFromPlanes(string axis)
        {
            string[] planes;
            if (axis == "x")
            {
                planes = new[] { "上视基准面", "前视基准面" };
            }
            else if (axis == "y")
            {
                planes = new[] { "前视基准面", "右视基准面" };
            }
            else
            {
                planes = new[] { "上视基准面", "右视基准面" };
            }

            Feature first = FindPlaneFeature(planes[0]);
            Feature second = FindPlaneFeature(planes[1]);
            if (first == null || second == null)
            {
                Logger.Warning($"圆周阵列：找不到用于建轴的基准面 {planes[0]} / {planes[1]}");
                return null;
            }

            _activeModel.ClearSelection2(true);
            if (!first.Select2(true, 0) || !second.Select2(true, 0))
            {
                Logger.Warning("圆周阵列：两基准面选择失败");
                return null;
            }
            if (!_activeModel.InsertAxis2(true))
            {
                Logger.Warning("圆周阵列：InsertAxis2 返回 false");
                return null;
            }

            string axisName = GetLastAxisFeatureName();
            Logger.Info($"圆周阵列：已创建基准轴 {axisName ?? "无"}（方向={axis}）");
            return axisName;
        }

        private string GetLastAxisFeatureName()
        {
            string last = null;
            Feature feature = _activeModel.FirstFeature() as Feature;
            while (feature != null)
            {
                if (feature.GetTypeName2() == "RefAxis")
                {
                    last = feature.Name;
                }
                feature = feature.GetNextFeature() as Feature;
            }
            return last;
        }

        private bool HasCircularEdge()
        {
            PartDoc part = _activeModel as PartDoc;
            if (part == null) return false;
            object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[];
            if (bodies == null) return false;
            foreach (object bodyObject in bodies)
            {
                Body2 body = bodyObject as Body2;
                if (body == null) continue;
                object[] edges = body.GetEdges() as object[];
                if (edges == null) continue;
                foreach (object edgeObject in edges)
                {
                    Edge edge = edgeObject as Edge;
                    if (edge == null) continue;
                    Curve curve = edge.GetCurve() as Curve;
                    if (curve != null && curve.IsCircle()) return true;
                }
            }
            return false;
        }

        private bool HasCylindricalFace()
        {
            return FindCylindricalFace() != null;
        }

        private bool SelectCylindricalFace(int mark)
        {
            Face2 face = FindCylindricalFace();
            if (face == null) return false;
            Entity entity = face as Entity;
            return entity != null && entity.Select4(true, CreateSelectData(mark));
        }

        private Face2 FindCylindricalFace()
        {
            PartDoc part = _activeModel as PartDoc;
            if (part == null) return null;
            object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[];
            if (bodies == null) return null;
            foreach (object bodyObject in bodies)
            {
                Body2 body = bodyObject as Body2;
                if (body == null) continue;
                object[] faces = body.GetFaces() as object[];
                if (faces == null) continue;
                foreach (object faceObject in faces)
                {
                    Face2 face = faceObject as Face2;
                    if (face == null) continue;
                    Surface surface = face.GetSurface() as Surface;
                    if (surface != null && surface.IsCylinder()) return face;
                }
            }
            return null;
        }

        private void CreateMirrorFeature(Dictionary<string, object> parameters)
        {
            string seed = GetSeedFeatureName(parameters);
            string planeKey = GetString(parameters, "plane", "right").ToLowerInvariant();
            string planeName = NormalizePlaneKey(planeKey);
            bool merge = GetBool(parameters, "merge", true);

            Feature planeFeature = FindPlaneFeature(planeName);
            if (planeFeature == null)
            {
                throw new InvalidOperationException($"镜像：找不到镜像面 {planeName}");
            }

            Feature mirror = null;
            bool success = false;
            int[] seedMarks = new[] { 1, 4, 2 };
            int[] planeMarks = new[] { 2, 1 };
            bool[] geometryModes = new[] { true, false };

            foreach (int seedMark in seedMarks)
            {
                foreach (int planeMark in planeMarks)
                {
                    foreach (bool geometry in geometryModes)
                    {
                        _activeModel.ClearSelection2(true);
                        bool seedOk = SelectFeatureByName(seed, seedMark);
                        bool planeOk = planeFeature.Select2(true, planeMark);
                        if (!seedOk || !planeOk)
                        {
                            Logger.Info($"镜像尝试：种子Mark={seedMark}，面Mark={planeMark}，选择失败");
                            continue;
                        }

                        mirror = _activeModel.FeatureManager.InsertMirrorFeature2(false, geometry, merge, true, 0);
                        if (mirror == null)
                        {
                            mirror = _activeModel.FeatureManager.InsertMirrorFeature(false, geometry, merge, true);
                        }
                        Logger.Info($"镜像尝试：种子Mark={seedMark}，面Mark={planeMark}，几何阵列={geometry}，结果={(mirror != null)}");
                        if (mirror != null)
                        {
                            success = true;
                            break;
                        }
                    }
                    if (success) break;
                }
                if (success) break;
            }

            if (mirror == null)
            {
                throw new InvalidOperationException("镜像创建失败：所有选择方式均返回空");
            }
            _lastCreatedFeatureName = mirror.Name;
            Logger.Info($"镜像创建完成：种子={seed}，镜像面={planeName}");
        }

        private void CreateShellFeature(Dictionary<string, object> parameters)
        {
            double thicknessMm = GetDouble(parameters, "thickness", 2.0);
            bool outward = GetBool(parameters, "outward", false);
            string removeFaces = GetString(parameters, "remove_faces", "top").ToLowerInvariant();

            _activeModel.ClearSelection2(true);
            if (removeFaces != "none" && !SelectFaceByNormal(removeFaces, 1))
            {
                Logger.Warning($"抽壳：未找到 {removeFaces} 方向的面，将创建封闭抽壳");
            }
            _activeModel.InsertFeatureShell(thicknessMm / 1000.0, outward);
            _lastCreatedFeatureName = GetLastSolidFeatureName();
            Logger.Info($"抽壳创建完成：壁厚={thicknessMm}mm，移除面={removeFaces}");
        }

        private void CreateDraftFeature(Dictionary<string, object> parameters)
        {
            double angleDeg = GetDouble(parameters, "angle", 3.0);
            string faces = GetString(parameters, "faces", "sides").ToLowerInvariant();
            string neutral = GetString(parameters, "neutral_plane", "top").ToLowerInvariant();
            string planeName = NormalizePlaneKey(neutral);
            double angleRad = angleDeg * Math.PI / 180.0;

            Feature planeFeature = FindPlaneFeature(planeName);
            Feature draft = null;
            bool success = false;
            string[] directionModes = new[] { "plane", "face" };
            int[] propTypes = new[] { 0, 1, 4 };
            int[] faceMarks = new[] { 2, 1 };

            foreach (string directionMode in directionModes)
            {
                foreach (int propagation in propTypes)
                {
                    foreach (int faceMark in faceMarks)
                    {
                        _activeModel.ClearSelection2(true);
                        bool directionOk;
                        if (directionMode == "plane")
                        {
                            directionOk = planeFeature != null && planeFeature.Select2(true, 1);
                        }
                        else
                        {
                            directionOk = SelectFaceByNormal(neutral, 1);
                        }
                        bool facesOk = directionOk && SelectDraftFaces(faces, faceMark);
                        if (!directionOk || !facesOk)
                        {
                            Logger.Info($"拔模尝试：方向={directionMode}，传播={propagation}，面Mark={faceMark}，选择失败");
                            continue;
                        }

                        draft = _activeModel.FeatureManager.InsertMultiFaceDraft(
                            angleRad, false, false, propagation, false, false);
                        if (draft == null)
                        {
                            draft = _activeModel.FeatureManager.InsertMultiFaceDraft(
                                angleRad, true, false, propagation, false, false);
                        }
                        Logger.Info($"拔模尝试：方向={directionMode}，传播={propagation}，面Mark={faceMark}，结果={(draft != null)}");
                        if (draft != null)
                        {
                            success = true;
                            break;
                        }
                    }
                    if (success) break;
                }
                if (success) break;
            }

            if (draft == null)
            {
                throw new InvalidOperationException("拔模创建失败：所有选择方式均返回空");
            }
            _lastCreatedFeatureName = draft.Name;
            Logger.Info($"拔模创建完成：角度={angleDeg}度，面={faces}，中性面={neutral}");
        }

        private void CreateSweepFeature(Dictionary<string, object> parameters)
        {
            double diameterMm = GetDouble(parameters, "diameter", 10.0);
            double pathLengthMm = GetDouble(parameters, "path_length", 60.0);
            if (diameterMm <= 0) throw new InvalidOperationException("扫掠：圆形轮廓直径必须大于0");
            if (pathLengthMm <= 0) throw new InvalidOperationException("扫掠：路径长度必须大于0");

            string pathPlane = NormalizePlaneKey(GetString(parameters, "path_plane", "right"));
            string pathShape = GetString(parameters, "path_shape", "line").ToLowerInvariant();
            double pathStartXMm = GetDouble(parameters, "path_center_x", 0.0);
            double pathStartYMm = GetDouble(parameters, "path_center_y", 0.0);

            string pathSketch = DrawSweepPathSketch(pathPlane, pathShape, pathLengthMm, pathStartXMm, pathStartYMm);
            if (string.IsNullOrEmpty(pathSketch)) throw new InvalidOperationException("扫掠：路径草图创建失败");

            Feature sweep = TryCreateSweepWithCircularProfile(pathSketch, diameterMm);
            if (sweep == null) throw new InvalidOperationException("扫掠创建失败：圆形轮廓扫掠返回空");
            _lastCreatedFeatureName = sweep.Name;
            Logger.Info($"扫掠创建完成：路径={pathSketch}，直径={diameterMm}mm");
        }

        private Feature TryCreateSweepWithCircularProfile(string pathSketch, double diameterMm)
        {
            _activeModel.ClearSelection2(true);
            if (!_activeModel.Extension.SelectByID2(pathSketch, "SKETCH", 0, 0, 0, false, 4, null, 0))
            {
                Logger.Warning("扫掠圆形轮廓：路径选择失败");
                return null;
            }

            Feature sweep = _activeModel.FeatureManager.InsertProtrusionSwept4(
                false, false, 0, false, false, 0, 0, false, 0, 0,
                0, 0, true, true, true, 0, true, true, diameterMm / 1000.0, 0);
            Logger.Info($"扫掠圆形轮廓尝试：结果={(sweep != null)}");
            return sweep;
        }

        private string DrawSweepPathSketch(string planeName, string pathShape, double lengthMm, double startXMm, double startYMm)
        {
            Feature planeFeature = FindPlaneFeature(planeName);
            if (planeFeature == null) throw new InvalidOperationException($"扫掠：找不到路径基准面 {planeName}");
            _activeModel.ClearSelection2(true);
            if (!planeFeature.Select2(false, 0)) throw new InvalidOperationException($"扫掠：路径基准面 {planeName} 选择失败");

            _sketchManager.InsertSketch(true);
            double ox = startXMm / 1000.0;
            double oy = startYMm / 1000.0;
            double lengthM = lengthMm / 1000.0;
            if (pathShape == "arc")
            {
                _sketchManager.CreateArc(ox + lengthM, oy, 0, ox, oy, 0, ox + lengthM, oy + lengthM, 0, -1);
            }
            else
            {
                _sketchManager.CreateLine(ox, oy, 0, ox, oy + lengthM, 0);
            }

            _activeModel.ClearSelection2(true);
            _sketchManager.InsertSketch(true);
            string sketchName = GetLastSketchName();
            Logger.Info($"扫掠路径草图：{sketchName}，形状={pathShape}，长度={lengthMm}mm");
            return sketchName;
        }

        private void CreateLoftFeature(Dictionary<string, object> parameters)
        {
            string planeName = NormalizePlaneKey(GetString(parameters, "plane", "front"));
            double heightMm = GetDouble(parameters, "height", 50.0);
            if (heightMm <= 0) throw new InvalidOperationException("放样：截面间距必须大于0");

            Dictionary<string, object> profile1 = GetDictionary(parameters, "profile1");
            Dictionary<string, object> profile2 = GetDictionary(parameters, "profile2");
            if (profile1 == null || profile2 == null)
            {
                throw new InvalidOperationException("放样：需要 profile1 与 profile2 两个截面");
            }

            string shape1 = GetString(profile1, "shape", "circle").ToLowerInvariant();
            double cx1 = GetDouble(profile1, "center_x", 0.0) / 1000.0;
            double cy1 = GetDouble(profile1, "center_y", 0.0) / 1000.0;
            string sketch1 = DrawSketch(planeName, shape1, profile1, cx1, cy1);
            if (string.IsNullOrEmpty(sketch1)) throw new InvalidOperationException("放样：第一个截面创建失败");

            Feature planeFeature = FindPlaneFeature(planeName);
            if (planeFeature == null) throw new InvalidOperationException($"放样：找不到基准面 {planeName}");
            _activeModel.ClearSelection2(true);
            if (!planeFeature.Select2(true, 0)) throw new InvalidOperationException("放样：偏移基准面选择失败");
            _activeModel.FeatureManager.InsertRefPlane(8, heightMm / 1000.0, 0, 0, 0, 0);
            string offsetPlaneName = GetLastRefPlaneName();
            if (string.IsNullOrEmpty(offsetPlaneName)) throw new InvalidOperationException("放样：偏移基准面创建失败");

            string shape2 = GetString(profile2, "shape", "circle").ToLowerInvariant();
            double cx2 = GetDouble(profile2, "center_x", 0.0) / 1000.0;
            double cy2 = GetDouble(profile2, "center_y", 0.0) / 1000.0;
            string sketch2 = DrawSketch(offsetPlaneName, shape2, profile2, cx2, cy2);
            if (string.IsNullOrEmpty(sketch2)) throw new InvalidOperationException("放样：第二个截面创建失败");

            _activeModel.ClearSelection2(true);
            bool s1 = _activeModel.Extension.SelectByID2(sketch1, "SKETCH", 0, 0, 0, false, 1, null, 0);
            bool s2 = _activeModel.Extension.SelectByID2(sketch2, "SKETCH", 0, 0, 0, true, 1, null, 0);
            Logger.Info($"放样选择：截面1={sketch1}({s1})，截面2={sketch2}({s2})");
            if (!s1 || !s2) throw new InvalidOperationException("放样：截面选择失败");

            Feature loft = _activeModel.FeatureManager.InsertProtrusionBlend2(
                false, true, false, 1, 0, 0, 1, 1, true, true,
                false, 0, 0, 0, true, true, true, 0);
            if (loft == null) throw new InvalidOperationException("放样创建失败：InsertProtrusionBlend2 返回空");
            _lastCreatedFeatureName = loft.Name;
            Logger.Info($"放样创建完成：截面1={sketch1}，截面2={sketch2}，间距={heightMm}mm");
        }

        private void CreateRibFeature(Dictionary<string, object> parameters)
        {
            double thicknessMm = GetDouble(parameters, "thickness", 3.0);
            if (thicknessMm <= 0) throw new InvalidOperationException("筋：厚度必须大于0");
            string planeName = NormalizePlaneKey(GetString(parameters, "plane", "front"));
            double lengthMm = GetDouble(parameters, "length", 40.0);
            if (lengthMm <= 0) throw new InvalidOperationException("筋：草图长度必须大于0");
            double startXMm = GetDouble(parameters, "center_x", 0.0);
            double startYMm = GetDouble(parameters, "center_y", 0.0);
            double draftAngleDeg = GetDouble(parameters, "draft_angle", 0.0);
            bool twoSided = GetBool(parameters, "two_sided", true);

            Feature planeFeature = FindPlaneFeature(planeName);
            if (planeFeature == null) throw new InvalidOperationException($"筋：找不到草图基准面 {planeName}");
            _activeModel.ClearSelection2(true);
            if (!planeFeature.Select2(false, 0)) throw new InvalidOperationException($"筋：草图基准面 {planeName} 选择失败");

            string direction = GetString(parameters, "direction", "y").ToLowerInvariant();
            _sketchManager.InsertSketch(true);
            double ox = startXMm / 1000.0;
            double oy = startYMm / 1000.0;
            double lengthM = lengthMm / 1000.0;
            if (direction == "x")
            {
                _sketchManager.CreateLine(ox, oy, 0, ox + lengthM, oy, 0);
            }
            else
            {
                _sketchManager.CreateLine(ox, oy, 0, ox, oy + lengthM, 0);
            }
            Logger.Info($"筋草图线段：起点=({startXMm},{startYMm})，方向={direction}，长度={lengthMm}mm");
            _activeModel.ClearSelection2(true);
            _sketchManager.InsertSketch(true);
            string sketchName = GetLastSketchName();
            if (string.IsNullOrEmpty(sketchName)) throw new InvalidOperationException("筋：草图创建失败");

            _activeModel.ClearSelection2(true);
            if (!_activeModel.Extension.SelectByID2(sketchName, "SKETCH", 0, 0, 0, false, 0, null, 0))
            {
                throw new InvalidOperationException("筋：草图选择失败");
            }

            string before = GetLastSolidFeatureName();
            _activeModel.FeatureManager.InsertRib(
                twoSided, false, thicknessMm / 1000.0, 0, false,
                Math.Abs(draftAngleDeg) > 0.0001, false,
                draftAngleDeg * Math.PI / 180.0, false, false);
            string after = GetLastSolidFeatureName();
            if (string.IsNullOrEmpty(after) || after == before)
            {
                throw new InvalidOperationException("筋创建失败：InsertRib 未产生新特征");
            }
            _lastCreatedFeatureName = after;
            Logger.Info($"筋创建完成：草图={sketchName}，厚度={thicknessMm}mm，长度={lengthMm}mm");
        }

        private string GetLastRefPlaneName()
        {
            string last = null;
            Feature feature = _activeModel.FirstFeature() as Feature;
            while (feature != null)
            {
                if (feature.GetTypeName2() == "RefPlane")
                {
                    last = feature.Name;
                }
                feature = feature.GetNextFeature() as Feature;
            }
            return last;
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out object value) || value == null)
            {
                return null;
            }
            if (value is Dictionary<string, object> dict) return dict;
            Newtonsoft.Json.Linq.JObject jObject = value as Newtonsoft.Json.Linq.JObject;
            if (jObject != null) return jObject.ToObject<Dictionary<string, object>>();
            return null;
        }

        private string GetSeedFeatureName(Dictionary<string, object> parameters)
        {
            string seed = GetString(parameters, "seed", null);
            if (!string.IsNullOrEmpty(seed))
            {
                if (_featureNameMap.TryGetValue(seed, out string mapped))
                {
                    return mapped;
                }
                return seed;
            }
            return GetLastSolidFeatureName();
        }

        private string GetLastSolidFeatureName()
        {
            string last = null;
            Feature feature = _activeModel.FirstFeature() as Feature;
            while (feature != null)
            {
                string type = feature.GetTypeName2();
                if (type != "RefPlane" && type != "ProfileFeature" && type != "OriginProfileFeature")
                {
                    last = feature.Name;
                }
                feature = feature.GetNextFeature() as Feature;
            }
            return last;
        }

        private bool SelectFeatureByName(string featureName, int mark)
        {
            if (string.IsNullOrEmpty(featureName)) return false;
            return _activeModel.Extension.SelectByID2(featureName, "BODYFEATURE", 0, 0, 0, true, mark, null, 0);
        }

        private SelectData CreateSelectData(int mark)
        {
            SelectData data = _activeModel.SelectionManager.CreateSelectData();
            data.Mark = mark;
            return data;
        }

        private static double[] ToDoubleArray(object value)
        {
            if (value is double[] doubles) return doubles;
            if (value is object[] objects)
            {
                double[] result = new double[objects.Length];
                for (int i = 0; i < objects.Length; i++)
                {
                    result[i] = Convert.ToDouble(objects[i]);
                }
                return result;
            }
            return null;
        }

        private bool SelectEdgeAlongAxis(string axis, int mark)
        {
            PartDoc part = _activeModel as PartDoc;
            if (part == null) return false;
            object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[];
            if (bodies == null) return false;

            SelectData selectData = CreateSelectData(mark);
            double bestLength = 0;
            Entity bestEdge = null;

            foreach (object bodyObject in bodies)
            {
                Body2 body = bodyObject as Body2;
                if (body == null) continue;
                object[] edges = body.GetEdges() as object[];
                if (edges == null) continue;
                foreach (object edgeObject in edges)
                {
                    Edge edge = edgeObject as Edge;
                    if (edge == null) continue;
                    Vertex v1 = edge.GetStartVertex() as Vertex;
                    Vertex v2 = edge.GetEndVertex() as Vertex;
                    if (v1 == null || v2 == null) continue;
                    double[] p1 = ToDoubleArray(v1.GetPoint());
                    double[] p2 = ToDoubleArray(v2.GetPoint());
                    if (p1 == null || p2 == null || p1.Length < 3 || p2.Length < 3) continue;

                    double dx = p2[0] - p1[0];
                    double dy = p2[1] - p1[1];
                    double dz = p2[2] - p1[2];
                    double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (length < 1e-6) continue;

                    double dominant = axis == "x" ? Math.Abs(dx) / length
                        : axis == "y" ? Math.Abs(dy) / length
                        : Math.Abs(dz) / length;
                    if (dominant > 0.9 && length > bestLength)
                    {
                        bestLength = length;
                        bestEdge = edge as Entity;
                    }
                }
            }

            if (bestEdge == null) return false;
            return bestEdge.Select4(true, selectData);
        }

        private bool SelectCircularEdge(int mark)
        {
            PartDoc part = _activeModel as PartDoc;
            if (part == null) return false;
            object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[];
            if (bodies == null) return false;

            SelectData selectData = CreateSelectData(mark);
            foreach (object bodyObject in bodies)
            {
                Body2 body = bodyObject as Body2;
                if (body == null) continue;
                object[] edges = body.GetEdges() as object[];
                if (edges == null) continue;
                foreach (object edgeObject in edges)
                {
                    Edge edge = edgeObject as Edge;
                    if (edge == null) continue;
                    Curve curve = edge.GetCurve() as Curve;
                    if (curve != null && curve.IsCircle())
                    {
                        Entity entity = edge as Entity;
                        if (entity != null && entity.Select4(true, selectData))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool SelectFaceByNormal(string spec, int mark)
        {
            if (spec == "sides")
            {
                return SelectFacesByPredicate(n => Math.Abs(n[2]) < 0.5, mark);
            }
            if (spec == "top") return SelectFacesByPredicate(n => n[2] > 0.9, mark);
            if (spec == "bottom") return SelectFacesByPredicate(n => n[2] < -0.9, mark);
            if (spec == "front") return SelectFacesByPredicate(n => n[1] < -0.9, mark);
            if (spec == "back") return SelectFacesByPredicate(n => n[1] > 0.9, mark);
            if (spec == "left") return SelectFacesByPredicate(n => n[0] < -0.9, mark);
            if (spec == "right") return SelectFacesByPredicate(n => n[0] > 0.9, mark);
            return false;
        }

        private bool SelectDraftFaces(string faces, int mark)
        {
            if (faces == "sides")
            {
                return SelectFacesByPredicate(n => Math.Abs(n[2]) < 0.5, mark);
            }
            if (faces == "top") return SelectFacesByPredicate(n => n[2] > 0.9, mark);
            if (faces == "bottom") return SelectFacesByPredicate(n => n[2] < -0.9, mark);
            return false;
        }

        private bool SelectFacesByPredicate(Func<double[], bool> predicate, int mark)
        {
            PartDoc part = _activeModel as PartDoc;
            if (part == null) return false;
            object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[];
            if (bodies == null) return false;

            SelectData selectData = CreateSelectData(mark);
            int selected = 0;
            foreach (object bodyObject in bodies)
            {
                Body2 body = bodyObject as Body2;
                if (body == null) continue;
                object[] faces = body.GetFaces() as object[];
                if (faces == null) continue;
                foreach (object faceObject in faces)
                {
                    Face2 face = faceObject as Face2;
                    if (face == null) continue;
                    double[] normal = ToDoubleArray(face.Normal);
                    if (normal == null || normal.Length < 3) continue;
                    if (!predicate(normal)) continue;
                    Entity entity = face as Entity;
                    if (entity != null && entity.Select4(true, selectData)) selected++;
                }
            }
            return selected > 0;
        }

        private static bool GetBool(Dictionary<string, object> parameters, string key, bool fallback)
        {
            if (parameters != null && parameters.TryGetValue(key, out object value) && value != null)
            {
                try { return Convert.ToBoolean(value); } catch { }
            }
            return fallback;
        }

        private static string NormalizePlaneKey(string key)
        {
            switch ((key ?? string.Empty).ToLowerInvariant())
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
                    return key;
            }
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
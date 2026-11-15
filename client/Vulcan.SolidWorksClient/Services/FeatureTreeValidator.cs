using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vulcan.SolidWorksClient.Models;

namespace Vulcan.SolidWorksClient.Services
{
    /// <summary>
    /// Client-side mirror of the server feature validator. The preview window
    /// uses this to catch edited or unsupported JSON before SolidWorks is touched.
    /// </summary>
    public static class FeatureTreeValidator
    {
        private static readonly HashSet<string> AllowedFeatureTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "extrude", "cut", "hole", "revolve", "fillet", "chamfer",
            "linear_pattern", "circular_pattern", "mirror", "shell",
            "draft", "sweep", "loft", "rib",
            "counterbore", "countersink", "hole_array",
            "note", "insert_component"
        };

        private static readonly HashSet<string> AllowedShapes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "circle", "rectangle", "polygon", "ellipse", "slot"
        };

        private static readonly HashSet<string> AllowedDirections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "x", "y", "z"
        };

        private static readonly HashSet<string> AllowedRibDirections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "x", "y"
        };

        private static readonly HashSet<string> AllowedMirrorPlanes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "front", "top", "right",
            "前视基准面", "上视基准面", "右视基准面",
            "face", "dessus", "droite"
        };

        private static readonly HashSet<string> AllowedNeutralPlanes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "front", "top", "right", "face", "dessus", "droite",
            "前视基准面", "上视基准面", "右视基准面"
        };

        private static readonly HashSet<string> AllowedRemoveFaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "top", "bottom", "front", "back", "left", "right", "none"
        };

        private static readonly HashSet<string> AllowedDraftFaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sides", "top", "bottom"
        };

        private static readonly HashSet<string> AllowedPathShapes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "line", "arc"
        };

        public static List<string> Validate(ModelData model)
        {
            var errors = new List<string>();
            if (model == null)
            {
                errors.Add("模型数据为空");
                return errors;
            }

            bool hasSingle = !string.IsNullOrWhiteSpace(model.feature_type);
            bool hasMulti = model.features != null && model.features.Count > 0;

            if (hasSingle && hasMulti)
            {
                errors.Add("响应不能同时包含 feature_type 和 features");
                return errors;
            }

            if (!hasSingle && !hasMulti)
            {
                errors.Add("缺少 feature_type 或 features");
                return errors;
            }

            if (hasSingle)
            {
                ValidateFeature(model.feature_type, model.@params, string.Empty, errors);
                return errors;
            }

            if (model.features.Count > 50)
            {
                errors.Add("特征数量过多，最多支持 50 个");
            }

            for (int index = 0; index < model.features.Count; index++)
            {
                FeatureItem feature = model.features[index];
                string prefix = string.Format("features[{0}]: ", index);
                if (feature == null)
                {
                    errors.Add(prefix + "特征为空");
                    continue;
                }
                ValidateFeature(feature.feature_type, feature.@params, prefix, errors);
            }

            if (errors.Count == 0)
            {
                ValidateGeometry(model.features, errors);
            }

            return errors;
        }

        private static readonly Dictionary<string, string> PlaneAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "front", "front" },
            { "face", "front" },
            { "前视", "front" },
            { "前视基准面", "front" },
            { "top", "top" },
            { "dessus", "top" },
            { "上视", "top" },
            { "上视基准面", "top" },
            { "right", "right" },
            { "droite", "right" },
            { "右视", "right" },
            { "右视基准面", "right" }
        };

        private static void ValidateGeometry(List<FeatureItem> features, List<string> errors)
        {
            int baseIndex;
            string basePlane;
            double length;
            double width;
            double depth;
            if (!FindBaseBlock(features, out baseIndex, out basePlane, out length, out width, out depth))
            {
                return;
            }

            const double tolerance = 1e-6;
            double minDim = Math.Min(length, Math.Min(width, depth));

            for (int index = 0; index < features.Count; index++)
            {
                FeatureItem feature = features[index];
                if (feature == null || feature.@params == null)
                {
                    continue;
                }

                string type = (feature.feature_type ?? string.Empty).Trim().ToLowerInvariant();
                JObject p = JObject.FromObject(feature.@params);
                string prefix = string.Format("features[{0}]: ", index);

                if ((type == "cut" || type == "hole" || type == "counterbore" ||
                     type == "countersink" || type == "hole_array") && index > baseIndex)
                {
                    string cutPlane = PlaneKey(GetStringValue(p, "plane"));
                    double[] bounds = CutBounds(basePlane, cutPlane, length, width, depth);
                    double? diameter = GetNullableNumber(p, "counterbore_diameter")
                        ?? GetNullableNumber(p, "countersink_diameter")
                        ?? GetNullableNumber(p, "diameter");
                    double? centerX = GetNullableNumber(p, "center_x");
                    double? centerY = GetNullableNumber(p, "center_y");
                    if (bounds != null && diameter.HasValue && centerX.HasValue && centerY.HasValue)
                    {
                        double radius = diameter.Value / 2.0;
                        if (Math.Abs(centerX.Value) + radius > bounds[0] + tolerance ||
                            Math.Abs(centerY.Value) + radius > bounds[1] + tolerance)
                        {
                            errors.Add(prefix + "孔中心超出基体范围");
                        }
                    }
                }

                if (type == "fillet")
                {
                    double? radius = GetNullableNumber(p, "radius");
                    if (radius.HasValue && radius.Value > minDim / 2.0 + tolerance)
                    {
                        errors.Add(prefix + "圆角半径超过基体最小尺寸的一半");
                    }
                }

                if (type == "chamfer")
                {
                    double? distance = GetNullableNumber(p, "distance");
                    if (distance.HasValue && distance.Value > minDim / 2.0 + tolerance)
                    {
                        errors.Add(prefix + "倒角距离超过基体最小尺寸的一半");
                    }
                }

                if (type == "shell")
                {
                    double? thickness = GetNullableNumber(p, "thickness");
                    if (thickness.HasValue && thickness.Value * 2.0 >= minDim - tolerance)
                    {
                        errors.Add(prefix + "抽壳壁厚达到或超过基体最小尺寸的一半");
                    }
                }

                if (type == "linear_pattern" && basePlane == "front")
                {
                    string direction = (GetStringValue(p, "direction") ?? string.Empty).ToLowerInvariant();
                    double? count = GetNullableNumber(p, "count");
                    double? spacing = GetNullableNumber(p, "spacing");
                    if ((direction == "x" || direction == "y") && count.HasValue && spacing.HasValue)
                    {
                        double extent = (count.Value - 1) * spacing.Value;
                        double limit = direction == "x" ? length : width;
                        if (extent > limit + tolerance)
                        {
                            errors.Add(prefix + "阵列范围超出基体尺寸");
                        }
                    }
                }
            }
        }

        private static bool FindBaseBlock(
            List<FeatureItem> features,
            out int index,
            out string plane,
            out double length,
            out double width,
            out double depth)
        {
            index = -1;
            plane = null;
            length = 0;
            width = 0;
            depth = 0;

            for (int i = 0; i < features.Count; i++)
            {
                FeatureItem feature = features[i];
                if (feature == null || feature.@params == null)
                {
                    continue;
                }
                if (!string.Equals((feature.feature_type ?? string.Empty).Trim(), "extrude", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                JObject p = JObject.FromObject(feature.@params);
                if (!string.Equals(GetStringValue(p, "shape"), "rectangle", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                double? l = GetNullableNumber(p, "length");
                double? w = GetNullableNumber(p, "width");
                double? d = GetNullableNumber(p, "depth");
                if (!l.HasValue || !w.HasValue || !d.HasValue)
                {
                    continue;
                }

                index = i;
                plane = PlaneKey(GetStringValue(p, "plane"));
                length = l.Value;
                width = w.Value;
                depth = d.Value;
                return true;
            }

            return false;
        }

        private static string PlaneKey(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            string key;
            return PlaneAliases.TryGetValue(value.Trim(), out key) ? key : null;
        }

        private static double[] CutBounds(string basePlane, string cutPlane, double length, double width, double depth)
        {
            if (basePlane != "front")
            {
                return null;
            }
            if (cutPlane == "front")
            {
                return new[] { length / 2.0, width / 2.0 };
            }
            if (cutPlane == "top")
            {
                return new[] { length / 2.0, depth / 2.0 };
            }
            if (cutPlane == "right")
            {
                return new[] { width / 2.0, depth / 2.0 };
            }
            return null;
        }

        private static string GetStringValue(JObject p, string key)
        {
            JToken token = p[key];
            return token == null || token.Type != JTokenType.String ? null : token.Value<string>();
        }

        private static double? GetNullableNumber(JObject p, string key)
        {
            JToken token = p[key];
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            {
                return null;
            }
            return token.Value<double>();
        }

        private static void ValidateFeature(
            string featureType,
            Dictionary<string, object> parameters,
            string prefix,
            List<string> errors)
        {
            string type = (featureType ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(type))
            {
                errors.Add(prefix + "缺少 feature_type");
                return;
            }

            if (!AllowedFeatureTypes.Contains(type))
            {
                errors.Add(prefix + "不支持的特征类型: " + type);
                return;
            }

            if (parameters == null)
            {
                errors.Add(prefix + "缺失或无效的 params");
                return;
            }

            JObject p;
            try
            {
                p = JObject.FromObject(parameters);
            }
            catch (Exception)
            {
                errors.Add(prefix + "params 无法解析");
                return;
            }

            switch (type)
            {
                case "extrude":
                case "cut":
                case "hole":
                case "revolve":
                    ValidateSketchFeature(type, p, prefix, errors);
                    break;
                case "fillet":
                    RequireNumber(p, "radius", errors, prefix, 0.01);
                    break;
                case "chamfer":
                    RequireNumber(p, "distance", errors, prefix, 0.01);
                    break;
                case "linear_pattern":
                    ValidateLinearPattern(p, prefix, errors);
                    break;
                case "circular_pattern":
                    ValidateCircularPattern(p, prefix, errors);
                    break;
                case "mirror":
                    RequireString(p, "plane", errors, prefix, AllowedMirrorPlanes);
                    RequireBool(p, "merge", errors, prefix);
                    RequireString(p, "seed", errors, prefix, null, false);
                    break;
                case "shell":
                    RequireNumber(p, "thickness", errors, prefix, 0.01);
                    RequireString(p, "remove_faces", errors, prefix, AllowedRemoveFaces, false);
                    RequireBool(p, "outward", errors, prefix);
                    break;
                case "draft":
                    RequireNumber(p, "angle", errors, prefix, -89, 89);
                    RequireString(p, "faces", errors, prefix, AllowedDraftFaces, false);
                    RequireString(p, "neutral_plane", errors, prefix, AllowedNeutralPlanes, false);
                    RequireInteger(p, "propagation", errors, prefix, 0, 4, false);
                    break;
                case "sweep":
                    RequireNumber(p, "diameter", errors, prefix, 0.01);
                    RequireNumber(p, "path_length", errors, prefix, 0.01);
                    RequireString(p, "path_plane", errors, prefix, AllowedMirrorPlanes, false);
                    RequireString(p, "path_shape", errors, prefix, AllowedPathShapes, false);
                    RequireNumber(p, "path_center_x", errors, prefix, double.MinValue, double.MaxValue, false);
                    RequireNumber(p, "path_center_y", errors, prefix, double.MinValue, double.MaxValue, false);
                    break;
                case "loft":
                    ValidateLoft(p, prefix, errors);
                    break;
                case "rib":
                    ValidateRib(p, prefix, errors);
                    break;
                case "counterbore":
                    ValidateHoleBody(p, prefix, errors);
                    RequireNumber(p, "hole_diameter", errors, prefix, 0.01);
                    RequireNumber(p, "counterbore_diameter", errors, prefix, 0.01);
                    RequireNumber(p, "counterbore_depth", errors, prefix, 0.01);
                    RequireLarger(p, "counterbore_diameter", "hole_diameter", errors, prefix);
                    break;
                case "countersink":
                    ValidateHoleBody(p, prefix, errors);
                    RequireNumber(p, "hole_diameter", errors, prefix, 0.01);
                    RequireNumber(p, "countersink_diameter", errors, prefix, 0.01);
                    RequireNumber(p, "countersink_angle", errors, prefix, 30, 180, false);
                    RequireLarger(p, "countersink_diameter", "hole_diameter", errors, prefix);
                    break;
                case "hole_array":
                    ValidateHoleBody(p, prefix, errors);
                    RequireNumber(p, "diameter", errors, prefix, 0.01);
                    RequireInteger(p, "count", errors, prefix, 2, 100);
                    RequireNumber(p, "spacing", errors, prefix, 0.01);
                    RequireString(p, "direction", errors, prefix, AllowedRibDirections, false);
                    RequireNumber(p, "angle", errors, prefix, -360, 360, false);
                    break;
                case "note":
                    ValidateNote(p, prefix, errors);
                    break;
                case "insert_component":
                    ValidateInsertComponent(p, prefix, errors);
                    break;
            }
        }

        private static void ValidateSketchFeature(string type, JObject p, string prefix, List<string> errors)
        {
            RequireString(p, "plane", errors, prefix);
            ValidateShape(p, errors, prefix);

            if (type == "extrude")
            {
                RequireNumber(p, "depth", errors, prefix, 0.01);
                RequireNumber(p, "draft_angle", errors, prefix, -89, 89, false);
                return;
            }

            if (type == "cut" || type == "hole")
            {
                RequireBool(p, "through_all", errors, prefix);
                bool throughAll = GetBool(p, "through_all", false);
                if (!throughAll)
                {
                    RequireNumber(p, "depth", errors, prefix, 0.01);
                }
                RequireNumber(p, "center_x", errors, prefix, double.MinValue, double.MaxValue, false);
                RequireNumber(p, "center_y", errors, prefix, double.MinValue, double.MaxValue, false);
                RequireNumber(p, "draft_angle", errors, prefix, -89, 89, false);
                return;
            }

            RequireNumber(p, "center_x", errors, prefix, double.MinValue, double.MaxValue, false);
            RequireNumber(p, "center_y", errors, prefix, double.MinValue, double.MaxValue, false);
            string axis = RequireString(p, "axis", errors, prefix, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "x", "y" }, false);
            if (string.IsNullOrEmpty(axis))
            {
                axis = "y";
            }
            axis = axis.ToLowerInvariant();
            RequireNumber(p, "angle", errors, prefix, 0.01, 360, false);

            double cx = GetNumber(p, "center_x", 0);
            double cy = GetNumber(p, "center_y", 0);
            if (axis == "y" && Math.Abs(cx) < 0.01)
            {
                errors.Add(prefix + "revolve 的 center_x 不能为 0，轮廓必须偏离旋转轴");
            }
            if (axis == "x" && Math.Abs(cy) < 0.01)
            {
                errors.Add(prefix + "revolve 的 center_y 不能为 0，轮廓必须偏离旋转轴");
            }
        }

        private static void ValidateShape(JObject p, List<string> errors, string prefix)
        {
            string shape = RequireString(p, "shape", errors, prefix, AllowedShapes);
            if (string.IsNullOrEmpty(shape))
            {
                return;
            }

            switch (shape.ToLowerInvariant())
            {
                case "circle":
                    RequireNumber(p, "diameter", errors, prefix, 0.01);
                    break;
                case "rectangle":
                case "slot":
                    RequireNumber(p, "length", errors, prefix, 0.01);
                    RequireNumber(p, "width", errors, prefix, 0.01);
                    break;
                case "polygon":
                    RequireNumber(p, "diameter", errors, prefix, 0.01);
                    RequireInteger(p, "sides", errors, prefix, 3, 100);
                    break;
                case "ellipse":
                    RequireNumber(p, "major_axis", errors, prefix, 0.01);
                    RequireNumber(p, "minor_axis", errors, prefix, 0.01);
                    break;
            }
        }

        private static void ValidateLinearPattern(JObject p, string prefix, List<string> errors)
        {
            RequireString(p, "direction", errors, prefix, AllowedDirections);
            RequireInteger(p, "count", errors, prefix, 2, 1000);
            RequireNumber(p, "spacing", errors, prefix, 0.01);
            RequireString(p, "seed", errors, prefix, null, false);
            RequireBool(p, "flip", errors, prefix);

            bool hasCount2 = HasValue(p, "count2");
            bool hasSpacing2 = HasValue(p, "spacing2");
            if (hasCount2 != hasSpacing2)
            {
                errors.Add(prefix + "count2 与 spacing2 必须同时提供");
            }
            else if (hasCount2)
            {
                RequireInteger(p, "count2", errors, prefix, 2, 1000);
                RequireNumber(p, "spacing2", errors, prefix, 0.01);
            }
        }

        private static void ValidateCircularPattern(JObject p, string prefix, List<string> errors)
        {
            RequireString(p, "axis", errors, prefix, AllowedDirections);
            RequireInteger(p, "count", errors, prefix, 2, 1000);
            RequireNumber(p, "angle", errors, prefix, 0.01, 360);
            RequireBool(p, "equal_spacing", errors, prefix);
            RequireBool(p, "flip", errors, prefix);
            RequireString(p, "seed", errors, prefix, null, false);
        }

        private static void ValidateLoft(JObject p, string prefix, List<string> errors)
        {
            RequireNumber(p, "height", errors, prefix, 0.01);
            RequireString(p, "plane", errors, prefix, null, false);

            JObject profile1 = p["profile1"] as JObject;
            if (profile1 == null)
            {
                errors.Add(prefix + "缺少 profile1");
            }
            else
            {
                ValidateShape(profile1, errors, prefix + "profile1.");
                RequireNumber(profile1, "center_x", errors, prefix + "profile1.", double.MinValue, double.MaxValue, false);
                RequireNumber(profile1, "center_y", errors, prefix + "profile1.", double.MinValue, double.MaxValue, false);
            }

            JObject profile2 = p["profile2"] as JObject;
            if (profile2 == null)
            {
                errors.Add(prefix + "缺少 profile2");
            }
            else
            {
                ValidateShape(profile2, errors, prefix + "profile2.");
                RequireNumber(profile2, "center_x", errors, prefix + "profile2.", double.MinValue, double.MaxValue, false);
                RequireNumber(profile2, "center_y", errors, prefix + "profile2.", double.MinValue, double.MaxValue, false);
            }
        }

        private static void ValidateNote(JObject p, string prefix, List<string> errors)
        {
            string text = RequireString(p, "text", errors, prefix);
            if (!string.IsNullOrEmpty(text) && text.Length > 500)
            {
                errors.Add(prefix + "text 不能超过 500 个字符");
            }
            RequireNumber(p, "x", errors, prefix, -10000, 10000, false);
            RequireNumber(p, "y", errors, prefix, -10000, 10000, false);
            RequireNumber(p, "height", errors, prefix, 0.1, 100, false);
        }

        private static void ValidateInsertComponent(JObject p, string prefix, List<string> errors)
        {
            string path = RequireString(p, "path", errors, prefix);
            if (!string.IsNullOrEmpty(path))
            {
                if (path.Length > 260)
                {
                    errors.Add(prefix + "path 过长");
                }
                string lower = path.ToLowerInvariant();
                if (!lower.EndsWith(".sldprt") && !lower.EndsWith(".sldasm"))
                {
                    errors.Add(prefix + "path 必须指向 .sldprt 或 .sldasm 文件");
                }
            }
            RequireNumber(p, "x", errors, prefix, -100000, 100000, false);
            RequireNumber(p, "y", errors, prefix, -100000, 100000, false);
            RequireNumber(p, "z", errors, prefix, -100000, 100000, false);
            RequireBool(p, "fixed", errors, prefix);
        }

        private static void ValidateHoleBody(JObject p, string prefix, List<string> errors)
        {
            RequireString(p, "plane", errors, prefix);
            bool throughAll = GetBool(p, "through_all", false);
            RequireBool(p, "through_all", errors, prefix);
            if (!throughAll)
            {
                RequireNumber(p, "depth", errors, prefix, 0.01);
            }
            RequireNumber(p, "center_x", errors, prefix, double.MinValue, double.MaxValue, false);
            RequireNumber(p, "center_y", errors, prefix, double.MinValue, double.MaxValue, false);
            RequireNumber(p, "draft_angle", errors, prefix, -89, 89, false);
        }

        private static void ValidateRib(JObject p, string prefix, List<string> errors)
        {
            RequireNumber(p, "thickness", errors, prefix, 0.01);
            RequireNumber(p, "length", errors, prefix, 0.01);
            RequireString(p, "plane", errors, prefix, null, false);
            RequireNumber(p, "center_x", errors, prefix, double.MinValue, double.MaxValue, false);
            RequireNumber(p, "center_y", errors, prefix, double.MinValue, double.MaxValue, false);
            RequireString(p, "direction", errors, prefix, AllowedRibDirections, false);
            RequireNumber(p, "draft_angle", errors, prefix, -89, 89, false);
            RequireBool(p, "two_sided", errors, prefix);
        }

        private static string RequireString(
            JObject p,
            string key,
            List<string> errors,
            string prefix,
            HashSet<string> allowed = null,
            bool required = true)
        {
            JToken token = p[key];
            if (token == null || token.Type == JTokenType.Null)
            {
                if (required)
                {
                    errors.Add(prefix + "缺少 " + key);
                }
                return null;
            }

            if (token.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.Value<string>()))
            {
                errors.Add(prefix + key + " 必须是非空字符串");
                return null;
            }

            string value = token.Value<string>().Trim();
            if (allowed != null && !allowed.Contains(value.ToLowerInvariant()))
            {
                errors.Add(prefix + key + " 不支持: " + value);
            }
            return value;
        }

        private static void RequireNumber(
            JObject p,
            string key,
            List<string> errors,
            string prefix,
            double minimum = double.MinValue,
            double maximum = double.MaxValue,
            bool required = true)
        {
            JToken token = p[key];
            if (token == null || token.Type == JTokenType.Null)
            {
                if (required)
                {
                    errors.Add(prefix + "缺少 " + key);
                }
                return;
            }

            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            {
                errors.Add(prefix + key + " 必须是数字");
                return;
            }

            double value = token.Value<double>();
            if (value < minimum)
            {
                errors.Add(prefix + key + " 必须 >= " + minimum);
            }
            if (value > maximum)
            {
                errors.Add(prefix + key + " 必须 <= " + maximum);
            }
        }

        private static void RequireInteger(
            JObject p,
            string key,
            List<string> errors,
            string prefix,
            int minimum,
            int maximum,
            bool required = true)
        {
            JToken token = p[key];
            if (token == null || token.Type == JTokenType.Null)
            {
                if (required)
                {
                    errors.Add(prefix + "缺少 " + key);
                }
                return;
            }

            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            {
                errors.Add(prefix + key + " 必须是整数");
                return;
            }

            double value = token.Value<double>();
            if (Math.Abs(value - Math.Round(value)) > 0.0000001)
            {
                errors.Add(prefix + key + " 必须是整数");
                return;
            }
            if (value < minimum)
            {
                errors.Add(prefix + key + " 必须 >= " + minimum);
            }
            if (value > maximum)
            {
                errors.Add(prefix + key + " 必须 <= " + maximum);
            }
        }

        private static void RequireLarger(
            JObject p,
            string largerKey,
            string smallerKey,
            List<string> errors,
            string prefix)
        {
            double? larger = GetNullableNumber(p, largerKey);
            double? smaller = GetNullableNumber(p, smallerKey);
            if (larger.HasValue && smaller.HasValue && larger.Value <= smaller.Value)
            {
                errors.Add(prefix + largerKey + " 必须大于 " + smallerKey);
            }
        }

        private static void RequireBool(JObject p, string key, List<string> errors, string prefix)
        {
            JToken token = p[key];
            if (token == null || token.Type == JTokenType.Null)
            {
                return;
            }
            if (token.Type != JTokenType.Boolean)
            {
                errors.Add(prefix + key + " 必须是布尔值");
            }
        }

        private static bool GetBool(JObject p, string key, bool fallback)
        {
            JToken token = p[key];
            if (token != null && token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }
            return fallback;
        }

        private static double GetNumber(JObject p, string key, double fallback)
        {
            JToken token = p[key];
            if (token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float))
            {
                return token.Value<double>();
            }
            return fallback;
        }

        private static bool HasValue(JObject p, string key)
        {
            JToken token = p[key];
            return token != null && token.Type != JTokenType.Null;
        }
    }
}

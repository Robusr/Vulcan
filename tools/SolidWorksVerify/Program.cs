using System;
using System.Collections.Generic;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Vulcan.SolidWorksClient.Models;
using Vulcan.SolidWorksClient.Services;

namespace SwVerify
{
    internal static class Program
    {
        private static ISldWorks _swApp;

        [STAThread]
        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            var log = new StringBuilder();
            int failures = 0;

            try
            {
                _swApp = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
                _swApp.Visible = true;
                Console.WriteLine("SolidWorks revision: " + _swApp.RevisionNumber());
                DisablePrompts();

                string template = ResolvePartTemplate();
                Console.WriteLine("Part template: " + (template ?? "<none>"));
                if (string.IsNullOrEmpty(template))
                {
                    Console.WriteLine("NO TEMPLATE");
                    return 2;
                }

                failures += RunScenario(template, "cylinder", Cylinder(), "拉伸直径50圆柱");
                failures += RunParametricEdit(template);
                failures += RunScenario(template, "plate-4holes", Plate(), "四角各打一个直径10mm通孔");
                failures += RunScenario(template, "slot-cut", SlotCut(), "拉伸板后切一个直槽口");
                failures += RunScenario(template, "polygon", Polygon(), "六边形拉伸");
                failures += RunScenario(template, "revolve", Revolve(), "旋转矩形轮廓");
                failures += RunScenario(template, "ellipse", Ellipse(), "椭圆拉伸");
                failures += RunScenario(template, "sweep-line", SweepLine(), "圆形轮廓扫掠直线路径");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine("HARNESS ERROR: " + ex);
            }
            finally
            {
                RestorePrompts();
            }

            Console.WriteLine(failures == 0 ? "SW VERIFY PASSED" : "SW VERIFY FAILURES: " + failures);
            return failures == 0 ? 0 : 1;
        }

        private static readonly int[] PromptToggles =
        {
            (int)swUserPreferenceToggle_e.swInputDimValOnCreate,
            (int)swUserPreferenceToggle_e.swSketchPromptToCloseSketch,
            (int)swUserPreferenceToggle_e.swSketchOverdefiningDimsPromptToSetState,
            (int)swUserPreferenceToggle_e.swExtRefNoPromptOrSave
        };

        private static bool[] _originalPromptToggles;

        private static void DisablePrompts()
        {
            try
            {
                _originalPromptToggles = new bool[PromptToggles.Length];
                for (int i = 0; i < PromptToggles.Length; i++)
                {
                    _originalPromptToggles[i] = _swApp.GetUserPreferenceToggle(PromptToggles[i]);
                }

                _swApp.SetUserPreferenceToggle(PromptToggles[0], false);
                _swApp.SetUserPreferenceToggle(PromptToggles[1], false);
                _swApp.SetUserPreferenceToggle(PromptToggles[2], false);
                _swApp.SetUserPreferenceToggle(PromptToggles[3], true);
                Console.WriteLine("prompts disabled: input-dim, close-sketch, over-define, ext-ref");
            }
            catch (Exception ex)
            {
                Console.WriteLine("prompt preference update failed: " + ex.Message);
            }
        }

        private static void RestorePrompts()
        {
            if (_originalPromptToggles == null)
            {
                return;
            }

            try
            {
                for (int i = 0; i < PromptToggles.Length; i++)
                {
                    _swApp.SetUserPreferenceToggle(PromptToggles[i], _originalPromptToggles[i]);
                }
                Console.WriteLine("prompts restored");
            }
            catch (Exception ex)
            {
                Console.WriteLine("prompt restore failed: " + ex.Message);
            }
        }

        private static string ResolvePartTemplate()
        {
            string[] candidates =
            {
                _swApp.GetUserPreferenceStringValue(
                    (int)swUserPreferenceStringValue_e.swDefaultTemplatePart),
                _swApp.GetDocumentTemplate((int)swDocumentTypes_e.swDocPART, "", 0, 0, 0)
            };

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && System.IO.File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string programData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
            string templates = System.IO.Path.Combine(programData, "SolidWorks", "SOLIDWORKS 2025", "templates");
            if (System.IO.Directory.Exists(templates))
            {
                string[] partTemplates = System.IO.Directory.GetFiles(templates, "*.prtdot");
                if (partTemplates.Length > 0)
                {
                    Array.Sort(partTemplates);
                    return partTemplates[0];
                }
            }

            return null;
        }

        private static void PrintErrors(string context)
        {
            try
            {
                object msgs;
                object ids;
                object types;
                _swApp.GetErrorMessages(out msgs, out ids, out types);
                if (msgs is string[] messages && messages.Length > 0)
                {
                    Console.WriteLine(context + " errors: " + string.Join(" | ", messages));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(context + " error query failed: " + ex.Message);
            }
        }

        private static int RunScenario(string template, string name, ModelData data, string prompt)
        {
            Console.WriteLine("=== SCENARIO " + name + " ===");
            ModelDoc2 doc = null;
            try
            {
                doc = _swApp.NewDocument(template, 0, 0, 0) as ModelDoc2;
                if (doc == null)
                {
                    PrintErrors(name);
                    Console.WriteLine("FAIL " + name + ": NewDocument returned null");
                    return 1;
                }

                var modeler = new SwModeler(_swApp);
                modeler.ExecuteModeling(data, prompt);

                Inspect(doc);
                Console.WriteLine("PASS " + name);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL " + name + ": " + ex.Message);
                return 1;
            }
            finally
            {
                if (doc != null)
                {
                    try
                    {
                        _swApp.QuitDoc(doc.GetTitle());
                    }
                    catch (Exception closeEx)
                    {
                        Console.WriteLine("close failed: " + closeEx.Message);
                    }
                }
            }
        }

        private static void Inspect(ModelDoc2 doc)
        {
            Feature feature = doc.FirstFeature() as Feature;
            int featureCount = 0;
            while (feature != null)
            {
                featureCount++;
                string type = feature.GetTypeName2();
                Console.WriteLine("  feature: " + feature.Name + " [" + type + "]");

                if (type == "ProfileFeature" || type == "3DProfileFeature")
                {
                    Sketch sketch = feature.GetSpecificFeature2() as Sketch;
                    int status = sketch == null ? -1 : sketch.GetConstrainedStatus();
                    var dims = new List<string>();
                    DisplayDimension dd = feature.GetFirstDisplayDimension();
                    while (dd != null)
                    {
                        Dimension dimension = dd.GetDimension() as Dimension;
                        if (dimension != null)
                        {
                            double value = 0;
                            try
                            {
                                value = dimension.SystemValue;
                            }
                            catch
                            {
                            }
                            dims.Add(dimension.FullName + "=" + (value * 1000.0).ToString("0.###") + "mm");
                        }
                        dd = feature.GetNextDisplayDimension(dd);
                    }
                    Console.WriteLine(
                        "    sketch: dims=" + dims.Count +
                        " constrained=" + ConstrainedStatusName(status) +
                        " [" + string.Join("; ", dims) + "]");
                    PrintRelations(sketch);
                }

                feature = feature.GetNextFeature() as Feature;
            }
            Console.WriteLine("  feature count: " + featureCount);
        }

        private static void PrintRelations(Sketch sketch)
        {
            if (sketch == null)
            {
                return;
            }

            object[] segments = sketch.GetSketchSegments() as object[];
            if (segments == null)
            {
                return;
            }

            var counts = new Dictionary<string, int>();
            foreach (object item in segments)
            {
                SketchSegment segment = item as SketchSegment;
                if (segment == null)
                {
                    continue;
                }
                int relationCount = segment.GetRelationsCount();
                if (relationCount <= 0)
                {
                    continue;
                }
                object[] relations = segment.GetRelations() as object[];
                if (relations == null)
                {
                    continue;
                }
                foreach (object relationItem in relations)
                {
                    SketchRelation relation = relationItem as SketchRelation;
                    if (relation == null)
                    {
                        continue;
                    }
                    string name = ConstraintName(relation.GetRelationType());
                    int value;
                    counts.TryGetValue(name, out value);
                    counts[name] = value + 1;
                }
            }

            if (counts.Count > 0)
            {
                var parts = new List<string>();
                foreach (KeyValuePair<string, int> pair in counts)
                {
                    parts.Add(pair.Key + "=" + pair.Value);
                }
                Console.WriteLine("    relations: " + string.Join(", ", parts));
            }
        }

        private static string ConstraintName(int type)
        {
            switch (type)
            {
                case 1: return "DISTANCE";
                case 4: return "HORIZONTAL";
                case 5: return "VERTICAL";
                case 6: return "TANGENT";
                case 9: return "COINCIDENT";
                case 14: return "SAMELENGTH";
                case 15: return "DIAMETER";
                case 17: return "FIXED";
                default: return "TYPE" + type;
            }
        }

        private static string ConstrainedStatusName(int status)
        {
            switch (status)
            {
                case 1: return "unknown(1)";
                case 2: return "under(2)";
                case 3: return "fully(3)";
                case 4: return "over(4)";
                case 5: return "no-solution(5)";
                case 6: return "invalid(6)";
                case 7: return "autosolve-off(7)";
                default: return status.ToString();
            }
        }

        private static int RunParametricEdit(string template)
        {
            Console.WriteLine("=== SCENARIO parametric-edit ===");
            ModelDoc2 doc = null;
            try
            {
                doc = _swApp.NewDocument(template, 0, 0, 0) as ModelDoc2;
                if (doc == null)
                {
                    Console.WriteLine("FAIL parametric-edit: NewDocument returned null");
                    return 1;
                }

                new SwModeler(_swApp).ExecuteModeling(Cylinder(), "验证尺寸驱动");

                Feature sketchFeature = null;
                Feature feature = doc.FirstFeature() as Feature;
                while (feature != null)
                {
                    if (feature.GetTypeName2() == "ProfileFeature")
                    {
                        sketchFeature = feature;
                        break;
                    }
                    feature = feature.GetNextFeature() as Feature;
                }

                DisplayDimension dd = sketchFeature == null ? null : sketchFeature.GetFirstDisplayDimension();
                Dimension dimension = dd == null ? null : dd.GetDimension() as Dimension;
                if (dimension == null)
                {
                    Console.WriteLine("FAIL parametric-edit: no sketch dimension found");
                    return 1;
                }

                double before = dimension.SystemValue * 1000.0;
                int setResult = dimension.SetSystemValue3(0.060, 1, null);
                doc.EditRebuild3();
                double after = dimension.SystemValue * 1000.0;
                double extent = BodyExtentX(doc) * 1000.0;

                Console.WriteLine(
                    "  dim=" + dimension.FullName +
                    " before=" + before.ToString("0.###") + "mm" +
                    " setResult=" + setResult +
                    " after=" + after.ToString("0.###") + "mm" +
                    " bodyX=" + extent.ToString("0.###") + "mm");

                bool ok = Math.Abs(after - 60.0) < 0.001 && Math.Abs(extent - 60.0) < 0.5;
                Console.WriteLine(ok ? "PASS parametric-edit" : "FAIL parametric-edit: geometry did not follow the dimension");
                return ok ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL parametric-edit: " + ex.Message);
                return 1;
            }
            finally
            {
                if (doc != null)
                {
                    try
                    {
                        _swApp.QuitDoc(doc.GetTitle());
                    }
                    catch (Exception closeEx)
                    {
                        Console.WriteLine("close failed: " + closeEx.Message);
                    }
                }
            }
        }

        private static double BodyExtentX(ModelDoc2 doc)
        {
            PartDoc part = doc as PartDoc;
            if (part == null)
            {
                return 0;
            }
            object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[];
            if (bodies == null || bodies.Length == 0)
            {
                return 0;
            }
            Body2 body = bodies[0] as Body2;
            double[] box = body.GetBodyBox() as double[];
            if (box == null || box.Length < 6)
            {
                return 0;
            }
            return box[3] - box[0];
        }

        private static ModelData Cylinder()
        {
            return Feature("extrude", P(
                "plane", "前视基准面",
                "shape", "circle",
                "diameter", 50.0,
                "depth", 100.0));
        }

        private static ModelData Plate()
        {
            return new ModelData
            {
                model_name = "Plate",
                features = new List<FeatureItem>
                {
                    new FeatureItem { feature_type = "extrude", name = "Base", @params = P(
                        "plane", "前视基准面", "shape", "rectangle",
                        "length", 200.0, "width", 100.0, "depth", 20.0) },
                    Hole(90, 40), Hole(90, -40), Hole(-90, 40), Hole(-90, -40)
                }
            };
        }

        private static FeatureItem Hole(double x, double y)
        {
            return new FeatureItem
            {
                feature_type = "cut",
                @params = P(
                    "plane", "前视基准面", "shape", "circle",
                    "diameter", 10.0, "through_all", true,
                    "center_x", x, "center_y", y)
            };
        }

        private static ModelData SlotCut()
        {
            return new ModelData
            {
                features = new List<FeatureItem>
                {
                    new FeatureItem { feature_type = "extrude", @params = P(
                        "plane", "前视基准面", "shape", "rectangle",
                        "length", 120.0, "width", 60.0, "depth", 10.0) },
                    new FeatureItem { feature_type = "cut", @params = P(
                        "plane", "前视基准面", "shape", "slot",
                        "length", 60.0, "width", 16.0, "through_all", true,
                        "center_x", 0.0, "center_y", 0.0) }
                }
            };
        }

        private static ModelData Polygon()
        {
            return Feature("extrude", P(
                "plane", "前视基准面",
                "shape", "polygon",
                "diameter", 60.0,
                "sides", 6.0,
                "depth", 20.0));
        }

        private static ModelData Revolve()
        {
            return Feature("revolve", P(
                "plane", "前视基准面",
                "shape", "rectangle",
                "length", 50.0,
                "width", 100.0,
                "center_x", 25.0,
                "center_y", 0.0,
                "axis", "y",
                "angle", 360.0));
        }

        private static ModelData Ellipse()
        {
            return Feature("extrude", P(
                "plane", "前视基准面",
                "shape", "ellipse",
                "major_axis", 80.0,
                "minor_axis", 40.0,
                "depth", 20.0));
        }

        private static ModelData SweepLine()
        {
            return Feature("sweep", P(
                "diameter", 10.0,
                "path_plane", "右视基准面",
                "path_shape", "line",
                "path_length", 40.0));
        }

        private static ModelData Feature(string type, Dictionary<string, object> parameters)
        {
            return new ModelData { feature_type = type, @params = parameters };
        }

        private static Dictionary<string, object> P(params object[] pairs)
        {
            var result = new Dictionary<string, object>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                result[(string)pairs[i]] = pairs[i + 1];
            }
            return result;
        }
    }
}

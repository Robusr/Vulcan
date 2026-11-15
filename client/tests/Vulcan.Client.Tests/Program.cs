using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using Vulcan.SolidWorksClient.Models;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.Client.Tests
{
    /// <summary>
    /// Dependency-free test runner for the client-side feature validator and
    /// request configuration. Build the client first, then run this executable.
    /// </summary>
    internal static class Program
    {
        private static int _failures;

        private static int Main()
        {
            Run("valid plate", () => ExpectErrors(Plate(), 0));
            Run("valid multi-feature tree", () => ExpectErrors(Multi(), 0));
            Run("unknown feature type", () => ExpectErrors(Feature("teleport", P()), 1));
            Run("missing params", () => ExpectErrors(new ModelData { feature_type = "extrude" }, 1));
            Run("missing feature envelope", () => ExpectErrors(new ModelData(), 1));
            Run("conflicting envelopes", () => ExpectErrors(Conflicting(), 1));
            Run("negative diameter", () =>
                ExpectErrors(Feature("extrude", P("plane", "前视基准面", "shape", "circle", "diameter", -1.0, "depth", 20.0)), 1));
            Run("cut through_all without depth", () =>
                ExpectErrors(Feature("cut", P("plane", "前视基准面", "shape", "circle", "diameter", 10.0, "through_all", true)), 0));
            Run("cut blind requires depth", () =>
                ExpectErrors(Feature("cut", P("plane", "前视基准面", "shape", "circle", "diameter", 10.0)), 1));
            Run("fillet requires radius", () => ExpectErrors(Feature("fillet", P()), 1));
            Run("revolve must be offset from axis", () =>
                ExpectErrors(Feature("revolve", P("plane", "前视基准面", "shape", "rectangle", "length", 50.0, "width", 100.0, "center_x", 0.0, "center_y", 0.0)), 1));
            Run("valid revolve", () =>
                ExpectErrors(Feature("revolve", P("plane", "前视基准面", "shape", "rectangle", "length", 50.0, "width", 100.0, "center_x", 25.0, "center_y", 0.0, "angle", 360.0)), 0));
            Run("linear pattern count2 needs spacing2", () =>
                ExpectErrors(Feature("linear_pattern", P("direction", "x", "count", 2, "spacing", 10.0, "count2", 2)), 1));
            Run("circular pattern angle range", () =>
                ExpectErrors(Feature("circular_pattern", P("axis", "z", "count", 4, "angle", 400.0)), 1));
            Run("mirror plane must be known", () =>
                ExpectErrors(Feature("mirror", P("plane", "diagonal")), 1));
            Run("shell thickness must be positive", () =>
                ExpectErrors(Feature("shell", P("thickness", 0.0)), 1));
            Run("draft angle range", () =>
                ExpectErrors(Feature("draft", P("angle", 95.0)), 1));
            Run("sweep requires path_length", () =>
                ExpectErrors(Feature("sweep", P("diameter", 10.0)), 1));
            Run("loft requires two profiles", () =>
                ExpectErrors(Feature("loft", P("height", 60.0)), 2));
            Run("rib direction must be x or y", () =>
                ExpectErrors(Feature("rib", P("thickness", 3.0, "length", 30.0, "direction", "z")), 1));
            Run("cut center outside base", () =>
                ExpectErrors(PlateWithCut(99.0, 0.0), 1));
            Run("cut center inside base", () =>
                ExpectErrors(PlateWithCut(90.0, 40.0), 0));
            Run("fillet radius larger than half base", () =>
                ExpectErrors(PlateWithExtra(Feature("fillet", P("radius", 11.0))), 1));
            Run("shell thicker than half base", () =>
                ExpectErrors(PlateWithExtra(Feature("shell", P("thickness", 10.0))), 1));
            Run("client timeout exceeds server timeout", () => ExpectApiTimeout(60));

            Console.WriteLine(
                _failures == 0
                    ? "ALL CLIENT TESTS PASSED"
                    : _failures + " CLIENT TEST(S) FAILED");
            return _failures == 0 ? 0 : 1;
        }

        private static void Run(string name, Action check)
        {
            try
            {
                check();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine("FAIL " + name + " -> " + ex.Message);
            }
        }

        private static void ExpectErrors(ModelData model, int expected)
        {
            List<string> errors = FeatureTreeValidator.Validate(model);
            if (errors.Count != expected)
            {
                throw new Exception(
                    "expected " + expected + " error(s), got " + errors.Count + ": " +
                    string.Join(" | ", errors));
            }
        }

        private static void ExpectApiTimeout(int seconds)
        {
            var client = new ApiClient("http://127.0.0.1:5000", null);
            FieldInfo field = typeof(ApiClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
            var httpClient = (HttpClient)field.GetValue(client);
            if (Math.Abs(httpClient.Timeout.TotalSeconds - seconds) > 0.001)
            {
                throw new Exception("timeout is " + httpClient.Timeout.TotalSeconds + "s, expected " + seconds + "s");
            }
        }

        private static ModelData Plate()
        {
            return Feature("extrude", P("plane", "前视基准面", "shape", "rectangle", "length", 200.0, "width", 100.0, "depth", 20.0));
        }

        private static ModelData Multi()
        {
            return new ModelData
            {
                model_name = "Plate",
                features = new List<FeatureItem>
                {
                    new FeatureItem { feature_type = "extrude", @params = P("plane", "前视基准面", "shape", "rectangle", "length", 200.0, "width", 100.0, "depth", 20.0) },
                    new FeatureItem { feature_type = "cut", @params = P("plane", "前视基准面", "shape", "circle", "diameter", 10.0, "through_all", true, "center_x", 90.0, "center_y", 40.0) },
                    new FeatureItem { feature_type = "fillet", @params = P("radius", 3.0) },
                    new FeatureItem { feature_type = "chamfer", @params = P("distance", 2.0) }
                }
            };
        }

        private static ModelData Conflicting()
        {
            return new ModelData
            {
                feature_type = "extrude",
                @params = P("plane", "前视基准面", "shape", "circle", "diameter", 10.0, "depth", 5.0),
                features = new List<FeatureItem>
                {
                    new FeatureItem { feature_type = "extrude", @params = P("plane", "前视基准面", "shape", "circle", "diameter", 10.0, "depth", 5.0) }
                }
            };
        }

        private static ModelData PlateWithCut(double centerX, double centerY)
        {
            return PlateWithFeature(new FeatureItem
            {
                feature_type = "cut",
                @params = P("plane", "前视基准面", "shape", "circle", "diameter", 10.0, "through_all", true, "center_x", centerX, "center_y", centerY)
            });
        }

        private static ModelData PlateWithExtra(ModelData extra)
        {
            return PlateWithFeature(new FeatureItem { feature_type = extra.feature_type, @params = extra.@params });
        }

        private static ModelData PlateWithFeature(FeatureItem feature)
        {
            return new ModelData
            {
                model_name = "Plate",
                features = new List<FeatureItem>
                {
                    new FeatureItem { feature_type = "extrude", @params = P("plane", "前视基准面", "shape", "rectangle", "length", 200.0, "width", 100.0, "depth", 20.0) },
                    feature
                }
            };
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

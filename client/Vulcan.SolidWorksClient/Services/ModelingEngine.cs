using System;
using System.Collections.Generic;
using Vulcan.SolidWorksClient.Models;

namespace Vulcan.SolidWorksClient.Services
{
    /// <summary>
    /// Seam over the CAD backend. The production implementation talks to
    /// SolidWorks through COM; CI uses a fake backend to exercise sequencing,
    /// seed resolution, and failure reporting without SolidWorks installed.
    /// </summary>
    public interface IFeatureBackend
    {
        void BeginRun(string userRequest);

        void EnsureDocument();

        string ExecuteFeature(
            string featureType,
            Dictionary<string, object> parameters,
            string featureName);

        void EndRun(bool success);
    }

    /// <summary>
    /// Runs a validated feature tree in order, resolves seed references to
    /// generated feature names, and reports partial results when a step fails.
    /// </summary>
    public sealed class ModelingEngine
    {
        private readonly IFeatureBackend _backend;

        public ModelingEngine(IFeatureBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public void Execute(ModelData modelData, string userRequest = "")
        {
            if (modelData == null)
            {
                throw new ArgumentNullException(nameof(modelData), "云端返回的模型参数为空");
            }

            bool hasSingle = !string.IsNullOrEmpty(modelData.feature_type);
            bool hasMulti = modelData.features != null && modelData.features.Count > 0;
            if (hasSingle && hasMulti)
            {
                throw new InvalidOperationException("响应不能同时包含 feature_type 和 features");
            }
            if (!hasSingle && !hasMulti)
            {
                throw new InvalidOperationException("云端未返回有效特征指令");
            }

            _backend.BeginRun(userRequest);
            _backend.EnsureDocument();

            var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var created = new List<string>();
            int index = 0;

            bool success = false;
            try
            {
                if (hasSingle)
                {
                    index = 1;
                    ExecuteOne(modelData.feature_type, modelData.@params, modelData.name, nameMap, created);
                }
                else
                {
                    Logger.Info($"开始执行多特征建模，共{modelData.features.Count}个特征");
                    foreach (FeatureItem feature in modelData.features)
                    {
                        if (feature == null)
                        {
                            throw new InvalidOperationException("特征列表中存在空特征");
                        }
                        index++;
                        ExecuteOne(feature.feature_type, feature.@params, feature.name, nameMap, created);
                    }
                }

                success = true;
                Logger.Info("=== 建模全流程执行完成 ===");
            }
            catch (Exception ex)
            {
                Logger.Error("建模执行失败", ex);
                string partial = created.Count == 0
                    ? "尚未创建任何特征。"
                    : $"已创建 {created.Count} 个特征：{string.Join("、", created)}。";
                throw new InvalidOperationException(
                    $"建模在第 {Math.Max(1, index)} 个特征处中止。{partial}请检查特征树后重试。",
                    ex);
            }
            finally
            {
                _backend.EndRun(success);
            }
        }

        private void ExecuteOne(
            string featureType,
            Dictionary<string, object> parameters,
            string featureName,
            Dictionary<string, string> nameMap,
            List<string> created)
        {
            if (string.IsNullOrEmpty(featureType))
            {
                Logger.Warning("空特征类型，已跳过");
                return;
            }

            Dictionary<string, object> resolved = ResolveSeed(parameters, nameMap);
            string createdName = _backend.ExecuteFeature(featureType, resolved, featureName);
            if (string.IsNullOrEmpty(createdName))
            {
                return;
            }

            created.Add(createdName);
            if (!string.IsNullOrEmpty(featureName))
            {
                nameMap[featureName] = createdName;
                Logger.Info($"特征名称映射：{featureName} -> {createdName}");
            }
        }

        private static Dictionary<string, object> ResolveSeed(
            Dictionary<string, object> parameters,
            Dictionary<string, string> nameMap)
        {
            if (parameters == null || nameMap.Count == 0)
            {
                return parameters;
            }

            object seedValue;
            if (!parameters.TryGetValue("seed", out seedValue) || seedValue == null)
            {
                return parameters;
            }

            string seed = seedValue.ToString().Trim();
            string mapped;
            if (string.IsNullOrEmpty(seed) || !nameMap.TryGetValue(seed, out mapped))
            {
                return parameters;
            }

            var copy = new Dictionary<string, object>(parameters);
            copy["seed"] = mapped;
            return copy;
        }
    }
}

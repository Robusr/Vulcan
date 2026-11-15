using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vulcan.SolidWorksClient.Models;

namespace Vulcan.SolidWorksClient.Services
{
    public class ApiClient
    {
        private const string DefaultBaseUrl = "http://127.0.0.1:5000";
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiClient(string baseUrl = null, string apiToken = null)
        {
            var settings = SettingsStore.Load();
            string explicitBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim();
            _baseUrl = (explicitBaseUrl
                ?? (string.IsNullOrWhiteSpace(settings.ServerUrl) ? null : settings.ServerUrl.Trim())
                ?? Environment.GetEnvironmentVariable("VULCAN_SERVER_URL")
                ?? DefaultBaseUrl).TrimEnd('/');

            string token = apiToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                token = settings.ServerToken;
            }
            if (string.IsNullOrWhiteSpace(token))
            {
                token = Environment.GetEnvironmentVariable("VULCAN_API_TOKEN");
            }

            bool allowInsecure = string.Equals(
                Environment.GetEnvironmentVariable("VULCAN_ALLOW_INSECURE"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (!Uri.TryCreate(_baseUrl, UriKind.Absolute, out Uri uri))
            {
                throw new ArgumentException($"VULCAN_SERVER_URL 不是有效地址: {_baseUrl}", nameof(baseUrl));
            }
            if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback && !allowInsecure)
            {
                throw new InvalidOperationException(
                    "非本机服务端必须使用 HTTPS。若在可信局域网内测试，请设置 VULCAN_ALLOW_INSECURE=true。");
            }

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            if (!string.IsNullOrWhiteSpace(settings.LlmApiKey))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-LLM-API-Key", settings.LlmApiKey);
            }
            if (!string.IsNullOrWhiteSpace(settings.LlmBaseUrl))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-LLM-Base-URL", settings.LlmBaseUrl.Trim());
            }
            if (!string.IsNullOrWhiteSpace(settings.LlmModel))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-LLM-Model", settings.LlmModel.Trim());
            }
            if (!string.IsNullOrWhiteSpace(settings.LlmProvider))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-LLM-Provider", settings.LlmProvider.Trim());
            }
        }

        /// <summary>
        /// 调用生成接口，获取建模参数。
        /// </summary>
        public async Task<ModelData> GenerateModelAsync(string userPrompt)
        {
            try
            {
                Logger.Info($"正在调用后端接口，prompt 长度：{userPrompt?.Length ?? 0}");

                var request = new ApiRequest { prompt = userPrompt };
                string jsonContent = JsonConvert.SerializeObject(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/v1/generate", content);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(responseContent);
                    string errorMsg = errorResponse?.message
                        ?? $"接口请求失败，状态码：{(int)response.StatusCode}";
                    Logger.Error($"后端接口调用失败：{errorMsg}", null);
                    throw new Exception(errorMsg);
                }

                Logger.Info($"后端接口返回成功，内容长度：{responseContent.Length}");
                var modelData = JsonConvert.DeserializeObject<ModelData>(responseContent);

                if (modelData == null)
                {
                    var nullEx = new Exception("后端返回空的建模参数");
                    Logger.Error(nullEx.Message, nullEx);
                    throw nullEx;
                }

                Logger.Info("建模参数解析成功");
                return modelData;
            }
            catch (TaskCanceledException timeoutEx)
            {
                string errorMsg = "后端接口请求超时，请检查后端服务是否启动";
                Logger.Error(errorMsg, timeoutEx);
                throw new Exception(errorMsg);
            }
            catch (JsonException jsonEx)
            {
                string errorMsg = $"后端返回格式解析失败：{jsonEx.Message}";
                Logger.Error(errorMsg, jsonEx);
                throw new Exception(errorMsg);
            }
            catch (Exception ex)
            {
                Logger.Error($"接口调用未知错误：{ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 流式调用生成接口；服务端不支持时自动回退到普通请求。
        /// </summary>
        public async Task<ModelData> GenerateModelStreamAsync(
            string userPrompt,
            IProgress<string> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info($"正在流式调用后端接口，prompt 长度：{userPrompt?.Length ?? 0}");
                var request = new ApiRequest { prompt = userPrompt };
                string jsonContent = JsonConvert.SerializeObject(request);
                using (var content = new StringContent(jsonContent, Encoding.UTF8, "application/json"))
                using (var message = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/generate/stream"))
                {
                    message.Content = content;
                    using (var response = await _httpClient.SendAsync(
                        message, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound ||
                            response.StatusCode == HttpStatusCode.MethodNotAllowed)
                        {
                            Logger.Info("服务端不支持流式接口，回退到普通请求");
                            return await GenerateModelAsync(userPrompt);
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            string errorBody = await response.Content.ReadAsStringAsync();
                            var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorBody);
                            string errorMsg = errorResponse?.message
                                ?? $"接口请求失败，状态码：{(int)response.StatusCode}";
                            throw new Exception(errorMsg);
                        }

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            int received = 0;
                            string line;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (!line.StartsWith("data:", StringComparison.Ordinal))
                                {
                                    continue;
                                }

                                JObject evt;
                                try
                                {
                                    evt = JObject.Parse(line.Substring(5).Trim());
                                }
                                catch (JsonException)
                                {
                                    continue;
                                }

                                string type = (string)evt["type"];
                                if (type == "delta")
                                {
                                    received += ((string)evt["text"] ?? string.Empty).Length;
                                    if (progress != null)
                                    {
                                        progress.Report($"正在生成参数... 已接收 {received} 字符");
                                    }
                                }
                                else if (type == "result")
                                {
                                    var data = evt["data"] == null
                                        ? null
                                        : evt["data"].ToObject<ModelData>();
                                    if (data == null)
                                    {
                                        throw new Exception("流式响应缺少建模参数");
                                    }
                                    Logger.Info("流式建模参数解析成功");
                                    return data;
                                }
                                else if (type == "error")
                                {
                                    string errorMessage = (string)evt["message"] ?? "流式生成失败";
                                    throw new Exception(errorMessage);
                                }
                            }
                        }
                    }
                }

                throw new Exception("流式响应意外结束，未收到建模参数");
            }
            catch (TaskCanceledException timeoutEx)
            {
                string errorMsg = "后端接口请求超时，请检查后端服务是否启动";
                Logger.Error(errorMsg, timeoutEx);
                throw new Exception(errorMsg);
            }
            catch (JsonException jsonEx)
            {
                string errorMsg = $"后端返回格式解析失败：{jsonEx.Message}";
                Logger.Error(errorMsg, jsonEx);
                throw new Exception(errorMsg);
            }
            catch (Exception ex)
            {
                Logger.Error($"流式接口调用失败：{ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 测试服务端连通性。
        /// </summary>
        public async Task<string> CheckHealthAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/health");
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"健康检查失败：{(int)response.StatusCode} {body}");
            }
            return body;
        }
    }
}

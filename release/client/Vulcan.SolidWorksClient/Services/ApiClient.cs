using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
            _baseUrl = (baseUrl ?? Environment.GetEnvironmentVariable("VULCAN_SERVER_URL") ?? DefaultBaseUrl).TrimEnd('/');
            string token = apiToken ?? Environment.GetEnvironmentVariable("VULCAN_API_TOKEN");
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
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        /// <summary>
        /// 调用后端生成接口，获取建模参数。
        /// </summary>
        /// <param name="userPrompt">用户输入的自然语言建模需求</param>
        /// <returns>解析后的 ModelData</returns>
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
    }
}

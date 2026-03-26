using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Vulcan.SolidWorksClient.Models;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.Services
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "http://127.0.0.1:5000"; // 你的后端地址

        public ApiClient()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30) // 和后端超时时间匹配
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// 调用后端生成接口，获取建模参数
        /// </summary>
        /// <param name="userPrompt">用户输入的自然语言建模需求</param>
        /// <returns>解析后的ModelData</returns>
        public async Task<ModelData> GenerateModelAsync(string userPrompt)
        {
            try
            {
                Logger.Info($"正在调用后端接口，用户需求：{userPrompt}");

                // 1. 构建请求
                var request = new ApiRequest { prompt = userPrompt };
                var jsonContent = JsonConvert.SerializeObject(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 2. 发送请求
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/v1/generate", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // 3. 处理错误响应
                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(responseContent);
                    string errorMsg = errorResponse?.message ?? $"接口请求失败，状态码：{(int)response.StatusCode}";
                    Logger.Error($"后端接口调用失败：{errorMsg}", null);
                    throw new Exception(errorMsg);
                }

                // 4. 解析成功响应
                Logger.Info($"后端接口返回成功，原始内容：{responseContent}");
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
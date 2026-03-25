using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Vulcan.SolidWorksClient.Models;
using Vulcan.SolidWorksClient.Services;

namespace Vulcan.SolidWorksClient.Services
{
    public class VulcanApiClient
    {
        // LLM调用接口地址
        private const string ServerApiUrl = "http://127.0.0.1:5000/api/v1/generate";
        private readonly HttpClient _httpClient;

        public VulcanApiClient()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        public async Task<ModelData> GenerateModelParamsAsync(string userPrompt)
        {
            try
            {
                var requestBody = new ApiRequest { prompt = userPrompt };
                string jsonContent = JsonConvert.SerializeObject(requestBody);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Logger.Info($"发送请求到云端：{userPrompt}");
                HttpResponseMessage response = await _httpClient.PostAsync(ServerApiUrl, httpContent);
                response.EnsureSuccessStatusCode();

                string responseContent = await response.Content.ReadAsStringAsync();
                ApiResponse result = JsonConvert.DeserializeObject<ApiResponse>(responseContent);

                if (result.status != "success")
                {
                    throw new Exception(result.message);
                }

                Logger.Info("云端参数获取成功");
                return result.data;
            }
            catch (Exception ex)
            {
                Logger.Error("云端API通信失败", ex);
                throw;
            }
        }
    }
}
using System.Collections.Generic;

namespace Vulcan.SolidWorksClient.Models
{
    /// <summary>
    /// 后端接口请求模型
    /// </summary>
    public class ApiRequest
    {
        /// <summary>
        /// 用户输入的自然语言建模prompt
        /// </summary>
        public string prompt { get; set; }
    }

    /// <summary>
    /// 后端接口错误响应模型
    /// </summary>
    public class ApiErrorResponse
    {
        public string message { get; set; }
        public string status { get; set; }
    }

    /// <summary>
    /// 云端返回的模型数据
    /// </summary>
    public class ModelData
    {
        /// <summary>
        /// 单特征模式：特征类型（兼容旧版）
        /// </summary>
        public string feature_type { get; set; }

        /// <summary>
        /// 单特征模式：特征参数（兼容旧版）
        /// </summary>
        public Dictionary<string, object> @params { get; set; }

        /// <summary>
        /// 单特征模式：特征名称
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// 模型名称
        /// </summary>
        public string model_name { get; set; }

        /// <summary>
        /// 多特征模式：特征序列（复杂建模用）
        /// </summary>
        public List<FeatureItem> features { get; set; }
    }

    /// <summary>
    /// 单个特征项
    /// </summary>
    public class FeatureItem
    {
        /// <summary>
        /// 特征类型
        /// </summary>
        public string feature_type { get; set; }

        /// <summary>
        /// 特征名称
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// 特征参数
        /// </summary>
        public Dictionary<string, object> @params { get; set; }
    }
}
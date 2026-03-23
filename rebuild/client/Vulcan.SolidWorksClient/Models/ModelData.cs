using System.Collections.Generic;

namespace Vulcan.SolidWorksClient.Models
{
    public class ApiResponse
    {
        public string status { get; set; }
        public ModelData data { get; set; }
        public string message { get; set; }
    }

    public class ModelData
    {
        public string feature_type { get; set; }
        public Dictionary<string, object> @params { get; set; }
        public string version { get; set; }
    }

    public class ApiRequest
    {
        public string prompt { get; set; }
    }
}
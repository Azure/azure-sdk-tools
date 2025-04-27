using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AzureSDKDSpecTools.Models
{
    public class ApiviewData
    {
        [JsonPropertyName("Language")]
        public string Language { get; set; } = string.Empty;
        [JsonPropertyName("Package Name")]
        public string PackageName { get; set; } = string.Empty;
        [JsonPropertyName("APIView review link")]
        public string ApiReviewUrl {  get; set; } = string.Empty;
    }
}

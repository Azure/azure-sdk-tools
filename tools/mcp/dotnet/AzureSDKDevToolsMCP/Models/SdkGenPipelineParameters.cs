using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AzureSDKDSpecTools.Models
{
    public class SdkGenPipelineParameters
    {
        [JsonPropertyName("SdkRepoCommit")]
        public string SdkRepoCommit { get; set; } = "main";

        [JsonPropertyName("ConfigType")]
        public string ApiSpecificationType { get; set; } = "TypeSpec";

        [JsonPropertyName("ConfigPath")]
        public required string ApiSpecificationPath { get; set; }

        [JsonPropertyName("ApiVersion")]
        public required string ApiVersion { get; set; }

        [JsonPropertyName("SdkReleaseType")]
        public required string SdkReleaseType { get; set; }

        [JsonPropertyName("SkipPullRequestCreation")]
        public bool CreatePullRequest { get; set; } = true;
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses
{
    public class PackageResponse: Response
    {
        [JsonPropertyName("Work item Id")]
        public int WorkItemId { get; set; }
        public string WorkItemUrl { get; set; } = string.Empty;
        [JsonPropertyName("Package work item status")]
        public string State { get; set; } = string.Empty;
        [JsonPropertyName("Package name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("Version")]
        public string Version { get; set; } = string.Empty;
        [JsonPropertyName("Language")]
        public string Language { get; set; } = string.Empty;
        [JsonPropertyName("Package display name")]
        public string DisplayName { get; set; } = string.Empty;
        [JsonPropertyName("Package type")]
        public string PackageType { get; set; } = string.Empty;
        [JsonPropertyName("Package root path")]
        public string PackageRepoPath { get; set; } = string.Empty;
        [JsonPropertyName("Latest pipeline run url")]
        public string LatestPipelineRun { get; set; } = string.Empty;
        [JsonPropertyName("Latest pipeline run status")]
        public string LatestPipelineStatus { get; set; } = string.Empty;
        [JsonPropertyName("Latest pipeline run details")]
        public string LatestPipelineRunDetails { get; set; } = string.Empty;
        [JsonPropertyName("Release pipeline URL")]
        public string PipelineDefinitionUrl { get; set; } = string.Empty;
        [JsonPropertyName("Change log verified")]
        public bool IsChangeLogReady {
            get
            {
                return !changeLogStatus.Equals("Failed", StringComparison.OrdinalIgnoreCase);
            }
        }
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public string changeLogStatus { get; set; } = string.Empty;
        [JsonPropertyName("Change log verification details")]
        public string ChangeLogValidationDetails { get; set; } = string.Empty;
        [JsonPropertyName("Is API view approved")]
        public bool IsApiViewApproved
        {
            get
            {
                return APIViewStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase);
            }
        }
        [JsonPropertyName("API view status")]
        public string APIViewStatus { get; set; } = string.Empty;
        [JsonPropertyName("API view validation details")]
        public string ApiViewValidationDetails { get; set; } = string.Empty;
        [JsonPropertyName("Is package name approved")]
        public bool IsPackageNameApproved {
            get
            {
                return PackageNameStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase);
            }
        }
        [JsonPropertyName("Package name status")]
        public string PackageNameStatus { get; set; } = string.Empty;
        [JsonPropertyName("Package name approval details")]
        public string PackageNameApprovalDetails { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public List<SDKReleaseInfo> PlannedReleases = [];
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public List<SDKReleaseInfo> ReleasedVersions = [];
        [JsonPropertyName("Is package ready for release")]
        public bool IsPackageReady = false;
        [JsonPropertyName("Planned release date")]
        public string PlannedReleaseDate { get; set; } = string.Empty;
        [JsonPropertyName("Package readiness details")]
        public string PackageReadinessDetails { get; set; } = string.Empty;

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class SDKReleaseInfo
    {
        public string Version { get; set; }
        public string ReleaseDate { get; set; }
        public string ReleaseType { get; set; }
    }
}

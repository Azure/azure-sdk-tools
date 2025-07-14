using System.Text;
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
        [JsonPropertyName("Release pipeline URL")]
        public string PipelineDefinitionUrl { get; set; } = string.Empty;
        [JsonPropertyName("Change log verified")]
        public bool IsChangeLogReady
        {
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
                return APIViewStatus.Equals("Approved") || APIViewStatus.Equals("Not required");
            }
        }
        [JsonPropertyName("API view status")]
        public string APIViewStatus { get; set; } = string.Empty;
        [JsonPropertyName("API view validation details")]
        public string ApiViewValidationDetails { get; set; } = string.Empty;
        [JsonPropertyName("Is package name approved")]
        public bool IsPackageNameApproved
        {
            get
            {
                return PackageNameStatus.Equals("Approved") || PackageNameStatus.Equals("Not required");
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
            //Create an output string with all the properties of the Package
            StringBuilder output = new StringBuilder();
            output.AppendLine($"### Work Item ID: {WorkItemId}");
            output.AppendLine($"### Work Item URL: {WorkItemUrl}");
            output.AppendLine($"### Package Work Item Status: {State}");
            output.AppendLine($"### Package Name: {Name}");
            output.AppendLine($"### Version: {Version}");
            output.AppendLine($"### Language: {Language}");
            output.AppendLine($"### Package Display Name: {DisplayName}");
            output.AppendLine($"### Package Type: {PackageType}");
            output.AppendLine($"### Package Repo Path: {PackageRepoPath}");
            output.AppendLine($"### Latest Pipeline Run URL: {LatestPipelineRun}");
            output.AppendLine($"### Latest Pipeline Run Status: {LatestPipelineStatus}");
            output.AppendLine($"### Release Pipeline URL: {PipelineDefinitionUrl}");
            output.AppendLine($"### Change Log Verified: {IsChangeLogReady}");
            output.AppendLine($"### Change Log Validation Details: {ChangeLogValidationDetails}");
            output.AppendLine($"### Is API View Approved: {IsApiViewApproved}");
            output.AppendLine($"### API View Status: {APIViewStatus}");
            output.AppendLine($"### API View Validation Details: {ApiViewValidationDetails}");
            output.AppendLine($"### Is Package Name Approved: {IsPackageNameApproved}");
            output.AppendLine($"### Package Name Status: {PackageNameStatus}");
            output.AppendLine($"### Package Name Approval Details: {PackageNameApprovalDetails}");
            output.AppendLine($"### Planned Release Date: {PlannedReleaseDate}");
            output.AppendLine($"### Is Package Ready for Release: {IsPackageReady}");
            output.AppendLine($"### Package Readiness Details: {PackageReadinessDetails}");
            if (PlannedReleases.Count > 0)
            {
                output.AppendLine("### Planned Releases:");
                foreach (var release in PlannedReleases)
                {
                    output.AppendLine($"- Version: {release.Version}, Release Date: {release.ReleaseDate}, Release Type: {release.ReleaseType}");
                }
            }
            output.AppendLine($"### Released Versions:");
            output.AppendLine($"- Total Released Versions: {ReleasedVersions.Count}");
            foreach (var release in ReleasedVersions)
            {
                output.AppendLine($"- Version: {release.Version}, Release Date: {release.ReleaseDate}, Release Type: {release.ReleaseType}");
            }
            output.AppendLine($"### Is Package Ready: {IsPackageReady}");
            output.AppendLine($"### Package Readiness Details: {PackageReadinessDetails}");
            output.AppendLine($"### Planned Release Date: {PlannedReleaseDate}");
            return ToString(output.ToString());
        }
    }

    public class SDKReleaseInfo
    {
        public string Version { get; set; }
        public string ReleaseDate { get; set; }
        public string ReleaseType { get; set; }
    }
}

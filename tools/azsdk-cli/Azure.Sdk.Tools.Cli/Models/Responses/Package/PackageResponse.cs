// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.Package
{
    public class PackageResponse : PackageResponseBase
    {
        [JsonPropertyName("work_item_id")]
        public int WorkItemId { get; set; }
        public string WorkItemUrl { get; set; } = string.Empty;
        [JsonPropertyName("package_work_item_status")]
        public string State { get; set; } = string.Empty;
        [JsonPropertyName("package_root_path")]
        public string PackageRepoPath { get; set; } = string.Empty;
        [JsonPropertyName("latest_pipeline_run_url")]
        public string LatestPipelineRun { get; set; } = string.Empty;
        [JsonPropertyName("latest_pipeline_run_status")]
        public string LatestPipelineStatus { get; set; } = string.Empty;
        [JsonPropertyName("release_pipeline_url")]
        public string PipelineDefinitionUrl { get; set; } = string.Empty;
        [JsonPropertyName("change_log_verified")]
        public bool IsChangeLogReady
        {
            get
            {
                return !changeLogStatus.Equals("Failed", StringComparison.OrdinalIgnoreCase);
            }
        }
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public string changeLogStatus { get; set; } = string.Empty;
        [JsonPropertyName("change_log_verification_details")]
        public string ChangeLogValidationDetails { get; set; } = string.Empty;
        [JsonPropertyName("is_apiview_approved")]
        public bool IsApiViewApproved
        {
            get
            {
                return APIViewStatus.Equals("Approved") || APIViewStatus.Equals("Not required");
            }
        }
        [JsonPropertyName("apiview_status")]
        public string APIViewStatus { get; set; } = string.Empty;
        [JsonPropertyName("apiview_validation_details")]
        public string ApiViewValidationDetails { get; set; } = string.Empty;
        [JsonPropertyName("is_package_name_approved")]
        public bool IsPackageNameApproved
        {
            get
            {
                return PackageNameStatus.Equals("Approved") || PackageNameStatus.Equals("Not required");
            }
        }

        [JsonPropertyName("package_name_status")]
        public string PackageNameStatus { get; set; } = string.Empty;
        [JsonPropertyName("package_name_approval_details")]
        public string PackageNameApprovalDetails { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public List<SDKReleaseInfo> PlannedReleases = [];
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public List<SDKReleaseInfo> ReleasedVersions = [];
        [JsonPropertyName("is_package_ready_for_release")]
        public bool IsPackageReady = false;
        [JsonPropertyName("planned_release_date")]
        public string PlannedReleaseDate { get; set; } = string.Empty;
        [JsonPropertyName("package_readiness_details")]
        public string PackageReadinessDetails { get; set; } = string.Empty;

        protected override string Format()
        {
            //Create an output string with all the properties of the Package
            StringBuilder output = new StringBuilder();
            output.AppendLine($"### Work Item ID: {WorkItemId}");
            output.AppendLine($"### Work Item URL: {WorkItemUrl}");
            output.AppendLine($"### Package Work Item Status: {State}");
            output.AppendLine($"### Package PackageName: {PackageName}");
            output.AppendLine($"### Version: {Version}");
            output.AppendLine($"### Language: {Language.ToString()}");
            output.AppendLine($"### Package Display PackageName: {DisplayName}");
            output.AppendLine($"### Package Type: {PackageType.ToString()}");
            output.AppendLine($"### Package Repo Path: {PackageRepoPath}");
            output.AppendLine($"### TypeSpec Project: {TypeSpecProject}");
            output.AppendLine($"### Latest Pipeline Run URL: {LatestPipelineRun}");
            output.AppendLine($"### Latest Pipeline Run Status: {LatestPipelineStatus}");
            output.AppendLine($"### Release Pipeline URL: {PipelineDefinitionUrl}");
            output.AppendLine($"### Change Log Verified: {IsChangeLogReady}");
            output.AppendLine($"### Change Log Validation Details: {ChangeLogValidationDetails}");
            output.AppendLine($"### Is API View Approved: {IsApiViewApproved}");
            output.AppendLine($"### API View Status: {APIViewStatus}");
            output.AppendLine($"### API View Validation Details: {ApiViewValidationDetails}");
            output.AppendLine($"### Is Package PackageName Approved: {IsPackageNameApproved}");
            output.AppendLine($"### Package PackageName Status: {PackageNameStatus}");
            output.AppendLine($"### Package PackageName Approval Details: {PackageNameApprovalDetails}");
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
            return output.ToString();
        }
    }

    public class SDKReleaseInfo
    {
        public string Version { get; set; }
        public string ReleaseDate { get; set; }
        public string ReleaseType { get; set; }
    }
}

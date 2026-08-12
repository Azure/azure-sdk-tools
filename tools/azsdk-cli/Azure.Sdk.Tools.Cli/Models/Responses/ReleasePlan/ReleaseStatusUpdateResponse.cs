// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan
{
    /// <summary>
    /// Response model for release status update operations.
    /// </summary>
    public class ReleaseStatusUpdateResponse : ReleasePlanBaseResponse
    {
        [JsonPropertyName("release_status")]
        public string ReleaseStatus { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public SdkLanguage Language { get; set; } = SdkLanguage.Unknown;

        [JsonPropertyName("release_plan_id")]
        public int ReleasePlanId { get; set; }

        [JsonPropertyName("package_name")]
        public string PackageName { get; set; } = string.Empty;

        [JsonPropertyName("package_version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PackageVersion { get; set; }

        [JsonPropertyName("sdk_release_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SdkReleaseType { get; set; }

        [JsonPropertyName("release_pipeline_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReleasePipelineUrl { get; set; }

        [JsonPropertyName("sdk_pull_request")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SdkPullRequest { get; set; }

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }

        [JsonPropertyName("release_plan_finished")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ReleasePlanFinished { get; set; }

        public void SetLanguage(string language)
        {
            Language = SdkLanguageHelpers.GetSdkLanguage(language);
        }
        protected override string Format()
        {
            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(ReleaseStatus))
            {
                result.AppendLine($"Release Status: {ReleaseStatus}");
            }
            if (!string.IsNullOrEmpty(PackageName))
            {
                result.AppendLine($"Package Name: {PackageName}");
            }
            if (!string.IsNullOrEmpty(PackageVersion))
            {
                result.AppendLine($"Package Version: {PackageVersion}");
            }
            if (!string.IsNullOrEmpty(SdkReleaseType))
            {
                result.AppendLine($"SDK Release Type: {SdkReleaseType}");
            }
            if (!string.IsNullOrEmpty(ReleasePipelineUrl))
            {
                result.AppendLine($"Release Pipeline URL: {ReleasePipelineUrl}");
            }
            if (!string.IsNullOrEmpty(SdkPullRequest))
            {
                result.AppendLine($"SDK Pull Request: {SdkPullRequest}");
            }
            if (Language != SdkLanguage.Unknown)
            {
                result.AppendLine($"Language: {Language}");
            }
            if (ReleasePlanId > 0)
            {
                result.AppendLine($"Release Plan ID: {ReleasePlanId}");
            }
            if (!string.IsNullOrEmpty(Message))
            {
                result.AppendLine(Message);
            }
            if (ReleasePlanFinished)
            {
                result.AppendLine("Release plan has been marked as Finished.");
            }
            return result.ToString();
        }
    }
}

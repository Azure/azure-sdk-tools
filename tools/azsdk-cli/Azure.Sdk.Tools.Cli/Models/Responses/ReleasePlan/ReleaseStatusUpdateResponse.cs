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
            if (Language != SdkLanguage.Unknown)
            {
                result.AppendLine($"Language: {Language}");
            }
            if (ReleasePlanId > 0)
            {
                result.AppendLine($"Release Plan ID: {ReleasePlanId}");
            }
            return result.ToString();
        }
    }
}

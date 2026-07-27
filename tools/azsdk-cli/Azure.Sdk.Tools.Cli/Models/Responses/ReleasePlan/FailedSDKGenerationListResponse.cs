// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList
{
    /// <summary>
    /// Represents a response containing release plans that had an SDK generation failure in the last 24 hours.
    /// </summary>
    public class FailedSDKGenerationListResponse : ReleasePlanBaseResponse
    {
        [JsonPropertyName("failed_release_plans")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<FailedSDKGenerationReleasePlan>? FailedReleasePlans { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        protected override string Format()
        {
            var result = new StringBuilder();
            if (FailedReleasePlans != null && FailedReleasePlans.Count > 0)
            {
                result.AppendLine($"Release plans with failed SDK generation: {FailedReleasePlans.Count}");
                result.AppendLine(new string('-', 40));

                foreach (var failed in FailedReleasePlans)
                {
                    var rp = failed.ReleasePlan;
                    result.AppendLine($"Release Plan ID: {rp.ReleasePlanId}");
                    result.AppendLine($"Release Plan Link: {rp.ReleasePlanLink}");
                    result.AppendLine($"Failed Languages: {string.Join(", ", failed.FailedLanguages)}");
                    result.AppendLine(new string('-', 40));
                }
            }
            else
            {
                result.AppendLine("No release plans with SDK generation failures in the last 24 hours.");
            }
            return result.ToString();
        }
    }
}

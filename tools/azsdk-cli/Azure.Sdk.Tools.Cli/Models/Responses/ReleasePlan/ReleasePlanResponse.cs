using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan
{
    /// <summary>
    /// Represents a response containing release plan details and the result of a release plan operation.
    /// </summary>
    public class ReleasePlanResponse : ReleasePlanBaseResponse
    {
        [JsonPropertyName("release_plan_details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReleasePlanWorkItem? ReleasePlanDetails { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
        [JsonPropertyName("warnings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Warnings { get; set; }
        [JsonPropertyName("release_plan_link")]
        public string ReleasePlanLink => ReleasePlanDetails != null ? ReleasePlanDetails.ReleasePlanLink : string.Empty;
        protected override string Format()
        {
            var result = new StringBuilder();
            if (ReleasePlanDetails != null)
            {
                result.AppendLine($"Release Plan ID: {ReleasePlanDetails.ReleasePlanId}");
                result.AppendLine($"Title: {ReleasePlanDetails.Title}");
                result.AppendLine($"Status: {ReleasePlanDetails.Status}");
                result.AppendLine($"Owner: {ReleasePlanDetails.Owner}");
                result.AppendLine($"SDK Release Month: {ReleasePlanDetails.SDKReleaseMonth}");
                result.AppendLine($"Release Plan Link: {ReleasePlanLink}");
                result.AppendLine($"Is API spec approved: {ReleasePlanDetails.IsSpecApproved}");
            }
            else
            {
                result.AppendLine("No release plan details available.");
            }
            if (Warnings?.Count > 0)
            {
                foreach (var warning in Warnings)
                {
                    result.AppendLine($"[WARNING] {warning}");
                }
            }
            return result.ToString();
        }
    }
}

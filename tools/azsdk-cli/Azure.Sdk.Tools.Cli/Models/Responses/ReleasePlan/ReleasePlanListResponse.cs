using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlanList
{
    /// <summary>
    /// Represents a response containing multiple release plans and the result of a release plan operation
    /// </summary>
    public class ReleasePlanListResponse : ReleasePlanBaseResponse
    {
        [JsonPropertyName("release_plans")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ReleasePlanWorkItem>? ReleasePlanDetailsList { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("dry_run")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DryRun { get; set; }

        /// <summary>
        /// Preview eligibility reasons keyed by Azure DevOps work item ID.
        /// </summary>
        [JsonPropertyName("eligibility_reasons")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, string>? EligibilityReasons { get; set; }

        public override string ToString()
        {
            // Keep confirmed candidates visible when a dry run also reports lookup failures.
            return DryRun && OperationStatus == Status.Failed
                ? Format() + Environment.NewLine + base.ToString()
                : base.ToString();
        }

        protected override string Format()
        {
            var result = new StringBuilder();
            if (DryRun)
            {
                result.AppendLine(Message);
            }
            if (ReleasePlanDetailsList != null && ReleasePlanDetailsList.Count > 0)
            {
                result.AppendLine($"Total Release Plans: {ReleasePlanDetailsList.Count}");
                result.AppendLine(new string('-', 40));

                for (int i = 0; i < ReleasePlanDetailsList.Count; i++)
                {
                    var rp = ReleasePlanDetailsList[i];
                    var planId = DryRun && rp.ReleasePlanId <= 0 ? rp.WorkItemId : rp.ReleasePlanId;
                    result.AppendLine($"[{i + 1}] Release Plan ID: {planId}");
                    result.AppendLine($"Title: {rp.Title}");
                    result.AppendLine($"Status: {rp.Status}");
                    result.AppendLine($"Owner: {rp.Owner}");
                    result.AppendLine($"SDK Release Month: {rp.SDKReleaseMonth}");
                    if (DryRun)
                    {
                        result.AppendLine($"Release Plan Link: {rp.ReleasePlanLink}");
                        if (EligibilityReasons?.TryGetValue(rp.WorkItemId, out var reason) == true)
                        {
                            result.AppendLine($"Eligibility Reason: {reason}");
                        }
                    }
                    result.AppendLine(new string('-', 40));
                }
            }
            else
            {
                result.AppendLine(DryRun && ReleasePlanDetailsList != null
                    ? "No eligible release plans found."
                    : "No release plan details available.");
            }
            return result.ToString();
        }
    }
}

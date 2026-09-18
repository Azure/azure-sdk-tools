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
        /// Eligibility reasons for previewed or successfully abandoned plans, keyed by Azure DevOps work item ID.
        /// </summary>
        [JsonPropertyName("eligibility_reasons")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, string>? EligibilityReasons { get; set; }

        [JsonPropertyName("preview_summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReleasePlanPreviewSummary? PreviewSummary { get; set; }

        [JsonPropertyName("skipped_plans")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ReleasePlanSkipDetails>? SkippedPlans { get; set; }

        public override string ToString()
        {
            // Preserve partial results and summaries alongside errors in either mode.
            return OperationStatus == Status.Failed && (ReleasePlanDetailsList != null || !string.IsNullOrWhiteSpace(Message))
                ? Format() + Environment.NewLine + base.ToString()
                : base.ToString();
        }

        protected override string Format()
        {
            var result = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(Message))
            {
                result.AppendLine(Message);
            }
            if (PreviewSummary != null)
            {
                result.AppendLine($"Scanned: {PreviewSummary.Scanned}; Eligible: {PreviewSummary.Eligible}; Skipped: {PreviewSummary.Skipped}; Evaluation errors: {PreviewSummary.EvaluationErrors}");
                foreach (var (category, count) in PreviewSummary.SkippedByReason)
                {
                    result.AppendLine($"  {category}: {count}");
                }
            }
            if (ReleasePlanDetailsList != null && ReleasePlanDetailsList.Count > 0)
            {
                result.AppendLine($"Total Release Plans: {ReleasePlanDetailsList.Count}");
                result.AppendLine(new string('-', 40));

                for (int i = 0; i < ReleasePlanDetailsList.Count; i++)
                {
                    var rp = ReleasePlanDetailsList[i];
                    var planId = rp.ReleasePlanId > 0 ? rp.ReleasePlanId : rp.WorkItemId;
                    result.AppendLine($"[{i + 1}] Release Plan ID: {planId}");
                    result.AppendLine($"Title: {rp.Title}");
                    result.AppendLine($"Status: {rp.Status}");
                    result.AppendLine($"Owner: {rp.Owner}");
                    result.AppendLine($"SDK Release Month: {rp.SDKReleaseMonth}");
                    result.AppendLine($"Release Plan Link: {rp.ReleasePlanLink}");
                    if (EligibilityReasons?.TryGetValue(rp.WorkItemId, out var reason) == true)
                    {
                        result.AppendLine($"{(DryRun ? "Eligibility" : "Abandonment")} Reason: {reason}");
                    }
                    result.AppendLine(new string('-', 40));
                }
            }
            else
            {
                result.AppendLine(ReleasePlanDetailsList != null && EligibilityReasons != null
                    ? (DryRun ? "No eligible release plans found." : "No release plans were abandoned.")
                    : "No release plan details available.");
            }
            if (SkippedPlans?.Count > 0)
            {
                result.AppendLine("Skipped release plans (first exclusion reason):");
                foreach (var plan in SkippedPlans)
                {
                    var planId = plan.ReleasePlanId > 0 ? plan.ReleasePlanId : plan.WorkItemId;
                    result.AppendLine($"- Release Plan ID: {planId} | {plan.Title} | Target month: {plan.TargetMonth}");
                    result.AppendLine($"  {plan.Category}: {plan.Reason}");
                    result.AppendLine($"  Release Plan Link: {plan.ReleasePlanLink}");
                }
            }
            return result.ToString();
        }
    }
}

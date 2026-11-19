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

        /// <summary>
        /// Skipped-plan dashboard links keyed by release plan ID, falling back to work item ID.
        /// </summary>
        [JsonPropertyName("skipped_plans")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, string>? SkippedPlans { get; set; }

        [JsonPropertyName("maintenance_results")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ReleasePlanMaintenanceResult>? MaintenanceResults { get; set; }

        [JsonPropertyName("maintenance_summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, Dictionary<string, int>>? MaintenanceSummary => MaintenanceResults?
            .GroupBy(result => result.Operation)
            .ToDictionary(operation => operation.Key.ToString(), operation => (operation.Key switch
            {
                ReleasePlanMaintenanceOperation.Preview => new[]
                {
                    ReleasePlanMaintenanceOutcome.Eligible, ReleasePlanMaintenanceOutcome.Skipped, ReleasePlanMaintenanceOutcome.Failed
                },
                ReleasePlanMaintenanceOperation.Cleanup => new[]
                {
                    ReleasePlanMaintenanceOutcome.Abandoned, ReleasePlanMaintenanceOutcome.Skipped, ReleasePlanMaintenanceOutcome.Failed
                },
                _ => new[]
                {
                    ReleasePlanMaintenanceOutcome.Delivered, ReleasePlanMaintenanceOutcome.Skipped, ReleasePlanMaintenanceOutcome.Failed
                }
            }).ToDictionary(outcome => outcome.ToString(), outcome => operation.Count(result => result.Outcome == outcome)));

        [JsonPropertyName("warnings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Warnings { get; set; }

        public override string ToString()
        {
            // Preserve partial results and summaries alongside errors in either mode.
            return OperationStatus == Status.Failed && (ReleasePlanDetailsList != null || !string.IsNullOrWhiteSpace(Message)
                || MaintenanceResults?.Count > 0 || Warnings?.Count > 0)
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
            if (MaintenanceSummary != null)
            {
                result.AppendLine("Maintenance summary:");
                if (MaintenanceSummary.Count == 0)
                {
                    result.AppendLine("No maintenance actions were performed.");
                }
                foreach (var (operation, outcomes) in MaintenanceSummary)
                {
                    result.AppendLine($"{operation}: {string.Join("; ", outcomes.Select(outcome => $"{outcome.Key}: {outcome.Value}"))}");
                }
            }
            if (MaintenanceResults?.Count > 0)
            {
                result.AppendLine("Per-plan maintenance outcomes:");
                foreach (var outcome in MaintenanceResults)
                {
                    result.AppendLine($"[{outcome.Outcome.ToString().ToUpperInvariant()}] {outcome.Operation} | Release Plan ID: {outcome.ReleasePlanId} | Work Item ID: {outcome.WorkItemId} | {outcome.Reason} | {outcome.ReleasePlanLink}");
                }
            }
            foreach (var warning in Warnings ?? [])
            {
                result.AppendLine($"[WARNING] {warning}");
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
                result.AppendLine("Skipped release plans:");
                foreach (var (planId, link) in SkippedPlans)
                {
                    result.AppendLine($"- Release Plan ID: {planId} | Release Plan Link: {link}");
                }
            }
            return result.ToString();
        }
    }
}

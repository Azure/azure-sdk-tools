using System.Text;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;
using Azure.Sdk.Tools.Cli.Services.Notification;

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
        public ReleasePlanBatchSummary? PreviewSummary { get; set; }

        /// <summary>
        /// Skipped-plan dashboard links keyed by release plan ID, falling back to work item ID.
        /// </summary>
        [JsonPropertyName("skipped_plans")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, string>? SkippedPlans { get; set; }

        // Counts of abandoned, skipped, and failed plans from actual cleanup.
        [JsonPropertyName("plan_cleanup_summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReleasePlanBatchSummary? PlanCleanupSummary { get; set; }

        // One overdue-reminder email result per backing Azure DevOps work-item ID.
        [JsonPropertyName("reminder_email_results")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, NotificationResult>? ReminderEmailResults { get; set; }

        // One confirmation-email result per successfully abandoned work item.
        [JsonPropertyName("abandonment_email_results")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, NotificationResult>? AbandonmentEmailResults { get; set; }

        // Accepted, skipped, and failed reminder-email counts derived from the results.
        [JsonPropertyName("reminder_email_summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReleasePlanBatchSummary? ReminderEmailSummary => CountEmailResults(ReminderEmailResults);

        // Confirmation-email counts, independent of successful state changes.
        [JsonPropertyName("abandonment_email_summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReleasePlanBatchSummary? AbandonmentEmailSummary => CountEmailResults(AbandonmentEmailResults);

        // Plan-specific follow-up information that does not fail the command.
        [JsonPropertyName("warnings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Warnings { get; set; }

        // A missing collection means no completed scan; an empty one reports zero counts.
        private static ReleasePlanBatchSummary? CountEmailResults(Dictionary<int, NotificationResult>? notifications) =>
            notifications == null ? null : new ReleasePlanBatchSummary
            {
                Scanned = notifications.Count,
                AcceptedEmailCount = notifications.Values.Count(result => result.Status == NotificationStatus.Sent),
                Skipped = notifications.Values.Count(result => result.Status is NotificationStatus.Disabled or NotificationStatus.SkippedNoRecipients),
                FailedItemCount = notifications.Values.Count(result => result.IsFailure)
            };

        public override string ToString()
        {
            // Preserve partial results and summaries alongside errors in either mode.
            return OperationStatus == Status.Failed && (ReleasePlanDetailsList != null || !string.IsNullOrWhiteSpace(Message)
                || PlanCleanupSummary != null || ReminderEmailResults != null || AbandonmentEmailResults != null || Warnings?.Count > 0)
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
            if (PlanCleanupSummary != null)
            {
                result.AppendLine($"Plan cleanup: Abandoned plans: {PlanCleanupSummary.AbandonedPlanCount}; Skipped: {PlanCleanupSummary.Skipped}; Failed: {PlanCleanupSummary.FailedItemCount}");
                result.AppendLine($"Scanned: {PlanCleanupSummary.Scanned}; Eligible candidates: {PlanCleanupSummary.Eligible}; Evaluation errors: {PlanCleanupSummary.EvaluationErrors}");
            }
            AppendEmailResults(result, "Reminder email", ReminderEmailResults, ReminderEmailSummary);
            AppendEmailResults(result, "Abandonment email", AbandonmentEmailResults, AbandonmentEmailSummary);
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

        // Show both plan IDs for every accepted, skipped, or failed email.
        private void AppendEmailResults(StringBuilder text, string notification,
            Dictionary<int, NotificationResult>? notifications, ReleasePlanBatchSummary? summary)
        {
            if (notifications == null || summary == null)
            {
                return;
            }
            text.AppendLine($"{notification}s: Accepted: {summary.AcceptedEmailCount}; Skipped: {summary.Skipped}; Failed: {summary.FailedItemCount}");
            foreach (var (workItemId, result) in notifications)
            {
                var plan = ReleasePlanDetailsList?.FirstOrDefault(plan => plan.WorkItemId == workItemId);
                var planId = plan?.ReleasePlanId > 0 ? plan.ReleasePlanId : workItemId;
                text.AppendLine($"[{result.Status.ToString().ToUpperInvariant()}] {notification} | Release Plan ID: {planId} | Work Item ID: {workItemId} | {result.ErrorMessage ?? "Accepted by the notification service; inbox delivery is not verified."} | {plan?.ReleasePlanLink}");
            }
        }
    }
}

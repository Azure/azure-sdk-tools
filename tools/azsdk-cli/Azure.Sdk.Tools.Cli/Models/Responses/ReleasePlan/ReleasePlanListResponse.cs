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
        public ReleasePlanPreviewSummary? PreviewSummary { get; set; }

        /// <summary>
        /// Skipped-plan dashboard links keyed by release plan ID, falling back to work item ID.
        /// </summary>
        [JsonPropertyName("skipped_plans")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, string>? SkippedPlans { get; set; }

        [JsonPropertyName("cleanup_summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReleasePlanPreviewSummary? CleanupSummary { get; set; }

        [JsonPropertyName("reminder_notifications")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, NotificationResult>? ReminderNotifications { get; set; }

        [JsonPropertyName("abandonment_notifications")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<int, NotificationResult>? AbandonmentNotifications { get; set; }

        [JsonPropertyName("reminder_summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReleasePlanPreviewSummary? ReminderSummary => SummarizeNotifications(ReminderNotifications);

        [JsonPropertyName("abandonment_notification_summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReleasePlanPreviewSummary? AbandonmentNotificationSummary => SummarizeNotifications(AbandonmentNotifications);

        [JsonPropertyName("warnings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Warnings { get; set; }

        private static ReleasePlanPreviewSummary? SummarizeNotifications(Dictionary<int, NotificationResult>? notifications) =>
            notifications == null ? null : new ReleasePlanPreviewSummary
            {
                Scanned = notifications.Count,
                Delivered = notifications.Values.Count(result => result.Status == NotificationStatus.Sent),
                Skipped = notifications.Values.Count(result => result.Status is NotificationStatus.Disabled or NotificationStatus.SkippedNoRecipients),
                Failed = notifications.Values.Count(result => result.IsFailure)
            };

        public override string ToString()
        {
            // Preserve partial results and summaries alongside errors in either mode.
            return OperationStatus == Status.Failed && (ReleasePlanDetailsList != null || !string.IsNullOrWhiteSpace(Message)
                || CleanupSummary != null || ReminderNotifications != null || AbandonmentNotifications != null || Warnings?.Count > 0)
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
            if (CleanupSummary != null)
            {
                result.AppendLine($"Cleanup: Abandoned: {CleanupSummary.Abandoned}; Skipped: {CleanupSummary.Skipped}; Failed: {CleanupSummary.Failed}");
                result.AppendLine($"Scanned: {CleanupSummary.Scanned}; Eligible candidates: {CleanupSummary.Eligible}; Evaluation errors: {CleanupSummary.EvaluationErrors}");
            }
            AppendNotificationResults(result, "Reminder", ReminderNotifications, ReminderSummary);
            AppendNotificationResults(result, "Confirmation", AbandonmentNotifications, AbandonmentNotificationSummary);
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

        private void AppendNotificationResults(StringBuilder text, string notification,
            Dictionary<int, NotificationResult>? notifications, ReleasePlanPreviewSummary? summary)
        {
            if (notifications == null || summary == null)
            {
                return;
            }
            text.AppendLine($"{notification}: Delivered: {summary.Delivered}; Skipped: {summary.Skipped}; Failed: {summary.Failed}");
            foreach (var (workItemId, result) in notifications)
            {
                var plan = ReleasePlanDetailsList?.FirstOrDefault(plan => plan.WorkItemId == workItemId);
                var planId = plan?.ReleasePlanId > 0 ? plan.ReleasePlanId : workItemId;
                text.AppendLine($"[{result.Status.ToString().ToUpperInvariant()}] {notification} | Release Plan ID: {planId} | Work Item ID: {workItemId} | {result.ErrorMessage ?? "Accepted by the notification service; inbox delivery is not verified."} | {plan?.ReleasePlanLink}");
            }
        }
    }
}

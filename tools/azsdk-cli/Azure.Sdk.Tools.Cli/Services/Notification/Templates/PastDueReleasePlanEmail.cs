using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services.Notification.Templates
{
    public class PastDueReleasePlanEmail : EmailPayload
    {
        private readonly ReleasePlanWorkItem releasePlan;

        public PastDueReleasePlanEmail(ReleasePlanWorkItem releasePlan)
        {
            this.releasePlan = releasePlan ?? throw new ArgumentNullException(nameof(releasePlan));
            EmailTo = string.IsNullOrWhiteSpace(releasePlan.ReleasePlanSubmittedByEmail)
                ? []
                : [releasePlan.ReleasePlanSubmittedByEmail];
        }

        public override string Subject => $"Your release plan ({ReleasePlanIdentifier}) is now past due";

        public override string Body =>
            $"""
            <html>
            <body>
                <p>Hi {releasePlan.Owner},</p>
                <p>Your release plan (<a href="{releasePlan.ReleasePlanLink}">{ReleasePlanIdentifier}</a>) is now past due and has been marked as abandoned because there are no active SDK PRs associated with it.</p>
                <p>If you intend to continue the release, please reopen the release plan and update the target release month accordingly. Going forward, please ensure that your release plan is actively managed and reaches either Completed or Closed status by the end of its target release month.</p>
                <p>Thank you.</p>
            </body>
            </html>
            """;

        private int ReleasePlanIdentifier => releasePlan.ReleasePlanId > 0
            ? releasePlan.ReleasePlanId
            : releasePlan.WorkItemId;
    }
}

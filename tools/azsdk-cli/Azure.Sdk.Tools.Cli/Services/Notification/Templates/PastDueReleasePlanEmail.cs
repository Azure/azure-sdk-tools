using System.Net;
using Azure.Sdk.Tools.Cli.Models;
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
                <p>Hi {WebUtility.HtmlEncode(releasePlan.Owner)},</p>
                <p>Your release plan (<a href="{WebUtility.HtmlEncode(releasePlan.ReleasePlanLink)}">{ReleasePlanIdentifier}</a>) is more than one month past its target release month ({WebUtility.HtmlEncode(releasePlan.SDKReleaseMonth)}) and has been marked as abandoned because {AbandonmentExplanation}.</p>
                <p>If you intend to continue the release, please reopen the release plan and update the target release month accordingly. Going forward, please ensure that your release plan is actively managed and reaches either Completed or Closed status by the end of its target release month.</p>
                <p>Thank you.</p>
            </body>
            </html>
            """;

        private string AbandonmentExplanation => releasePlan.ApiReleaseType == ApiReleaseType.PrivatePreview
            ? "its spec PR is missing or has not been merged"
            : "there are no active SDK PRs associated with it";

        private int ReleasePlanIdentifier => releasePlan.ReleasePlanId > 0
            ? releasePlan.ReleasePlanId
            : releasePlan.WorkItemId;
    }
}

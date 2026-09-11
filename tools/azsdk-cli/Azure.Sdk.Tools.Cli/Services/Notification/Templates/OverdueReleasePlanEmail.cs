// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services.Notification.Templates
{
    public class OverdueReleasePlanEmail : EmailPayload
    {
        private readonly ReleasePlanWorkItem _releasePlan;
        private readonly bool _hasInactiveWork;

        public OverdueReleasePlanEmail(ReleasePlanWorkItem releasePlan, bool hasInactiveWork)
        {
            _releasePlan = releasePlan ?? throw new ArgumentNullException(nameof(releasePlan));
            _hasInactiveWork = hasInactiveWork;
            EmailTo = string.IsNullOrWhiteSpace(releasePlan.ReleasePlanSubmittedByEmail)
                ? []
                : [releasePlan.ReleasePlanSubmittedByEmail];
        }

        public override string Subject => $"Your release plan ({ReleasePlanIdentifier}) is now past due";

        public override string Body =>
            $"""
            <html>
            <body>
                <p>Hello {WebUtility.HtmlEncode(_releasePlan.Owner)},</p>
                <p>Your release plan (<a href="{WebUtility.HtmlEncode(_releasePlan.ReleasePlanLink)}">{ReleasePlanIdentifier}</a>) is past due. Its target release month was {WebUtility.HtmlEncode(_releasePlan.SDKReleaseMonth)}.</p>
                {ReleaseWorkSummary}
                <p><strong>Required actions:</strong></p>
                <ul>
                    <li>Update the Target Release Month, or</li>
                    {ReleaseWorkAction}
                </ul>
                {AbandonmentNotice}
                <p>Thank you.</p>
            </body>
            </html>
            """;

        private bool IsPrivatePreview => _releasePlan.ApiReleaseType == ApiReleaseType.PrivatePreview;

        private string ReleaseWorkSummary
        {
            get
            {
                if (IsPrivatePreview)
                {
                    return "<p>The spec PR for this Private Preview release plan is missing or has not been merged.</p>";
                }

                var missingSDKs = _releasePlan.SDKInfo
                    .Where(info => !string.Equals(info.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase)
                        && (_releasePlan.IsManagementPlane || !string.Equals(info.Language, "Go", StringComparison.OrdinalIgnoreCase))
                        && !string.Equals(info.ReleaseExclusionStatus, "Requested", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(info.ReleaseExclusionStatus, "Approved", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(info.ReleaseExclusionStatus, "MissingEmitterConfig", StringComparison.OrdinalIgnoreCase))
                    .Select(info => WebUtility.HtmlEncode(info.Language));
                var plane = _releasePlan.IsManagementPlane ? "Management Plane" : "Data Plane";
                return $"<p><strong>Azure SDK Type:</strong> {plane}</p>"
                    + $"<p><strong>SDKs not yet published:</strong> {string.Join(", ", missingSDKs)}</p>";
            }
        }

        private string ReleaseWorkAction => IsPrivatePreview
            ? "<li>Merge the spec PR, or</li><li>Abandon the release plan using the Azure SDK Agent.</li>"
            : _hasInactiveWork
                ? "<li>Abandon the release plan using the Azure SDK Agent.</li>"
                : "<li>Complete remaining SDK release activities.</li>";

        private string AbandonmentNotice => _hasInactiveWork
            ? "<p>Inactive release plans are automatically abandoned after one full overdue calendar month unless the target month or release activity is updated.</p>"
            : string.Empty;

        private int ReleasePlanIdentifier => _releasePlan.ReleasePlanId > 0
            ? _releasePlan.ReleasePlanId
            : _releasePlan.WorkItemId;
    }
}
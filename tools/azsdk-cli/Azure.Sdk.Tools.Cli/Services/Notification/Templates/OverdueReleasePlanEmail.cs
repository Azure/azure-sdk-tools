// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services.Notification.Templates
{
    public class OverdueReleasePlanEmail : EmailPayload
    {
        private readonly ReleasePlanEmailContent _content;
        // Null means activity could not be verified, so the reminder must not assert a work state.
        private readonly bool? _hasInactiveWork;
        private readonly string? _workReason;

        public OverdueReleasePlanEmail(ReleasePlanWorkItem releasePlan, bool? hasInactiveWork, string? workReason, DateTimeOffset sentAt)
        {
            ArgumentNullException.ThrowIfNull(releasePlan);
            if (hasInactiveWork.HasValue)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(workReason);
            }
            _content = new ReleasePlanEmailContent(releasePlan, sentAt);
            _hasInactiveWork = hasInactiveWork;
            _workReason = workReason;
            EmailTo = string.IsNullOrWhiteSpace(releasePlan.ReleasePlanSubmittedByEmail)
                ? []
                : [releasePlan.ReleasePlanSubmittedByEmail];
        }

        public override string Subject => $"Your Azure SDK Release Plan ({_content.Id}) is now past due";

        public override string Body =>
            $"""
            <html>
            <body>
                <p>Hello {_content.Owner},</p>
                <p>Your <a href="{_content.Url}">Azure SDK Release Plan {_content.Id}</a> is past due. Its target release month was <strong>{_content.TargetMonth}</strong>. Please review the details below and take the action that matches your release.</p>
                {_content.OverviewAndDetails}
                <p><strong>Why you are receiving this reminder:</strong> {ReleaseWorkSummary}</p>
                {AbandonmentNotice}
                <p><strong>What to do next</strong></p>
                <ol>
                    {_content.UpdateTargetAction}
                    {(_hasInactiveWork == null ? _content.VerificationAction : _content.CompleteReleaseAction + _content.AbandonAction)}
                </ol>
                {ReleasePlanEmailContent.SupportAndSignature}
            </body>
            </html>
            """;

        private string ReleaseWorkSummary => _hasInactiveWork.HasValue
            ? WebUtility.HtmlEncode(_workReason)
            : _content.IsUnknownReleaseType
                ? "The recorded release type is missing or unrecognized. We cannot determine which release-completion and automatic-abandonment rules apply until this information is corrected."
                : "We could not retrieve or validate the current status or approval of one or more linked GitHub pull requests. This is a verification issue, not confirmation that your release is inactive.";

        private string AbandonmentNotice
        {
            get
            {
                if (_hasInactiveWork == null)
                {
                    return "<p><strong>What happens if you take no action:</strong> This verification result does not establish that your Release Plan is inactive. Automatic abandonment requires a successful eligibility check. A later monthly check may mark it <strong>Abandoned</strong> if it remains overdue, its grace month has ended, and eligibility is confirmed. Update the target release month now if the release is still planned.</p>";
                }
                if (_hasInactiveWork == false)
                {
                    var protection = _content.IsPrivatePreview
                        ? "A merged specification pull request prevents automatic abandonment for a Private Preview release."
                        : "For Public Preview and GA releases, an approved open or merged SDK pull request, or any SDK recorded as released, prevents automatic abandonment.";
                    return $"<p><strong>What happens if you take no action:</strong> Your Release Plan remains past due, but it is not currently eligible for automatic abandonment. {protection} If that protection no longer applies, a later monthly cleanup can mark an overdue Release Plan <strong>Abandoned</strong> once its grace month has ended.</p>";
                }
                if (_content.CleanupDate is not DateTime cleanupDate)
                {
                    return "<p><strong>What happens if you take no action:</strong> The cleanup eligibility date could not be calculated. Correct the target release month in the Release Plan dashboard.</p>";
                }

                var deadline = cleanupDate <= _content.SentOnUtc
                    ? "This Release Plan is eligible for automatic abandonment now. Please take action now."
                    : $"Please take action before {cleanupDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)}.";
                var condition = _content.IsPrivatePreview
                    ? "its specification pull request is still missing or unmerged"
                    : "no SDK is recorded as released, no linked SDK pull request is merged, and no open SDK pull request is approved";
                var privateGuidance = _content.IsPrivatePreview
                    ? "<p>For a Private Preview release, opening a specification pull request is not sufficient to prevent abandonment; it must be merged, or the target release month must be updated.</p>"
                    : string.Empty;
                return $"<p><strong>What happens if you take no action: {deadline}</strong> {_content.GraceMonth} is the grace month for the {_content.TargetMonth} target. Starting with the {cleanupDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture)} monthly cleanup, this Release Plan will be marked <strong>Abandoned</strong> if it is still overdue and {condition}. Setting a future target release month moves the cleanup eligibility date; editing other details alone does not.</p>{privateGuidance}";
            }
        }
    }
}
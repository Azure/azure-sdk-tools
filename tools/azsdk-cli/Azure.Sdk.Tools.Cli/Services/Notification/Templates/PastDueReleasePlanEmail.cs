// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services.Notification.Templates
{
    public class PastDueReleasePlanEmail : EmailPayload
    {
        private readonly ReleasePlanEmailContent _content;
        private readonly string _reason;

        public PastDueReleasePlanEmail(ReleasePlanWorkItem releasePlan, string reason, DateTimeOffset abandonedAt)
        {
            ArgumentNullException.ThrowIfNull(releasePlan);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            _content = new ReleasePlanEmailContent(releasePlan, abandonedAt);
            _reason = reason;
            EmailTo = string.IsNullOrWhiteSpace(releasePlan.ReleasePlanSubmittedByEmail)
                ? []
                : [releasePlan.ReleasePlanSubmittedByEmail];
        }

        public override string Subject => $"Your Azure SDK Release Plan ({_content.Id}) has been abandoned";

        public override string Body =>
            $"""
            <html>
            <body>
                <p>Hello {_content.Owner},</p>
                <p>Your <a href="{_content.Url}">Azure SDK Release Plan {_content.Id}</a> has been marked <strong>Abandoned</strong> during the {_content.SentOnUtc.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)} monthly cleanup. Its target release month was <strong>{_content.TargetMonth}</strong>, and the {_content.GraceMonth} grace month has ended.</p>
                {_content.OverviewAndDetails}
                <p><strong>Why this Release Plan was abandoned:</strong> At the time of cleanup: {WebUtility.HtmlEncode(_reason)}</p>
                <p><strong>What to do next</strong></p>
                <ol>
                    {_content.NewReleasePlanAction}
                </ol>
                {ReleasePlanEmailContent.SupportAndSignature}
            </body>
            </html>
            """;

    }
}

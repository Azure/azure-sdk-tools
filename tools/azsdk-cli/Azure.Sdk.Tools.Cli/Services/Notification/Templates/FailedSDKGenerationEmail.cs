// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services.Notification.Templates
{
    /// <summary>
    /// Email template used to notify the Azure SDK experience team about release plans that had one
    /// or more SDK pull request statuses updated to "Failed to generate SDK." in the last 24 hours.
    /// The email is addressed only to the AzSDK experience alias.
    /// </summary>
    public class FailedSDKGenerationEmail : EmailPayload
    {
        private readonly List<FailedSDKGenerationReleasePlan> failedReleasePlans;

        public FailedSDKGenerationEmail(List<FailedSDKGenerationReleasePlan> failedReleasePlans)
        {
            this.failedReleasePlans = failedReleasePlans ?? throw new ArgumentNullException(nameof(failedReleasePlans));
            EmailTo = [AzSdkSupportAlias];
            CC = [];
        }

        public override string Subject =>
            $"Azure SDK generation failures in the last 24 hours ({failedReleasePlans.Count})";

        public override string Body =>
            $"""
            <html>
            <body>
                <p>Hello,</p>
                <p>The following release plans had one or more SDK pull requests fail to generate in the last 24 hours.</p>
                <h3>Summary</h3>
                <ul>
                    <li><strong>No. of release plans with failed SDK generation:</strong> {failedReleasePlans.Count}</li>
                    {PerLanguageSummary}
                </ul>
                <br>
                <h3>Release plans with failed SDK generation</h3>
                {ReleasePlanTable}
                <br>
                <p>Best regards,</p>
                <p>Azure SDK Team</p>
            </body>
            </html>
            """;

        private string PerLanguageSummary
        {
            get
            {
                var languageCounts = failedReleasePlans
                    .SelectMany(plan => plan.FailedLanguages)
                    .GroupBy(language => language, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

                var builder = new StringBuilder();
                foreach (var group in languageCounts)
                {
                    builder.Append($"<li><strong>No. of SDK gen failed for {group.Key}:</strong> {group.Count()}</li>");
                }

                return builder.ToString();
            }
        }

        private string ReleasePlanTable
        {
            get
            {
                var builder = new StringBuilder();
                builder.Append("<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" style=\"border-collapse: collapse;\">");
                builder.Append("<tr><th>Release Plan</th><th>Failed Languages</th></tr>");

                foreach (var plan in failedReleasePlans)
                {
                    var link = plan.ReleasePlan.ReleasePlanLink;
                    var languages = string.Join(", ", plan.FailedLanguages);
                    builder.Append($"<tr><td><a href=\"{link}\">{link}</a></td><td>{languages}</td></tr>");
                }

                builder.Append("</table>");
                return builder.ToString();
            }
        }
    }
}

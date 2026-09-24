// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services.Notification.Templates;

public class ReleasePlanSdkGenerationEmail : EmailPayload
{
    private readonly ReleasePlanWorkItem _completedPlan;
    private readonly ReleasePlanWorkItem _pendingPlan;
    private readonly bool _generationQueued;

    public ReleasePlanSdkGenerationEmail(ReleasePlanWorkItem completedPlan, ReleasePlanWorkItem pendingPlan, bool generationQueued)
    {
        _completedPlan = completedPlan ?? throw new ArgumentNullException(nameof(completedPlan));
        _pendingPlan = pendingPlan ?? throw new ArgumentNullException(nameof(pendingPlan));
        _generationQueued = generationQueued;

        EmailTo = string.IsNullOrWhiteSpace(pendingPlan.ReleasePlanSubmittedByEmail)
            ? []
            : [pendingPlan.ReleasePlanSubmittedByEmail];

        // Keep test-plan notifications scoped to the submitter, matching other release-plan emails.
        if (!pendingPlan.IsTestReleasePlan)
        {
            EmailTo.Add("azsdkexp@microsoft.com");
            if (pendingPlan.IsManagementPlane)
            {
                CC.Add("sdkreleaseowners@microsoft.com");
            }
        }
    }

    public override string Subject =>
        _generationQueued
            ? $"SDK generation queued for release plan {_pendingPlan.ReleasePlanId} (API version {_pendingPlan.SpecAPIVersion})"
            : $"Action required: SDK generation could not be queued for release plan {_pendingPlan.ReleasePlanId}";

    public override string Body =>
        $"""
        <html>
        <body>
            <p>Hello,</p>
            <p><a href="{DashboardLink(_completedPlan)}">Release plan {_completedPlan.ReleasePlanId}</a> is now complete (Finished).</p>
            <p>We identified your pending <a href="{DashboardLink(_pendingPlan)}"> release plan {_pendingPlan.ReleasePlanId}</a>
            for API version <strong>{WebUtility.HtmlEncode(_pendingPlan.SpecAPIVersion)}</strong>.</p>
            {QueueStatusContent}
            <p>The generated SDK pull requests will appear on the
            <a href="{DashboardLink(_pendingPlan)}">release plan dashboard</a> once they are ready.
            Please use the dashboard to monitor progress and review the pull requests when they become available.</p>
            <p>Best regards,<br>Azure SDK Team</p>
        </body>
        </html>
        """;

    private string QueueStatusContent => _generationQueued
        ? $"<p>We have automatically queued SDK generation using API version <strong>{WebUtility.HtmlEncode(_pendingPlan.SpecAPIVersion)}</strong>.</p>"
        : $"""
          <p>We were unable to queue SDK generation automatically for this release plan.</p>
          <p>Please use the <a href="https://aka.ms/azsdk/agent">azsdk agent</a> to generate SDKs
          for release plan {_pendingPlan.ReleasePlanId} using API version <strong>{WebUtility.HtmlEncode(_pendingPlan.SpecAPIVersion)}</strong>.
          For more information and next steps, visit the <a href="{DashboardLink(_pendingPlan)}">release plan dashboard</a>.</p>
          """;

    private static string DashboardLink(ReleasePlanWorkItem plan)
    {
        var dashboard = plan.IsTestReleasePlan
            ? ReleasePlanWorkItem.DashboardBaseUrlTest
            : ReleasePlanWorkItem.DashboardBaseUrl;
        return $"{dashboard[..dashboard.IndexOf('?')]}?releasePlan={plan.ReleasePlanId.ToString(CultureInfo.InvariantCulture)}";
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services.Notification.Templates;

// Shared copy and formatting only; eligibility comes from the release-work assessment.
internal sealed class ReleasePlanEmailContent
{
    private readonly ReleasePlanWorkItem _plan;

    public ReleasePlanEmailContent(ReleasePlanWorkItem plan, DateTimeOffset sentAt)
    {
        _plan = plan;
        SentOnUtc = sentAt.UtcDateTime.Date;
        if (DateTime.TryParseExact(plan.SDKReleaseMonth, ["MMMM yyyy", "MMM yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var targetMonth))
        {
            GraceMonth = targetMonth.AddMonths(1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
            CleanupDate = targetMonth.AddMonths(2);
        }
    }

    public int Id => _plan.ReleasePlanId > 0 ? _plan.ReleasePlanId : _plan.WorkItemId;
    public string Url => WebUtility.HtmlEncode(_plan.ReleasePlanLink);
    public string Owner => WebUtility.HtmlEncode(_plan.Owner);
    public string TargetMonth => WebUtility.HtmlEncode(_plan.SDKReleaseMonth);
    public string ServiceName => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(_plan.ProductName) ? _plan.Title : _plan.ProductName);
    public bool IsPrivatePreview => _plan.ApiReleaseType == ApiReleaseType.PrivatePreview;
    public bool IsUnknownReleaseType => _plan.ApiReleaseType == ApiReleaseType.Unknown;
    public DateTime SentOnUtc { get; }
    public DateTime? CleanupDate { get; }
    public string? GraceMonth { get; }
    public string FutureMonth => SentOnUtc.AddMonths(1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    public string OverviewAndDetails =>
        $"""
        <p>A Release Plan is a guided workflow that tracks an API release from specification review through the SDK generation and release steps applicable to its release type. Release Plans can be created automatically when a specification pull request that adds a new API version is merged. <a href="https://aka.ms/azsdkdocs/release-plans">Learn more about Release Plans.</a></p>
        <ul>
            <li><strong>Release Plan:</strong> <a href="{Url}">{Id}</a></li>
            <li><strong>Service:</strong> {ServiceName}</li>
            <li><strong>Release type:</strong> {WebUtility.HtmlEncode(IsUnknownReleaseType ? "Unrecognized" : _plan.ApiReleaseType.ToDisplayLabel())}</li>
            <li><strong>SDK plane:</strong> {(_plan.IsManagementPlane ? "Management (ARM) plane" : "Data plane")}</li>
            <li><strong>Target release month:</strong> {TargetMonth}</li>
        </ul>
        """;

    public string UpdateTargetAction =>
        $"""
        <li><strong>If the release is still planned:</strong> Use the <a href="https://aka.ms/azsdk/agent">Azure SDK Tools Agent</a> to set a realistic future target release month. For example:
        <blockquote>&ldquo;Update the target release month for Release Plan {Id} to {FutureMonth}&rdquo;</blockquote>
        Confirm that the updated month appears in the Release Plan dashboard.</li>
        """;

    public string CompleteReleaseAction => IsPrivatePreview
        ? """
          <li><strong>If the specification is ready:</strong> Use the <a href="https://aka.ms/azsdk/agent">Azure SDK Tools Agent</a> to identify the correct specification pull request and ensure it is linked to this Release Plan. Complete the required reviews and merge the pull request. Confirm its merged status in the dashboard.</li>
          """
        : """
          <li><strong>If you are ready to complete the release:</strong> Ask the <a href="https://aka.ms/azsdk/agent">Azure SDK Tools Agent</a> to guide you through generating or locating the required SDK pull requests. Review and merge them, then follow the dashboard to publish the remaining SDKs and confirm their release status.</li>
          """;

    public string AbandonAction =>
        $"""
        <li><strong>If the release is no longer needed:</strong> Use the <a href="https://aka.ms/azsdk/agent">Azure SDK Tools Agent</a> to abandon the Release Plan yourself:
        <blockquote>&ldquo;Abandon Release Plan {Id}&rdquo;</blockquote>
        Confirm that its status changes to <strong>Abandoned</strong>.</li>
        """;

    public string VerificationAction => IsUnknownReleaseType
        ? $"""
          <li><strong>Confirm the release type:</strong> Review the <a href="{Url}">Release Plan {Id}</a> dashboard and confirm whether this is a Private Preview, Public Preview, or GA release. Contact the Azure SDK Team using the support channel below if the recorded release type is missing or incorrect.</li>
          """
        : $"""
          <li><strong>Check the linked pull requests:</strong> Open the <a href="{Url}">Release Plan {Id}</a> dashboard and confirm that the correct GitHub pull requests are linked. If the links are correct, contact the Azure SDK Team using the support channel below so we can investigate the verification issue.</li>
          """;

    public string NewReleasePlanAction =>
        $"""
        <li><strong>If you want to continue this release:</strong> Create a <strong>new Release Plan</strong> with an updated target release month using the <a href="https://aka.ms/azsdk/agent">Azure SDK Tools Agent</a>. For example:
        <blockquote>&ldquo;Help me create a new Release Plan for {ServiceName}, targeting {FutureMonth}. Use Release Plan {Id} as context&rdquo;</blockquote>
        The Agent can guide you through the required information and next steps. Confirm the new Release Plan ID, target release month, and linked specification and SDK details in its dashboard. Creating a new Release Plan does not reopen the abandoned Release Plan.</li>
        <li><strong>If this release is no longer needed:</strong> No further action is required for this abandoned Release Plan.</li>
        """;

    public const string SupportAndSignature = """
        <p>If you need assistance, contact the Azure SDK Team through the <a href="https://teams.microsoft.com/l/channel/19%3A6d2c19322c254a80bcc521675134da03%40thread.skype/AzSDK%20Tools%20Agent?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&amp;tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47">Azure SDK Tools Agent Teams channel</a>. Please include your Release Plan ID.</p>
        <p>Best regards,</p>
        <p>Azure SDK Team</p>
        """;
}
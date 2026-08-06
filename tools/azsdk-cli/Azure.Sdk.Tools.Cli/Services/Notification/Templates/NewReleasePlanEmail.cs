// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services.Notification.Templates
{
    /// <summary>
    /// Email template used to notify stakeholders when a new release plan has been created.
    /// The subject and body are constructed directly from the <see cref="ReleasePlanWorkItem"/>
    /// reference supplied in the constructor.
    /// </summary>
    public class NewReleasePlanEmail : EmailPayload
    {
        private const string AutomationCreatedUsing = "Automation";

        private const string ReleasePlanDocumentationUrl = "https://aka.ms/azsdkdocs/release-plans";

        private const string ReleasePlanOverview =
            "<p>A release plan is a guided workflow that tracks an <strong>API release</strong> from API spec review, through SDK generation, to SDK release. " +
            "Each release plan is scoped to <strong>one release type</strong> (Private Preview, Public Preview, or GA) " +
            "<strong>and one plane</strong> (data plane or management (ARM) plane). " +
            "A release plan is required before any Azure SDK-related Cloud Lifecycle (CPEX) KPIs can be approved for this release. " +
            "<a href=\"" + ReleasePlanDocumentationUrl + "\">Learn more about release plans.</a></p>";

        private const string AutomatedSdkPullRequestNextStep =
            "<li>SDK pull requests: One SDK pull request per language (.NET, Java, JavaScript/TypeScript, Python, and Go (optional for data plane)) will be generated and linked to this plan. " +
            "When each PR is ready, review and approve it, then complete the merge and release by following your release plan dashboard. " +
            "The Azure SDK Tools Agent can walk you through these steps.</li>";

        private const string ManualSdkPullRequestNextStep =
            "<li>SDK pull requests: Use the azsdk agent to generate SDK pull requests and link them to this plan. " +
            "When each pull request is ready, review and approve it, then follow the release plan dashboard to merge and release it.</li>";

        private const string KpiAttestationActionRequiredSection =
            "<li><strong>Action required:</strong> If your product is part of an official Cloud Lifecycle phase release, complete the CPEX / KPI attestation details. " +
            "This release plan is missing its Service Tree Product ID, Service ID, or Product Type (for example, Feature, SKU, or Offering). " +
            "These details link the plan to its Service Tree product so Cloud Lifecycle KPIs can be auto-attested when the release completes. " +
            "Use the azsdk agent to add them.</li>";

        private const string MissingSdkDetailsNextStep =
            "<li>SDK pull requests: SDK details are currently missing from the release plan, likely because the emitter configuration is not defined in tspconfig.yaml. " +
            "As a result, SDK pull requests cannot be generated until the emitter configuration is added to tspconfig.yaml and the release plan is updated with the SDK details.</li>";

        private const string AzSdkAgentDocumentationUrl = "https://aka.ms/azsdk/agent";

        private const string ManagementSdkOwnerAlias = "sdkowners@microsoft.com";

        private const string AzSdkSupportAlias = "azsdkexp@microsoft.com";

        private readonly ReleasePlanWorkItem releasePlan;

        public NewReleasePlanEmail(ReleasePlanWorkItem releasePlan)
        {
            this.releasePlan = releasePlan ?? throw new ArgumentNullException(nameof(releasePlan));

            // Notify the release plan submitter. Test release plans are only sent to the submitter;
            // azsdk support and (for management plane) sdkowners are CC'd on non-test release plans.
            EmailTo = string.IsNullOrWhiteSpace(releasePlan.ReleasePlanSubmittedByEmail)
                ? []
                : [releasePlan.ReleasePlanSubmittedByEmail];

            var ccRecipients = new List<string>();
            if (!releasePlan.IsTestReleasePlan)
            {
                ccRecipients.Add(AzSdkSupportAlias);
                if (releasePlan.IsManagementPlane)
                {
                    ccRecipients.Add(ManagementSdkOwnerAlias);
                }
            }
            CC = ccRecipients;
        }

        public override string Subject =>
            $"Azure SDK Release plan created for {releasePlan.ProductName} ({releasePlan.ApiReleaseType.ToDisplayLabel()})";

        public override string Body =>
            $"""
            <html>
            <body>
                <p>Hello,</p>
                {CreationSummary}
                {ReleasePlanOverview}
                <p>The release plan dashboard contains the actions required to complete this release plan.</p>
                <ul>
                    <li><strong>Release plan:</strong> <a href="{releasePlan.ReleasePlanLink}">{releasePlan.ReleasePlanLink}</a></li>
                    <li><strong>Release plan type:</strong> {releasePlan.ApiReleaseType.ToDisplayLabel()}</li>
                </ul>
                <h3>What happens next</h3>
                <ul>
                    {SdkPullRequestNextStepContent}
                    {KpiAttestationActionRequiredContent}
                </ul>
                <p>For more information about the azsdk agent, see <a href="{AzSdkAgentDocumentationUrl}">{AzSdkAgentDocumentationUrl}</a>.</p>
                <p>If you need any assistance, please reach out to the AzSDK Agent team via the <a href="https://teams.microsoft.com/l/channel/19%3A6d2c19322c254a80bcc521675134da03%40thread.skype/AzSDK%20Tools%20Agent?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47">Teams channel</a>.</p>
                <p>Best regards,</p>
                <p>Azure SDK Team</p>
            </body>
            </html>
            """;

        private string CreationSummary
        {
            get
            {
                var specPullRequest = SpecPullRequest;
                var isAutomationCreated =
                    string.Equals(releasePlan.CreatedUsing, AutomationCreatedUsing, StringComparison.OrdinalIgnoreCase);

                return isAutomationCreated && !string.IsNullOrWhiteSpace(specPullRequest)
                    ? $"""<p>An Azure SDK release plan has been automatically created after merging <a href="{specPullRequest}">{specPullRequest}</a>, which added a new API version.</p>"""
                    : "<p>An Azure SDK release plan has been created.</p>";
            }
        }

        private string SpecPullRequest
        {
            get
            {
                var specPullRequest = releasePlan.SpecPullRequests?.FirstOrDefault();
                return string.IsNullOrWhiteSpace(specPullRequest) ? releasePlan.ActiveSpecPullRequest : specPullRequest;
            }
        }

        private string SdkPullRequestNextStep =>
            releasePlan.IsManagementPlane
            && string.Equals(releasePlan.CreatedUsing, AutomationCreatedUsing, StringComparison.OrdinalIgnoreCase)
            ? AutomatedSdkPullRequestNextStep
            : ManualSdkPullRequestNextStep;

        private string KpiAttestationActionRequiredContent => IsMissingProductInfo ? KpiAttestationActionRequiredSection : string.Empty;

        private string SdkPullRequestNextStepContent => IsMissingSdkDetails ? MissingSdkDetailsNextStep : SdkPullRequestNextStep;

        private bool IsMissingSdkDetails =>
            releasePlan.SDKInfo is null
            || releasePlan.SDKInfo.Count == 0
            || releasePlan.SDKInfo.All(sdkInfo => string.IsNullOrWhiteSpace(sdkInfo.PackageName));

        private bool IsMissingProductInfo =>
            string.IsNullOrWhiteSpace(releasePlan.ProductTreeId)
            || string.IsNullOrWhiteSpace(releasePlan.ServiceTreeId)
            || string.IsNullOrWhiteSpace(releasePlan.ProductType);
    }
}

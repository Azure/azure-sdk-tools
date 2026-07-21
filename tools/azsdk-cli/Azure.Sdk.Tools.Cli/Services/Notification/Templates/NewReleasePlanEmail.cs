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
        private const string ManagementPlaneMessage =
            "<p>SDK pull requests will be auto generated and linked to the release plan.</p>";

        private const string DataPlaneMessage =
            "<p>Please use azsdk agent to generate the SDK pull requests and link them to the release plan.</p>";

        private const string KpiAttestationSection =
            "<p>This release plan is currently missing Product ID, Service ID, or Product Type.</p>" +
            "<p>Please use azsdk agent to update the release plan with Product ID, Service ID, and Product Type to complete KPI attestation when release plan is completed.</p>";

        private readonly ReleasePlanWorkItem releasePlan;

        public NewReleasePlanEmail(ReleasePlanWorkItem releasePlan)
        {
            this.releasePlan = releasePlan ?? throw new ArgumentNullException(nameof(releasePlan));
        }

        public override string Subject =>
            $"Release plan created for {releasePlan.ProductName} ({releasePlan.ApiReleaseType.ToDisplayLabel()})";

        public override string Body =>
            $"""
            <html>
            <body>
                <p>Hello,</p>
                <p>A release plan has been created successfully after merging <a href="{SpecPullRequest}">{SpecPullRequest}</a>, which added a new API version.</p>
                <p>The release plan dashboard contains the actions required to complete this release plan.</p>
                <ul>
                    <li><strong>Release plan:</strong> <a href="{releasePlan.ReleasePlanLink}">{releasePlan.ReleasePlanLink}</a></li>
                    <li><strong>Release plan type:</strong> {releasePlan.ApiReleaseType.ToDisplayLabel()}</li>
                </ul>
                <br>
                {PlaneSpecificMessage}
                <br>
                {KpiAttestationSectionContent}
                <br>
                <p>If you need any assistance, please reach out to the AzSDK Agent team via the <a href="https://teams.microsoft.com/l/channel/19%3A6d2c19322c254a80bcc521675134da03%40thread.skype/AzSDK%20Tools%20Agent?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47">Teams channel</a>.</p>
                <p>Best regards,</p>
                <p>Azure SDK PM Team</p>
            </body>
            </html>
            """;

        private string SpecPullRequest
        {
            get
            {
                var specPullRequest = releasePlan.SpecPullRequests?.FirstOrDefault();
                return string.IsNullOrWhiteSpace(specPullRequest) ? releasePlan.ActiveSpecPullRequest : specPullRequest;
            }
        }

        private string PlaneSpecificMessage => releasePlan.IsManagementPlane ? ManagementPlaneMessage : DataPlaneMessage;

        private string KpiAttestationSectionContent => IsMissingProductInfo ? KpiAttestationSection : string.Empty;

        private bool IsMissingProductInfo =>
            string.IsNullOrWhiteSpace(releasePlan.ProductTreeId)
            || string.IsNullOrWhiteSpace(releasePlan.ServiceTreeId)
            || string.IsNullOrWhiteSpace(releasePlan.ProductType);
    }
}

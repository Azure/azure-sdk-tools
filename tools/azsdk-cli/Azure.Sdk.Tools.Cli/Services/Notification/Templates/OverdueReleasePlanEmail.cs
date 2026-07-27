// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.AzureDevOps;

namespace Azure.Sdk.Tools.Cli.Services.Notification.Templates
{
    /// <summary>
    /// Email template used to notify the owner of an overdue release plan (one whose SDK release
    /// month has passed) that one or more SDKs have not yet been published. The plan submitter is
    /// the primary recipient and the AzSDK apex alias is CC'd.
    /// </summary>
    public class OverdueReleasePlanEmail : EmailPayload
    {
        private const string SdkApexAlias = "azsdkapex@microsoft.com";

        private readonly ReleasePlanWorkItem releasePlan;

        public OverdueReleasePlanEmail(ReleasePlanWorkItem releasePlan)
        {
            this.releasePlan = releasePlan ?? throw new ArgumentNullException(nameof(releasePlan));

            EmailTo = string.IsNullOrWhiteSpace(releasePlan.ReleasePlanSubmittedByEmail)
                ? []
                : [releasePlan.ReleasePlanSubmittedByEmail];
            CC = [SdkApexAlias];
        }

        public override string Subject =>
            "Action Required: Azure SDKs Not Yet Published for Your Release Plan";

        public override string Body =>
            $"""
            <html>
            <body>
                <p>Hello {releasePlan.Owner},</p>
                <p>Our automation has detected that one or more Azure SDKs generated for your release plan have not yet been published to the required language package managers.</p>
                <ul>
                    <li><strong>Azure SDK Type:</strong> {Plane}</li>
                    <li><strong>SDKs not yet published:</strong> {string.Join(", ", MissingSDKs)}</li>
                    <li><strong>Release Plan:</strong> <a href="{releasePlan.ReleasePlanLink}">{releasePlan.ReleasePlanLink}</a></li>
                    <li><strong>Release Plan Target Release Date:</strong> {releasePlan.SDKReleaseMonth}</li>
                </ul>
                <p>Per Azure SDK release requirements, all Tier 1 language SDKs must be <strong>published to their respective package managers</strong> before a release plan can be marked as complete.</p>
                <p>Until the missing SDKs are published:</p>
                <ul>
                    <li>The release plan cannot be completed in Release Planner.</li>
                    <li>If this release is in scope for CPEX, Cloud Lifecycle phase KPIs for Public Preview or GA will remain incomplete.</li>
                </ul>
                <p><strong>Required actions:</strong></p>
                <ol>
                    <li>Publish the missing SDKs to their respective package managers, or</li>
                    <li>Update the target release date in the release plan, or</li>
                    <li>If publication is not intended, file an approved exception: <a href="https://eng.ms/docs/products/azure-developer-experience/onboard/request-exception">https://eng.ms/docs/products/azure-developer-experience/onboard/request-exception</a></li>
                </ol>
                <p>Once publication is complete, this status will clear automatically. Thank you for helping maintain consistent, complete Azure SDK releases across all mandatory Tier 1 languages.</p>
                <p>Best regards,</p>
                <p>Azure SDK PM Team</p>
            </body>
            </html>
            """;

        private string Plane => releasePlan.IsManagementPlane ? "Management Plane" : "Data Plane";

        /// <summary>
        /// SDKs not yet released. Skips Go for Data Plane, and any excluded/missing-emitter languages.
        /// </summary>
        private List<string> MissingSDKs =>
            releasePlan.SDKInfo
                .Where(info => (string.IsNullOrEmpty(info.ReleaseStatus) || !string.Equals(info.ReleaseStatus, "Released", StringComparison.OrdinalIgnoreCase))
                         && (releasePlan.IsManagementPlane || !string.Equals(info.Language, "Go", StringComparison.OrdinalIgnoreCase))
                         && !string.Equals(info.ReleaseExclusionStatus, "Requested", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(info.ReleaseExclusionStatus, "Approved", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(info.ReleaseExclusionStatus, "MissingEmitterConfig", StringComparison.OrdinalIgnoreCase))
                .Select(info => info.Language)
                .ToList();
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

internal static class ReleasePlanAuthorizationFixture
{
    public static bool Matches(string releasePlanId, string workItemId) =>
        releasePlanId is "50003" or "50004" || workItemId is "35003" or "35004";

    public static bool IsAdmin(string releasePlanId, string workItemId) =>
        releasePlanId == "50004" || workItemId == "35004";

    public static ReleasePlanResponse GetResponse(bool isAdmin)
    {
        var id = isAdmin ? 50004 : 50003;
        var workItemId = isAdmin ? 35004 : 35003;
        return new ReleasePlanResponse
        {
            Message = "Successfully retrieved release plan.",
            ReleasePlanDetails = new ReleasePlanWorkItem
            {
                ReleasePlanId = id,
                WorkItemId = workItemId,
                WorkItemUrl = $"https://dev.azure.com/example/Release/_apis/wit/workItems/{workItemId}",
                WorkItemHtmlUrl = $"https://dev.azure.com/example/Release/_workitems/edit/{workItemId}",
                ReleasePlanSubmittedByEmail = "notification-only@example.com",
                Title = "Contoso authorization fixture",
                Status = "In Progress",
                SDKReleaseMonth = "December 2026",
                ApiReleaseType = ApiReleaseType.GA
            },
            Capabilities = new ReleasePlanCapabilities
            {
                CanAbandon = isAdmin,
                Reason = isAdmin
                    ? "You can abandon this release plan after confirming the action. Permissions are checked again when abandoning."
                    : "Only release-plan administrators can abandon a release plan. Ask an administrator to review an abandonment request."
            }
        };
    }

    public static ReleaseWorkflowResponse AbandonResponse(bool isAdmin) => isAdmin
        ? new ReleaseWorkflowResponse
        {
            Status = "Success",
            Details = ["Release plan 50004 has been successfully abandoned.", $"Release plan link: {ReleasePlanWorkItem.DashboardBaseUrl}50004"]
        }
        : new ReleaseWorkflowResponse
        {
            ResponseError = "Only release-plan administrators can abandon a release plan.",
            NextSteps = ["Ask a release-plan administrator to review this request. Do not bypass this restriction using another tool or a direct work-item update."]
        };
}
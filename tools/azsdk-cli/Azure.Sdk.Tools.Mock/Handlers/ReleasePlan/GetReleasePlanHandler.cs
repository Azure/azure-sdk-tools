// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_get_release_plan.
/// Returns the Contoso release plan for its known IDs, TypeSpec project path, or spec PR.
/// </summary>
public class GetReleasePlanHandler : IMockToolHandler
{
    private const string ContosoTypeSpecProjectPath = "specification/contosowidgetmanager/Contoso.WidgetManager";
    private const string DefaultSpecPullRequestUrl = "https://github.com/Azure/azure-rest-api-specs/pull/12345";

    public string ToolName => "azsdk_get_release_plan";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var workItemId = arguments?.GetValueOrDefault("workItemId")?.ToString()
            ?? arguments?.GetValueOrDefault("workItem")?.ToString()
            ?? "0";
        var releasePlanId = arguments?.GetValueOrDefault("releasePlanId")?.ToString() ?? "0";
        var specPullRequestUrl = arguments?.GetValueOrDefault("specPullRequestUrl")?.ToString() ?? "";
        var typeSpecProjectPath = arguments?.GetValueOrDefault("typeSpecProjectPath")?.ToString() ?? "";
        var apiReleaseType = arguments?.GetValueOrDefault("apiReleaseType")?.ToString() ?? "";
        var isKnownSpecPullRequest = string.Equals(
            specPullRequestUrl,
            "https://github.com/Azure/azure-rest-api-specs/pull/38387",
            StringComparison.OrdinalIgnoreCase);
        var isKnownTypeSpecProject = string.Equals(typeSpecProjectPath, ContosoTypeSpecProjectPath, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(apiReleaseType) || string.Equals(apiReleaseType, "GA", StringComparison.OrdinalIgnoreCase));

        return workItemId == "35000"
            || releasePlanId == "50001"
            || isKnownSpecPullRequest
            || isKnownTypeSpecProject
            ? ContosoReleasePlanResponse(isKnownSpecPullRequest ? specPullRequestUrl : DefaultSpecPullRequestUrl)
            : MockToolFactory.GetDefaultResponse();
    }

    private static ReleasePlanResponse ContosoReleasePlanResponse(string activeSpecPullRequestUrl) => new()
    {
        TypeSpecProject = "specification/contosowidgetmanager/Contoso.WidgetManager",
        PackageType = SdkType.Dataplane,
        Message = "Release plan found",
        Warnings =
        [
            $"Release plan 49999 ({ReleasePlanWorkItem.DashboardBaseUrl}49999) is past due. Its target release month was May 2026."
        ],
        NextSteps =
        [
            "Either postpone the past-due plan by updating its target release month, or abandon it and record the reason in the release plan dashboard."
        ],
        ReleasePlanDetails = new ReleasePlanWorkItem
        {
            WorkItemId = 35000,
            Title = "Release Plan - Contoso.WidgetManager",
            Status = "Active",
            Owner = "testuser@microsoft.com",
            SDKReleaseMonth = "06/2026",
            ReleasePlanId = 50001,
            ReleasePlanType = "GA",
            IsDataPlane = true,
            SpecType = "TypeSpec",
            ActiveSpecPullRequest = activeSpecPullRequestUrl,
            APISpecProjectPath = ContosoTypeSpecProjectPath,
            SDKReleaseType = "beta",
            SDKInfo =
            [
                new SDKInfo { Language = ".NET", PackageName = "Azure.Template.Contoso", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-net/pull/45001" },
                new SDKInfo { Language = "Python", PackageName = "azure-contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-python/pull/45002" },
                new SDKInfo { Language = "JavaScript", PackageName = "@azure/contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-js/pull/45003" },
                new SDKInfo { Language = "Java", PackageName = "azure-contoso-widgetmanager", SdkPullRequestUrl = "https://github.com/Azure/azure-sdk-for-java/pull/45004" },
            ]
        }
    };
}

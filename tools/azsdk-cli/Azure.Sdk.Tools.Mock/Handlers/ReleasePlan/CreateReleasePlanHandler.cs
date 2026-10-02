// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.AzureDevOps;
using Azure.Sdk.Tools.Cli.Models.Responses.ReleasePlan;

namespace Azure.Sdk.Tools.Mock.Handlers.ReleasePlan;

/// <summary>
/// Mock handler for azsdk_create_release_plan.
/// Creates a Contoso release plan from fixture metadata for the expected local TypeSpec project.
/// </summary>
public class CreateReleasePlanHandler : IMockToolHandler
{
    public string ToolName => "azsdk_create_release_plan";

    public CommandResponse Handle(Dictionary<string, object?>? arguments)
    {
        var typespecPath = ReleasePlanMockResponses.NormalizeProjectPath(arguments?.GetValueOrDefault("typeSpecProjectPath")?.ToString() ?? "");
        if (!string.Equals(typespecPath, ReleasePlanMockResponses.ContosoTypeSpecProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            return new ReleasePlanResponse { ResponseError = "Provide the local Contoso TypeSpec project at the selected commit." };
        }

        return ContosoReleasePlanResponse(ReleasePlanMockResponses.ContosoTypeSpecProjectPath, arguments);
    }

    private static ReleasePlanResponse ContosoReleasePlanResponse(string typespecPath, Dictionary<string, object?>? arguments)
    {
        var specPr = arguments?.GetValueOrDefault("specPullRequestUrl")?.ToString()?.Trim() ?? string.Empty;
        var hasSpecPullRequest = !string.IsNullOrWhiteSpace(specPr);
        if (!ApiReleaseTypeExtensions.TryParseFromUserInput(arguments?.GetValueOrDefault("apiReleaseType")?.ToString() ?? "", out var releaseType))
        {
            return new ReleasePlanResponse { ResponseError = "Choose Private Preview, Public Preview, or GA." };
        }
        var response = new ReleasePlanResponse
        {
            TypeSpecProject = typespecPath,
            PackageType = SdkType.Dataplane,
            Message = "Release plan created successfully",
            Warnings = hasSpecPullRequest
                ? [$"Release plan 49999 ({ReleasePlanWorkItem.DashboardBaseUrl}49999) is past due. Its target release month was May 2026."]
                : null,
            NextSteps = hasSpecPullRequest
                ? ["Either postpone the past-due plan by updating its target release month, or abandon it and record the reason in the release plan dashboard."]
                : null,
            ReleasePlanDetails = new ReleasePlanWorkItem
            {
                WorkItemId = 35000,
                Title = "Release Plan - Contoso.WidgetManager",
                Status = "Active",
                Owner = "testuser@microsoft.com",
                SDKReleaseMonth = arguments?.GetValueOrDefault("targetReleaseMonthYear")?.ToString() ?? "December 2026",
                ReleasePlanId = 50001,
                ApiReleaseType = releaseType,
                IsDataPlane = true,
                SpecType = "TypeSpec",
                ActiveSpecPullRequest = specPr,
                APISpecProjectPath = typespecPath,
                SDKReleaseType = releaseType.GetDefaultSdkReleaseType(),
                SDKInfo =
                [
                    new SDKInfo { Language = ".NET", PackageName = "Azure.Template.Contoso" },
                    new SDKInfo { Language = "Python", PackageName = "azure-contoso-widgetmanager" },
                    new SDKInfo { Language = "JavaScript", PackageName = "@azure/contoso-widgetmanager" },
                    new SDKInfo { Language = "Java", PackageName = "azure-contoso-widgetmanager" },
                ]
            }
        };
        return ReleasePlanMockResponses.SaveTarget(arguments, update: false, response: response);
    }
}
